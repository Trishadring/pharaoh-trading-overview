using System;
using System.Collections.Generic;

namespace TradingOverview;

internal readonly struct RouteTrade
{
    internal RouteTrade(string good, bool playerImports, int annualCapacity)
    {
        Good = good;
        PlayerImports = playerImports;
        AnnualCapacity = annualCapacity;
    }

    internal string Good { get; }
    internal bool PlayerImports { get; }
    internal int AnnualCapacity { get; }
}

internal readonly struct TradeTotals
{
    internal TradeTotals(int imported, int maxImport, int exported, int maxExport)
    {
        Imported = imported;
        MaxImport = maxImport;
        Exported = exported;
        MaxExport = maxExport;
    }

    internal int Imported { get; }
    internal int MaxImport { get; }
    internal int Exported { get; }
    internal int MaxExport { get; }

    internal static TradeTotals Calculate(
        string good,
        int imported,
        int exported,
        bool individualUnits,
        IEnumerable<RouteTrade> routes)
    {
        if (good == null)
        {
            throw new ArgumentNullException(nameof(good));
        }

        var maxImport = 0;
        var maxExport = 0;
        if (routes != null)
        {
            foreach (var route in routes)
            {
                if (!string.Equals(route.Good, good, StringComparison.Ordinal))
                {
                    continue;
                }

                if (route.PlayerImports)
                {
                    maxImport += route.AnnualCapacity;
                }
                else
                {
                    maxExport += route.AnnualCapacity;
                }
            }
        }

        var divisor = individualUnits ? 100 : 1;
        return new TradeTotals(
            imported / divisor,
            maxImport / divisor,
            exported / divisor,
            maxExport / divisor);
    }
}
