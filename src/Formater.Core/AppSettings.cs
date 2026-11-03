using System.Text.Json;

namespace Formater.Core;

/// <summary>Fixed details that are the same on every quotation. Editable in the app's Settings.</summary>
public sealed class AppSettings
{
    public string Title { get; set; } = "Quotation";
    public string SellerName { get; set; } = "T.SAIFUDDIN & CO";
    public List<string> SellerLines { get; set; } =
        ["No.72", "N.R.Road", "Bangalore", "560002 Karnataka", "India", "GSTIN 29AADFT8885H1ZF"];

    /// <summary>Used for automatic GST when the buyer has no GSTIN: same state → CGST + SGST.</summary>
    public string HomeState { get; set; } = "Karnataka";
    public decimal GstPercent { get; set; } = 18m;
    public string BuyerLabel { get; set; } = "Buyer";
    public string DefaultUnit { get; set; } = "Nos";
    public string DefaultTerms { get; set; } = "Against Delivery";
    public List<string> TermsAndConditions { get; set; } =
        ["1. Payment: 100% Against Proforma", "2. Packing and forwarding charges extra"];

    public bool OpenPdfAfterExport { get; set; } = true;
    public string? PdfFolder { get; set; }

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();
        }
        catch (JsonException)
        {
            // A damaged settings file falls back to the defaults rather than stopping the app.
        }
        return new AppSettings();
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Options));
    }
}
