using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Controller550W.Core;
using Controller550W.Controller.Views;

static class Program
{
    static readonly List<string> checks=[];
    [STAThread] static int Main(string[] args)
    {
        var output=Path.GetFullPath(args[0]); Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        try
        {
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            var config=new ControllerSettings{Profiles=[new(){DisplayName="Fixture AI",UseIndependentSettings=true}]};
            ControllerSettings? saved=null; var saveCount=0;
            var w=new SettingsWindow(config,value=>{saved=value;saveCount++;});
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            object? Invoke(string name,params object[] values)=>typeof(SettingsWindow).GetMethod(name,flags)!.Invoke(w,values);
            var edit=(ControllerSettings)typeof(SettingsWindow).GetField("edit",flags)!.GetValue(w)!;
            var status=(TextBlock)typeof(SettingsWindow).GetField("status",flags)!.GetValue(w)!;
            foreach(var a in new[]{edit.Animation,edit.Profiles[0].AnimationOverrides})
            {
                var controls=(DependencyObject)Invoke("AnimationControls",a)!;
                var paths=Walk(controls).OfType<TextBox>().Select(t=>BindingOperations.GetBinding(t,TextBox.TextProperty)?.Path.Path??"").ToArray();
                Check(new[]{"ParticleResidueMs","ParticleToLogoMs","LogoHoldMs"}.All(x=>paths.Count(p=>p==x)==1),"Three distinct bound timings exist in global/independent controls");
                Check(!new[]{"LogoRevealMs","CoreHoldMs","CoreDissolveMs"}.Any(paths.Contains),"Deprecated regroup/robot timing fields are absent");
                Check(new[]{"LogoBurstMs","ParticleCount","MultiWindowRevealMs","HandoffCompressMs","FadeMs"}.All(paths.Contains),"Later effective animation fields retained");
                var text=Walk(controls).OfType<TextBlock>().Select(t=>t.Text).ToArray();
                Check(new[]{"中央粒子待机时长（毫秒）","粒子 → 550C 总时长（毫秒）","550C 完整停留时长（毫秒）"}.All(text.Contains),"Three timing labels use unambiguous Chinese");
                var help=Walk(controls).OfType<Button>().Select(b=>b.ToolTip).OfType<ToolTip>().Select(t=>(t.Content as TextBlock)?.Text??"").ToArray();
                Check(help.Any(x=>x.Contains("实际重组时间 = 总时长 − 待机时长"))&&help.Any(x=>x.Contains("并行准备后续动画资源")),"Timing help explains subtraction and concurrent preparation");
                Check(text.Any(x=>x.Contains("整体时间倍率缩放")),"Timing multiplier is explained");
            }
            edit.Animation.ParticleResidueMs=1000;edit.Animation.ParticleToLogoMs=800;
            Invoke("Save"); Check(saveCount==0&&status.Text.Contains("总时长必须大于中央粒子待机时长"),"Invalid global combination blocks save with clear Chinese message");
            Check(edit.Animation.ParticleResidueMs==1000&&edit.Animation.ParticleToLogoMs==800,"Invalid global input is not silently changed");
            edit.Animation.ParticleResidueMs=500;edit.Animation.ParticleToLogoMs=1000;
            var independent=edit.Profiles[0].AnimationOverrides; independent.ParticleResidueMs=1500;independent.ParticleToLogoMs=1500;
            Invoke("Save");Check(saveCount==0&&status.Text.StartsWith("Fixture AI："),"Equal total/idle blocks independent save and names application");
            Invoke("ApplyToAll");Check(saveCount==0&&status.Text.Contains("总时长必须大于中央粒子待机时长"),"Invalid combination blocks bulk apply before confirmation dialog");
            independent.ParticleToLogoMs=2100;independent.LogoHoldMs=320;
            Invoke("Save");Check(saveCount==1&&saved!.Profiles[0].AnimationOverrides.ParticleToLogoMs==2100&&saved.Profiles[0].AnimationOverrides.LogoHoldMs==320,"Valid exact new timings reach save callback");
            Check(!w.IsVisible,"No desktop settings window displayed"); w.Close();
            File.WriteAllText(output,JsonSerializer.Serialize(new{pass=true,checks,desktopWindowsShown=0},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("PASS "+checks.Count+" hidden V4.5 timing UI checks");return 0;
        }
        catch(Exception e){File.WriteAllText(output,JsonSerializer.Serialize(new{pass=false,error=e.ToString()}));Console.Error.WriteLine(e);return 1;}
    }
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);checks.Add(name);}
    static IEnumerable<DependencyObject> Walk(DependencyObject root){yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Walk(child))yield return item;}
}
