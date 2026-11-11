using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NusaLite.Models;
using NusaLite.Views;

namespace NusaLite;

public partial class MainWindow
{
    private void AnimateTo(IslandSpec spec, bool fast)
    {
        var generation = ++_transition;
        var fadeOutMs = fast ? 60 : 90;
        var fadeInMs = fast ? 180 : 260;
        var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
        var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };

        var fadeOut = new DoubleAnimation(
            ContentHost.Opacity,
            0,
            TimeSpan.FromMilliseconds(fadeOutMs))
        {
            EasingFunction = easeIn
        };

        var slideOut = new DoubleAnimation(
            ContentSlide.Y,
            -4,
            TimeSpan.FromMilliseconds(fadeOutMs))
        {
            EasingFunction = easeIn,
            FillBehavior = FillBehavior.Stop
        };

        fadeOut.Completed += (_, _) =>
        {
            if (generation != _transition)
            {
                return;
            }

            ContentHost.BeginAnimation(OpacityProperty, null);
            ContentHost.Opacity = 0;
            ContentSlide.BeginAnimation(TranslateTransform.YProperty, null);
            ContentSlide.Y = 7;
            ContentHost.Content = GetView(_view);
            ApplySpec(spec, fast);

            var beginTime = TimeSpan.FromMilliseconds(fast ? 20 : 60);
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(fadeInMs))
            {
                BeginTime = beginTime,
                EasingFunction = easeOut
            };
            ContentHost.BeginAnimation(OpacityProperty, fadeIn);

            var slideIn = new DoubleAnimation(7, 0, TimeSpan.FromMilliseconds(fast ? 220 : 320))
            {
                BeginTime = beginTime,
                EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            ContentSlide.BeginAnimation(TranslateTransform.YProperty, slideIn);
        };

