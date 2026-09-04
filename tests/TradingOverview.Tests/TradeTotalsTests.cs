using System.Collections.Generic;
using Xunit;

namespace TradingOverview;

public sealed class TradeTotalsTests
{
    [Fact]
    public void CalculatesCompletedTradeAndOpenRouteCapacityByDirection()
    {
        var routes = new List<RouteTrade>
        {
            new("Grain", playerImports: false, annualCapacity: 1500),
            new("Grain", playerImports: false, annualCapacity: 2500),
            new("Grain", playerImports: true, annualCapacity: 4000),
            new("Beer", playerImports: true, annualCapacity: 4000)
        };

        var result = TradeTotals.Calculate("Grain", imported: 300, exported: 600, individualUnits: false, routes);

        Assert.Equal(300, result.Imported);
        Assert.Equal(4000, result.MaxImport);
        Assert.Equal(600, result.Exported);
        Assert.Equal(4000, result.MaxExport);
    }

    [Fact]
    public void ReturnsZeroCapacityWhenNoOpenRoutesApply()
    {
        var result = TradeTotals.Calculate("Grain", 0, 0, false, new List<RouteTrade>());

        Assert.Equal(0, result.MaxImport);
        Assert.Equal(0, result.MaxExport);
    }

    [Fact]
    public void ScalesIndividualUnitsLikeTheCommerceOverseer()
    {
        var routes = new[] { new RouteTrade("Chariots", playerImports: true, annualCapacity: 1500) };

        var result = TradeTotals.Calculate("Chariots", imported: 300, exported: 100, individualUnits: true, routes);

        Assert.Equal(3, result.Imported);
        Assert.Equal(15, result.MaxImport);
        Assert.Equal(1, result.Exported);
    }
}
