namespace Formater.Core;

/// <summary>A party block on the page (seller or buyer).</summary>
public sealed class Party
{
    public string Label { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Lines { get; set; } = [];
}

/// <summary>One item row. Amount = qty × rate × (1 − discount%), rounded half-up to paise.</summary>
public sealed class LineItem
{
    public string Name { get; set; } = "";
    public List<string> Desc { get; set; } = [];
    public decimal Qty { get; set; }
    public string Unit { get; set; } = "";
    public decimal Rate { get; set; }
    public decimal DiscountPercent { get; set; }
}

public sealed record Tax(string Label, decimal Percent);

/// <summary>Everything that appears on a quotation (SPEC §8, plus a per-item discount).</summary>
public sealed class Quotation
{
    public string Title { get; set; } = "Quotation";
    public string RefNo { get; set; } = "";
    public string OtherRef { get; set; } = "";
    public string Date { get; set; } = "";
    public string Terms { get; set; } = "";
    public Party Seller { get; set; } = new();
    public Party Buyer { get; set; } = new() { Label = "Buyer" };
    public List<LineItem> Items { get; set; } = [];
    public List<Tax> Taxes { get; set; } = [];
    public List<string> TermsAndConditions { get; set; } = [];
}