        ContentHost.BeginAnimation(OpacityProperty, fadeOut);
        ContentSlide.BeginAnimation(TranslateTransform.YProperty, slideOut);
    }

    private void ApplySpec(IslandSpec spec, bool fast)
    {
        if (!IsLoaded)
        {
            SetImmediate(spec);
            return;
        }

        ApplyContentScale();

        var generation = _transition;
        var duration = TimeSpan.FromMilliseconds(fast ? 190 : 320);
        var ease = new QuinticEase { EasingMode = EasingMode.EaseOut };

        var width = IslandHost.ActualWidth > 0 ? IslandHost.ActualWidth : spec.Width;
        var height = IslandHost.ActualHeight > 0 ? IslandHost.ActualHeight : spec.Height;
        var radius = Notch.CornerRadius;
        var ear = Notch.EarRadius;

        IslandHost.Width = spec.Width;
        IslandHost.Height = spec.Height;
        Notch.CornerRadius = spec.Radius;
        Notch.EarRadius = spec.Ear;

        IslandHost.BeginAnimation(WidthProperty, Animate(width, spec.Width, duration, ease, generation));
        IslandHost.BeginAnimation(HeightProperty, Animate(height, spec.Height, duration, ease, generation));
        Notch.BeginAnimation(Controls.NotchShape.CornerRadiusProperty, Animate(radius, spec.Radius, duration, ease, generation));
        Notch.BeginAnimation(Controls.NotchShape.EarRadiusProperty, Animate(ear, spec.Ear, duration, ease, generation));

        _spec = spec;
        ApplyActiveViewLayout(spec);
    }

    private void ApplyActiveViewLayout(IslandSpec spec)
    {
        if (ContentHost.Content is LyricsView lyrics)
        {
            lyrics.ApplyLayout(_browserForeground && _view == IslandView.Lyrics, spec.Width, spec.Height);
        }
        else if (ContentHost.Content is IdleView idle)
        {
            var scale = _browserForeground ? CompactUiScale : 1.0;
            idle.Width = spec.Width / scale;
            idle.Height = spec.Height / scale;
        }
    }

    private void ApplyContentScale()
    {
        var scale = _view == IslandView.Lyrics || !_browserForeground ? 1.0 : CompactUiScale;
        ContentHost.LayoutTransform = Math.Abs(scale - 1.0) < 0.001
            ? Transform.Identity
            : new ScaleTransform(scale, scale);
    }

    private void SetImmediate(IslandSpec spec)
    {
        IslandHost.BeginAnimation(WidthProperty, null);
        IslandHost.BeginAnimation(HeightProperty, null);
        Notch.BeginAnimation(Controls.NotchShape.CornerRadiusProperty, null);
        Notch.BeginAnimation(Controls.NotchShape.EarRadiusProperty, null);

        ApplyContentScale();

        IslandHost.Width = spec.Width;
        IslandHost.Height = spec.Height;
        Notch.CornerRadius = spec.Radius;
        Notch.EarRadius = spec.Ear;

        _spec = spec;
        ApplyActiveViewLayout(spec);
    }

    private DoubleAnimation Animate(
        double from, double to, TimeSpan duration, IEasingFunction ease, int generation)
    {
        var animation = new DoubleAnimation(from, to, duration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        };

        animation.Completed += (_, _) =>
        {
            if (generation == _transition)
            {
                UpdateWindowRegion();
            }
        };

        return animation;
    }

    private IslandSpec ResolveSpec(IslandView view)
    {
        if (view == IslandView.Lyrics)
        {
            return BuildLyricsSpec(_browserForeground);
        }

        var spec = view == IslandView.Idle ? BuildIdleSpec() : SpecFor(view);
        if (!_browserForeground)
        {
            return spec;
        }

        return new IslandSpec(
            Math.Ceiling(spec.Width * CompactUiScale),
            Math.Ceiling(spec.Height * CompactUiScale),
            Math.Ceiling(spec.Radius * CompactUiScale),
            spec.Ear);
    }

    private IslandSpec BuildIdleSpec()
    {
        var widest = MeasureTextWidth(_viewModel.IdleLine, _uiFont, 13.5, FontWeights.Medium);
        var width = Math.Ceiling((widest + 88 + (_viewModel.HasSourceIcon ? 24 : 0)) / 12) * 12;
        width = Math.Clamp(width, 216, 400);
        return new IslandSpec(width, 40, 20, 0);
    }

    private IslandSpec BuildLyricsSpec(bool compact)
    {        var widest = MeasureTextWidth(_viewModel.LyricCurrent, _displayFont, compact ? 13.5 : 15.5, FontWeights.SemiBold);

        if (compact)
        {
            widest = Math.Max(widest, MeasureTextWidth(_viewModel.TrackArtist, _uiFont, 8.5, FontWeights.Normal));
        }
        else
        {
            widest = Math.Max(widest, MeasureTextWidth(_viewModel.LyricPrev, _uiFont, 11, FontWeights.Normal));
            widest = Math.Max(widest, MeasureTextWidth(_viewModel.LyricNext, _uiFont, 11, FontWeights.Normal));
            widest = Math.Max(widest, MeasureTextWidth(_viewModel.TrackTitle, _displayFont, 9.5, FontWeights.SemiBold));
            widest = Math.Max(widest, MeasureTextWidth(_viewModel.TrackArtist, _uiFont, 8.5, FontWeights.Normal));
        }

        var fixedWidth = compact ? 97.0 : 121.0;
        var min = compact ? 292.0 : 360.0;
        var max = compact ? 430.0 : 560.0;
        var width = Math.Ceiling((fixedWidth + widest + 10) / 24) * 24;
        width = Math.Clamp(width, min, max);
        var height = compact ? 76.0 : 142.0;
        var radius = compact ? 16.0 : 20.0;

        return new IslandSpec(width, height, radius, 0);
    }

    private double MeasureTextWidth(string? text, FontFamily font, double fontSize, FontWeight weight)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var probe = new TextBlock
        {
            Text = text,
            FontFamily = font,
            FontSize = fontSize,
            FontWeight = weight,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.None
        };

        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return probe.DesiredSize.Width;
    }

    private void UpdateIdleLayout()
    {
        if (_view != IslandView.Idle || _hwnd == IntPtr.Zero || !IsVisible)
        {
            return;
        }

        var spec = ResolveSpec(IslandView.Idle);

        if (Math.Abs(spec.Width - _spec.Width) < 0.5 &&
            Math.Abs(spec.Height - _spec.Height) < 0.5)
        {
            return;
        }

        ApplySpec(spec, fast: false);
    }

    private void UpdateLyricsLayout()
    {
        if (_view != IslandView.Lyrics || _hwnd == IntPtr.Zero || !IsVisible)
        {
            return;
        }

        var compact = _browserForeground;
        var spec = BuildLyricsSpec(compact);

        if (Math.Abs(spec.Width - _spec.Width) < 0.5 &&
            Math.Abs(spec.Height - _spec.Height) < 0.5)
        {
            return;
        }

        ApplySpec(spec, fast: false);
    }

    private static IslandSpec SpecFor(IslandView view) => view switch
    {
        IslandView.Idle => new IslandSpec(198, 34, 12, 0),
        IslandView.Hub => new IslandSpec(410, 88, 18, 0),
        IslandView.Lyrics => new IslandSpec(360, 142, 20, 0),
        IslandView.Peek => new IslandSpec(362, 68, 16, 0),
        _ => new IslandSpec(554, 158, 22, 0)
    };

    private readonly record struct IslandSpec(double Width, double Height, double Radius, double Ear);
}
