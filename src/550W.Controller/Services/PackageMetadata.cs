using System.Xml.Linq;
namespace Controller550W.Controller.Services;
internal static class PackageMetadata
{
    public static (string AppId,string Icon)? ForExecutable(string executable)
    {
        if(!executable.Contains("WindowsApps",StringComparison.OrdinalIgnoreCase))return null;
        try{var dir=Path.GetDirectoryName(executable);for(int i=0;i<6&&!string.IsNullOrEmpty(dir);i++,dir=Path.GetDirectoryName(dir))
            {var manifest=Path.Combine(dir,"AppxManifest.xml");if(!File.Exists(manifest))continue;var doc=XDocument.Load(manifest);var folder=Path.GetFileName(dir);var split=folder.LastIndexOf("__",StringComparison.Ordinal);var first=folder.IndexOf('_');if(first<1||split<first)return null;var family=folder[..first]+"_"+folder[(split+2)..];
             var app=doc.Descendants().FirstOrDefault(x=>x.Name.LocalName=="Application"&&x.Attribute("Executable") is {} a&&Path.GetFullPath(Path.Combine(dir,a.Value)).Equals(Path.GetFullPath(executable),StringComparison.OrdinalIgnoreCase));if(app==null)return null;
             var visual=app.Descendants().FirstOrDefault(x=>x.Name.LocalName=="VisualElements");return(family+"!"+app.Attribute("Id")?.Value,ResolveAsset(dir,visual?.Attribute("Square44x44Logo")?.Value??""));}}
        catch{}return null;
    }
    public static string ResolveAsset(string folder,string name)
    {
        try{var full=Path.Combine(folder,name);if(File.Exists(full))return full;var dir=Path.GetDirectoryName(full);if(dir!=null&&Directory.Exists(dir))return Directory.GetFiles(dir,Path.GetFileNameWithoutExtension(full)+"*.png").OrderByDescending(x=>new FileInfo(x).Length).FirstOrDefault()??"";}catch{}return "";
    }
}
