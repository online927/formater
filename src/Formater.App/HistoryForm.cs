using System.Globalization;
using System.Text;
using Formater.Core;

namespace Formater.App;

/// <summary>
/// Every quotation exported on this PC, newest first. Nothing can be deleted: a mistake is marked Cancelled
/// and keeps its number.
/// </summary>
internal sealed class HistoryForm : Form
{
    private readonly QuotationRegister _register;
    private readonly Action<string> _copyToEditor;
    private readonly ListView _list = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        MultiSelect = false,
        HideSelection = false,
        GridLines = true,
    };
    private readonly TextBox _search = new() { Width = 280, PlaceholderText = "Search Ref No., company, total…" };
    private readonly Label _count = new() { AutoSize = true, Margin = new Padding(12, 7, 0, 0), ForeColor = SystemColors.GrayText };
    private readonly Button _openPdf = new() { Text = "Open PDF", AutoSize = true };
    private readonly Button _showFolder = new() { Text = "Show in folder", AutoSize = true };
    private readonly Button _copyText = new() { Text = "Copy text to editor", AutoSize = true };
    private readonly Button _cancel = new() { Text = "Mark cancelled…", AutoSize = true };
    private List<RegisterEntry> _entries = [];

    public HistoryForm(QuotationRegister register, Action<string> copyToEditor)
    {
        _register = register;
        _copyToEditor = copyToEditor;
        Text = "History — every exported quotation";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1100, 640);
        MinimumSize = new Size(760, 400);
        ShowInTaskbar = false;
        Padding = new Padding(10);

        _list.Columns.Add("Ref No.", 140);
        _list.Columns.Add("Saved", 135);
        _list.Columns.Add("Company", 260);
        _list.Columns.Add("Items", 55, HorizontalAlignment.Right);
        _list.Columns.Add("Total", 120, HorizontalAlignment.Right);
        _list.Columns.Add("Status", 160);
        _list.Columns.Add("PDF", 400);

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Padding = new Padding(0, 0, 0, 8) };
        top.Controls.AddRange([new Label { Text = "Search", AutoSize = true, Margin = new Padding(0, 7, 6, 0) }, _search, _count]);

        var close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel };
        var exportCsv = new Button { Text = "Export list (CSV)…", AutoSize = true };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
        buttons.Controls.AddRange([_openPdf, _showFolder, _copyText, _cancel, exportCsv, close]);
        CancelButton = close;

        Controls.Add(_list);
        Controls.Add(top);
        Controls.Add(buttons);

        _search.TextChanged += (_, _) => Fill();
        _list.SelectedIndexChanged += (_, _) => UpdateButtons();
        _list.DoubleClick += (_, _) => OpenPdf();
        _openPdf.Click += (_, _) => OpenPdf();
        _showFolder.Click += (_, _) => ShowInFolder();
        _copyText.Click += (_, _) => CopyText();
        _cancel.Click += (_, _) => MarkCancelled();
        exportCsv.Click += (_, _) => ExportCsv();

        Reload();
    }

    private RegisterEntry? Selected => _list.SelectedItems.Count == 1 ? _list.SelectedItems[0].Tag as RegisterEntry : null;

    private void Reload()
    {
        try
        {
            _entries = _register.Entries().OrderByDescending(e => e.IssuedAt).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            _entries = [];
            MessageBox.Show(this, "The number register cannot be read: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        Fill();
    }

    private void Fill()
    {
        var term = _search.Text.Trim();
        var shown = _entries.Where(e => term.Length == 0 ||
                                        e.Ref.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                        e.Company.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                        e.Total.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var e in shown)
        {
            var item = new ListViewItem(e.Ref) { Tag = e };
            item.SubItems.Add(e.IssuedAt.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture));
            item.SubItems.Add(e.Company);
            item.SubItems.Add(e.Items.ToString(CultureInfo.InvariantCulture));
            item.SubItems.Add(e.Total);
            item.SubItems.Add(e.Cancelled ? "Cancelled" + (e.CancelReason.Length > 0 ? ": " + e.CancelReason : "") : "Issued");
            item.SubItems.Add(File.Exists(e.PdfPath) ? e.PdfPath : "(missing) " + e.PdfPath);
            if (e.Cancelled) item.ForeColor = SystemColors.GrayText;
            _list.Items.Add(item);
        }
        _list.EndUpdate();
        _count.Text = term.Length == 0 ? $"{_entries.Count} quotation(s)" : $"{shown.Count} of {_entries.Count}";
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var e = Selected;
        _openPdf.Enabled = e is not null;
        _showFolder.Enabled = e is not null;
        _copyText.Enabled = e is not null && e.SourceText.Length > 0;
        _cancel.Enabled = e is { Cancelled: false };
    }

    private void OpenPdf()
    {
        if (Selected is not { } e) return;
        if (!File.Exists(e.PdfPath))
        {
            MessageBox.Show(this, $"The PDF is no longer at\n{e.PdfPath}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        MainForm.OpenFile(e.PdfPath);
    }

    private void ShowInFolder()
    {
        if (Selected is not { } e) return;
        try
        {
            if (File.Exists(e.PdfPath))
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{e.PdfPath}\"");
            else if (Path.GetDirectoryName(e.PdfPath) is { } dir && Directory.Exists(dir))
                MainForm.OpenFile(dir);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CopyText()
    {
        if (Selected is not { } e) return;
        var answer = MessageBox.Show(this,
            $"Put the text of {e.Ref} in the editor?\n\nIt replaces the text there now. The Ref line is removed, so the " +
            "copy gets a new number when you export it.",
            Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
        if (answer != DialogResult.OK) return;
        _copyToEditor(QuotationTextParser.RemoveRefLines(e.SourceText));
        Close();
    }

    private void MarkCancelled()
    {
        if (Selected is not { Cancelled: false } e) return;
        var reason = Prompt($"Cancel {e.Ref} ({e.Company})?\n\nThe PDF and the number stay in the history, marked " +
                            "cancelled. The number is never used again. Reason (optional):");
        if (reason is null) return;
        try
        {
            _register.Cancel(e.Ref, reason);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        Reload();
    }

    private string? Prompt(string message)
    {
        using var form = new Form
        {
            Text = "Mark cancelled",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12),
        };
        var box = new TextBox { Width = 420 };
        var ok = new Button { Text = "Mark cancelled", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Keep", DialogResult = DialogResult.Cancel, AutoSize = true };
        var panel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill };
        var buttons = new FlowLayoutPanel { AutoSize = true };
        buttons.Controls.AddRange([ok, cancel]);
        panel.Controls.AddRange([new Label { Text = message, AutoSize = true, MaximumSize = new Size(420, 0) }, box, buttons]);
        form.Controls.Add(panel);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        return form.ShowDialog(this) == DialogResult.OK ? box.Text : null;
    }

    private void ExportCsv()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Export the list",
            Filter = "CSV for Excel (*.csv)|*.csv",
            FileName = $"Formater-history-{DateTime.Today:yyyy-MM-dd}.csv",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        static string Cell(string v) => "\"" + v.Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder("Ref No.,Saved,Date,Company,Items,Total,Status,Reason,PDF\r\n");
        foreach (var e in _entries)
        {
            sb.AppendJoin(',', Cell(e.Ref), Cell(e.IssuedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
                    Cell(e.Date), Cell(e.Company), e.Items.ToString(CultureInfo.InvariantCulture), Cell(e.Total),
                    Cell(e.Cancelled ? "Cancelled" : "Issued"), Cell(e.CancelReason), Cell(e.PdfPath))
                .Append("\r\n");
        }
        try
        {
            File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
