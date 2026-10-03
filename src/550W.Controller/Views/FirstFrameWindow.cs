using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Controller550W.Controller.Native;
using Controller550W.Rendering;

namespace Controller550W.Controller.Views;

// A small native GDI surface. It owns its window procedure; it never hooks a target process.
// Avoids allocating a full-screen WPF rendering surface in the resident watcher.
internal sealed class FirstFrameWindow
{
    delegate nint Procedure(nint hwnd,uint message,nint wparam,nint lparam);
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct WindowClass
    {public uint Style;public Procedure Proc;public int ClassExtra,WindowExtra;public nint Instance,Icon,Cursor,Background;public string? Menu;public string Name;}
    [StructLayout(LayoutKind.Sequential)]struct PaintInfo
    {public nint Dc;public int Erase;public NativeMethods.Rect Paint;public int Restore,Update;[MarshalAs(UnmanagedType.ByValArray,SizeConst=32)]public byte[] Reserved;}
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern ushort RegisterClassW(ref WindowClass cls);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern nint CreateWindowExW(uint ex,string cls,string name,uint style,int x,int y,int width,int height,nint parent,nint menu,nint instance,nint parameter);
    [DllImport("user32.dll")]static extern nint DefWindowProcW(nint hwnd,uint message,nint wparam,nint lparam);
    [DllImport("user32.dll")]static extern nint BeginPaint(nint hwnd,out PaintInfo paint);
    [DllImport("user32.dll")]static extern bool EndPaint(nint hwnd,ref PaintInfo paint);
    [DllImport("user32.dll")]static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll")]static extern bool InvalidateRect(nint hwnd,nint rect,bool erase);
    [DllImport("user32.dll")]static extern bool UpdateWindow(nint hwnd);
    [DllImport("user32.dll")]static extern nint LoadCursorW(nint instance,nint name);
    static readonly Procedure proc=WndProc;static readonly Dictionary<nint,FirstFrameWindow> windows=[];static bool registered;
    static Bitmap? defaultCore;readonly Rectangle bounds;readonly Bitmap? core;readonly ParticleIntroRenderer? intro;readonly Bitmap? scene;readonly bool ownsCore,shutdown,escape;readonly Action skip;
    DispatcherTimer? introTimer;
    readonly BufferedGraphicsContext frameContext=new();BufferedGraphics? frame;
    public long ParticleStartTimestamp {get;private set;}
    readonly TaskCompletionSource rendered=new(TaskCreationOptions.RunContinuationsAsynchronously);nint handle;double progress=-1;OverlayCoverGuard? coverGuard;bool backdrop;
    public bool ParticlesPainted {get;private set;}
    public Task Rendered=>rendered.Task;public bool IsVisible=>handle!=0;
    public FirstFrameWindow(Rectangle bounds,string path,bool shutdown,bool allowEscape,Action skip,AnimationSettings? firstFrameSettings=null)
    {
        this.bounds=bounds;this.shutdown=shutdown;escape=allowEscape;this.skip=skip;
        if(!shutdown){if(firstFrameSettings!=null)intro=new ParticleIntroRenderer(firstFrameSettings,Path.Combine(AppContext.BaseDirectory,"Themes","550W"));return;}
        if(path.Equals(DefaultPath,StringComparison.OrdinalIgnoreCase)){defaultCore??=Read(DefaultPath)??Fallback();core=defaultCore;}
        else{core=Read(path)??Read(DefaultPath)??Fallback();ownsCore=true;}
        if(shutdown)scene=Read(Path.Combine(AppContext.BaseDirectory,"Themes","550W","assets","shutdown-scene.png"));
    }
    static string DefaultPath=>Path.Combine(AppContext.BaseDirectory,"Themes","550W","assets","core.png");
    static Bitmap? Read(string path){try{using var stream=File.OpenRead(path);using var source=new Bitmap(stream);return new Bitmap(source);}catch{return null;}}
    static Bitmap Fallback(){var b=new Bitmap(640,560);using var g=Graphics.FromImage(b);g.Clear(Color.Transparent);using var pen=new Pen(Color.DimGray,8);using var fill=new SolidBrush(Color.FromArgb(29,31,34));g.FillPolygon(fill,new Point[]{new(80,90),new(140,40),new(500,40),new(560,90),new(560,470),new(500,520),new(140,520),new(80,470)});g.DrawEllipse(pen,160,120,320,320);g.FillEllipse(Brushes.Black,172,132,296,296);using var red=new SolidBrush(Color.FromArgb(255,45,45));g.FillEllipse(red,272,232,96,96);return b;}
    public void Show()
    {
        if(handle!=0)return;if(!registered){var cls=new WindowClass{Proc=proc,Name="550W.Native.FirstFrame",Cursor=LoadCursorW(0,32512)};if(RegisterClassW(ref cls)==0)throw new IOException("原生首帧窗口创建失败。");registered=true;}
        handle=CreateWindowExW(0x88,"550W.Native.FirstFrame","550W · First Frame",0x80000000,bounds.X,bounds.Y,bounds.Width,bounds.Height,0,0,0,0);if(handle==0)throw new IOException("原生首帧窗口未创建。");windows[handle]=this;
        NativeMethods.SetWindowPos(handle,-1,bounds.X,bounds.Y,bounds.Width,bounds.Height,0x40);NativeMethods.SetForegroundWindow(handle);UpdateWindow(handle);
        if(!shutdown)coverGuard=new OverlayCoverGuard(handle,()=>!backdrop&&handle!=0);
        if(intro!=null){introTimer=new DispatcherTimer(DispatcherPriority.Render){Interval=TimeSpan.FromMilliseconds(1000d/60)};introTimer.Tick+=(_,_)=>{if(handle!=0&&!backdrop){InvalidateRect(handle,0,false);UpdateWindow(handle);}};introTimer.Start();}
    }
    static nint WndProc(nint hwnd,uint message,nint wparam,nint lparam)
    {
        if(!windows.TryGetValue(hwnd,out var window))return DefWindowProcW(hwnd,message,wparam,lparam);
        if(message==0xF){var dc=BeginPaint(hwnd,out var paint);try{window.PaintFrame(dc);window.rendered.TrySetResult();}catch{window.rendered.TrySetResult();}finally{EndPaint(hwnd,ref paint);}return 0;}
        if(message==0x14)return 1;
        if(message==0x10||message==0x100&&(long)wparam==0x1B&&window.escape){window.skip();return 0;}
        return DefWindowProcW(hwnd,message,wparam,lparam);
    }
    void PaintFrame(nint dc)
    {
        // Clear and draw offscreen, then copy the complete frame once. Never expose
        // the black clear between the logo/particle/HUD drawing operations.
        if(frame==null){frameContext.MaximumBuffer=new Size(bounds.Width+1,bounds.Height+1);frame=frameContext.Allocate(dc,new Rectangle(0,0,bounds.Width,bounds.Height));}
        if(!backdrop)Draw(frame.Graphics);
        frame.Render(dc);
    }
    void Draw(Graphics g)
    {
        g.Clear(Color.Black);g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
        var scale=Math.Min(bounds.Width/1920d,bounds.Height/1080d);
        if(!shutdown&&!backdrop&&intro!=null){if(ParticleStartTimestamp==0)ParticleStartTimestamp=Stopwatch.GetTimestamp();intro.Draw(g,Stopwatch.GetElapsedTime(ParticleStartTimestamp).TotalMilliseconds,bounds.Width,bounds.Height);ParticlesPainted=true;}
        if(shutdown&&scene!=null)
        {
            var p=progress<0?0:progress;var s=p<.36?1:Math.Max(.01,1-(p-.36)/.26*.99);
            if(p<.62)DrawImage(g,scene,bounds.Width/2d,bounds.Height/2d,1920*scale*s,1080*scale*s,1);
            if(p>=.36&&p<.87){var y=p<.62?1:p<.78?Math.Max(.012,1-(p-.62)/.16):.012;var x=p<.78?1:Math.Max(.005,1-(p-.78)/.09);DrawImage(g,core!,bounds.Width/2d,bounds.Height/2d,250*scale*x,330*scale*y,Math.Min(1,(p-.36)/.2));}
            if(p>=.87&&p<1){var size=(float)(5*scale*(1-p)/.13);g.FillEllipse(Brushes.White,(float)bounds.Width/2-size/2,(float)bounds.Height/2-size/2,size,size);}
        }
        else if(shutdown)DrawImage(g,core!,bounds.Width/2d,bounds.Height/2d,250*scale,330*scale,1);

    }
    static void DrawImage(Graphics g,Bitmap image,double x,double y,double width,double height,double alpha)
    {
        var fit=Math.Min(width/image.Width,height/image.Height);width=image.Width*fit;height=image.Height*fit;if(width<1||height<1)return;
        using var attributes=new ImageAttributes();var matrix=new ColorMatrix{Matrix33=(float)Math.Clamp(alpha,0,1)};attributes.SetColorMatrix(matrix);
        g.DrawImage(image,new Rectangle((int)(x-width/2),(int)(y-height/2),Math.Max(1,(int)width),Math.Max(1,(int)height)),0,0,image.Width,image.Height,GraphicsUnit.Pixel,attributes);
    }
    public void HoldBackdrop(){backdrop=true;introTimer?.Stop();intro?.Dispose();coverGuard?.Dispose();/* Keep the last complete frame underneath the committed WebView. */}
    public void Close(){introTimer?.Stop();intro?.Dispose();coverGuard?.Dispose();frame?.Dispose();frame=null;frameContext.Dispose();if(handle==0)return;var old=handle;handle=0;windows.Remove(old);DestroyWindow(old);scene?.Dispose();if(ownsCore)core?.Dispose();rendered.TrySetCanceled();}
    public async Task FallbackCollapse(int duration)
    {
        var time=Stopwatch.StartNew();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(25)};timer.Tick+=(_,_)=>{progress=Math.Min(1,time.Elapsed.TotalMilliseconds/duration);if(handle!=0){InvalidateRect(handle,0,false);UpdateWindow(handle);}};timer.Start();try{await Task.Delay(duration);}finally{timer.Stop();Close();}
    }
}
