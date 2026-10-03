using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using Controller550W.Core;
namespace Controller550W.Rendering;
// Static copy of the existing residue frame while WebView initializes. No timer or alternate choreography.
public static class ParticleFirstFrame
{
    static double Rnd(int i){var n=Math.Sin((i+1)*127.1+311.7)*43758.5453123;return n-Math.Floor(n);}
    public static Bitmap Create(AnimationSettings settings)
    {
        var bitmap=new Bitmap(512,384,PixelFormat.Format32bppPArgb);
        using var g=Graphics.FromImage(bitmap);g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
        using var bodies=new GraphicsPath(FillMode.Winding);using var glows=new GraphicsPath(FillMode.Winding);
        var count=Math.Clamp(settings.ParticleCount,100,2400);var size=Math.Clamp(settings.ParticleSize,.6,7);
        for(int i=0;i<count;i++)
        {
            var angle=Rnd(i)*Math.PI*2;var radius=35+Math.Sqrt(Rnd(i+451))*155;var seed=Rnd(i+944);var s=size*(.6+Rnd(i+630)*.8);
            var x=256+Math.Cos(angle)*radius+Math.Sin(seed*9)*5;var y=192+Math.Sin(angle)*radius*.67+Math.Cos(seed*9)*4;
            bodies.AddRectangle(new RectangleF((float)x,(float)y,(float)s,(float)s));
            if(seed>.86)glows.AddRectangle(new RectangleF((float)(x-s*2),(float)(y-s*2),(float)(s*5),(float)(s*5)));
        }
        var hex=settings.ColorProfile switch{"green"=>"#82ba6b","cyan"=>"#57a9b1","white"=>"#b5b9bc","amber"=>"#e8a020",_=>"#e05030"};
        var color=ColorTranslator.FromHtml(hex);using var body=new SolidBrush(color);using var glow=new SolidBrush(Color.FromArgb(33,color));g.FillPath(body,bodies);g.FillPath(glow,glows);return bitmap;
    }
    public static BitmapSource Image(AnimationSettings settings)
    {
        using var bitmap=Create(settings);using var stream=new MemoryStream();bitmap.Save(stream,ImageFormat.Png);stream.Position=0;var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();return image;
    }
}

// Adapter for the existing deterministic intro, while its WebView is preparing.
// Hosts supply the same elapsed clock; this class owns no timer or second timeline.
// SVG raster/targets are built from BOOT_MARKUP by scripts/build-intro-cache.cjs.
public sealed class ParticleIntroRenderer : IDisposable
{
    sealed class Target
    {
        public Target() { }
        public double X { get; set; }
        public double Y { get; set; }
        public bool Red { get; set; }
    }
    sealed class Particle
    {
        public double MidX, MidY, LogoX, LogoY, BurstX, BurstY, Seed, Size, StartX, StartY;
        public int Group;
    }
    readonly Particle[] particles;
    readonly Bitmap logo, hud;
    readonly GraphicsPath[] bodies = new GraphicsPath[3], glows = new GraphicsPath[3];
    readonly SolidBrush[] bodyBrushes = new SolidBrush[3], glowBrushes = new SolidBrush[3];
    readonly Color[] colors;
    readonly ImageAttributes logoAttributes = new();
    readonly ImageAttributes hudAttributes = new();
    readonly ColorMatrix logoMatrix = new();
    readonly double idleMs, totalMs, holdMs, burstMs, windowMs;
    bool disposed;

    static double Rnd(int i) { var n = Math.Sin((i + 1) * 127.1 + 311.7) * 43758.5453123; return n - Math.Floor(n); }
    static double Clamp(double value) => Math.Clamp(value, 0, 1);
    static double Ease(double t) => 1 - Math.Pow(1 - t, 3);
    static double Mix(double a, double b, double t) => a + (b - a) * t;
    static void Idle(Particle p, double elapsedMs, out double x, out double y)
    {
        x = p.MidX + Math.Sin(p.Seed * 9) * 5 + 1.5 * (Math.Sin(elapsedMs / 1000 * .9 + p.Seed * 9) - Math.Sin(p.Seed * 9));
        y = p.MidY + Math.Cos(p.Seed * 9) * 4 + 1.2 * (Math.Cos(elapsedMs / 1000 * .75 + p.Seed * 7) - Math.Cos(p.Seed * 7));
    }

