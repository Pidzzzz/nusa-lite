namespace NusaLite.Models;

public sealed record PowerInfo
{
    public static readonly PowerInfo Unknown = new()
    {
        Percent = -1,
        OnAc = false,
        Charging = false,
        Present = false
    };

    public int Percent { get; init; }

    public bool OnAc { get; init; }

    public bool Charging { get; init; }

    public bool Present { get; init; }

    public bool IsPlugged => OnAc || Charging;

    public string PercentText => Percent < 0 ? "--" : $"{Percent}%";

    public bool Equals(PowerInfo? other)
        => other is not null
           && other.Percent == Percent
           && other.OnAc == OnAc
           && other.Charging == Charging
           && other.Present == Present;

    public override int GetHashCode() => HashCode.Combine(Percent, OnAc, Charging, Present);
}
