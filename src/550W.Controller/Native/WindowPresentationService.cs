using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;
namespace Controller550W.Controller.Native;

internal static class WindowPresentationService
{
    [DllImport("user32.dll")] static extern bool ShowWindowAsync(nint hwnd, int command);
    public static void Apply(nint hwnd, WindowPresentation presentation, bool foreground = false)
    {
        if (!NativeMethods.IsWindow(hwnd)) return;
        if (presentation == WindowPresentation.Maximize) ShowWindowAsync(hwnd, 3);
        else if (presentation is WindowPresentation.Center or WindowPresentation.FillWorkArea)
        {
            ShowWindowAsync(hwnd, 9); var area = Forms.Screen.FromHandle(hwnd).WorkingArea;
            if (NativeMethods.GetWindowRect(hwnd, out var r))
            {
                var width = presentation == WindowPresentation.FillWorkArea ? area.Width : Math.Min(area.Width, r.Right - r.Left);
                var height = presentation == WindowPresentation.FillWorkArea ? area.Height : Math.Min(area.Height, r.Bottom - r.Top);
                NativeMethods.SetWindowPos(hwnd, 0, area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height, 0x4 | 0x10);
            }
        }
        if (foreground) NativeMethods.SetForegroundWindow(hwnd);
    }
}
