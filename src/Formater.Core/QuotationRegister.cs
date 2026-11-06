using System.Text.Json;

namespace Formater.Core;

/// <summary>One issued Ref No. Entries are never deleted; a mistake is marked Cancelled.</summary>
public sealed class RegisterEntry
{
    public string Ref { get; set; } = "";
    public int FyStart { get; set; }

    /// <summary>The running number, or 0 when the Ref No. was typed in the text.</summary>
    public int Number { get; set; }
    public DateTime IssuedAt { get; set; }
    public string Title { get; set; } = "";
    public string Company { get; set; } = "";
    public string Date { get; set; } = "";
    public int Items { get; set; }
    public string Total { get; set; } = "";
    public string PdfPath { get; set; } = "";
    public string SourceText { get; set; } = "";
    public bool Cancelled { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string CancelReason { get; set; } = "";
}

public sealed class RegisterData
{
    /// <summary>Next running number per financial year (key: the year it starts, e.g. "2026").</summary>
    public Dictionary<string, int> NextNumber { get; set; } = [];
    public List<RegisterEntry> Entries { get; set; } = [];
}

/// <summary>
/// The permanent list of every Ref No. the app has issued (a JSON file). A number is only added once its PDF
/// has been saved, and no Ref No. can ever be issued twice. Writes are serialised with a lock file, so two
/// copies of the app on the same PC cannot hand out the same number.
/// </summary>
public sealed class QuotationRegister
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public QuotationRegister(string path) => FilePath = path;

    public string FilePath { get; }

    public RegisterData Load()
    {
        if (!File.Exists(FilePath)) return new RegisterData();
        using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return JsonSerializer.Deserialize<RegisterData>(stream, Options) ?? new RegisterData();
    }

    public IReadOnlyList<RegisterEntry> Entries() => Load().Entries;

    public RegisterEntry? Find(string reference) => Find(Load(), reference);

    /// <summary>The Ref No. the next export will get. Nothing is reserved.</summary>
    public string PeekNext(string pattern, int fyStart) => NextFree(Load(), pattern, fyStart).Ref;

    /// <summary>The running number the next export in that financial year will get.</summary>
    public int NextNumber(int fyStart) => EffectiveNext(Load(), fyStart);

    /// <summary>The highest running number used in that financial year, or 0.</summary>
    public int LastUsedNumber(int fyStart) => MaxUsed(Load(), fyStart);

    /// <summary>Starts an issue: holds the lock until disposed. Nothing is written until <see cref="Issue.Commit"/>.</summary>
    public Issue BeginIssue() => new(this);

    /// <summary>Changes the next running number. It can never go back to a number already used.</summary>
    public void SetNextNumber(int fyStart, int next, string pattern)
    {
        using var issue = BeginIssue();
        var max = MaxUsed(issue.Data, fyStart);
        if (next < 1) throw new InvalidOperationException("The next number must be 1 or more.");
        if (next <= max)
            throw new InvalidOperationException(
                $"Numbers up to {Numbering.Format(pattern, fyStart, max)} are already used in {Numbering.FyLabel(fyStart)}. " +
                $"The next number must be {max + 1} or more.");
        issue.Data.NextNumber[Key(fyStart)] = next;
        issue.Save();
    }

    /// <summary>Marks an issued quotation as cancelled. Its number stays used.</summary>
    public void Cancel(string reference, string reason)
    {
        using var issue = BeginIssue();
        var entry = Find(issue.Data, reference) ?? throw new InvalidOperationException($"{reference} is not in the register.");
        if (entry.Cancelled) return;
        entry.Cancelled = true;
        entry.CancelledAt = DateTime.Now;
        entry.CancelReason = reason.Trim();
        issue.Save();
    }

    internal static string Key(int fyStart) => fyStart.ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal static RegisterEntry? Find(RegisterData data, string reference)
    {
        var r = Normalize(reference);
        return data.Entries.FirstOrDefault(e => Normalize(e.Ref) == r);
    }

    private static string Normalize(string reference) =>
        string.Concat(reference.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();

    private static int MaxUsed(RegisterData data, int fyStart) =>
        data.Entries.Where(e => e.FyStart == fyStart && e.Number > 0).Select(e => e.Number).DefaultIfEmpty(0).Max();

    private static int EffectiveNext(RegisterData data, int fyStart)
    {
        var stored = data.NextNumber.TryGetValue(Key(fyStart), out var n) ? n : 1;
        return Math.Max(stored, MaxUsed(data, fyStart) + 1);
    }

    /// <summary>The next running number whose Ref No. is not already taken (e.g. typed by hand earlier).</summary>
    internal static (string Ref, int Number) NextFree(RegisterData data, string pattern, int fyStart)
    {
        for (var n = EffectiveNext(data, fyStart); ; n++)
        {
            var reference = Numbering.Format(pattern, fyStart, n);
            if (Find(data, reference) is null) return (reference, n);
        }
    }

    internal void Write(RegisterData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(data, Options));
        if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak");
        else File.Move(temp, FilePath);
    }

    /// <summary>An exclusive hold on the register while a number is issued.</summary>
    public sealed class Issue : IDisposable
    {
        private readonly QuotationRegister _register;
        private readonly FileStream _lock;

        internal Issue(QuotationRegister register)
        {
            _register = register;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(register.FilePath))!);
            _lock = OpenLock(register.FilePath + ".lock");
            Data = register.Load();
        }

        internal RegisterData Data { get; }

        public RegisterEntry? Find(string reference) => QuotationRegister.Find(Data, reference);

        /// <summary>The next free Ref No. for the pattern; it is only used once committed.</summary>
        public (string Ref, int Number) Allocate(string pattern, int fyStart) => NextFree(Data, pattern, fyStart);

        /// <summary>Records the issued quotation. Fails if its Ref No. was already used.</summary>
        public void Commit(RegisterEntry entry)
        {
            if (Find(entry.Ref) is { } used)
                throw new InvalidOperationException($"Ref No. {entry.Ref} was already used on {used.IssuedAt:dd MMM yyyy}.");
            Data.Entries.Add(entry);
            if (entry.Number > 0)
            {
                var key = Key(entry.FyStart);
                var stored = Data.NextNumber.TryGetValue(key, out var n) ? n : 1;
                Data.NextNumber[key] = Math.Max(stored, entry.Number + 1);
            }
            Save();
        }

        internal void Save() => _register.Write(Data);

        public void Dispose() => _lock.Dispose();

        private static FileStream OpenLock(string path)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException) when (attempt < 100)
                {
                    Thread.Sleep(100);
                }
            }
        }
    }
}
