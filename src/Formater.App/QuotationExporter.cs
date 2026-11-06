using Formater.Core;

namespace Formater.App;

internal sealed record ExportOutcome(bool Ok, string Message, string Ref = "", string ArchivePath = "", int Pages = 0, string Total = "")
{
    public static ExportOutcome Fail(string message) => new(false, message);
}

/// <summary>
/// Issues the Ref No., draws the quotation with it, saves the PDF in the archive folder and records it in the
/// register. The number is only used once the PDF has been written; a Ref No. can never be used twice.
/// </summary>
internal static class QuotationExporter
{
    public static async Task<ExportOutcome> ExportAsync(
        PdfEngine engine, QuotationRegister register, AppSettings settings, ParseResult parsed, string sourceText, DateOnly today)
    {
        if (!parsed.IsValid) return ExportOutcome.Fail(string.Join("\n", parsed.Errors));

        var q = parsed.Quotation;
        var fy = Numbering.FyStart(today);
        var typed = q.RefNo.Trim();

        QuotationRegister.Issue issue;
        try
        {
            issue = register.BeginIssue();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ExportOutcome.Fail("The number register is busy or cannot be opened: " + ex.Message);
        }

        using (issue)
        {
            string reference;
            var number = 0;
            if (typed.Length > 0)
            {
                if (issue.Find(typed) is { } used) return ExportOutcome.Fail(UsedMessage(used));
                reference = typed;
            }
            else
            {
                if (Numbering.Validate(settings.RefPattern) is { } problem)
                    return ExportOutcome.Fail(problem + " Fix it in Settings → Numbering.");
                (reference, number) = issue.Allocate(settings.RefPattern, fy);
            }

            q.RefNo = reference;
            var model = Calculator.ToRenderModel(q, settings.DiscountColumn);
            var pages = await engine.RenderAsync(model);

            var folder = Path.Combine(AppPaths.ArchiveFolder(settings), Numbering.FyLabel(fy));
            string path;
            try
            {
                Directory.CreateDirectory(folder);
                path = FileNames.Unique(Path.Combine(folder, FileNames.Make(settings.FileNamePattern, q)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return ExportOutcome.Fail($"Cannot use the PDF folder {folder}: {ex.Message}");
            }

            if (!await engine.PrintAsync(path))
                return ExportOutcome.Fail($"Could not write {path}.\nIf a PDF of that name is open, close it and try again.");

            var total = model.Totals[^1].Value;
            try
            {
                issue.Commit(new RegisterEntry
                {
                    Ref = reference,
                    FyStart = fy,
                    Number = number,
                    IssuedAt = DateTime.Now,
                    Title = q.Title,
                    Company = q.Buyer.Name,
                    Date = q.Date,
                    Items = q.Items.Count,
                    Total = total,
                    PdfPath = path,
                    SourceText = sourceText,
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                // Not recorded, so the number is not used: remove the PDF that carries it.
                TryDelete(path);
                return ExportOutcome.Fail("The number register could not be saved, so nothing was exported: " + ex.Message);
            }

            return new ExportOutcome(true, "", reference, path, pages, total);
        }
    }

    public static string UsedMessage(RegisterEntry used) =>
        $"Ref No. {used.Ref} was already used on {used.IssuedAt:dd MMM yyyy HH:mm} for {used.Company}" +
        (used.Cancelled ? " (cancelled)" : "") +
        ". Each number can only be used once: remove the Ref line to get the next number.";

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left in place; the register is the authority on which numbers are used.
        }
    }
}
