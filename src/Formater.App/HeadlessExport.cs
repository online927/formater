using System.Globalization;
using Formater.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Formater.App;

/// <summary>
/// Formater.exe --export input.txt output.pdf [--date yyyy-MM-dd] [--data-dir folder] [--settings file] [--no-register]
/// Makes the PDF without showing a window. Like the app, it issues the next Ref No. (or checks a typed one),
/// keeps the PDF in the PDF folder, records it in the register and copies it to output.pdf.
/// --no-register only draws the PDF: no number is used and nothing is recorded.
/// Exit codes: 0 saved, 1 input has errors or the Ref No. was already used, 2 bad arguments,
/// 3 PDF could not be written, 4 other failure.
/// </summary>
internal static class HeadlessExport
{
    public static int Run(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("usage: Formater.exe --export <input.txt|input.json> <output.pdf> " +
                                    "[--date yyyy-MM-dd] [--data-dir folder] [--settings file] [--no-register]");
            return 2;
        }

        var input = args[1];
        var output = Path.GetFullPath(args[2]);
        var today = DateOnly.FromDateTime(DateTime.Today);
        string? settingsFile = null;
        var register = true;
        for (var i = 3; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--date" when i + 1 < args.Length:
                    today = DateOnly.ParseExact(args[++i], "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    break;
                case "--settings" when i + 1 < args.Length:
                    settingsFile = args[++i];
                    break;
                case "--data-dir" when i + 1 < args.Length:
                    AppPaths.UseDataFolder(args[++i]);
                    break;
                case "--no-register":
                    register = false;
                    break;
                default:
                    Console.Error.WriteLine($"Unknown option {args[i]}");
                    return 2;
            }
        }

        var settings = AppSettings.Load(settingsFile ?? AppPaths.SettingsFile);
        var text = File.ReadAllText(input);
        var parsed = QuotationInput.Read(text, settings, today);
        foreach (var w in parsed.Warnings) Console.Error.WriteLine("warning: " + w);
        foreach (var e in parsed.Errors) Console.Error.WriteLine("error: " + e);
        if (!parsed.IsValid) return 1;
        var reg = new QuotationRegister(AppPaths.RegisterFile);

        var code = 4;
        using var form = new Form
        {
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-10000, -10000),
            Size = new Size(900, 1200),
        };
        var web = new WebView2 { Dock = DockStyle.Fill };
        form.Controls.Add(web);
        form.Shown += async (_, _) =>
        {
            try
            {
                var engine = new PdfEngine(web);
                await engine.InitAsync();
                code = register
                    ? await ExportRegistered(engine, reg, settings, parsed, text, today, output)
                    : await ExportOnly(engine, reg, settings, parsed, today, output);
            }
            catch (Exception ex)
            {
                code = 4;
                Console.Error.WriteLine(ex.Message);
            }
            finally
            {
                form.Close();
            }
        };
        Application.Run(form);
        return code;
    }

    private static async Task<int> ExportRegistered(PdfEngine engine, QuotationRegister reg, AppSettings settings,
        ParseResult parsed, string text, DateOnly today, string output)
    {
        var outcome = await QuotationExporter.ExportAsync(engine, reg, settings, parsed, text, today);
        if (!outcome.Ok)
        {
            Console.Error.WriteLine(outcome.Message);
            return outcome.Message.Contains("already used", StringComparison.Ordinal) ? 1 : 3;
        }
        if (!string.Equals(Path.GetFullPath(outcome.ArchivePath), output, StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.Copy(outcome.ArchivePath, output, overwrite: true);
        }
        Console.WriteLine($"Saved {outcome.Ref} to {output} (kept in {outcome.ArchivePath}; {outcome.Pages} page(s), total {outcome.Total})");
        return 0;
    }

    private static async Task<int> ExportOnly(PdfEngine engine, QuotationRegister reg, AppSettings settings,
        ParseResult parsed, DateOnly today, string output)
    {
        var q = parsed.Quotation;
        if (q.RefNo.Trim().Length == 0) q.RefNo = reg.PeekNext(settings.RefPattern, Numbering.FyStart(today));
        var model = Calculator.ToRenderModel(q, settings.DiscountColumn);
        var pages = await engine.RenderAsync(model);
        if (!await engine.PrintAsync(output))
        {
            Console.Error.WriteLine($"Could not write {output}");
            return 3;
        }
        Console.WriteLine($"Saved {output} ({pages} page(s), total {model.Totals[^1].Value}; not registered)");
        return 0;
    }
}
