using System.Collections.Concurrent;
using System.Diagnostics;
using Controller550W.Controller.Native;

namespace Controller550W.Controller.Services;

internal sealed class OptimizedLaunchService(Func<ControllerSettings> settings, ApplicationMonitor monitor, AnimationCoordinator animation, LogService log)
{
    readonly ConcurrentDictionary<string,byte> launching=new();
    public async Task Launch(AppProfile profile,long origin=0)
    {
        if(origin==0)origin=Stopwatch.GetTimestamp();
        if(!launching.TryAdd(profile.Id,0))return;
        try
        {
            var existing=monitor.FindWindow(profile);
            if(existing!=0){WindowPresentationService.Apply(existing,profile.WindowPresentation,true);log.Write("LaunchExistingWindow",profile.DisplayName);return;}
            monitor.BeginManagedLaunch(profile,Guid.NewGuid().ToString("N"));
            bool animate=profile.Enabled&&!settings().Paused;
            if(profile.AppLaunchOffsetMs<0){await Task.Run(()=>StartApp(profile));await Task.Delay(-profile.AppLaunchOffsetMs);}
            var firstVisual=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var offset=animate&&profile.AppLaunchOffsetMs>0?Task.Delay(profile.AppLaunchOffsetMs):Task.CompletedTask;
            if(animate)animation.RequestAnimation(profile,AnimationKind.Boot,managed:true,origin:origin,firstVisual:()=>firstVisual.TrySetResult());
            if(profile.AppLaunchOffsetMs>=0)
            {
                // The signal comes from the painted native cover, never a fixed startup delay.
                // A failed cover must not prevent the target from launching.
                if(animate)try{await firstVisual.Task.WaitAsync(TimeSpan.FromSeconds(1));}catch(TimeoutException){}
                await offset;await Task.Run(()=>StartApp(profile));
            }
            await Task.Delay(8000);
        }
        catch(Exception e){log.Write("ManagedLaunchFailed",$"{profile.DisplayName}, {e.GetType().Name}: {e.Message}");animation.CancelProfile(profile.Id);}
        finally{launching.TryRemove(profile.Id,out _);}
    }
    static void StartApp(AppProfile p)
    {
        var target=string.IsNullOrWhiteSpace(p.LaunchTarget)?p.ExecutablePath:p.LaunchTarget;
        if(string.IsNullOrWhiteSpace(target))throw new IOException("请先为此应用配置启动目标。");
        var info=new ProcessStartInfo{UseShellExecute=true};
        if(p.LaunchKind==LaunchTargetKind.Executable&&PackageMetadata.ForExecutable(target) is {} package){p.LaunchKind=LaunchTargetKind.AppUserModelId;p.LaunchTarget=target=package.AppId;if(p.IconSource.Length==0)p.IconSource=package.Icon;}
        if(p.LaunchKind is LaunchTargetKind.AppUserModelId or LaunchTargetKind.ShellAppsFolder)
        {info.FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe");info.ArgumentList.Add(target.StartsWith("shell:AppsFolder\\",StringComparison.OrdinalIgnoreCase)?target:"shell:AppsFolder\\"+target);}
        else if(p.LaunchKind==LaunchTargetKind.UriProtocol)
        {if(!Uri.TryCreate(target,UriKind.Absolute,out var uri)||new[]{"javascript","data","file"}.Contains(uri.Scheme))throw new IOException("启动协议无效。");info.FileName=target;}
        else {info.FileName=target;info.Arguments=p.LaunchArguments;if(p.WorkingDirectory.Length>0)info.WorkingDirectory=p.WorkingDirectory;}
        Process.Start(info)?.Dispose();
    }
}
