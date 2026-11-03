namespace Formater.App;

internal static class AppPaths
{
    public static string DataFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Formater");

    public static string SettingsFile => Path.Combine(DataFolder, "settings.json");
    public static string DraftFile => Path.Combine(DataFolder, "draft.txt");

    /// <summary>WebView2 needs a writable folder; next to the exe may be read-only (Program Files).</summary>
    public static string WebViewFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Formater", "WebView2");

    public static string DefaultPdfFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Quotations");
}
