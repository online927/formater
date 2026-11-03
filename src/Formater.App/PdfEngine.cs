using System.Text.Json;
using Formater.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Formater.App;

/// <summary>
/// Hosts the quotation page in WebView2 (the Chromium engine built into Windows), draws quotations
/// with the page's layout script and prints them to PDF: A4, no margins, backgrounds on.
/// </summary>
internal sealed class PdfEngine
{
    public const string RuntimeDownloadUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

    private readonly WebView2 _web;
    private TaskCompletionSource<string>? _loaded;

    public PdfEngine(WebView2 web) => _web = web;

    public bool Ready { get; private set; }

    public static bool RuntimeInstalled()
    {
        try
        {
            return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString());
        }
        catch (WebView2RuntimeNotFoundException)
        {
            return false;
        }
    }

    public async Task InitAsync()
    {
        Directory.CreateDirectory(AppPaths.WebViewFolder);
        var env = await CoreWebView2Environment.CreateAsync(null, AppPaths.WebViewFolder);
        await _web.EnsureCoreWebView2Async(env);

        var core = _web.CoreWebView2;
        var s = core.Settings;
        s.AreDefaultContextMenusEnabled = false;
        s.AreDevToolsEnabled = false;
        s.IsStatusBarEnabled = false;
        s.AreBrowserAcceleratorKeysEnabled = false;
        s.IsZoomControlEnabled = false;
        s.AreDefaultScriptDialogsEnabled = false;
        s.IsWebMessageEnabled = true;
        core.NewWindowRequested += (_, e) => e.Handled = true;
        core.WebMessageReceived += OnMessage;

        _loaded = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        core.NavigateToString(PageBuilder.EmptyPage());
        var state = await _loaded.Task.WaitAsync(TimeSpan.FromSeconds(60));
        if (state != "ready")
            throw new InvalidOperationException(
                "The Ubuntu fonts could not be loaded, so the PDF would not match the layout.");
        Ready = true;
    }

    private void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.TryGetWebMessageAsString());
            _loaded?.TrySetResult(doc.RootElement.GetProperty("type").GetString() ?? "");
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or KeyNotFoundException)
        {
            // Not one of our messages.
        }
    }

    /// <summary>Draws the quotation in the page and returns the number of pages.</summary>
    public async Task<int> RenderAsync(RenderModel model)
    {
        var raw = await _web.CoreWebView2.ExecuteScriptAsync(
            $"JSON.stringify(window.formater.render({model.ToJson()}))");
        var json = JsonSerializer.Deserialize<string>(raw)
                   ?? throw new InvalidOperationException("The page did not answer.");
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.GetProperty("ok").GetBoolean())
            throw new InvalidOperationException("The page could not be drawn: " +
                                                doc.RootElement.GetProperty("error").GetString());
        return doc.RootElement.GetProperty("pages").GetInt32();
    }

    /// <summary>Prints the current page to a PDF file. False when the file could not be written.</summary>
    public async Task<bool> PrintAsync(string path)
    {
        var core = _web.CoreWebView2;
        var ps = core.Environment.CreatePrintSettings();
        ps.Orientation = CoreWebView2PrintOrientation.Portrait;
        ps.PageWidth = 8.27;    // A4, as Chromium's own "A4" (595.4 × 842.4 pt)
        ps.PageHeight = 11.7;
        ps.MarginTop = 0;
        ps.MarginBottom = 0;
        ps.MarginLeft = 0;
        ps.MarginRight = 0;
        ps.ScaleFactor = 1.0;
        ps.ShouldPrintBackgrounds = true;
        ps.ShouldPrintHeaderAndFooter = false;
        ps.ShouldPrintSelectionOnly = false;
        return await core.PrintToPdfAsync(path, ps);
    }
}
