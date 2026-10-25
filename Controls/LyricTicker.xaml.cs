using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NusaLite.Controls;

public partial class LyricTicker : UserControl
{
    public static readonly DependencyProperty CurrentProperty = DependencyProperty.Register(
        nameof(Current), typeof(string), typeof(LyricTicker),
        new PropertyMetadata(string.Empty, OnCurrentChanged));

    public string Current
    {
        get => (string)GetValue(CurrentProperty);
        set => SetValue(CurrentProperty, value);
    }

    public LyricTicker()
    {
        InitializeComponent();
        Loaded += (_, _) => AnimateIn();
        IsVisibleChanged += (_, e) =>
        {
            if (IsVisible)
            {
                AnimateIn();
            }
        };
    }

    private static void OnCurrentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ticker = (LyricTicker)d;
        ticker.Line.Text = e.NewValue as string ?? string.Empty;

        if (ticker.IsLoaded)
        {
            ticker.AnimateIn();
        }
    }

    private void AnimateIn()
    {
        if (string.IsNullOrWhiteSpace(Line.Text))
        {
            Line.BeginAnimation(OpacityProperty, null);
            Line.Opacity = 0;
            AccentBar.BeginAnimation(OpacityProperty, null);
            AccentBar.Opacity = 0;
            return;
        }

        Line.BeginAnimation(OpacityProperty, null);
        LineSlide.BeginAnimation(TranslateTransform.YProperty, null);
        Line.Opacity = 0.12;
        LineSlide.Y = 5;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Line.BeginAnimation(OpacityProperty, new DoubleAnimation(0.12, 1, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = ease
        });
        LineSlide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(5, 0, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
        });
        AccentBar.BeginAnimation(OpacityProperty, new DoubleAnimation(0.35, 0.85, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = ease
        });
    }
}
