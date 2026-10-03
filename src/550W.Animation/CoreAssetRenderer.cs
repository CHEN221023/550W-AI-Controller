using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
namespace Controller550W.Animation;

internal static class CoreAssetRenderer
{
    public static void Run(string source,string output)
    {
        if(!File.Exists(source)||new FileInfo(source).Length>16*1024*1024||!new[]{".png",".svg",".webp"}.Contains(System.IO.Path.GetExtension(source).ToLowerInvariant()))return;
        var cache=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(output)!,"render-"+Guid.NewGuid().ToString("N"));
        var app=new Application{ShutdownMode=ShutdownMode.OnMainWindowClose};var web=new WebView2CompositionControl();var window=new Window{Title="550W · 核心资源准备",Width=640,Height=560,WindowStyle=WindowStyle.None,ShowInTaskbar=false,ShowActivated=false,Opacity=0,Content=web};
        var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(9)};timer.Tick+=(_,_)=>{timer.Stop();window.Close();};timer.Start();
        window.Loaded+=async(_,_)=>{try{
            var env=await CoreWebView2Environment.CreateAsync(null,cache);await web.EnsureCoreWebView2Async(env);var browser=web.CoreWebView2;browser.Settings.AreDevToolsEnabled=false;browser.Settings.AreDefaultContextMenusEnabled=false;browser.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;
            browser.AddWebResourceRequestedFilter("https://core.550w.local/image",CoreWebView2WebResourceContext.All);browser.WebResourceRequested+=(_,e)=>{var ext=System.IO.Path.GetExtension(source).ToLowerInvariant();e.Response=env.CreateWebResourceResponse(File.OpenRead(source),200,"OK","Content-Type: "+(ext==".svg"?"image/svg+xml":ext==".webp"?"image/webp":"image/png")+"\r\nAccess-Control-Allow-Origin: *");};
            browser.WebMessageReceived+=(_,e)=>{try{using var doc=JsonDocument.Parse(e.WebMessageAsJson);if(doc.RootElement.TryGetProperty("error",out var error))File.WriteAllText(output+".error.txt",error.GetString());else{var png=doc.RootElement.GetProperty("png").GetString();var bytes=Convert.FromBase64String(png!);if(bytes.Length>8)File.WriteAllBytes(output,bytes);}}catch{}timer.Stop();window.Close();};
            browser.NavigateToString("<meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; img-src https://core.550w.local; script-src 'unsafe-inline'\"><canvas id='c' width='640' height='560'></canvas><script>let image=new Image();image.crossOrigin='anonymous';image.onload=()=>{try{let s=Math.min(640/image.width,560/image.height);c.getContext('2d').drawImage(image,(640-image.width*s)/2,(560-image.height*s)/2,image.width*s,image.height*s);chrome.webview.postMessage({png:c.toDataURL('image/png').split(',')[1]});}catch(e){chrome.webview.postMessage({error:String(e)});}};image.onerror=()=>chrome.webview.postMessage({error:'Image decode failed'});image.src='https://core.550w.local/image';</script>");
        }catch(Exception e){try{File.WriteAllText(output+".error.txt",e.ToString());}catch{}window.Close();}};
        window.Closed+=(_,_)=>{timer.Stop();web.Dispose();};app.Run(window);try{Directory.Delete(cache,true);}catch{}
    }
}
