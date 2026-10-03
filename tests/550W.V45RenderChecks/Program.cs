using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Controller550W.Rendering;
using Controller550W.Core;

static class Program
{
 static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
 static string Hash(Bitmap image){using var ms=new MemoryStream();image.Save(ms,ImageFormat.Png);return Convert.ToHexString(SHA256.HashData(ms.ToArray()));}
 static Bitmap Draw(ParticleIntroRenderer renderer,double elapsed){var result=new Bitmap(1920,1080,PixelFormat.Format32bppPArgb);using var g=Graphics.FromImage(result);g.Clear(Color.Transparent);renderer.Draw(g,elapsed,1920,1080);return result;}
 static void Main(string[] args)
 {
  var theme=Path.GetFullPath(args[0]);using var renderer=new ParticleIntroRenderer(new AnimationSettings{ParticleResidueMs=500,ParticleToLogoMs=1000,LogoHoldMs=450},theme);
  using var initial=Draw(renderer,0);using var idle=Draw(renderer,250);using var reveal=Draw(renderer,750);using var complete=Draw(renderer,1000);using var lastHold=Draw(renderer,1449);using var burst=Draw(renderer,1600);using var fade=Draw(renderer,2600);using var end=Draw(renderer,3200);
  Check(Hash(initial)!=Hash(idle),"idle moves before the deadline");
  Check(Hash(idle)!=Hash(reveal),"reassembly starts within total time");
  Check(Hash(complete)==Hash(lastHold),"formed logo holds from total=1000 through total+hold-1");
  Check(Hash(lastHold)!=Hash(burst),"burst begins after the independent hold");
  using var empty=new Bitmap(1920,1080,PixelFormat.Format32bppPArgb);
  Check(Hash(end)==Hash(empty),"adapter ends after unchanged burst and crossfade durations");
  using var expectedLogo=new Bitmap(1920,1080,PixelFormat.Format32bppPArgb);using(var g=Graphics.FromImage(expectedLogo)){using var logo=new Bitmap(Path.Combine(theme,"assets","intro-logo.png"));g.DrawImageUnscaled(logo,560,425);}
  Directory.CreateDirectory("outputs/550W-AI-Controller-Source/docs/images-v45-native");
  complete.Save("outputs/550W-AI-Controller-Source/docs/images-v45-native/04-logo.png");expectedLogo.Save("outputs/550W-AI-Controller-Source/docs/images-v45-native/expected-logo.png");
  Check(Hash(complete)==Hash(expectedLogo),"formed 550C equals the generated original SVG raster");
  Check(initial.GetPixel(960,540).A==0&&initial.GetPixel(0,0).A==0,"adapter adds no background panel");
  var rejected=false;try{using var invalid=new ParticleIntroRenderer(new AnimationSettings{ParticleResidueMs=1000,ParticleToLogoMs=800},theme);}catch(ArgumentException){rejected=true;}Check(rejected,"invalid idle/total combination rejected");
  foreach(var color in new[]{"green","cyan","white","amber"}){using var r=new ParticleIntroRenderer(new AnimationSettings{ColorProfile=color},theme);using var frame=Draw(r,0);Check(Hash(frame)!=Hash(initial),"palette "+color+" retained");}
  using var buffer=new Bitmap(1920,1080,PixelFormat.Format32bppPArgb);using var graphics=Graphics.FromImage(buffer);var clock=Stopwatch.StartNew();for(var i=0;i<100;i++){graphics.Clear(Color.Black);renderer.Draw(graphics,i*30,1920,1080);}clock.Stop();
  Directory.CreateDirectory("outputs/550W-AI-Controller-Source/docs/images-v45-native");initial.Save("outputs/550W-AI-Controller-Source/docs/images-v45-native/01-initial.png");idle.Save("outputs/550W-AI-Controller-Source/docs/images-v45-native/02-idle.png");reveal.Save("outputs/550W-AI-Controller-Source/docs/images-v45-native/03-reveal.png");complete.Save("outputs/550W-AI-Controller-Source/docs/images-v45-native/04-logo.png");
  File.WriteAllText("outputs/550W-AI-Controller-Source/docs/native-test-v45-results.json",JsonSerializer.Serialize(new{passed=12,idleMs=500,totalMs=1000,holdMs=450,targetCount=3365,drawMeanMs=clock.Elapsed.TotalMilliseconds/100},new JsonSerializerOptions{WriteIndented=true}));
  Console.WriteLine("Mean Draw including test surface clear: "+(clock.Elapsed.TotalMilliseconds/100).ToString("F2")+"ms");
 }
}
