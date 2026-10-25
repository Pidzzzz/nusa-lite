using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NusaLite.Controls;

public sealed class ArcRing : FrameworkElement
{
    public static readonly DependencyProperty PercentProperty = DependencyProperty.Register(
        nameof(Percent), typeof(double), typeof(ArcRing),
        new FrameworkPropertyMetadata(
            0.0,
            FrameworkPropertyMetadataOptions.AffectsRender,
            OnPercentChanged));

    private static readonly DependencyProperty AnimatedPercentProperty = DependencyProperty.Register(
        nameof(AnimatedPercent), typeof(double), typeof(ArcRing),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness), typeof(double), typeof(ArcRing),
        new FrameworkPropertyMetadata(6.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(Brush), typeof(ArcRing),
        new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromArgb(34, 255, 255, 255)), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(ArcRing),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Percent
    {
        get => (double)GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
    }

    private double AnimatedPercent
    {
        get => (double)GetValue(AnimatedPercentProperty);
        set => SetValue(AnimatedPercentProperty, value);
    }

    private static void OnPercentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ring = (ArcRing)d;
        var value = (double)e.NewValue;
        var target = double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);

        if (!ring.IsLoaded || Math.Abs(ring.AnimatedPercent - target) < 0.0005)
        {
            ring.BeginAnimation(AnimatedPercentProperty, null);
            ring.AnimatedPercent = target;
            return;
        }

        ring.BeginAnimation(
            AnimatedPercentProperty,
            new DoubleAnimation
            {
                From = ring.AnimatedPercent,
                To = target,
                Duration = TimeSpan.FromMilliseconds(520),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd
            },
            HandoffBehavior.SnapshotAndReplace);
    }

    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public Brush TrackBrush
    {
        get => (Brush)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var thickness = Math.Max(1, Thickness);
        var radius = Math.Min(ActualWidth, ActualHeight) / 2 - thickness / 2;
        if (radius <= 0)
        {
            return;
        }

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var trackPen = new Pen(TrackBrush, thickness);
        trackPen.Freeze();
        drawingContext.DrawEllipse(null, trackPen, center, radius, radius);

        var percent = Math.Clamp(AnimatedPercent, 0, 1);
        if (percent <= 0.0001)
        {
            return;
        }

        var pen = new Pen(Stroke, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        pen.Freeze();

        if (percent >= 0.999)
        {
            drawingContext.DrawEllipse(null, pen, center, radius, radius);
            return;
        }

        var startAngle = -90.0;
        var endAngle = startAngle + percent * 360.0;
        var startPoint = PointOnCircle(center, radius, startAngle);
        var endPoint = PointOnCircle(center, radius, endAngle);

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(startPoint, false, false);
            context.ArcTo(endPoint, new Size(radius, radius), 0, percent > 0.5, SweepDirection.Clockwise, true, false);
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(null, pen, geometry);
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180.0;
        return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
    }
}
