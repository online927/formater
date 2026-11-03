using System.Globalization;
using Formater.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Formater.App;

/// <summary>
/// Formater.exe --export input.txt output.pdf [--date yyyy-MM-dd] [--settings settings.json]
/// Makes the PDF without showing a window (used by the build's smoke test, and handy for batch files).
/// Exit codes: 0 saved, 1 input has errors, 2 bad arguments, 3 PDF could not be written, 4 other failure.
/// </summary>
internal static class HeadlessExport
{
    public static int Run(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("usage: Formater.exe --export <input.txt|input.json> <output.pdf> [--date yyyy-MM-dd] [--settings file]");
            return 2;
        }

        var input = args[1];
        var output = Path.GetFullPath(args[2]);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var settingsFile = AppPaths.SettingsFile;
        for (var i = 3; i < args.Length - 1; i++)
        {
            if (args[i] == "--date") today = DateOnly.ParseExact(args[++i], "yyyy-MM-dd", CultureInfo.InvariantCulture);
            else if (args[i] == "--settings") settingsFile = args[++i];
        }

        var result = QuotationInput.Read(File.ReadAllText(input), AppSettings.Load(settingsFile), today);
        foreach (var w in result.Warnings) Console.Error.WriteLine("warning: " + w);
        foreach (var e in result.Errors) Console.Error.WriteLine("error: " + e);
        if (!result.IsValid) return 1;
        var model = Calculator.ToRenderModel(result.Quotation);

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
                var pages = await engine.RenderAsync(model);
                if (await engine.PrintAsync(output))
                {
                    code = 0;
                    Console.WriteLine($"Saved {output} ({pages} page(s), total {model.Totals[^1].Value})");
                }
                else
                {
                    code = 3;
                    Console.Error.WriteLine($"Could not write {output}");
                }
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
}
