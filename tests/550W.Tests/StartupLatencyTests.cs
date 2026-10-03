using Controller550W.Core;
using Xunit;
namespace Controller550W.Tests;
public class StartupLatencyTests
{
    readonly DateTimeOffset t=DateTimeOffset.Parse("2026-10-02T00:00:00Z");
    [Theory][InlineData(StartTrigger.ProcessStart)][InlineData(StartTrigger.WindowShow)]
    public void ProcessFirstBootDoesNotRepeatWhenSlowWindowArrives(StartTrigger trigger)
    {
        var p=new AppProfile{StartTrigger=trigger};var s=new AppLifecycle();s.Update(p,new(0,0),t,true);
        Assert.True(s.TryBeginProcessBoot(p,1,t.AddSeconds(1)));
        Assert.Equal(RuntimePhase.Starting,s.Phase);
        Assert.False(s.TryBeginProcessBoot(p,4,t.AddSeconds(2)));
        Assert.Null(s.Update(p,new(4,0),t.AddSeconds(3)).Animation);
        Assert.Null(s.Update(p,new(4,1),t.AddSeconds(10)).Animation);
        Assert.Equal(RuntimePhase.Visible,s.Phase);
    }
    [Fact] public void ExistingOrUninitializedOrDisabledOrManagedAppDoesNotReplay()
    {
        var p=new AppProfile();var s=new AppLifecycle();Assert.False(s.TryBeginProcessBoot(p,1,t));
        s.Update(p,new(1,1),t,true);Assert.False(s.TryBeginProcessBoot(p,2,t.AddSeconds(5)));
        s=new();s.Update(p,new(0,0),t,true);p.Enabled=false;Assert.False(s.TryBeginProcessBoot(p,1,t.AddSeconds(1)));
        p.Enabled=true;s.BeginManagedLaunch("test",t);Assert.False(s.TryBeginProcessBoot(p,1,t.AddSeconds(1)));
        Assert.Null(s.Update(p,new(1,1),t.AddSeconds(2)).Animation);
    }
    [Fact] public void ProcessCrashReleasesBootAndLaterRestartWorks()
    {
        var p=new AppProfile();var s=new AppLifecycle();s.Update(p,new(0,0),t,true);
        Assert.True(s.TryBeginProcessBoot(p,1,t.AddSeconds(1)));
        var stopped=s.Update(p,new(0,0),t.AddSeconds(5));Assert.True(stopped.Stopped);Assert.Null(stopped.Animation);
        Assert.True(s.TryBeginProcessBoot(p,1,t.AddSeconds(10)));
        Assert.Null(s.Update(p,new(1,1),t.AddSeconds(15)).Animation);
    }
    [Fact] public void NormalWindowReopenStillWorksAfterProcessFirstBoot()
    {
        var p=new AppProfile{StartTrigger=StartTrigger.WindowShow};var s=new AppLifecycle();s.Update(p,new(0,0),t,true);
        Assert.True(s.TryBeginProcessBoot(p,1,t.AddSeconds(1)));s.Update(p,new(1,1),t.AddSeconds(2));
        s.Update(p,new(1,0),t.AddSeconds(15));Assert.Equal(AnimationKind.Boot,s.Update(p,new(1,1),t.AddSeconds(20)).Animation);
    }
}
