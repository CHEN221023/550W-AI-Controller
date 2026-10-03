using System.Diagnostics;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;
namespace Controller550W.Controller.Services;

internal static class CoreAssetService
{
    public static async Task<string> Prepare(string source,string dataDirectory)
    {
        if(!File.Exists(source)||new FileInfo(source).Length>16*1024*1024)throw new IOException("文件不存在或超过 16MB。");
        var bytes=await File.ReadAllBytesAsync(source);var name=Convert.ToHexString(SHA256.HashData(bytes))[..24]+".png";var dir=Path.Combine(dataDirectory,"CoreAssets");Directory.CreateDirectory(dir);var file=Path.Combine(dir,name);if(File.Exists(file))return file;
        if(Path.GetExtension(source).Equals(".png",StringComparison.OrdinalIgnoreCase)){await File.WriteAllBytesAsync(file,bytes);return file;}
        var info=new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"550W.Animation.exe")){UseShellExecute=false,CreateNoWindow=true};
        foreach(var value in new[]{"--render-core","--input",source,"--output",file})info.ArgumentList.Add(value);
        using var process=Process.Start(info)??throw new IOException("图像转换程序未启动。");
        using var job=new Controller550W.Controller.Native.AnimationJob(process);
        try{await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(12));}catch{try{process.Kill(true);}catch{}throw new IOException("图像转换超时。");}
        if(!File.Exists(file))throw new IOException("图像格式未能解码。");return file;
    }
}
