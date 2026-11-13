using System.Windows.Media.Animation;

namespace NusaLite.Infrastructure;

public sealed class MorphEase : IEasingFunction
{
    public static MorphEase Shared { get; } = new();

    private const double X1 = 0.16;
    private const double Y1 = 1.0;
    private const double X2 = 0.3;
    private const double Y2 = 1.0;

    public double Ease(double normalizedTime)
    {
        if (normalizedTime <= 0.0)
        {
            return 0.0;
        }

        if (normalizedTime >= 1.0)
        {
            return 1.0;
        }

        var u = normalizedTime;
        for (var i = 0; i < 6; i++)
        {
            var error = SampleX(u) - normalizedTime;
            if (Math.Abs(error) < 1e-6)
            {
                break;
            }

            var slope = SampleDerivativeX(u);
            if (Math.Abs(slope) < 1e-6)
            {
                break;
            }

            u = Math.Clamp(u - error / slope, 0.0, 1.0);
        }

        return SampleY(u);
    }

    private static double SampleX(double t)
    {
        var s = 1.0 - t;
        return 3.0 * s * s * t * X1 + 3.0 * s * t * t * X2 + t * t * t;
    }

    private static double SampleY(double t)
    {
        var s = 1.0 - t;
        return 3.0 * s * s * t * Y1 + 3.0 * s * t * t * Y2 + t * t * t;
    }

    private static double SampleDerivativeX(double t)
    {
        var s = 1.0 - t;
        return 3.0 * s * s * X1 + 6.0 * s * t * (X2 - X1) + 3.0 * t * t * (1.0 - X2);
    }
}
