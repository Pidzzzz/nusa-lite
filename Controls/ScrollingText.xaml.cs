using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NusaLite.Controls;

public partial class ScrollingText : UserControl
{
    private const double Speed = 35.0;
    private const double Pause = 1.2;
    private const double Park = 1.2;

    private readonly Stopwatch _clock = new();
    private bool _ticking;
    private bool _needsMeasure = true;
    private double _overflow;
    private double _scrollSeconds;
    private double _cycleSeconds;

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(ScrollingText),
        new PropertyMetadata(string.Empty, OnTextChanged));

    public static readonly DependencyProperty LineHeightProperty = DependencyProperty.Register(
        nameof(LineHeight), typeof(double), typeof(ScrollingText),
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

    public ScrollingText()
    {
        InitializeComponent();
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        Host.SizeChanged += (_, _) => _needsMeasure = true;
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ScrollingText)d;
        control.Line.Text = e.NewValue as string ?? string.Empty;
        control._needsMeasure = true;
        control._clock.Reset();
        control.Slide.X = 0;
    }

    private void Attach()
    {
        if (!_ticking)
        {
            CompositionTarget.Rendering += OnRendering;
            _ticking = true;
        }

        if (_needsMeasure)
        {
            MeasureCycle(startClock: !_clock.IsRunning);
        }
    }

    private void Detach()
    {
        if (_ticking)
        {
            CompositionTarget.Rendering -= OnRendering;
            _ticking = false;
        }
    }

    private void MeasureCycle(bool startClock)
    {
        UpdateLayout();

        var viewport = Host.ActualWidth;
        if (viewport <= 0)
        {
            return;
        }

        Line.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var textWidth = Line.DesiredSize.Width;
        Line.InvalidateMeasure();

        _overflow = textWidth - viewport;
        _needsMeasure = false;

        if (_overflow <= 1)
        {
            _overflow = 0;
            _scrollSeconds = 0;
            _cycleSeconds = 0;
            _clock.Stop();
            Slide.X = 0;
            return;
        }

        _scrollSeconds = _overflow / Speed;
        _cycleSeconds = Pause + _scrollSeconds + Park;

        if (startClock || !_clock.IsRunning)
        {
            _clock.Restart();
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (_needsMeasure)
        {
            MeasureCycle(startClock: !_clock.IsRunning);
        }

        if (_cycleSeconds <= 0)
        {
            return;
        }

        var elapsed = _clock.Elapsed.TotalSeconds % _cycleSeconds;

        if (elapsed < Pause)
        {
            Slide.X = 0;
        }
        else if (elapsed < Pause + _scrollSeconds)
        {
            Slide.X = -_overflow * (elapsed - Pause) / _scrollSeconds;
        }
        else
        {
            Slide.X = -_overflow;
        }
    }
}
