using System.Text.Json;

namespace Formater.Core;

/// <summary>Fixed details that are the same on every quotation. Editable in the app's Settings.</summary>
public sealed class AppSettings
{
    /// <summary>2 = the "Quotation/Proforma" format with a sign-off. Files without it are from version 1.</summary>
    public int FormatVersion { get; set; } = CurrentFormatVersion;
    public const int CurrentFormatVersion = 2;

    public string Title { get; set; } = "Quotation/Proforma";
    public string SellerName { get; set; } = "T.SAIFUDDIN & CO";
    public List<string> SellerLines { get; set; } =
        ["No.72 N.R.Road", "Bangalore", "Karnataka 560002", "+917019740620", "+919739394657", "GSTIN 29AADFT8885H1ZF"];

    /// <summary>Used for automatic GST when the buyer has no GSTIN: same state → CGST + SGST.</summary>
    public string HomeState { get; set; } = "Karnataka";
    public decimal GstPercent { get; set; } = 18m;
    public string BuyerLabel { get; set; } = "Buyer/Consignee";
    public string DefaultUnit { get; set; } = "Nos";

    /// <summary>Shown as "Terms :" under the date. Empty hides the line (the reference has it empty).</summary>
    public string DefaultTerms { get; set; } = "";
    public List<string> TermsAndConditions { get; set; } =
        ["1. Payment: 100% Against Proforma", "2. Packing and forwarding charges extra"];
    public List<string> SignOff { get; set; } = ["Regards,", "Name,", "Tsaifuddin and Company"];

    public bool OpenPdfAfterExport { get; set; } = true;
    public string? PdfFolder { get; set; }

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path)) return FromJson(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            // A damaged settings file falls back to the defaults rather than stopping the app.
        }
        return new AppSettings();
    }

    /// <summary>
    /// Reads saved settings. Values saved by the first version that are still that version's defaults move to
    /// the current format's defaults; anything the user changed is kept.
    /// </summary>
    public static AppSettings FromJson(string json)
    {
        var s = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty(nameof(FormatVersion), out _)) return s;

        var d = new AppSettings();
        if (s.Title == "Quotation") s.Title = d.Title;
        if (s.SellerLines.SequenceEqual(["No.72", "N.R.Road", "Bangalore", "560002 Karnataka", "India", "GSTIN 29AADFT8885H1ZF"]))
            s.SellerLines = d.SellerLines;
        if (s.BuyerLabel == "Buyer") s.BuyerLabel = d.BuyerLabel;
        if (s.DefaultTerms == "Against Delivery") s.DefaultTerms = d.DefaultTerms;
        s.FormatVersion = CurrentFormatVersion;
        return s;
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Options));
    }
}
