using System.Globalization;

namespace TradingOverview;

internal static class CompactNumber
{
    internal static string Format(int value)
    {
        if (value < 1000)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        return (value / 1000d).ToString("0.#", CultureInfo.InvariantCulture) + "K";
    }
}
