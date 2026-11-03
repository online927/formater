using System.Globalization;
using System.Text;

namespace Formater.Core;

/// <summary>Decimal rounding and Indian number formatting (SPEC §7).</summary>
public static class Money
{
    /// <summary>Round half-up (away from zero) to 2 decimals, like Python's ROUND_HALF_UP.</summary>
    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Indian digit grouping with 2 decimals: 999.00, 12,011.00, 1,23,456.70, 1,23,45,678.91.</summary>
    public static string Format(decimal value)
    {
        var text = Round2(value).ToString("0.00", CultureInfo.InvariantCulture);
        var negative = text.StartsWith('-');
        if (negative) text = text[1..];
        var dot = text.IndexOf('.');
        var whole = text[..dot];
        var frac = text[(dot + 1)..];

        if (whole.Length > 3)
        {
            var head = whole[..^3];
            var tail = whole[^3..];
            var groups = new List<string>();
            while (head.Length > 2)
            {
                groups.Insert(0, head[^2..]);
                head = head[..^2];
            }
            if (head.Length > 0) groups.Insert(0, head);
            groups.Add(tail);
            whole = string.Join(",", groups);
        }

        var sb = new StringBuilder();
        if (negative) sb.Append('-');
        return sb.Append(whole).Append('.').Append(frac).ToString();
    }

    /// <summary>Quantity with 2 decimals and no grouping, as in the reference ("5.00").</summary>
    public static string Qty(decimal qty) => Round2(qty).ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>A percentage without trailing zeros: 18, 2.5, 12.75.</summary>
    public static string Percent(decimal percent) =>
        percent.ToString("0.####", CultureInfo.InvariantCulture);
}
