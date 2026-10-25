using NusaLite.Interop;
using NusaLite.Models;

namespace NusaLite.Services;

public sealed class PowerService
{
    public PowerInfo Read()
    {
        if (!NativeMethods.GetSystemPowerStatus(out var status))
        {
            return PowerInfo.Unknown;
        }

        var percent = status.BatteryLifePercent == 255 ? -1 : status.BatteryLifePercent;
        var present = percent >= 0 && (status.BatteryFlag & 128) == 0;

        return new PowerInfo
        {
            Percent = percent,
            OnAc = status.AcLineStatus == 1,
            Charging = (status.BatteryFlag & 8) != 0,
            Present = present
        };
    }
}
