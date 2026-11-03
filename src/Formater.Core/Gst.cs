using System.Text.RegularExpressions;

namespace Formater.Core;

public enum GstMode
{
    /// <summary>Pick IGST or CGST + SGST from the buyer's GSTIN, or failing that, the address.</summary>
    Auto,
    Igst,
    CgstSgst,
    None,
}

public static partial class Gst
{
    [GeneratedRegex(@"\b(\d{2})[A-Z]{5}\d{4}[A-Z][A-Z0-9]Z[A-Z0-9]\b", RegexOptions.IgnoreCase)]
    private static partial Regex GstinRegex();

    /// <summary>Find a GSTIN anywhere in the given lines (upper-cased), or null.</summary>
    public static string? FindGstin(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var m = GstinRegex().Match(line);
            if (m.Success) return m.Value.ToUpperInvariant();
        }
        return null;
    }

    public static bool IsGstin(string value) => GstinRegex().IsMatch(value.Trim()) && value.Trim().Length == 15;

    /// <summary>
    /// Intra-state when the buyer GSTIN has the seller's state code; without a buyer GSTIN,
    /// when the buyer address mentions the seller's home state.
    /// </summary>
    public static (bool IntraState, string Reason) Decide(
        string? sellerGstin, string homeState, string? buyerGstin, IEnumerable<string> buyerAddress)
    {
        var sellerCode = sellerGstin?[..2] ?? "29";
        if (buyerGstin is not null)
        {
            var buyerCode = buyerGstin[..2];
            return buyerCode == sellerCode
                ? (true, $"buyer GSTIN state code {buyerCode} = seller {sellerCode}")
                : (false, $"buyer GSTIN state code {buyerCode} ≠ seller {sellerCode}");
        }

        if (!string.IsNullOrWhiteSpace(homeState))
        {
            var word = new Regex($@"\b{Regex.Escape(homeState.Trim())}\b", RegexOptions.IgnoreCase);
            if (buyerAddress.Any(l => word.IsMatch(l)))
                return (true, $"no buyer GSTIN, address is in {homeState.Trim()}");
        }

        return (false, string.IsNullOrWhiteSpace(homeState)
            ? "no buyer GSTIN"
            : $"no buyer GSTIN, address is not in {homeState.Trim()}");
    }

    /// <summary>Tax lines for a GST rate: IGST18 (18%) or CGST9 (9%) + SGST9 (9%).</summary>
    public static List<Tax> Taxes(bool intraState, decimal percent)
    {
        if (percent <= 0) return [];
        if (!intraState)
        {
            var p = Money.Percent(percent);
            return [new Tax($"IGST{p} ({p}%)", percent)];
        }

        var half = percent / 2;
        var h = Money.Percent(half);
        return [new Tax($"CGST{h} ({h}%)", half), new Tax($"SGST{h} ({h}%)", half)];
    }
}
