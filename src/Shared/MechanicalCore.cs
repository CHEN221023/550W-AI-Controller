using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Controller550W.Rendering;

public static class MechanicalCore
{
    static ImageSource? cached; static string cachedPath = "";
    public static ImageSource Load(string path)
    {
        if (cached != null && cachedPath == path) return cached;
        try
        {
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(Path.GetFullPath(path)); image.EndInit(); image.Freeze(); cached = image; cachedPath = path; return image;
        }
        catch { return Fallback; }
    }
    public static readonly ImageSource Fallback = CreateFallback();
    static ImageSource CreateFallback()
    {
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(28, 31, 34)), new Pen(new SolidColorBrush(Color.FromRgb(103, 111, 115)), 3), Geometry.Parse("M90,40 L310,40 355,82 355,268 310,310 90,310 45,268 45,82Z"));
            dc.DrawEllipse(Brushes.Black, new Pen(Brushes.DimGray, 12), new Point(200,175),104,104);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(55, 7, 5)), new Pen(new SolidColorBrush(Color.FromRgb(224,80,48)),4),new Point(200,175),80,80);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255,45,45)),null,new Point(200,175),31,31);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255,112,80)),null,new Point(200,175),12,12);
            foreach(var p in new[]{new Point(75,72),new Point(325,72),new Point(75,278),new Point(325,278)})dc.DrawEllipse(Brushes.Black,new Pen(Brushes.Gray,2),p,6,6);
        }
        group.Freeze(); var image = new DrawingImage(group); image.Freeze(); return image;
    }
}
