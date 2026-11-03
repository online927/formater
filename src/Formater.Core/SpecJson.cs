using System.Globalization;
using System.Text.Json;

namespace Formater.Core;

/// <summary>Reads the SPEC §8 JSON data model (docs/handoff/sample_data.json). Numbers may be strings.</summary>
public static class SpecJson
{
    public static Quotation Parse(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var root = doc.RootElement;

        var q = new Quotation
        {
            Title = Str(root, "title", "Quotation"),
            RefNo = Str(root, "ref_no"),
            OtherRef = Str(root, "other_ref"),
            Date = Str(root, "date"),
            Terms = Str(root, "terms"),
            Seller = ReadParty(root, "seller", ""),
            Buyer = ReadParty(root, "buyer", "Buyer"),
            TermsAndConditions = Strings(root, "terms_and_conditions"),
        };

        if (root.TryGetProperty("items", out var items))
        {
            foreach (var it in items.EnumerateArray())
            {
                q.Items.Add(new LineItem
                {
                    Name = Str(it, "name"),
                    Desc = Strings(it, "desc"),
                    Qty = Num(it, "qty"),
                    Unit = Str(it, "unit"),
                    Rate = Num(it, "rate"),
                    DiscountPercent = Num(it, "discount"),
                });
            }
        }

        if (root.TryGetProperty("taxes", out var taxes))
            foreach (var t in taxes.EnumerateArray())
                q.Taxes.Add(new Tax(Str(t, "label"), Num(t, "percent")));

        return q;
    }

    private static Party ReadParty(JsonElement root, string name, string defaultLabel)
    {
        if (!root.TryGetProperty(name, out var p)) return new Party { Label = defaultLabel };
        return new Party { Label = Str(p, "label", defaultLabel), Name = Str(p, "name"), Lines = Strings(p, "lines") };
    }

    private static string Str(JsonElement e, string name, string fallback = "") =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : fallback;

    private static List<string> Strings(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Select(x => x.GetString() ?? "").ToList()
            : [];

    private static decimal Num(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return 0m;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDecimal(),
            JsonValueKind.String => decimal.Parse(v.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture),
            _ => 0m,
        };
    }
}
