using System.Globalization;
using System.Text.RegularExpressions;

namespace Formater.Core;

/// <summary>
/// Ref No. patterns such as "T/{FY}/{N:5}" → "T/2627/00001". The financial year runs April to March and the
/// running number restarts at 1 every financial year.
/// Tokens: {FY} = 2627, {FY-} = 2026-27, {YYYY} = 2026 (year the FY starts), {N} = 1, {N:5} = 00001.
/// </summary>
public static partial class Numbering
{
    public const string DefaultPattern = "T/{FY}/{N:5}";

    [GeneratedRegex(@"\{N(?::(?<w>\d{1,2}))?\}")]
    private static partial Regex NumberToken();

    [GeneratedRegex(@"\{[^}]*\}")]
    private static partial Regex AnyToken();

    /// <summary>The calendar year in which the financial year containing <paramref name="date"/> starts.</summary>
    public static int FyStart(DateOnly date) => date.Month >= 4 ? date.Year : date.Year - 1;

    /// <summary>2026 → "2627".</summary>
    public static string FyCode(int fyStart) =>
        (fyStart % 100).ToString("00", CultureInfo.InvariantCulture) +
        ((fyStart + 1) % 100).ToString("00", CultureInfo.InvariantCulture);

    /// <summary>2026 → "2026-27".</summary>
    public static string FyLabel(int fyStart) =>
        fyStart.ToString(CultureInfo.InvariantCulture) + "-" +
        ((fyStart + 1) % 100).ToString("00", CultureInfo.InvariantCulture);

    public static string Format(string pattern, int fyStart, int number)
    {
        var s = NumberToken().Replace(pattern, m =>
        {
            var width = m.Groups["w"].Success ? int.Parse(m.Groups["w"].Value, CultureInfo.InvariantCulture) : 0;
            return number.ToString(new string('0', Math.Max(width, 1)), CultureInfo.InvariantCulture);
        });
        return s.Replace("{FY-}", FyLabel(fyStart))
                .Replace("{FY}", FyCode(fyStart))
                .Replace("{YYYY}", fyStart.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Null when the pattern is usable, otherwise what is wrong with it.</summary>
    public static string? Validate(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return "The Ref No. format is empty.";
        if (NumberToken().Matches(pattern).Count != 1)
            return "The Ref No. format needs exactly one running number: {N} or {N:5} (5 digits).";
        foreach (Match m in AnyToken().Matches(pattern))
            if (!NumberToken().IsMatch(m.Value) && m.Value is not ("{FY}" or "{FY-}" or "{YYYY}"))
                return $"Unknown part {m.Value}. Use {{FY}}, {{FY-}}, {{YYYY}} and {{N:5}}.";
        return null;
    }
}

/// <summary>PDF file names from a pattern such as "Quotation-{Ref}" → "Quotation-T-2627-00001.pdf".</summary>
public static class FileNames
{
    public const string DefaultPattern = "Quotation-{Ref}";

    public static string Make(string pattern, Quotation q)
    {
        var name = (string.IsNullOrWhiteSpace(pattern) ? DefaultPattern : pattern)
            .Replace("{Ref}", q.RefNo)
            .Replace("{Company}", q.Buyer.Name)
            .Replace("{Title}", q.Title)
            .Replace("{Date}", q.Date);
        if (name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        return Safe(name) + ".pdf";
    }

    /// <summary>Characters Windows does not allow in file names (/ \ : * ? " &lt; &gt; |) become "-".</summary>
    public static string Safe(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
        var clean = new string(name.Select(c => invalid.Contains(c) || char.IsControl(c) ? '-' : c).ToArray())
            .Trim().TrimEnd('.');
        return clean.Length > 0 ? clean : "Quotation";
    }

    /// <summary>The path itself, or "name (2).pdf", "name (3).pdf" … when a file of that name exists.</summary>
    public static string Unique(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var stem = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (var i = 2; ; i++)
        {
            var candidate = Path.Combine(dir, $"{stem} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
    }
}
