using System.Diagnostics;
using Formater.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Formater.App;

/// <summary>Paste the quotation text on the left, see the page on the right, Export PDF.</summary>
internal sealed class MainForm : Form
{
    internal const string FormatHelp =
        "Paste or type the quotation as text. Only Company, Ref and at least one item are required.\n\n" +
        "Company: BuyersHub\n" +
        "Address: Plot 27 , PRT Colony , Karmanghat      (optional, one line each)\n" +
        "Address: Hyderabad Telangana 500079\n" +
        "GSTIN: 36AAZFB2304B1ZQ      (optional)\n" +
        "Phone: +919182706754          (optional)\n" +
        "Email: sales@buyershub.org.in  (optional)\n" +
        "Ref: 100186\n" +
        "Other Ref: R                  (optional)\n\n" +
        "Items, one per line:   item name, discount, qty, rate\n" +
        "Vernier Hook Depth Gauge, 10%, 5, 12011\n" +
        "  Make - Insize               (indented lines = small lines under the item)\n" +
        "Digital Caliper, 0, 2, 3450, Pcs, Make - Mitutoyo | Model : 500-196\n" +
        "                              (optional unit and | separated small lines after the rate)\n\n" +
        "GST is automatic: buyer GSTIN starting 29 (or a Karnataka address) → CGST 9% + SGST 9%,\n" +
        "otherwise IGST 18%. Change it with  GST: IGST,  GST: CGST+SGST,  GST: 12%  or  GST: None.\n\n" +
        "Also optional: Date: 02/11/2026,  Terms: 30 days credit,  T&C: one line each\n" +
        "(or a \"Terms & Conditions:\" line followed by the lines). The date is today by default.\n" +
        "Rows copied from Excel (tab separated) work too.";

    private const string ExampleText =
        "Company: BuyersHub\r\n" +
        "Address: Plot 27 , PRT Colony , Karmanghat\r\n" +
        "Address: Hyderabad Telangana 500079\r\n" +
        "Address: India\r\n" +
        "GSTIN: 36AAZFB2304B1ZQ\r\n" +
        "Phone: +919182706754\r\n" +
        "Email: sales@buyershub.org.in\r\n" +
        "Ref: 100186\r\n" +
        "Other Ref: R\r\n" +
        "\r\n" +
        "Vernier Hook Depth Gauge, 10%, 5, 12011\r\n" +
        "  Make - Insize\r\n" +
        "  Model : 1234-2001\r\n" +
        "Digital Caliper 150mm, 0%, 2, 3450\r\n";

