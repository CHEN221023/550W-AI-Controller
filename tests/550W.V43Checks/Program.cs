using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Controller550W.Core;
using Controller550W.Controller.Views;
using Controller550W.Controller.Services;

static class Program
{
    static readonly List<string> checks=[];
    [STAThread] static int Main(string[] args)
    {
        var output=Path.GetFullPath(args[0]);Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        try
        {
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            var config=new ControllerSettings{Profiles=[new(){DisplayName="Fixture AI",UseIndependentSettings=true}]};
            var w=new SettingsWindow(config,_=>{});
            object Invoke(string method,params object[] values)=>typeof(SettingsWindow).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(w,values)!;
            string[] Paths(DependencyObject root)=>Walk(root).OfType<TextBox>().Select(t=>BindingOperations.GetBinding(t,TextBox.TextProperty)?.Path.Path??"").ToArray();
            foreach(var a in new[]{config.Animation,config.Profiles[0].AnimationOverrides})
            {
                var controls=(DependencyObject)Invoke("AnimationControls",a);var fields=Paths(controls);
                Check(!fields.Contains("CoreHoldMs")&&!fields.Contains("CoreDissolveMs"),"Removed core timing controls in global/independent settings");
                Check(new[]{"ParticleResidueMs","LogoRevealMs","LogoHoldMs","LogoBurstMs","ParticleCount","ParticleSize","ParticleSpread","MultiWindowRevealMs","HandoffCompressMs","FadeMs"}.All(fields.Contains),"Retained active particle/logo/HUD/handoff settings");
                var text=string.Join("\n",Walk(controls).OfType<TextBlock>().Select(t=>t.Text));Check(!new[]{"机器人","四点","激光","核心阶段","核心粒子化"}.Any(text.Contains),"Removed obsolete visible descriptions");
            }
            foreach(var global in new[]{true,false})
            {
                var sound=(DependencyObject)Invoke("SoundControls",config.Audio,global);
                var keys=Walk(sound).OfType<ComboBox>().SelectMany(c=>c.Items.Cast<object>()).Select(x=>x.GetType().GetProperty("Value")?.GetValue(x)?.ToString()??"").ToArray();
                Check(!keys.Contains("CORE")&&!keys.Contains("POWER_ON"),"Removed robot voice/sound selectors");Check(keys.Contains("CORE_EXPAND")&&keys.Contains("HUD_SCAN"),"Retained logo/HUD audio selectors");
            }
            var identification=new StackPanel();Invoke("BuildIdentification",identification,config.Profiles[0]);Check(!Walk(identification).OfType<Button>().Any(b=>b.Content?.ToString()?.Contains("核心")==true),"Removed obsolete core asset picker");
            var about=(DependencyObject)Invoke("About");var aboutText=Walk(about).OfType<TextBlock>().Select(t=>t.Text).ToArray();
            Check(ProductChangelog.Entries.Select(x=>x.Version).SequenceEqual(new[]{"V4.3 Beta","V4.2 Beta"}),"Changelog starts at V4.2 Beta and includes V4.3 Beta");
            Check(aboutText.Any(x=>x==ProductChangelog.BetaNotice)&&ProductChangelog.BetaNotice.Contains("V1.0.0"),"Beta notice is present above changelog entries");
            Check(!w.IsVisible,"No settings window displayed");w.Close();
            File.WriteAllText(output,JsonSerializer.Serialize(new{pass=true,checks,desktopWindowsShown=0},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("PASS "+checks.Count+" focused hidden UI checks");return 0;
        }
        catch(Exception e){File.WriteAllText(output,JsonSerializer.Serialize(new{pass=false,error=e.ToString()}));Console.Error.WriteLine(e);return 1;}
    }
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);checks.Add(name);}
    static IEnumerable<DependencyObject> Walk(DependencyObject root){yield return root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Walk(child))yield return item;}
}
