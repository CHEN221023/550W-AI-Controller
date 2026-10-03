// Built against the .NET Framework already present on Windows 10/11. No resident process.
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
class Launcher
{
    [STAThread] static void Main(string[] args)
    {
        long origin=Stopwatch.GetTimestamp();string profile="",pipe="",controller="",data="",action="launch";
        for(int i=0;i+1<args.Length;i++){if(args[i]=="--launch-profile")profile=args[++i];else if(args[i]=="--close-profile"){profile=args[++i];action="close";}else if(args[i]=="--cancel-close-profile"){profile=args[++i];action="cancel-close";}else if(args[i]=="--pipe")pipe=args[++i];else if(args[i]=="--controller")controller=args[++i];else if(args[i]=="--data-dir")data=args[++i];}
        if(profile.Length==0)return;
        try {using(var stream=new NamedPipeClientStream(".",pipe,PipeDirection.Out)){stream.Connect(200);using(var writer=new StreamWriter(stream,new UTF8Encoding(false))){writer.WriteLine(action+"|"+profile+"|"+origin);}}return;}catch{}
        try{if(File.Exists(controller)){var command=(action=="close"?"--close-profile ":action=="cancel-close"?"--cancel-close-profile ":"--launch-profile ")+Quote(profile)+(data.Length==0?"":" --data-dir "+Quote(data));Process.Start(new ProcessStartInfo(controller,command){UseShellExecute=true});}}catch{}
    }
    static string Quote(string value){return "\""+value.Replace("\"","")+"\"";}
}
