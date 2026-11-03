using Formater.Core;

public class MoneyTests
{
    [Theory]
    [InlineData("0", "0.00")]
    [InlineData("999", "999.00")]
    [InlineData("12011", "12,011.00")]
    [InlineData("60055", "60,055.00")]
    [InlineData("123456.7", "1,23,456.70")]
    [InlineData("12345678.91", "1,23,45,678.91")]
    [InlineData("1234567890.5", "1,23,45,67,890.50")]
    [InlineData("0.005", "0.01")]
    [InlineData("2.345", "2.35")]
    [InlineData("-1234.5", "-1,234.50")]
    public void Indian_grouping_with_half_up_rounding(string value, string expected) =>
        Assert.Equal(expected, Money.Format(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Qty_has_two_decimals() => Assert.Equal("5.00", Money.Qty(5));

    [Theory]
    [InlineData("18", "18")]
    [InlineData("2.50", "2.5")]
    [InlineData("9.0", "9")]
    public void Percent_drops_trailing_zeros(string value, string expected) =>
        Assert.Equal(expected, Money.Percent(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));
}
