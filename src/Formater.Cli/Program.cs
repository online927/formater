using System.Globalization;
using Formater.Core;

// formater-html <input.txt|input.json> <output.html> [--date 2026-11-02] [--settings settings.json]
// Writes the same self-contained page the Windows app prints, for tests and for printing with Playwright.
if (args.Length < 2)
{
    Console.Error.WriteLine("usage: formater-html <input.txt|input.json> <output.html> [--date yyyy-MM-dd] [--settings file]");
    return 2;
}

var today = DateOnly.FromDateTime(DateTime.Today);
var settings = new AppSettings();
for (var i = 2; i < args.Length - 1; i++)
{
    if (args[i] == "--date") today = DateOnly.ParseExact(args[++i], "yyyy-MM-dd", CultureInfo.InvariantCulture);
    else if (args[i] == "--settings") settings = AppSettings.Load(args[++i]);
}

var result = QuotationInput.Read(File.ReadAllText(args[0]), settings, today);
foreach (var w in result.Warnings) Console.Error.WriteLine("warning: " + w);
foreach (var e in result.Errors) Console.Error.WriteLine("error: " + e);
if (!result.IsValid) return 1;

// No Ref in the text: show the first number of the format (this tool has no register).
if (result.Quotation.RefNo.Length == 0)
    result.Quotation.RefNo = Numbering.Format(settings.RefPattern, Numbering.FyStart(today), 1);
var model = Calculator.ToRenderModel(result.Quotation, settings.DiscountColumn);
File.WriteAllText(args[1], PageBuilder.Page(model));
Console.WriteLine($"{args[1]}: {model.Items.Count} item(s), {result.GstNote}, total {model.Totals[^1].Value}");
return 0;
