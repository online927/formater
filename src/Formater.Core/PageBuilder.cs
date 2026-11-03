using System.Reflection;
using System.Text;

namespace Formater.Core;

/// <summary>
/// Builds the self-contained HTML page (fonts and logos embedded) that the app shows as the preview and
/// prints to PDF with Chromium (WebView2 on Windows, Playwright in tests).
/// </summary>
public static class PageBuilder
{
    private static readonly Lazy<string> Shell = new(BuildShell);

    /// <summary>The page with no data; call <c>formater.render(model)</c> in the page to draw a quotation.</summary>
    public static string EmptyPage() => Shell.Value.Replace("{{DATA}}", "");

    /// <summary>The page with a quotation embedded; it draws itself once the fonts have loaded.</summary>
    public static string Page(RenderModel model) => Shell.Value.Replace("{{DATA}}", model.ToJson());

    private static string BuildShell()
    {
        var html = Text("page.html");
        return html
            .Replace("{{FONT_REGULAR}}", Convert.ToBase64String(Bytes("Ubuntu-Regular.ttf")))
            .Replace("{{FONT_BOLD}}", Convert.ToBase64String(Bytes("Ubuntu-Bold.ttf")))
            .Replace("{{LOGO_BANNER}}", Text("logo-banner.svg"))
            .Replace("{{LOGO_SQUARE}}", Text("logo-square.svg"))
            .Replace("{{LAYOUT_JS}}", Text("layout.js"));
    }

    private static byte[] Bytes(string name)
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
                      ?? throw new InvalidOperationException($"Missing embedded resource {name}");
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private static string Text(string name) => Encoding.UTF8.GetString(Bytes(name)).TrimStart('﻿');
}
