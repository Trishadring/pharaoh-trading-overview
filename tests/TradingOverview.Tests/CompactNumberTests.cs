using Xunit;

namespace TradingOverview;

public sealed class CompactNumberTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(999, "999")]
    [InlineData(1000, "1K")]
    [InlineData(1500, "1.5K")]
    [InlineData(2000, "2K")]
    [InlineData(2314, "2.3K")]
    [InlineData(11700, "11.7K")]
    internal void FormatsCompactTradeValues(int value, string expected)
    {
        Assert.Equal(expected, CompactNumber.Format(value));
    }
}
