using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace Formater.Core;

/// <summary>Pre-formatted page data handed to the page layout script (Resources/layout.js).</summary>
public sealed class RenderModel
{
    public string Title { get; set; } = "";
    public string RefLine { get; set; } = "";
    public string OtherRefLine { get; set; } = "";
    public string Date { get; set; } = "";
    public string Terms { get; set; } = "";
    public RenderParty Seller { get; set; } = new();
    public RenderParty Buyer { get; set; } = new();
    public bool ShowDiscount { get; set; }
    public List<RenderItem> Items { get; set; } = [];
    public List<RenderTotal> Totals { get; set; } = [];
    public List<string> TermsAndConditions { get; set; } = [];

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Keeps ₹ readable while still escaping <, > and & so the JSON is safe inside <script>.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    public string ToJson() => JsonSerializer.Serialize(this, Options);
}

public sealed class RenderParty
{
    public string Label { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Lines { get; set; } = [];
}

public sealed class RenderItem
{
    public string N { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Desc { get; set; } = [];
    public string Qty { get; set; } = "";
    public string Unit { get; set; } = "";
    public string Rate { get; set; } = "";
    public string Discount { get; set; } = "";
    public string Amount { get; set; } = "";
}

public sealed class RenderTotal
{
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public bool Bold { get; set; }
}
