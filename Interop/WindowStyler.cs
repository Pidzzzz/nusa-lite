using System.Runtime.InteropServices;
using System.Text;
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

    public static bool IsForegroundFullscreen(IntPtr currentHwnd)
    {
        var fg = NativeMethods.GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == currentHwnd)
        {
            return false;
        }

        if (!NativeMethods.IsWindowVisible(fg))
        {
            return false;
        }

        var sb = new StringBuilder(256);
        NativeMethods.GetClassName(fg, sb, sb.Capacity);
        var cls = sb.ToString();

        if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd"
            or "Windows.UI.Core.CoreWindow" or "MultitaskingViewFrame")
        {
            return false;
        }

        var currentMonitor = NativeMethods.MonitorFromWindow(currentHwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var fgMonitor = NativeMethods.MonitorFromWindow(fg, NativeMethods.MONITOR_DEFAULTTONEAREST);

        if (currentMonitor == IntPtr.Zero || fgMonitor == IntPtr.Zero || currentMonitor != fgMonitor)
        {
            return false;
        }

        var mi = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(currentMonitor, ref mi))
        {
            return false;
        }

        if (!NativeMethods.GetWindowRect(fg, out var fgRect))
        {
            return false;
        }

        return fgRect.Left <= mi.rcMonitor.Left &&
               fgRect.Top <= mi.rcMonitor.Top &&
               fgRect.Right >= mi.rcMonitor.Right &&
               fgRect.Bottom >= mi.rcMonitor.Bottom;
    }

    public static bool? IsForegroundBrowser(IntPtr currentHwnd)
    {
        var fg = NativeMethods.GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == currentHwnd)
        {
            return null;
        }

        try
        {
            NativeMethods.GetWindowThreadProcessId(fg, out var pid);
            if (pid == 0 || pid == Environment.ProcessId)
            {
                return null;
            }

            using var process = System.Diagnostics.Process.GetProcessById((int)pid);
            var name = process.ProcessName;
            return name.Contains("chrome", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("msedge", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("firefox", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("brave", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("opera", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("vivaldi", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return null;
        }
    }
}