    public ParticleIntroRenderer(AnimationSettings settings, string themeDirectory)
    {
        if (settings.ParticleToLogoMs <= settings.ParticleResidueMs)
            throw new ArgumentException("粒子 → 550C 总时长必须大于中央粒子待机时长。", nameof(settings));
        var multiplier = settings.DurationMultiplier;
        idleMs = settings.ParticleResidueMs * multiplier;
        totalMs = settings.ParticleToLogoMs * multiplier;
        holdMs = settings.LogoHoldMs * multiplier;
        burstMs = settings.LogoBurstMs * multiplier;
        windowMs = settings.MultiWindowRevealMs * multiplier;
        var assets = Path.Combine(themeDirectory, "assets");
        var targets = JsonSerializer.Deserialize<Target[]>(File.ReadAllText(Path.Combine(assets, "intro-logo-targets.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (targets == null || targets.Length == 0) throw new IOException("550C 图形缓存为空。");
        using (var source = new Bitmap(Path.Combine(assets, "intro-logo.png"))) logo = (Bitmap)source.Clone();
        // Same authored HUD first frame, rendered at build time. If the WebView is
        // still preparing, the native bridge must not end in an empty black frame.
        var scheme=settings.ColorProfile is "green" or "cyan" or "white" or "amber" ? settings.ColorProfile : "movie_red";
        using (var source = new Bitmap(Path.Combine(assets, "boot-hud-"+scheme+".png"))) hud = (Bitmap)source.Clone();
        hudAttributes.SetWrapMode(WrapMode.TileFlipXY);
        var hex = settings.ColorProfile switch { "green" => "#82ba6b", "cyan" => "#57a9b1", "white" => "#b5b9bc", "amber" => "#e8a020", _ => "#e05030" };
        colors = new[] { ColorTranslator.FromHtml(hex), Color.FromArgb(255, 45, 45), Color.White };
        for (var i = 0; i < 3; i++)
        {
            bodies[i] = new GraphicsPath(FillMode.Winding); glows[i] = new GraphicsPath(FillMode.Winding);
            bodyBrushes[i] = new SolidBrush(colors[i]); glowBrushes[i] = new SolidBrush(Color.FromArgb(33, colors[i]));
        }
        var count = Math.Clamp(settings.ParticleCount, 100, 2400);
        var size = Math.Clamp(settings.ParticleSize, .6, 7); var spread = Math.Clamp(settings.ParticleSpread, .35, 2);
        particles = new Particle[count];
        for (var i = 0; i < count; i++)
        {
            var angle = Rnd(i) * Math.PI * 2; var radius = 35 + Math.Sqrt(Rnd(i + 451)) * 155;
            var target = targets[(int)Math.Floor(Rnd(i + 83) * targets.Length)];
            var p = new Particle
            {
                MidX = 960 + Math.Cos(angle) * radius, MidY = 540 + Math.Sin(angle) * radius * .67,
                LogoX = target.X, LogoY = target.Y, Group = target.Red ? 1 : 2,
                BurstX = 960 + (Rnd(i + 177) * 2 - 1) * 1050 * spread,
                BurstY = 540 + (Rnd(i + 290) * 2 - 1) * 620 * spread,
                Seed = Rnd(i + 944), Size = size * (.6 + Rnd(i + 630) * .8)
            };
            Idle(p, idleMs, out p.StartX, out p.StartY); particles[i] = p;
        }
    }

    public void Draw(Graphics g, double elapsedMs, double width, double height)
    {
        if (disposed || width <= 0 || height <= 0) return;
        elapsedMs = Math.Max(0, elapsedMs);
        var holdEnd = totalMs + holdMs; var burstEnd = holdEnd + burstMs;
        int mode; double t;
        if (elapsedMs < idleMs) { mode = 0; t = 0; }
        else if (elapsedMs < totalMs) { mode = 1; t = (elapsedMs - idleMs) / (totalMs - idleMs); }
        else if (elapsedMs < holdEnd) { mode = 2; t = 0; }
        else if (elapsedMs < burstEnd) { mode = 3; t = burstMs <= 0 ? 1 : (elapsedMs - holdEnd) / burstMs; }
        else { mode = 4; t = windowMs <= 0 ? 1 : (elapsedMs - burstEnd) / windowMs; }
        t = Clamp(t);
        var state = g.Save();
        try
        {
            var scale = Math.Min(width / 1920, height / 1080);
            g.TranslateTransform((float)((width - 1920 * scale) / 2), (float)((height - 1080 * scale) / 2));
            g.ScaleTransform((float)scale, (float)scale);
            g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            if(elapsedMs>=burstEnd)
            {
                var hudAlpha=windowMs<=0?1:Ease(Clamp((elapsedMs-burstEnd)/windowMs));
                logoMatrix.Matrix33=(float)hudAlpha;hudAttributes.SetColorMatrix(logoMatrix);
                g.SmoothingMode=SmoothingMode.None;g.PixelOffsetMode=PixelOffsetMode.Default;
                if(Math.Abs(scale-1)<.000001)g.InterpolationMode=InterpolationMode.NearestNeighbor;
                g.DrawImage(hud,new Rectangle(0,0,1920,1080),0,0,1920,1080,GraphicsUnit.Pixel,hudAttributes);
                g.InterpolationMode=InterpolationMode.HighQualityBilinear;
                g.SmoothingMode=SmoothingMode.AntiAlias;
                g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                if(elapsedMs>=burstEnd+windowMs)return;
            }
            var logoAlpha = mode == 1 ? Clamp((t - .3) / .7) : mode == 2 ? 1 : mode == 3 ? 1 - Clamp(t * 4) : 0;
            if (logoAlpha > 0)
            {
                // Keep the completed cached SVG pixel-aligned at the native handover.
                g.PixelOffsetMode = PixelOffsetMode.Default;
                logoMatrix.Matrix33 = (float)logoAlpha; logoAttributes.SetColorMatrix(logoMatrix);
                if(logoAlpha>=1&&Math.Abs(scale-1)<.000001)g.DrawImageUnscaled(logo,560,425);
                else g.DrawImage(logo, new Rectangle(560, 425, 800, 230), 0, 0, 800, 230, GraphicsUnit.Pixel, logoAttributes);
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            }
            if (mode == 2) return;
            for (var i = 0; i < 3; i++) { bodies[i].Reset(); glows[i].Reset(); }
            var eased = mode == 4 ? 1 + t * .08 : Ease(t);
            foreach (var p in particles)
            {
                double x, y;
                if (mode == 0) Idle(p, elapsedMs, out x, out y);
                else if (mode == 1) { x = Mix(p.StartX, p.LogoX, eased); y = Mix(p.StartY, p.LogoY, eased); }
                else { x = Mix(p.LogoX, p.BurstX, eased); y = Mix(p.LogoY, p.BurstY, eased); }
                var group = mode == 0 ? 0 : p.Group; var size = p.Size;
                bodies[group].AddRectangle(new RectangleF((float)x, (float)y, (float)size, (float)size));
                if (p.Seed > .86) glows[group].AddRectangle(new RectangleF((float)(x - size * 2), (float)(y - size * 2), (float)(size * 5), (float)(size * 5)));
            }
            var alpha = mode == 4 ? 1 - t : mode == 1 ? 1 - Clamp((t - .45) / .55) * .82 : 1;
            for (var i = mode == 0 ? 0 : 1; i < (mode == 0 ? 1 : 3); i++)
            {
                bodyBrushes[i].Color = Color.FromArgb((int)Math.Round(alpha * 255), colors[i]);
                glowBrushes[i].Color = Color.FromArgb((int)Math.Round(alpha * .13 * 255), colors[i]);
                g.FillPath(bodyBrushes[i], bodies[i]); g.FillPath(glowBrushes[i], glows[i]);
            }
        }
        finally { g.Restore(state); }
    }

    public void Dispose()
    {
        if (disposed) return; disposed = true; logo.Dispose(); hud.Dispose(); logoAttributes.Dispose(); hudAttributes.Dispose();
        for (var i = 0; i < 3; i++) { bodies[i].Dispose(); glows[i].Dispose(); bodyBrushes[i].Dispose(); glowBrushes[i].Dispose(); }
    }
}
