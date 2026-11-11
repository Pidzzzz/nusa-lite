using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NusaLite.Models;

namespace NusaLite;

public partial class MainWindow
{
    // ----- Dots ----------------------------------------------------------

    private void BuildDots()
    {
        foreach (var view in Carousel)
        {
            var dot = new Border
            {
                Width = 4,
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Background = _dotIdleBrush,
                Margin = new Thickness(2, 0, 2, 0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = view,
                ToolTip = LabelFor(view)
            };

            dot.MouseLeftButtonDown += (sender, e) =>
            {
                SwitchTo((IslandView)((Border)sender).Tag);
                e.Handled = true;
            };

            DotsPanel.Children.Add(dot);
            _dots.Add(dot);
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        _dotIdleBrush = TryFindResource("DotIdleBrush") as Brush ?? _dotIdleBrush;
        UpdateDots();
    }

    private void UpdateDots()
    {
        foreach (var dot in _dots)
        {
            var dotView = dot.Tag is IslandView view ? view : IslandView.Idle;
            var active = dotView == _view;
            var target = active ? 16.0 : 4.0;
            var from = dot.ActualWidth > 0 ? dot.ActualWidth : target;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            dot.Width = target;
            dot.Background = active ? AccentFor(dotView) : _dotIdleBrush;
            dot.BeginAnimation(WidthProperty, new DoubleAnimation(from, target, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            });
            dot.BeginAnimation(OpacityProperty, new DoubleAnimation(
                dot.Opacity,
                active ? 1.0 : 0.7,
                TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = ease
            });
        }
    }

    private Brush AccentFor(IslandView view) => view switch
    {
        IslandView.Lyrics => (Brush)FindResource("GoldBrush"),
        IslandView.Focus => (Brush)FindResource("EmberBrush"),
        IslandView.Water => (Brush)FindResource("AquaBrush"),
        IslandView.Shelf => (Brush)FindResource("LimeBrush"),
        _ => (Brush)FindResource("AccentBrush")
    };

    private void SetDotsVisible(bool visible, bool fast)
    {
        if (_dotsVisible != visible)
        {
            _dotsVisible = visible;
            DotsBar.IsHitTestVisible = visible;
            var beginTime = visible ? TimeSpan.FromMilliseconds(fast ? 40 : 190) : TimeSpan.Zero;
            var duration = TimeSpan.FromMilliseconds(visible ? 220 : 140);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var fromY = DotsSlide.Y;

            DotsSlide.BeginAnimation(TranslateTransform.YProperty, null);
            if (visible)
            {
                DotsSlide.Y = 4;
                fromY = 4;
            }
            else
            {
                DotsSlide.Y = 0;
            }

            DotsSlide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(
                fromY,
                visible ? 0 : 4,
                duration)
            {
                BeginTime = beginTime,
                EasingFunction = ease,
                FillBehavior = FillBehavior.HoldEnd
            });

            var animation = new DoubleAnimation(visible ? 1 : 0, duration)
            {
                BeginTime = beginTime,
                EasingFunction = ease
            };
            DotsBar.BeginAnimation(OpacityProperty, animation);
            UpdateWindowRegion();
        }
    }

    private static string LabelFor(IslandView view) => view switch
    {
        IslandView.Player => "Pemutar",
        IslandView.Lyrics => "Lirik",
        IslandView.Focus => "Fokus",
        IslandView.Water => "Air",
        _ => "Shelf"
    };
}
