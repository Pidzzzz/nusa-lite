using System.Text;
using System.Windows;
using NusaLite.Interop;
using NusaLite.Models;

namespace NusaLite;

public partial class MainWindow
{
    public void SetFullscreenAutoHide(bool enabled)
    {
        if (!enabled && _isFullscreenSuppressed)
        {
            _isFullscreenSuppressed = false;
            Visibility = Visibility.Visible;
            RepositionWindow();
            UpdateWindowRegion();
        }
    }

    private void FullscreenTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isFullscreenSuppressed)
        {
            RefreshBrowserState();
        }

        if (!_viewModel.Settings.Current.FullscreenAutoHide || _hwnd == IntPtr.Zero)
        {
            if (_isFullscreenSuppressed)
            {
                _isFullscreenSuppressed = false;
                Visibility = Visibility.Visible;
                RepositionWindow();
                UpdateWindowRegion();
            }
            return;
        }

        var isFullscreen = WindowStyler.IsForegroundFullscreen(_hwnd);
        if (isFullscreen && !_isFullscreenSuppressed)
        {
            _isFullscreenSuppressed = true;
            Visibility = Visibility.Hidden;
        }
        else if (!isFullscreen && _isFullscreenSuppressed)
        {
            _isFullscreenSuppressed = false;
            Visibility = Visibility.Visible;
            RepositionWindow();
            UpdateWindowRegion();
        }
    }

    private void RefreshBrowserState()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var browser = WindowStyler.IsForegroundBrowser(_hwnd);
        if (browser is { } value && value != _browserForeground)
        {
            _browserForeground = value;

            if (_view == IslandView.Lyrics)
            {
                UpdateLyricsLayout();
            }
            else
            {
                ApplySpec(ResolveSpec(_view), fast: false);
            }
        }

        var autoGuarded = _viewModel.Settings.Current.AutoClickGuard &&
                          !_clickGuard &&
                          IsForegroundMaximizedApp();
        if (autoGuarded != _autoGuarded)
        {
            _autoGuarded = autoGuarded;
            CancelDwellIfGuarded();
        }

        UpdateClickThrough();
    }

    private bool IsForegroundMaximizedApp()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return false;
        }

        var fg = NativeMethods.GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == _hwnd)
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
        if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "MultitaskingViewFrame")
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(fg, out var pid);
        if (pid == 0 || pid == Environment.ProcessId)
        {
            return false;
        }

        var style = NativeMethods.GetWindowLongPtr(fg, NativeMethods.GwlStyle).ToInt64();
        return (style & NativeMethods.WsMaximize) != 0;
    }
}
