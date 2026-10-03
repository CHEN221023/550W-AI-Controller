// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 550W AI Controller contributors.
// Separate offline renderer. Redistribution and modification are permitted
// under GNU GPL version 3 or later. No warranty; see accompanying COPYING.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
class VoiceWorker
{
    delegate int Callback(IntPtr samples,int count,IntPtr events);
    [DllImport("espeak-ng.dll",CallingConvention=CallingConvention.Cdecl)]static extern int espeak_Initialize(int output,int milliseconds,[MarshalAs(UnmanagedType.LPStr)]string path,int options);
    [DllImport("espeak-ng.dll",CallingConvention=CallingConvention.Cdecl)]static extern void espeak_SetSynthCallback(Callback callback);
    [DllImport("espeak-ng.dll",CallingConvention=CallingConvention.Cdecl)]static extern int espeak_SetVoiceByName([MarshalAs(UnmanagedType.LPStr)]string name);
    [DllImport("espeak-ng.dll",CallingConvention=CallingConvention.Cdecl)]static extern int espeak_SetParameter(int parameter,int value,int relative);
    [DllImport("espeak-ng.dll",CallingConvention=CallingConvention.Cdecl)]static extern int espeak_Synth(byte[] text,UIntPtr size,uint position,int positionType,uint endPosition,uint flags,out uint identifier,IntPtr data);
    [DllImport("espeak-ng.dll",CallingConvention=CallingConvention.Cdecl)]static extern int espeak_Terminate();
    static readonly List<short> samples=new List<short>();static readonly Callback callback=Collect;
    static int Collect(IntPtr pointer,int count,IntPtr events){if(pointer==IntPtr.Zero||count<=0)return 0;if(samples.Count+count>22050*45)return 1;var data=new short[count];Marshal.Copy(pointer,data,0,count);samples.AddRange(data);return 0;}
    [STAThread]static void Main(string[] args)
    {
        string input="",output="";for(int i=0;i+1<args.Length;i++){if(args[i]=="--input")input=args[++i];else if(args[i]=="--output")output=args[++i];}
        try{
            if(input.Length==0||output.Length==0||!File.Exists(input)||new FileInfo(input).Length>16384){Environment.ExitCode=2;return;}
            var phrase=File.ReadAllText(input,Encoding.UTF8);if(phrase.Length==0){Environment.ExitCode=2;return;}if(phrase.Length>180)phrase=phrase.Substring(0,180);
            int rate=espeak_Initialize(2,0,AppDomain.CurrentDomain.BaseDirectory,0);if(rate<1)throw new Exception("Offline voice data unavailable");
            espeak_SetSynthCallback(callback);bool chinese=false;foreach(char c in phrase)if(c>=0x3400&&c<=0x9fff)chinese=true;
            if(espeak_SetVoiceByName(chinese?"cmn":"en-us")!=0)throw new Exception("Offline voice not found");
            espeak_SetParameter(1,160,0);espeak_SetParameter(2,100,0);espeak_SetParameter(3,28,0);espeak_SetParameter(4,38,0);
            var bytes=Encoding.UTF8.GetBytes(phrase+"\0");uint id;if(espeak_Synth(bytes,(UIntPtr)bytes.Length,0,1,0,1,out id,IntPtr.Zero)!=0||samples.Count<100)throw new Exception("Offline synthesis failed");
            using(var writer=new BinaryWriter(File.Create(output))){int length=samples.Count*2;writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(length+36);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(length);foreach(short sample in samples)writer.Write(sample);}
        }catch(Exception e){Environment.ExitCode=1;try{File.WriteAllText(output+".error.txt",e.Message);}catch{}}finally{try{espeak_Terminate();}catch{}}
    }
}
