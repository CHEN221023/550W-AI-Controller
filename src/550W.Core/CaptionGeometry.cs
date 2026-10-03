namespace Controller550W.Core;
public readonly record struct PhysicalRect(int Left,int Top,int Right,int Bottom)
{
    public int Width=>Right-Left;public int Height=>Bottom-Top;
    public bool Contains(int x,int y)=>x>=Left&&x<Right&&y>=Top&&y<Bottom;
}
public static class CaptionGeometry
{
    public static bool IsReliable(PhysicalRect window,PhysicalRect close)
        =>close.Width>=12&&close.Width<=200&&close.Height>=10&&close.Height<=140&&close.Left>=window.Left&&close.Right<=window.Right+8&&close.Top>=window.Top-8&&close.Bottom<=window.Top+160&&close.Right>=window.Right-200;
}
