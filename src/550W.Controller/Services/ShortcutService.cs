using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
namespace Controller550W.Controller.Services;

internal sealed class ShortcutService(string dataDirectory,string pipeName)
{
    public static void ReadMetadata(AppProfile p,string file)
    {
        object? shell=null,link=null;
        try{shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);dynamic ws=shell!;link=ws.CreateShortcut(file);dynamic l=link!;
            p.SourceShortcut=file;p.LaunchKind=LaunchTargetKind.Shortcut;p.LaunchTarget=file;p.LaunchArguments="";p.WorkingDirectory=l.WorkingDirectory;
            string path=l.TargetPath;string icon=l.IconLocation;p.ExecutablePath=path;p.ProcessNames=[Path.GetFileName(path)];p.IconSource=icon;
        }finally{if(link!=null)Marshal.FinalReleaseComObject(link);if(shell!=null)Marshal.FinalReleaseComObject(shell);}
    }
    public string Create(AppProfile p,string file)
    {
        if(!file.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase))file+=".lnk";
        if(File.Exists(file)&&!IsOwned(p,file))throw new IOException("此路径已存在其他快捷方式。请选择一个新名称。");
        if(!string.IsNullOrEmpty(p.SourceShortcut)&&Path.GetFullPath(file).Equals(Path.GetFullPath(p.SourceShortcut),StringComparison.OrdinalIgnoreCase))throw new IOException("请选择与原快捷方式不同的名称。");
        var icon=CacheIcon(p);var launcher=Path.Combine(AppContext.BaseDirectory,"550W.Launcher.exe");var controller=Path.Combine(AppContext.BaseDirectory,"550W-AI-Controller.exe");
        object? shell=null,link=null;
        try{shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);dynamic ws=shell!;link=ws.CreateShortcut(file);dynamic l=link!;
            l.TargetPath=File.Exists(launcher)?launcher:controller;
            l.Arguments=File.Exists(launcher)?$"--launch-profile {Quote(p.Id)} --pipe {Quote(pipeName)} --controller {Quote(controller)} --data-dir {Quote(dataDirectory)}":$"--launch-profile {Quote(p.Id)} --data-dir {Quote(dataDirectory)}";
            l.WorkingDirectory=AppContext.BaseDirectory;l.Description="550W 优化启动 · "+p.DisplayName;l.IconLocation=icon+",0";l.Save();p.OptimizedShortcutPath=file;p.LaunchMode=LaunchMode.OptimizedShortcut;
            return file;
        }finally{if(link!=null)Marshal.FinalReleaseComObject(link);if(shell!=null)Marshal.FinalReleaseComObject(shell);}
    }
    public bool IsOwned(AppProfile p,string file)
    {
        object? shell=null,link=null;try{shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);dynamic ws=shell!;link=ws.CreateShortcut(file);dynamic l=link!;string target=l.TargetPath,args=l.Arguments;
            return (target.Equals(Path.Combine(AppContext.BaseDirectory,"550W.Launcher.exe"),StringComparison.OrdinalIgnoreCase)||target.Equals(Path.Combine(AppContext.BaseDirectory,"550W-AI-Controller.exe"),StringComparison.OrdinalIgnoreCase))&&args.Contains("--launch-profile",StringComparison.Ordinal)&&args.Contains(Quote(p.Id),StringComparison.Ordinal);
        }catch{return false;}finally{if(link!=null)Marshal.FinalReleaseComObject(link);if(shell!=null)Marshal.FinalReleaseComObject(shell);}
    }
    public void Remove(AppProfile p){if(File.Exists(p.OptimizedShortcutPath)){if(!IsOwned(p,p.OptimizedShortcutPath))throw new IOException("此文件已不属于 550W 优化快捷方式。");File.Delete(p.OptimizedShortcutPath);}p.OptimizedShortcutPath="";p.LaunchMode=LaunchMode.PassiveDetection;}
    string CacheIcon(AppProfile p)
    {
        string source=p.IconSource;
        if(source.Length==0&&File.Exists(p.SourceShortcut)){var clone=WireJson.Clone(p);ReadMetadata(clone,p.SourceShortcut);source=clone.IconSource;}
        if(source.Length==0)source=p.ExecutablePath;
        if(source==p.ExecutablePath&&PackageMetadata.ForExecutable(p.ExecutablePath) is {} package&&package.Icon.Length>0)source=package.Icon;
        var comma=source.LastIndexOf(',');var index=0;if(comma>0&&int.TryParse(source[(comma+1)..],out var n)){index=n;source=source[..comma];}source=Environment.ExpandEnvironmentVariables(source.Trim('"'));
        using var bitmap=ReadBitmap(source,index);using var resized=new Bitmap(bitmap,new Size(256,256));using var png=new MemoryStream();resized.Save(png,ImageFormat.Png);var bytes=png.ToArray();
        var dir=Path.Combine(dataDirectory,"Icons");Directory.CreateDirectory(dir);var name=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(p.Id+source+index)))[..20]+".ico";var file=Path.Combine(dir,name);
        using(var output=new BinaryWriter(File.Create(file))){output.Write((ushort)0);output.Write((ushort)1);output.Write((ushort)1);output.Write((byte)0);output.Write((byte)0);output.Write((byte)0);output.Write((byte)0);output.Write((ushort)1);output.Write((ushort)32);output.Write(bytes.Length);output.Write(22);output.Write(bytes);}
        p.IconCachePath=file;return file;
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern uint ExtractIconEx(string file,int index,nint[] large,nint[] small,uint count);
    [DllImport("user32.dll")]static extern bool DestroyIcon(nint icon);
    static Bitmap ReadBitmap(string file,int index)
    {
        try{
            if(File.Exists(file)&&new[]{".png",".jpg",".webp"}.Contains(Path.GetExtension(file).ToLowerInvariant()))return new Bitmap(file);
            var icons=new nint[1];var small=new nint[1];if(File.Exists(file)&&ExtractIconEx(file,index,icons,small,1)>0&&icons[0]!=0){try{using var icon=(Icon)Icon.FromHandle(icons[0]).Clone();return icon.ToBitmap();}finally{DestroyIcon(icons[0]);if(small[0]!=0)DestroyIcon(small[0]);}}
            using var fallback=File.Exists(file)?Icon.ExtractAssociatedIcon(file):(Icon)SystemIcons.Application.Clone();return (fallback??SystemIcons.Application).ToBitmap();
        }catch{return SystemIcons.Application.ToBitmap();}
    }
    static string Quote(string value)=>"\""+value.Replace("\"","")+"\"";
}
