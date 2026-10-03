using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Controller550W.Controller.Native;

namespace Controller550W.Controller.Services;

internal sealed class CaptionProxyService : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]struct TitleBarInfo
    {public uint Size;public NativeMethods.Rect Bar;[MarshalAs(UnmanagedType.ByValArray,SizeConst=6)]public uint[] State;[MarshalAs(UnmanagedType.ByValArray,SizeConst=6)]public NativeMethods.Rect[] Rects;}
    [DllImport("user32.dll")]static extern nint GetForegroundWindow();
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(nint hwnd,StringBuilder text,int count);
    [DllImport("user32.dll")]static extern nint SetWindowLongPtrW(nint hwnd,int index,nint value);
    readonly Func<ControllerSettings> settings;readonly ApplicationMonitor monitor;readonly ManagedCloseService close;readonly LogService log;
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(200)};Window? proxy;nint tracked;PhysicalRect bounds;bool disposed;
    public CaptionProxyService(Func<ControllerSettings> settings,ApplicationMonitor monitor,ManagedCloseService close,LogService log)
    {this.settings=settings;this.monitor=monitor;this.close=close;this.log=log;timer.Tick+=(_,_)=>Refresh();monitor.WindowMetadataChanged+=OnMetadata;Configure();}
    public void Configure(){if(settings().Profiles.Any(p=>p.Enabled&&p.OptimizedShutdown&&p.CloseEntry==CloseEntry.AutoCaptionProxy))timer.Start();else{timer.Stop();proxy?.Hide();}}
    void OnMetadata(nint hwnd,uint type){if(disposed||!timer.IsEnabled)return;Application.Current.Dispatcher.BeginInvoke(()=>{if(proxy!=null&&(hwnd==tracked||type==3))proxy.Hide();Refresh();});}
    public static bool TryBounds(nint hwnd,out PhysicalRect result)
    {
        result=default;if(!NativeMethods.IsWindow(hwnd)||NativeMethods.IsIconic(hwnd)||!NativeMethods.IsWindowVisible(hwnd))return false;
        var cls=new StringBuilder(256);GetClassName(hwnd,cls,cls.Capacity);
        // Verified native-caption implementations only. Custom Electron/Store title bars use Managed Close.
        if(!(cls.ToString().StartsWith("HwndWrapper[",StringComparison.Ordinal)||cls.ToString()=="Notepad"))return false;
        if(((long)NativeMethods.GetWindowLongPtrW(hwnd,-16)&0xC00000)!=0xC00000)return false;
        var data=new TitleBarInfo{Size=(uint)Marshal.SizeOf<TitleBarInfo>(),State=new uint[6],Rects=new NativeMethods.Rect[6]};var pointer=Marshal.AllocHGlobal((int)data.Size);
        try{Marshal.StructureToPtr(data,pointer,false);if(NativeMethods.SendMessageTimeout(hwnd,0x33F,0,pointer,0x2|0x20,80,out _)==0)return false;data=Marshal.PtrToStructure<TitleBarInfo>(pointer);
            if((data.State[5]&0x18001)!=0||!NativeMethods.GetWindowRect(hwnd,out var window))return false;var r=data.Rects[5];result=new(r.Left,r.Top,r.Right,r.Bottom);return CaptionGeometry.IsReliable(new(window.Left,window.Top,window.Right,window.Bottom),result);
        }finally{Marshal.FreeHGlobal(pointer);}
    }
    public static string SupportText(nint hwnd)=>TryBounds(hwnd,out _)?"已识别标准 Windows 关闭按钮。代理仅在此窗口位于前台时启用。":"此应用暂不支持可靠的关闭按钮代理，将使用 托管关闭模式。";
    void Refresh()
    {
        if(disposed)return;var cfg=settings();if(cfg.Paused){proxy?.Hide();return;}
        // No UIA/tree polling. Only an enabled, foreground, native-caption target is inspected.
        var foreground=GetForegroundWindow();AppProfile? match=null;
        foreach(var p in cfg.Profiles.Where(p=>p.Enabled&&p.OptimizedShutdown&&p.CloseEntry==CloseEntry.AutoCaptionProxy))if(monitor.FindWindow(p)==foreground){match=p;break;}
        if(match==null||!TryBounds(foreground,out var rect)){proxy?.Hide();return;}
        tracked=foreground;bounds=rect;
        if(proxy==null)
        {
            proxy=new Window{Title="550W · Caption Close Proxy",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=new SolidColorBrush(Color.FromArgb(1,0,0,0)),ShowInTaskbar=false,ShowActivated=false,Topmost=true,Width=40,Height=30};
            proxy.SourceInitialized+=(_,_)=>{var h=new WindowInteropHelper(proxy).Handle;SetWindowLongPtrW(h,-20,(nint)((long)NativeMethods.GetWindowLongPtrW(h,-20)|0x08000080));};
            proxy.MouseLeftButtonDown+=(_,e)=>{e.Handled=true;var hwnd=tracked;proxy.Hide();NativeMethods.GetCursorPos(out var point);if(GetForegroundWindow()==hwnd&&TryBounds(hwnd,out var fresh)&&fresh==bounds&&fresh.Contains(point.X,point.Y)){var p=settings().Profiles.FirstOrDefault(x=>x.Enabled&&x.OptimizedShutdown&&monitor.FindWindow(x)==hwnd);if(p!=null)close.Request(p);}};
        }
        if(!proxy.IsVisible)proxy.Show();NativeMethods.SetWindowPos(new WindowInteropHelper(proxy).Handle,-1,rect.Left,rect.Top,rect.Width,rect.Height,0x10|0x40);
    }
    public void Dispose(){disposed=true;timer.Stop();monitor.WindowMetadataChanged-=OnMetadata;proxy?.Close();proxy=null;}
}
