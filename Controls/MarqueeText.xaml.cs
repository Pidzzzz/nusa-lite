using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace NusaLite.Controls;

public partial class MarqueeText : UserControl
{
    private const double ScrollSpeed = 35.0;
    private const double PauseSeconds = 1.2;

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(MarqueeText),
        new PropertyMetadata(string.Empty, OnTextChanged));

    public static readonly DependencyProperty LineHeightProperty = DependencyProperty.Register(
        nameof(LineHeight), typeof(double), typeof(MarqueeText),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public double LineHeight
    {
        get => (double)GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    public MarqueeText()
    {
        InitializeComponent();
        Loaded += (_, _) => Restart();
        Unloaded += (_, _) => Stop();
        SizeChanged += (_, _) => Restart();
        Line.SizeChanged += (_, _) => Restart();
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true)
            {
                Restart();
            }
            else
            {
                Stop();
            }
        };
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var marquee = (MarqueeText)d;
        marquee.Line.Text = e.NewValue as string ?? string.Empty;
        marquee.Restart();
    }

    private void Restart()
    {
        Stop();

        if (!IsLoaded || !IsVisible)
        {
            return;
        }

        Dispatcher.BeginInvoke(new Action(StartMarquee), DispatcherPriority.Loaded);
    }

    private void Stop()
    {
        Slide.BeginAnimation(TranslateTransform.XProperty, null);
        Slide.X = 0;
    }

    private void StartMarquee()
    {
        if (!IsLoaded || !IsVisible)
        {
            return;
        }

        Slide.BeginAnimation(TranslateTransform.XProperty, null);
        Slide.X = 0;

        UpdateLayout();

        var viewport = Host.ActualWidth > 0 ? Host.ActualWidth : ActualWidth;
        var overflow = Line.ActualWidth - viewport;
        if (viewport <= 0 || overflow <= 1)
        {
            return;
        }

        var scrollSeconds = overflow / ScrollSpeed;
        var totalSeconds = (PauseSeconds * 2) + scrollSeconds + 0.01;

        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromSeconds(totalSeconds),
            RepeatBehavior = RepeatBehavior.Forever
        };

        animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.Zero));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.FromSeconds(PauseSeconds)));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(-overflow, TimeSpan.FromSeconds(PauseSeconds + scrollSeconds)));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(-overflow, TimeSpan.FromSeconds(PauseSeconds + scrollSeconds + PauseSeconds)));
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, TimeSpan.FromSeconds(totalSeconds)));

        Slide.BeginAnimation(TranslateTransform.XProperty, animation);
    }
}
