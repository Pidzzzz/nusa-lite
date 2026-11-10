using System.Windows.Media.Animation;
using NusaLite.Interop;

namespace NusaLite;

public partial class MainWindow
{
    private bool IsEffectiveGuard => _clickGuard || _autoGuarded;

    public void SetClickGuard(bool enabled)
    {
        if (enabled && !_clickGuard)
        {
            _viewBeforeGuard = _view;
        }

        _clickGuard = enabled;
        CancelDwellIfGuarded();
        UpdateClickThrough();
        ApplyGuardGhost();
    }

    public void RefreshGuardGhost()
    {
        if (_clickGuard && _ghostFaded)
        {
            AnimateWindowOpacity(ClampedGuardOpacity());
            return;
        }

        ApplyGuardGhost();
    }

    private double ClampedGuardOpacity()
    {
        var value = _viewModel.Settings.Current.GuardOpacity;
        if (value < 0.15)
        {
            return 0.15;
        }

        return value > 1.0 ? 1.0 : value;
    }

    private void ApplyGuardGhost()
    {
        if (_clickGuard)
        {
            _ghostTimer.Stop();
            _ghostFaded = false;
            AnimateWindowOpacity(0.95);
        }
        else
        {
            _ghostTimer.Stop();
            _ghostFaded = false;
            AnimateWindowOpacity(1.0);
        }
    }

    private void BumpGhost()
    {
        if (!_clickGuard)
        {
            return;
        }

        AnimateWindowOpacity(0.95);
        _ghostFaded = false;
    }

    private void UpdateGhostByCursor()
    {
        if (!_clickGuard || _peekActive)
        {
            return;
        }

        var near = IsCursorNearIsland();

        if (near && !_ghostFaded)
        {
            _ghostTimer.Stop();
            AnimateWindowOpacity(ClampedGuardOpacity());
            _ghostFaded = true;
        }
        else if (!near && _ghostFaded && !_ghostTimer.IsEnabled)
        {
            _ghostTimer.Start();
        }
    }

    private void GhostTimer_Tick(object? sender, EventArgs e)
    {
        _ghostTimer.Stop();

        if (!_clickGuard || _peekActive)
        {
            return;
        }

        if (!IsCursorNearIsland())
        {
            AnimateWindowOpacity(0.95);
            _ghostFaded = false;
        }
    }

    private void AnimateWindowOpacity(double target)
    {
        var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(250))
        {
            FillBehavior = FillBehavior.HoldEnd
        };
        BeginAnimation(OpacityProperty, animation);
    }

    private void CancelDwellIfGuarded()
    {
        if (IsEffectiveGuard && _dwellPending)
        {
            _dwellPending = false;
            _dwellTimer?.Stop();
        }
    }

    private void ToggleClickGuard()
    {
        var enabled = !_viewModel.Settings.Current.ClickGuardEnabled;
        _viewModel.Settings.Current.ClickGuardEnabled = enabled;
        _viewModel.Settings.Save();
        SetClickGuard(enabled);
    }

    private void UpdateClickThrough()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var passThrough = IsEffectiveGuard ||
                          (IsVisible &&
                           _browserForeground &&
                           !_hover &&
                           !_viewModel.IsPinned &&
                           !_dragActive);

        WindowStyler.SetClickThrough(_hwnd, passThrough);
    }
}
