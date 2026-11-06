using Formater.Core;

namespace Formater.App;

internal static class AppPaths
{
    private static string? _dataOverride;

    /// <summary>Keep settings, register and PDFs under one folder (used by --data-dir, e.g. for tests).</summary>
    public static void UseDataFolder(string folder) => _dataOverride = Path.GetFullPath(folder);

    public static string DataFolder =>
        _dataOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Formater");

    public static string SettingsFile => Path.Combine(DataFolder, "settings.json");
    public static string DraftFile => Path.Combine(DataFolder, "draft.txt");

    /// <summary>The permanent list of every Ref No. issued on this PC.</summary>
    public static string RegisterFile => Path.Combine(DataFolder, "register.json");

    /// <summary>WebView2 needs a writable folder; next to the exe may be read-only (Program Files).</summary>
    public static string WebViewFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Formater", "WebView2");

    /// <summary>Where every exported PDF is kept unless Settings name another folder.</summary>
    public static string DefaultArchiveFolder =>
        _dataOverride is not null
            ? Path.Combine(_dataOverride, "PDFs")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Formater", "PDFs");

    public static string ArchiveFolder(AppSettings s) =>
        string.IsNullOrWhiteSpace(s.ArchiveFolder) ? DefaultArchiveFolder : s.ArchiveFolder;

    /// <summary>Where the "save a copy" dialog opens the first time.</summary>
    public static string DefaultPdfFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Quotations");
}
