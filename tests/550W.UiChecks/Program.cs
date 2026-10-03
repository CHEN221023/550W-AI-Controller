using System.IO;
using System.IO.Compression;
using Controller550W.Controller.Services;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Controller550W.Core;
using Controller550W.Controller.Views;

static class Program
{
    static List<string> checks=[];
    [STAThread] static int Main(string[] args)
    {
        var output=Path.GetFullPath(args.FirstOrDefault()??"ui-checks");Directory.CreateDirectory(output);
        try
        {
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            var config=new ControllerSettings{Profiles=[new(){DisplayName="ChatGPT",ProcessNames=["ChatGPT.exe"],ExecutablePath=@"C:\Applications\ChatGPT\ChatGPT.exe",RuntimeStatus="窗口已显示"},new(){DisplayName="Claude",ProcessNames=["Claude.exe"]},new(){DisplayName="DeepSeek",ProcessNames=["DeepSeek.exe"]}]};
            var w=new SettingsWindow(config,_=>{},dataDirectory:Path.Combine(output,"fixture-data"));
            var root=(FrameworkElement)w.Content; w.Content=null; root.Resources.MergedDictionaries.Add(w.Resources); root.Margin=new Thickness(0); root.Width=1272; root.Height=770;
            void Layout(){root.Measure(new Size(1272,770));root.Arrange(new Rect(0,0,1272,770));root.UpdateLayout();Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle); foreach(var visual in Descendants(root).OfType<FrameworkElement>())visual.BeginAnimation(UIElement.OpacityProperty,null);}
            void Capture(string name){Layout();var dv=new DrawingVisual();using(var dc=dv.RenderOpen()){dc.DrawRectangle(w.Background,null,new Rect(0,0,1320,818));dc.DrawRectangle(new VisualBrush(root){AutoLayoutContent=false},null,new Rect(24,24,1272,770));}var bmp=new RenderTargetBitmap(1320,818,96,96,PixelFormats.Pbgra32);bmp.Render(dv);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using var stream=File.Create(Path.Combine(output,name+".png"));encoder.Save(stream);}
            Layout();var nav=Descendants(root).OfType<ListBox>().Single(x=>x.Items.Count==7&&x.Items[0] is ListBoxItem);
            Check(nav.Items.Count==7,"Seven primary navigation sections");
            Capture("01-applications");
            foreach(var title in new[]{"动画","系统检测","网络","声音","常规","关于","应用管理"})
            {nav.SelectedItem=nav.Items.Cast<ListBoxItem>().Single(x=>(string)x.Tag==title);Layout();Check(Descendants(root).OfType<TextBlock>().Any(x=>x.Text==title),"Navigation: "+title);if(title is "动画" or "声音" or "关于")Capture(title=="动画"?"02-animation":title=="声音"?"03-sound":"04-about");}
            typeof(SettingsWindow).GetField("profilePage",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(w,2);typeof(SettingsWindow).GetMethod("ShowProfile",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(w,null);Layout();
            Check(Descendants(root).OfType<TextBlock>().Any(x=>x.Text=="当前此应用使用全局设置"),"Follow-global explanation visible");Check(!Descendants(root).OfType<TextBlock>().Any(x=>x.Text=="粒子数量"),"Inherited profile hides duplicated controls");Capture("05-inherited");
            var own=Descendants(root).OfType<RadioButton>().Single(x=>(string)x.Content=="使用独立设置");own.IsChecked=true;Layout();
            var edited=(ControllerSettings)typeof(SettingsWindow).GetField("edit",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(w)!;Check(edited.Profiles[0].UseIndependentSettings,"Independent mode changes configuration");Check(Descendants(root).OfType<Expander>().Count()>=6,"Layered animation sections exist");Capture("06-independent");
            var follow=Descendants(root).OfType<RadioButton>().Single(x=>(string)x.Content=="跟随全局设置");follow.IsChecked=true;Layout();Check(!edited.Profiles[0].UseIndependentSettings,"Return to global updates configuration");
            nav.SelectedIndex=1;Layout();var folds=Descendants(root).OfType<Expander>().ToArray();Check(folds.Count(x=>x.IsExpanded)==1,"Only one animation group expanded by default"); Check(!Descendants(root).OfType<TextBlock>().Any(x=>x.Text.Contains("Choice {")),"Choice records never leak into visible labels");folds[2].IsExpanded=true;Layout();Check(folds.Count(x=>x.IsExpanded)==1,"Accordion collapses previous group");Check(Descendants(root).OfType<Button>().Any(x=>Equals(x.Content,"?")),"Parameter help available");Capture("07-particles");
            var compact=new Size(1032,602);root.Width=1032;root.Height=602;root.Measure(compact);root.Arrange(new Rect(compact));root.UpdateLayout();Console.WriteLine($"Compact desired={root.DesiredSize}, actual={root.ActualWidth}x{root.ActualHeight}, margin={root.Margin}"); Check(Math.Abs(root.ActualWidth-1032)<1 && root.ActualHeight<=603,"Minimum window layout fits 1080x650");
            Check(!w.IsVisible,"No settings window shown or desktop focus taken");
            var exportData=Path.Combine(output,"fixture-data");
            var log=new LogService(exportData);log.Write("ControllerStart");log.Write("FIRST_VISUAL","PRIVATE_SENTINEL, latency=12.3ms");log.Error("UiError",new IOException("PRIVATE_SENTINEL"),true);
            var privateConfig=WireJson.Clone(config);privateConfig.Profiles[0].DisplayName="PRIVATE_SENTINEL";privateConfig.Profiles[0].ExecutablePath=@"C:\PRIVATE_SENTINEL\App.exe";privateConfig.Audio.Phrases["READY"]="PRIVATE_SENTINEL";
            var export=DiagnosticExportService.ExportAsync(Path.Combine(output,"diagnostic-sample.zip"),exportData,privateConfig).GetAwaiter().GetResult();
            using(var zip=ZipFile.OpenRead(export)){Check(zip.GetEntry("app.log")!=null&&zip.GetEntry("crash.log")!=null&&zip.GetEntry("system-info.json")!=null,"Actual export service writes diagnostic ZIP");foreach(var entry in zip.Entries){using var reader=new StreamReader(entry.Open());Check(!reader.ReadToEnd().Contains("PRIVATE_SENTINEL"),"Privacy: "+entry.FullName);}using var sys=new StreamReader(zip.GetEntry("system-info.json")!.Open());var report=JsonDocument.Parse(sys.ReadToEnd());Check(report.RootElement.GetProperty("version").GetString()!.StartsWith("3.0.0"),"System report uses V3 assembly");Check(report.RootElement.TryGetProperty("monitors",out _)&&report.RootElement.TryGetProperty("webView2Version",out _),"Monitor and WebView diagnostics present");}
            w.Close();
            File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new{passed=checks.Count,checks,desktopWindowsShown=0},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"PASS {checks.Count} UI checks; no desktop windows shown.");return 0;
        }
        catch(Exception e){File.WriteAllText(Path.Combine(output,"failure.txt"),e.ToString());Console.WriteLine(e);return 1;}
    }
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
    static IEnumerable<DependencyObject> Descendants(DependencyObject parent){for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var inner in Descendants(child))yield return inner;}}
}
