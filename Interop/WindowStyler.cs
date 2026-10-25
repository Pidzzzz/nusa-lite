using System.Windows.Forms;

namespace NusaLite.Interop;



internal static class WindowStyler
{
    public static void MakeOverlay(IntPtr hwnd)
    {
        var ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle).ToInt64();
        ex |= NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate | NativeMethods.WsExLayered;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlExStyle, new IntPtr(ex));
    }

    public static void SetClickThrough(IntPtr hwnd, bool enabled)
    {
        var ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle).ToInt64();
        var updated = enabled
            ? ex | NativeMethods.WsExTransparent
            : ex & ~NativeMethods.WsExTransparent;

        if (updated != ex)
        {
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlExStyle, new IntPtr(updated));
        }
    }

    public static void PositionTopCenter(IntPtr hwnd, double widthDips, double heightDips)
    {
        var screen = Screen.PrimaryScreen;
        if (screen is null)
        {
            return;
        }

        var dpi = NativeMethods.GetDpiForWindow(hwnd);
        var scale = dpi > 0 ? dpi / 96.0 : 1.0;
        var width = Math.Max(1, (int)Math.Round(widthDips * scale));
        var height = Math.Max(1, (int)Math.Round(heightDips * scale));

        var bounds = screen.WorkingArea;
        var x = bounds.X + (bounds.Width - width) / 2;
        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HwndTopmost,
            x,
            bounds.Y,
            width,
            height,
            NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow | NativeMethods.SwpNoOwnerZOrder);
    }

    public static System.Windows.Point GetCursorPositionDips()
    {
        if (!NativeMethods.GetCursorPos(out var p))
        {
            return new System.Windows.Point(-10000, -10000);
        }

        using var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
        var scale = g.DpiX / 96.0;
        return new System.Windows.Point(p.X / scale, p.Y / scale);
    }
}
