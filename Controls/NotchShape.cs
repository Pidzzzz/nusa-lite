using System.Windows;
using System.Windows.Media;

namespace NusaLite.Controls;

public sealed class NotchShape : FrameworkElement
{
    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(NotchShape),
        new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(5, 6, 12)), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(double), typeof(NotchShape),
        new FrameworkPropertyMetadata(18.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EarRadiusProperty = DependencyProperty.Register(
        nameof(EarRadius), typeof(double), typeof(NotchShape),
        new FrameworkPropertyMetadata(11.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush Fill
    {
        get => (Brush)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public double EarRadius
    {
        get => (double)GetValue(EarRadiusProperty);
        set => SetValue(EarRadiusProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 4 || height < 4)
        {
            return;
        }

        var ear = Math.Clamp(EarRadius, 0, Math.Min(height, width / 2));
        var pillWidth = width - ear * 2;
        if (pillWidth <= 2)
        {
            return;
        }

        var radius = Math.Clamp(CornerRadius, 0, Math.Min(height / 2, pillWidth / 2));
        var left = ear;
        var right = width - ear;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(0, 0), true, true);
            context.LineTo(new Point(width, 0), true, false);
            context.ArcTo(new Point(right, ear), new Size(ear, ear), 0, false, SweepDirection.Counterclockwise, true, false);
            context.LineTo(new Point(right, height - radius), true, false);
            context.ArcTo(new Point(right - radius, height), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
            context.LineTo(new Point(left + radius, height), true, false);
            context.ArcTo(new Point(left, height - radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
            context.LineTo(new Point(left, ear), true, false);
            context.ArcTo(new Point(0, 0), new Size(ear, ear), 0, false, SweepDirection.Counterclockwise, true, false);
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(Fill, null, geometry);
    }
}
