using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using NusaLite.Interop;
using NusaLite.Models;

namespace NusaLite;

public partial class MainWindow
{
    // ----- Pointer -------------------------------------------------------

    private void HoverTimer_Tick(object? sender, EventArgs e)
    {
        UpdateGhostByCursor();

        var inside = IsCursorInside();

        if (!inside)
        {
            _insideSince = 0;
            if (_hover)
            {
                PointerLeave();
            }
            return;
        }

        if (_hover)
        {
            return;
        }

        if (_insideSince == 0)
        {
            _insideSince = Environment.TickCount64;
        }

        var dwell = _browserForeground ? 220 : 0;
        if (Environment.TickCount64 - _insideSince >= dwell)
        {
            PointerEnter();
        }
    }

    private bool IsCursorInside()
    {
        return IsCursorWithinRect(2);
    }

    private bool IsCursorNearIsland()
    {
        return IsCursorWithinRect(24);
    }

    private bool IsCursorWithinRect(int margin)
    {
        if (_hwnd == IntPtr.Zero || !NativeMethods.GetCursorPos(out var cursor))
        {
            return false;
        }

        if (!NativeMethods.GetWindowRect(_hwnd, out var window))
        {
            return false;
        }

        var scale = NativeMethods.GetDpiForWindow(_hwnd) / 96.0;

        if (IsInside(cursor, IslandHost, window, scale, margin))
        {
            return true;
        }

        return _dotsVisible && DotsBar.ActualWidth > 0 && IsInside(cursor, DotsBar, window, scale, margin);
    }

    private bool IsInside(NativeMethods.Point cursor, FrameworkElement element, NativeMethods.Rect window, double scale, int margin = 2)
    {
        var origin = element.TranslatePoint(new Point(0, 0), Root);
        var left = window.Left + origin.X * scale;
        var top = window.Top + origin.Y * scale;
        var right = left + element.ActualWidth * scale;
        var bottom = top + element.ActualHeight * scale;

        return cursor.X >= left - margin && cursor.X <= right + margin && cursor.Y >= top - margin && cursor.Y <= bottom + margin;
    }

    private void PointerEnter()
    {
        if (IsEffectiveGuard)
        {
            return;
        }

        _collapseTimer.Stop();
        if (_hover)
        {
            return;
        }

        var delay = _viewModel.Settings.Current.HoverDelayMs;
        if (delay <= 0)
        {
            if (_dwellPending)
            {
                _dwellPending = false;
                _dwellTimer?.Stop();
            }

            ExpandFromHover();
            return;
        }

        if (_dwellPending)
        {
            return;
        }

        _dwellPending = true;
        _dwellTimer ??= CreateDwellTimer();
        _dwellTimer.Stop();
        _dwellTimer.Interval = TimeSpan.FromMilliseconds(delay);
        _dwellTimer.Start();
    }

    private DispatcherTimer CreateDwellTimer()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Input);
        timer.Tick += DwellTimer_Tick;
        return timer;
    }

    private void DwellTimer_Tick(object? sender, EventArgs e)
    {
        _dwellTimer?.Stop();
        _dwellPending = false;

        if (IsEffectiveGuard || _hover || !IsCursorInside())
        {
            return;
        }

        ExpandFromHover();
    }

    private void ExpandFromHover()
    {
        _collapseTimer.Stop();
        if (IsEffectiveGuard || _hover)
        {
            return;
        }

        _hover = true;
        UpdateClickThrough();

        if (_peekActive)
        {
            _peekActive = false;
            _peekTimer.Stop();
            SwitchTo(_viewModel.IsPinned ? _viewBeforePeek : IslandView.Hub, force: true);
            return;
        }

        if (_view == IslandView.Idle)
        {
            SwitchTo(IslandView.Hub);
        }
    }

    private void PointerLeave()
    {
        if (_dwellPending)
        {
            _dwellPending = false;
            _dwellTimer?.Stop();
        }

        _hover = false;
        UpdateClickThrough();
        if (_viewModel.IsPinned || _dragActive)
        {
            return;
        }

        _collapseTimer.Stop();
        _collapseTimer.Start();
    }

    private void CollapseTimer_Tick(object? sender, EventArgs e)
    {
        _collapseTimer.Stop();
        if (_hover || _viewModel.IsPinned)
        {
            return;
        }

        if (IsCursorInside())
        {
            return;
        }

        SwitchTo(IslandView.Idle);
    }

    private void Island_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            _suppressClickUp = false;
        }

        if (e.ClickCount == 2)
        {
            _clickTimer.Stop();
            _singleClickPending = false;
            _suppressClickUp = true;
            _viewModel.IsPinned = !_viewModel.IsPinned;
            e.Handled = true;
        }
    }

    private void Island_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_suppressClickUp)
        {
            _suppressClickUp = false;
            return;
        }

        if (e.ClickCount > 1)
        {
            return;
        }

        _singleClickPending = true;
        _clickTimer.Stop();
        _clickTimer.Start();
    }

    private void ClickTimer_Tick(object? sender, EventArgs e)
    {
        _clickTimer.Stop();
        if (!_singleClickPending)
        {
            return;
        }

        _singleClickPending = false;

        if (_view == IslandView.Hub)
        {
            SwitchTo(_viewModel.IsPlaying && _viewModel.HasLyrics ? IslandView.Lyrics : IslandView.Player);
        }
        else if (_view == IslandView.Idle)
        {
            SwitchTo(_viewModel.IsPlaying && _viewModel.HasLyrics ? IslandView.Lyrics : IslandView.Hub);
        }
        else if (_view == IslandView.Lyrics)
        {
            SwitchTo(IslandView.Player);
        }
        else if (_view == IslandView.Player)
        {
            SwitchTo(_viewModel.HasLyrics ? IslandView.Lyrics : IslandView.Hub);
        }
    }

    private void Island_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var direction = e.Delta > 0 ? -1 : 1;
        var index = Array.IndexOf(Carousel, _view);

        IslandView target;
        if (index < 0)
        {
            target = direction > 0 ? Carousel[0] : Carousel[^1];
        }
        else
        {
            target = Carousel[(index + direction + Carousel.Length) % Carousel.Length];
        }

        SwitchTo(target);
        e.Handled = true;
    }
}