    private readonly TextBox _input;
    private readonly RichTextBox _messages;
    private readonly WebView2 _web;
    private readonly ToolStripButton _export;
    private readonly ToolStripStatusLabel _status;
    private readonly SplitContainer _split;
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 300 };
    private AppSettings _settings;
    private PdfEngine? _engine;
    private ParseResult? _last;

    public MainForm()
    {
        Text = "Formater — Quotation PDF";
        Icon = LoadIcon();
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1320, 900);
        MinimumSize = new Size(900, 600);
        KeyPreview = true;
        _settings = AppSettings.Load(AppPaths.SettingsFile);

        // Toolbar
        var tools = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(6, 4, 6, 4) };
        _export = new ToolStripButton("Export PDF  (Ctrl+E)")
        {
            Font = new Font(Font, FontStyle.Bold),
            Enabled = false,
            ToolTipText = "Save the quotation as a PDF file",
        };
        _export.Click += async (_, _) => await ExportAsync();
        var example = new ToolStripButton("Example");
        example.Click += (_, _) => _input!.Text = ExampleText;
        var clear = new ToolStripButton("Clear");
        clear.Click += (_, _) => { _input!.Clear(); _input.Focus(); };
        var settings = new ToolStripButton("Settings");
        settings.Click += (_, _) => EditSettings();
        var help = new ToolStripButton("Text format");
        help.Click += (_, _) => MessageBox.Show(this, FormatHelp, "Text format", MessageBoxButtons.OK, MessageBoxIcon.Information);
        tools.Items.AddRange([_export, new ToolStripSeparator(), example, clear, new ToolStripSeparator(), settings, help]);

        // Left: text and messages
        _input = new TextBox
        {
            Multiline = true,
            AcceptsReturn = true,
            AcceptsTab = true,
            WordWrap = false,
            ScrollBars = ScrollBars.Both,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 10.5f),
            MaxLength = 0,
            PlaceholderText = "Paste the quotation here, for example:\r\n\r\n" + ExampleText,
        };
        _input.TextChanged += (_, _) => { _debounce.Stop(); _debounce.Start(); };
        _debounce.Tick += async (_, _) => { _debounce.Stop(); await RefreshAsync(); };

        _messages = new RichTextBox
        {
            ReadOnly = true,
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = SystemColors.Window,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            DetectUrls = false,
        };

        var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(8) };
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
        left.Controls.Add(new Label { Text = "Quotation text", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 0, 0, 4) }, 0, 0);
        left.Controls.Add(_input, 0, 1);
        left.Controls.Add(new Label { Text = "What the app understood", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 8, 0, 4) }, 0, 2);
        var messagesBorder = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(6) };
        messagesBorder.Controls.Add(_messages);
        left.Controls.Add(messagesBorder, 0, 3);

        // Right: page preview
        _web = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.FromArgb(128, 133, 140) };

        _split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1 };
        _split.Panel1.Controls.Add(left);
        _split.Panel2.Controls.Add(_web);

        var statusStrip = new StatusStrip();
        _status = new ToolStripStatusLabel("Starting…") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        statusStrip.Items.Add(_status);

        Controls.Add(_split);
        Controls.Add(tools);
        Controls.Add(statusStrip);

        Load += async (_, _) => await OnLoadAsync();
        FormClosing += (_, _) => SaveDraft();
        KeyDown += async (_, e) =>
        {
            if (e.Control && (e.KeyCode == Keys.E || e.KeyCode == Keys.S))
            {
                e.SuppressKeyPress = true;
                await ExportAsync();
            }
        };
    }

    private static Icon? LoadIcon()
    {
        using var s = typeof(MainForm).Assembly.GetManifestResourceStream("app.ico");
        return s is null ? null : new Icon(s);
    }

    private async Task OnLoadAsync()
    {
        _split.SplitterDistance = Math.Min(560, ClientSize.Width / 2);
        try
        {
            if (File.Exists(AppPaths.DraftFile)) _input.Text = File.ReadAllText(AppPaths.DraftFile);
        }
        catch (IOException)
        {
            // No draft; start empty.
        }

        try
        {
            _engine = new PdfEngine(_web);
            await _engine.InitAsync();
        }
        catch (Exception ex)
        {
            _engine = null;
            _status.Text = "Preview not available";
            MessageBox.Show(this, "The page preview could not start, so PDFs cannot be made.\n\n" + ex.Message,
                "Formater", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        await RefreshAsync();
        _input.Focus();
        _input.SelectionStart = _input.TextLength;
    }

    private async Task<ParseResult> RefreshAsync()
    {
        var result = QuotationInput.Read(_input.Text, _settings, DateOnly.FromDateTime(DateTime.Today));
        _last = result;
        int? pages = null;
        if (_engine?.Ready == true)
        {
            try
            {
                pages = await _engine.RenderAsync(Calculator.ToRenderModel(result.Quotation));
            }
            catch (Exception ex)
            {
                result.Errors.Add(new ParseMessage(0, "Preview failed: " + ex.Message));
            }
        }
        if (!ReferenceEquals(_last, result)) return result; // a newer refresh has started
        ShowMessages(result, pages);
        _export.Enabled = _engine?.Ready == true && result.IsValid;
        return result;
    }

    private void ShowMessages(ParseResult r, int? pages)
    {
        _messages.Clear();
        if (_input.TextLength == 0)
        {
            Append("Paste the quotation text on the left. Click \"Text format\" for the full format,\n" +
                   "or \"Example\" to load a sample.", SystemColors.GrayText);
            _status.Text = _engine?.Ready == true ? "Ready" : _status.Text;
            return;
        }

        foreach (var e in r.Errors) Append("✖ " + e, Color.Firebrick);
        foreach (var w in r.Warnings) Append("⚠ " + w, Color.DarkGoldenrod);

        var q = r.Quotation;
        var model = Calculator.ToRenderModel(q);
        Append($"Buyer: {(q.Buyer.Name.Length > 0 ? q.Buyer.Name : "—")}   ·   Ref No. {(q.RefNo.Length > 0 ? q.RefNo : "—")}" +
               (q.OtherRef.Length > 0 ? $"   ·   Other Ref: {q.OtherRef}" : ""), SystemColors.WindowText);
        Append($"Date: {q.Date}   ·   Terms: {(q.Terms.Length > 0 ? q.Terms : "—")}", SystemColors.WindowText);
        for (var i = 0; i < model.Items.Count; i++)
        {
            var it = model.Items[i];
            var disc = q.Items[i].DiscountPercent != 0 ? $" − {it.Discount}" : "";
            Append($"  {it.N}. {it.Name}:  {it.Qty} {it.Unit} × {it.Rate}{disc} = {it.Amount}", SystemColors.WindowText);
        }
        Append("GST: " + r.GstNote, SystemColors.WindowText);
        Append(string.Join("   ·   ", model.Totals.Select(t => $"{t.Label} {t.Value}")), SystemColors.WindowText, bold: true);

        _status.Text = r.IsValid
            ? $"Ready to export — {model.Items.Count} item(s), {(pages ?? 1)} page(s), total {model.Totals[^1].Value}"
            : $"{r.Errors.Count} problem(s) to fix before exporting";
        _messages.SelectionStart = 0;
        _messages.ScrollToCaret();
    }

    private void Append(string text, Color color, bool bold = false)
    {
        _messages.SelectionStart = _messages.TextLength;
        _messages.SelectionColor = color;
        _messages.SelectionFont = new Font(_messages.Font, bold ? FontStyle.Bold : FontStyle.Regular);
        _messages.AppendText(text + "\n");
    }

    private async Task ExportAsync()
    {
        var engine = _engine;
        if (engine?.Ready != true) return;
        _debounce.Stop();
        var result = await RefreshAsync();
        if (!result.IsValid)
        {
            MessageBox.Show(this, "Please fix these first:\n\n" + string.Join("\n", result.Errors),
                "Formater", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var q = result.Quotation;
        var folder = _settings.PdfFolder is { Length: > 0 } f && Directory.Exists(f) ? f : AppPaths.DefaultPdfFolder;
        Directory.CreateDirectory(folder);
        using var dialog = new SaveFileDialog
        {
            Title = "Save quotation PDF",
            Filter = "PDF files (*.pdf)|*.pdf",
            DefaultExt = "pdf",
            AddExtension = true,
            OverwritePrompt = true,
            InitialDirectory = folder,
            FileName = SafeFileName($"{q.Title}-{q.RefNo}.pdf"),
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _status.Text = "Saving PDF…";
        UseWaitCursor = true;
        bool saved;
        try
        {
            saved = await engine.PrintAsync(dialog.FileName);
        }
        catch (Exception ex)
        {
            saved = false;
            _status.Text = "PDF not saved: " + ex.Message;
        }
        finally
        {
            UseWaitCursor = false;
        }

        if (!saved)
        {
            MessageBox.Show(this, $"Could not save {dialog.FileName}.\n\nIf the PDF is open in another program, close it and try again.",
                "Formater", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _status.Text = "Saved " + dialog.FileName;
        _settings.PdfFolder = Path.GetDirectoryName(dialog.FileName);
        TrySaveSettings();
        if (_settings.OpenPdfAfterExport)
        {
            try
            {
                Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // No PDF viewer installed; the file is saved anyway.
            }
        }
    }

    private void EditSettings()
    {
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog(this) != DialogResult.OK) return;
        _settings = form.Result;
        TrySaveSettings();
        _ = RefreshAsync();
    }

    private void TrySaveSettings()
    {
        try
        {
            _settings.Save(AppPaths.SettingsFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status.Text = "Settings not saved: " + ex.Message;
        }
    }

    private void SaveDraft()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataFolder);
            File.WriteAllText(AppPaths.DraftFile, _input.Text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The draft is a convenience only.
        }
    }

    internal static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
        var clean = new string(name.Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim();
        return clean.Length > 0 ? clean : "Quotation.pdf";
    }
}
