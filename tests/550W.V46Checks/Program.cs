using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Controller550W.Core;
using Controller550W.Rendering;

static class Program
{
    static readonly List<string> checks=[];
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
    static void Check(bool valid,string label){if(!valid)throw new Exception(label);checks.Add(label);Console.WriteLine("PASS "+label);}
    static byte[] Pixels(Bitmap image){var bits=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);try{var bytes=new byte[bits.Stride*bits.Height];Marshal.Copy(bits.Scan0,bytes,0,bytes.Length);return bytes;}finally{image.UnlockBits(bits);}}
    static string Hash(Bitmap image)=>Convert.ToHexString(SHA256.HashData(Pixels(image)));
    static Bitmap Draw(ParticleIntroRenderer r,double ms){var b=new Bitmap(1920,1080,PixelFormat.Format32bppPArgb);using var g=Graphics.FromImage(b);g.Clear(Color.Transparent);r.Draw(g,ms,1920,1080);return b;}
    [STAThread] static int Main(string[] args)
    {
        var root=Path.GetFullPath(args[0]);var output=Path.Combine(root,"docs/native-test-v46-results.json");
        try
        {
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            var theme=Path.Combine(root,"Themes/550W");
            using(var r=new ParticleIntroRenderer(new AnimationSettings{ParticleResidueMs=500,ParticleToLogoMs=1000,LogoHoldMs=450},theme))
            {
                foreach(var (time,name) in new[]{(0,"01-initial"),(250,"02-idle"),(750,"03-reveal"),(1000,"04-logo")})
                {using var actual=Draw(r,time);using var old=new Bitmap(Path.Combine(root,"docs/images-v45-native",name+".png"));Check(Hash(actual)==Hash(old),"Unchanged V4.5 native pixels: "+name);}
            }
            // Reproduce the reported short settings and a WebView not ready until 4.4 seconds.
            var cfg=new AnimationSettings{ParticleResidueMs=500,ParticleToLogoMs=1500,LogoHoldMs=300,LogoBurstMs=500,MultiWindowRevealMs=100,DurationMultiplier=.75};
            var hudStart=(cfg.ParticleToLogoMs+cfg.LogoHoldMs+cfg.LogoBurstMs)*cfg.DurationMultiplier;
            var hudEnd=hudStart+cfg.MultiWindowRevealMs*cfg.DurationMultiplier;
            using(var r=new ParticleIntroRenderer(cfg,theme))
            {
                var blackFrames=0;var samples=0;
                for(double time=0;time<=4400;time+=1000d/60){using var image=Draw(r,time);var pixels=Pixels(image);var lit=0;for(var i=0;i<pixels.Length;i+=4)if(pixels[i+3]>0&&(pixels[i]>8||pixels[i+1]>8||pixels[i+2]>8))lit++;if(lit<100)blackFrames++;samples++;}
                Check(blackFrames==0,$"No black frame in {samples} native frames through delayed 4400ms WebView readiness");
                using var before=Draw(r,hudStart);using var cross=Draw(r,hudStart+cfg.MultiWindowRevealMs*cfg.DurationMultiplier/2);using var after=Draw(r,hudEnd);using var late=Draw(r,4400);
                using var expected=new Bitmap(Path.Combine(theme,"assets/boot-hud-movie_red.png"));
                Directory.CreateDirectory(Path.Combine(root,"docs/images-v46"));after.Save(Path.Combine(root,"docs/images-v46/native-hud-end.png"));
                Check(Hash(after)==Hash(expected)&&Hash(late)==Hash(expected),"HUD already complete at configured crossfade end and remains until web commit");
                Check(cross.GetPixel(10,40).A>before.GetPixel(10,40).A,"Original HUD appears during particle crossfade, not after a delay");
                Directory.CreateDirectory(Path.Combine(root,"docs/images-v46"));cross.Save(Path.Combine(root,"docs/images-v46/native-crossfade.png"));late.Save(Path.Combine(root,"docs/images-v46/native-late-webview.png"));
            }
            foreach(var scheme in new[]{"movie_red","green","cyan","white","amber"})
            {using var r=new ParticleIntroRenderer(new AnimationSettings{ColorProfile=scheme,MultiWindowRevealMs=0},theme);using var image=Draw(r,8000);using var expected=new Bitmap(Path.Combine(theme,"assets/boot-hud-"+scheme+".png"));Check(Hash(image)==Hash(expected),"Zero-duration transition preserves original HUD palette: "+scheme);}

            var controller=typeof(Controller550W.Controller.Views.SettingsWindow).Assembly;
            var shellType=controller.GetType("Controller550W.Controller.Views.FirstFrameWindow")!;
            var shell=Activator.CreateInstance(shellType,Flags,null,[new Rectangle(0,0,1920,1080),"",false,false,(Action)(()=>{}),cfg],null)!;
            var paint=shellType.GetMethod("PaintFrame",Flags)!;
            shellType.GetField("<ParticleStartTimestamp>k__BackingField",Flags)!.SetValue(shell,Stopwatch.GetTimestamp()-(long)(4.4*Stopwatch.Frequency));
            using(var surface=new Bitmap(1920,1080,PixelFormat.Format32bppPArgb))
            {
                void Paint(){using var g=Graphics.FromImage(surface);var dc=g.GetHdc();try{paint.Invoke(shell,[dc]);}finally{g.ReleaseHdc(dc);}}
                Paint();var expected=Hash(surface);shellType.GetMethod("HoldBackdrop",Flags)!.Invoke(shell,null);
                for(var i=0;i<12;i++){Paint();if(Hash(surface)!=expected)throw new Exception("Backdrop cleared or alternated after commit");}
                Check(true,"Buffered native paint preserves a complete frame across 12 backdrop repaints");
                shellType.GetMethod("Close",Flags)!.Invoke(shell,null);shellType.GetMethod("Close",Flags)!.Invoke(shell,null);
                Check(shellType.GetField("frame",Flags)!.GetValue(shell)==null,"Buffered surface released; repeated close is safe");
            }

            // Exercise the real WPF hover path that raised the user's NotSupportedException.
            var serviceType=typeof(ToolTip).Assembly.GetType("System.Windows.Controls.PopupControlService")!;
            var service=serviceType.GetProperty("Current",Flags)!.GetValue(null)!;
            var show=serviceType.GetMethod("ShowToolTip",Flags)!;var dismiss=serviceType.GetMethod("DismissToolTips",Flags)!;
            var oldTip=new ToolTip{Content="fixture",StaysOpen=false,Visibility=Visibility.Hidden,Opacity=0};
            var reproduced=false;try{show.Invoke(service,[new Button{ToolTip=oldTip},false]);}catch(TargetInvocationException e){reproduced=e.InnerException is NotSupportedException;}finally{oldTip.IsOpen=false;dismiss.Invoke(service,null);}
            Check(reproduced,"Baseline help hover reproduces NotSupportedException notification trigger");
            var ui=controller.GetType("Controller550W.Controller.Views.Ui")!;
            var button=(Button)ui.GetMethod("Help",Flags)!.Invoke(null,["Fixture help text"])!;
            var tip=(ToolTip)button.ToolTip;tip.Visibility=Visibility.Hidden;tip.Opacity=0;tip.Placement=PlacementMode.Absolute;tip.HorizontalOffset=-20000;tip.VerticalOffset=-20000;
            var errors=0;app.DispatcherUnhandledException+=(_,e)=>{errors++;e.Handled=true;};
            for(var i=0;i<20;i++){show.Invoke(service,[button,false]);CheckOpen(tip);tip.IsOpen=false;dismiss.Invoke(service,null);button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));CheckOpen(tip);tip.IsOpen=false;dismiss.Invoke(service,null);}
            Check(errors==0,"20 hover/click/dismiss cycles cause no exception and no generic error notification");
            Check(tip.StaysOpen&&!tip.IsOpen,"ToolTipService owns normal dismissal; help remains usable");
            File.WriteAllText(output,JsonSerializer.Serialize(new{pass=true,checks,hudStartMs=hudStart,hudCompleteMs=hudEnd,lateWebviewMs=4400,desktopWindowsShown=0},new JsonSerializerOptions{WriteIndented=true}));
            return 0;
        }
        catch(Exception e){File.WriteAllText(output,JsonSerializer.Serialize(new{pass=false,checks,error=e.ToString()},new JsonSerializerOptions{WriteIndented=true}));Console.Error.WriteLine(e);return 1;}
    }
    static void CheckOpen(ToolTip tip){if(!tip.IsOpen)throw new Exception("Help tooltip did not open");}
}
