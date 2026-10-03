using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows;
using Controller550W.Controller.Native;
using Controller550W.Controller.Views;

namespace Controller550W.Controller.Services;

internal sealed class ManagedCloseService(Func<ControllerSettings> settings, ApplicationMonitor monitor, AnimationCoordinator animation, LogService log) : IDisposable
{
    sealed class CloseSession
    {
        public readonly ManagedCloseLifecycle State=new(); public readonly CancellationTokenSource Life=new();
        public nint Hwnd; public System.Drawing.Rectangle PreparedScreen; public AnimationHostSession? Host; public Task Prepare=Task.CompletedTask; public CloseConfirmationWindow? Dialog;public bool Confirmed,Cancelling;
    }
    readonly ConcurrentDictionary<string,CloseSession> sessions=new();bool disposed;
    public void Request(AppProfile profile)
    {
        if(!Application.Current.Dispatcher.CheckAccess()){Application.Current.Dispatcher.BeginInvoke(()=>Request(profile));return;}
        if(disposed)return;var hwnd=monitor.FindWindow(profile);if(hwnd==0){log.Write("CloseTargetUnavailable",profile.DisplayName);return;}
        if(!profile.OptimizedShutdown||settings().Paused){NormalClose(hwnd,profile.DisplayName);return;}
        var current=new CloseSession{Hwnd=hwnd};if(!sessions.TryAdd(profile.Id,current)) {sessions[profile.Id].Dialog?.Activate();return;}
        current.State.TryRequest();Log(current,profile);current.State.Preparing();Log(current,profile);
        // Start preparation before showing confirmation. It is always hidden and silent.
        current.Prepare=Prepare(current,profile);
        current.State.WaitConfirmation();Log(current,profile);
        if(profile.CloseConfirmation)
        {try{
            current.Dialog=new(profile.DisplayName);current.Dialog.Confirmed+=()=>{_=Confirm(current,profile);};current.Dialog.Cancelled+=()=>{_=Cancel(current,profile);};
            if(hwnd!=0)new System.Windows.Interop.WindowInteropHelper(current.Dialog).Owner=hwnd;current.Dialog.Show();
            log.Write("CloseConfirmationShown",profile.DisplayName);
        }catch(Exception e){log.Write("CloseConfirmationUnavailable",e.GetType().Name);NormalClose(hwnd,profile.DisplayName);_=Cancel(current,profile);}}
        else _=Confirm(current,profile);
    }
    void Log(CloseSession s,AppProfile p)=>log.Write("ManagedCloseState",$"{p.DisplayName}, {s.State.Phase}, session={s.State.SessionId}");
    async Task Prepare(CloseSession s,AppProfile p)
    {
        try
        {
            var session=animation.CreateSession(p,AnimationKind.Shutdown,s.Hwnd);session.Preload=true;s.PreparedScreen=new(session.MonitorX,session.MonitorY,session.MonitorWidth,session.MonitorHeight);s.Host=new(session,s.Life.Token);
            s.Host.Message+=(type,detail)=>{log.Write(type=="PRELOAD_READY"?"ShutdownPreloadReady":type=="error"?"ShutdownPreloadFailed":type,$"{p.DisplayName}, {detail}");};
            await s.Host.StartAsync();await s.Host.Prepared.Task.WaitAsync(TimeSpan.FromSeconds(10),s.Life.Token);
        }
        catch(Exception e){if(!s.Life.IsCancellationRequested)log.Write("ShutdownPreloadFailed",$"{p.DisplayName}, {e.GetType().Name}");}
    }
    async Task Cancel(CloseSession s,AppProfile p)
    {
        if(s.Confirmed||s.Cancelling)return;s.Cancelling=true;s.Dialog?.Dismiss();s.State.Cancel();Log(s,p);s.Life.Cancel();await Release(s,p);log.Write("ShutdownCancelled",p.DisplayName);
    }
    public void CancelRequest(AppProfile p){if(sessions.TryGetValue(p.Id,out var s)&&!s.Confirmed)_=Cancel(s,p);}
    async Task Confirm(CloseSession s,AppProfile p)
    {
        if(!s.State.Confirm())return;s.Confirmed=true;Log(s,p);animation.CancelProfile(p.Id);
        FirstFrameWindow? shell=null;var watch=Stopwatch.StartNew();bool visible=false;
        try
        {
            var session=animation.CreateSession(p,AnimationKind.Shutdown,s.Hwnd);
            shell=new(new(session.MonitorX,session.MonitorY,session.MonitorWidth,session.MonitorHeight),session.CoreImagePath,true,session.Settings.AllowEscape,()=>{shell?.Close();});shell.Show();
            await shell.Rendered.WaitAsync(TimeSpan.FromMilliseconds(900));try{await Task.Run(NativeMethods.DwmFlush).WaitAsync(TimeSpan.FromMilliseconds(400));}catch(TimeoutException){}visible=true;log.Write("SHUTDOWN_OVERLAY_VISIBLE",$"{p.DisplayName}, confirmElapsed={watch.ElapsedMilliseconds}ms");
            // A target may move to another monitor while confirmation is open.
            // Never activate an already prepared overlay on the previous screen.
            var screenMatches=s.PreparedScreen==new System.Drawing.Rectangle(session.MonitorX,session.MonitorY,session.MonitorWidth,session.MonitorHeight);
            if(s.Host!=null&&s.Host.Prepared.Task.IsCompletedSuccessfully&&s.Host.Available&&screenMatches)
            {
                await s.Host.Send(new{type="activate"});await s.Host.ShutdownBegan.Task.WaitAsync(TimeSpan.FromMilliseconds(650));
                log.Write("SHUTDOWN_BEGIN_CONFIRMED",p.DisplayName);
                // The native cover stays until the already painted WebView has moved above the target.
                await Task.Delay(80);shell.Close();shell=null;
                CloseOnce(s,p,true,false);await s.Host.Completion.WaitAsync(TimeSpan.FromSeconds(7));
            }
            else
            {
                log.Write("ShutdownNativeFallback",p.DisplayName);var fallback=shell.FallbackCollapse(session.Settings.Scale(session.Settings.ShutdownMs));
                log.Write("SHUTDOWN_BEGIN_CONFIRMED",p.DisplayName);CloseOnce(s,p,true,false);await fallback;shell=null;
            }
        }
        catch(Exception e){log.Write("ShutdownFallback",$"{p.DisplayName}, {e.GetType().Name}");CloseOnce(s,p,visible,true);}
        finally {shell?.Close();s.State.Complete();Log(s,p);s.Life.Cancel();await Release(s,p);}
    }
    void CloseOnce(CloseSession s,AppProfile p,bool visible,bool fallback)
    {if(s.State.RequestNormalClose(visible,fallback)){Log(s,p);NormalClose(NativeMethods.IsWindow(s.Hwnd)?s.Hwnd:monitor.FindWindow(p),p.DisplayName);}}
    void NormalClose(nint hwnd,string name)
    {if(hwnd!=0&&NativeMethods.IsWindow(hwnd)){var sent=NativeMethods.PostMessage(hwnd,0x10,0,0);log.Write("WM_CLOSE",$"{name}, hwnd={hwnd}, sent={sent}");}else log.Write("CloseTargetUnavailable",name);}
    async Task Release(CloseSession s,AppProfile p)
    {try{await s.Prepare.WaitAsync(TimeSpan.FromMilliseconds(900));}catch{}if(s.Host!=null)await s.Host.DisposeAsync();sessions.TryRemove(p.Id,out _);s.Life.Dispose();}
    public void Dispose()
    {disposed=true;foreach(var pair in sessions){var s=pair.Value;if(s.Confirmed&&s.State.RequestNormalClose(false,true))NormalClose(s.Hwnd,pair.Key);s.Life.Cancel();s.Dialog?.Dismiss();if(s.Host!=null)_=s.Host.DisposeAsync();}sessions.Clear();}
}
