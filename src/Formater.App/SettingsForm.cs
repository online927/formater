using System.Globalization;
using System.Text.Json;
using Formater.Core;

namespace Formater.App;

/// <summary>Settings in tabs: Company, Document, Numbering, GST, Bottom of page, Saving.</summary>
internal sealed class SettingsForm : Form
{
    private static readonly (GstMode Mode, string Text)[] GstModes =
    [
        (GstMode.Auto, "Automatic (from the buyer's GSTIN or address)"),
        (GstMode.Igst, "Always IGST"),
        (GstMode.CgstSgst, "Always CGST + SGST"),
        (GstMode.None, "No GST"),
    ];

    private static readonly (DiscountColumn Mode, string Text)[] DiscountModes =
    [
        (DiscountColumn.Auto, "Only when an item has a discount"),
        (DiscountColumn.Always, "Always"),
        (DiscountColumn.Never, "Never (Rate shows the rate after discount)"),
    ];

    private readonly QuotationRegister _register;
    private readonly AppSettings _original;
    private readonly int _fy = Numbering.FyStart(DateOnly.FromDateTime(DateTime.Today));
    private int _loadedNextNumber;

    // Company
    private readonly TextBox _sellerName = Single();
    private readonly TextBox _sellerLines = Multi(6);
    private readonly TextBox _homeState = Single();

    // Document
    private readonly TextBox _title = Single();
    private readonly TextBox _buyerLabel = Single();
    private readonly ComboBox _dateFormat = new() { Width = 200 };
    private readonly Label _datePreview = Hint("");
    private readonly ComboBox _discount = DropDown(DiscountModes.Select(d => d.Text));
    private readonly TextBox _unit = Single(120);
    private readonly TextBox _units = Multi(4);

    // Numbering
    private readonly TextBox _refPattern = Single(200);
    private readonly NumericUpDown _nextNumber = new() { Minimum = 1, Maximum = 9_999_999, Width = 120 };
    private readonly Label _refPreview = new() { AutoSize = true, Font = new Font(SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont, FontStyle.Bold) };
    private readonly Label _lastUsed = Hint("");

    // GST
    private readonly ComboBox _gstMode = DropDown(GstModes.Select(g => g.Text));
    private readonly NumericUpDown _gst = new() { DecimalPlaces = 2, Maximum = 100, Increment = 0.5m, Width = 90 };

    // Bottom of page
    private readonly TextBox _terms = Single();
    private readonly TextBox _tc = Multi(4);
    private readonly TextBox _signOff = Multi(3);

    // Saving
    private readonly TextBox _archive = Single(380);
    private readonly TextBox _fileName = Single(260);
    private readonly Label _fileNamePreview = Hint("");
    private readonly CheckBox _askCopy = new() { Text = "After saving, also ask where to save a copy", AutoSize = true };
    private readonly CheckBox _open = new() { Text = "Open the PDF after saving it", AutoSize = true };

    public AppSettings Result { get; private set; }

    public SettingsForm(AppSettings settings, QuotationRegister register)
    {
        _original = settings;
        _register = register;
        Result = settings;
        Text = "Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(760, 600);
        Padding = new Padding(10);

        _dateFormat.Items.AddRange([.. AppSettings.DateFormats]);

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(14, 6) };
        tabs.TabPages.Add(Page("Company",
            ("Company name", _sellerName, null),
            ("Address lines", _sellerLines, "One per line, as printed at the top left. Put your GSTIN on the last line."),
            ("Your state", _homeState, "Buyers in this state get CGST + SGST when they have no GSTIN.")));
        tabs.TabPages.Add(Page("Document",
            ("Title", _title, "Top right of the page, e.g. Quotation/Proforma."),
            ("Buyer heading", _buyerLabel, null),
            ("Date format", Stack(_dateFormat, _datePreview), null),
            ("Disc % column", _discount, null),
            ("Default unit", _unit, "Used when an item line has no unit."),
            ("Units", _units, "Words recognised as a unit after the rate, one per line or comma separated.")));
        tabs.TabPages.Add(Page("Numbering",
            ("Ref No. format", _refPattern,
                "{FY} = 2627,  {FY-} = 2026-27,  {YYYY} = 2026,  {N:5} = 00001 (5 digits),  {N} = 1.\n" +
                "The number restarts at 1 every financial year (1 April)."),
            ($"Next number ({Numbering.FyLabel(_fy)})", Stack(_nextNumber, _lastUsed),
                "Can be moved forward, never back to a number already used."),
            ("Next Ref No.", _refPreview, null)));
        tabs.TabPages.Add(Page("GST",
            ("GST", _gstMode, "A  GST:  line in the text overrides this for one quotation."),
            ("GST rate %", _gst, null)));
        tabs.TabPages.Add(Page("Bottom of page",
            ("Terms", _terms, "Shown as  Terms :  under the date; leave empty to hide it."),
            ("Terms & Conditions", _tc, "One per line, numbered as you want them."),
            ("Sign-off", _signOff, "Printed under the terms, one per line, e.g. Regards, / your name / Tsaifuddin and Company.")));
        var browse = new Button { Text = "Browse…", AutoSize = true };
        browse.Click += (_, _) => BrowseArchive();
        var openFolder = new Button { Text = "Open folder", AutoSize = true };
        openFolder.Click += (_, _) => OpenArchiveFolder();
        tabs.TabPages.Add(Page("Saving",
            ("PDF folder", Stack(Row(_archive, browse, openFolder),
                Hint("Every exported PDF is kept here, in one folder per financial year (e.g. 2026-27).")), null),
            ("File name", Stack(_fileName, _fileNamePreview), "{Ref}, {Company}, {Title}, {Date}. / and other characters Windows does not allow become -."),
            ("", Stack(_askCopy, _open), null)));

        var save = new Button { Text = "Save", AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var reset = new Button { Text = "Reset to defaults", AutoSize = true };
        var export = new Button { Text = "Export settings…", AutoSize = true };
        var import = new Button { Text = "Import settings…", AutoSize = true };
        save.Click += (_, _) => OnSave();
        reset.Click += (_, _) =>
        {
            if (MessageBox.Show(this, "Put every setting back to its default? The Ref No. register is not changed.",
                    "Settings", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
                Fill(new AppSettings { PdfFolder = _original.PdfFolder });
        };
        export.Click += (_, _) => ExportSettings();
        import.Click += (_, _) => ImportSettings();
        AcceptButton = save;
        CancelButton = cancel;

        var buttons = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 2, Padding = new Padding(0, 10, 0, 0) };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var leftButtons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        leftButtons.Controls.AddRange([export, import, reset]);
        var rightButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        rightButtons.Controls.AddRange([save, cancel]);
        buttons.Controls.Add(leftButtons, 0, 0);
        buttons.Controls.Add(rightButtons, 1, 0);

        Controls.Add(tabs);
        Controls.Add(buttons);

        _dateFormat.TextChanged += (_, _) => UpdatePreviews();
        _refPattern.TextChanged += (_, _) => UpdatePreviews();
        _nextNumber.ValueChanged += (_, _) => UpdatePreviews();
        _fileName.TextChanged += (_, _) => UpdatePreviews();
        _title.TextChanged += (_, _) => UpdatePreviews();

        Fill(settings);
        LoadNumbering();
    }

    // ---------- layout helpers ----------

    private static TextBox Single(int width = 460) => new() { Width = width };

    private static TextBox Multi(int lines) => new()
    {
        Multiline = true,
        AcceptsReturn = true,
        ScrollBars = ScrollBars.Vertical,
        Width = 460,
        Height = lines * 20 + 8,
    };

    private static ComboBox DropDown(IEnumerable<string> items)
    {
        var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
        c.Items.AddRange([.. items]);
        return c;
    }

    private static Label Hint(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = SystemColors.GrayText,
        MaximumSize = new Size(470, 0),
        Margin = new Padding(3, 2, 3, 4),
    };

    private static FlowLayoutPanel Stack(params Control[] controls)
    {
        var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        p.Controls.AddRange(controls);
        return p;
    }

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var p = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        p.Controls.AddRange(controls);
        return p;
    }

    private static TabPage Page(string title, params (string Label, Control Control, string? Hint)[] rows)
    {
        var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(10) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        foreach (var (label, control, hint) in rows)
        {
            var r = grid.RowCount++;
            grid.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 7, 10, 0) }, 0, r);
            grid.Controls.Add(hint is null ? control : Stack(control, Hint(hint)), 1, r);
        }
        var page = new TabPage(title) { AutoScroll = true, UseVisualStyleBackColor = true };
        page.Controls.Add(grid);
        return page;
    }

    // ---------- values ----------

    private void Fill(AppSettings s)
    {
        _sellerName.Text = s.SellerName;
        _sellerLines.Text = string.Join("\r\n", s.SellerLines);
        _homeState.Text = s.HomeState;

        _title.Text = s.Title;
        _buyerLabel.Text = s.BuyerLabel;
        _dateFormat.Text = s.DateFormat;
        _discount.SelectedIndex = Math.Max(0, Array.FindIndex(DiscountModes, d => d.Mode == s.DiscountColumn));
        _unit.Text = s.DefaultUnit;
        _units.Text = string.Join(", ", s.Units);

        _refPattern.Text = s.RefPattern;

        _gstMode.SelectedIndex = Math.Max(0, Array.FindIndex(GstModes, g => g.Mode == s.DefaultGstMode));
        _gst.Value = Math.Clamp(s.GstPercent, 0, 100);

        _terms.Text = s.DefaultTerms;
        _tc.Text = string.Join("\r\n", s.TermsAndConditions);
        _signOff.Text = string.Join("\r\n", s.SignOff);

        _archive.Text = s.ArchiveFolder ?? "";
        _archive.PlaceholderText = AppPaths.DefaultArchiveFolder;
        _fileName.Text = s.FileNamePattern;
        _askCopy.Checked = s.AskForCopyLocation;
        _open.Checked = s.OpenPdfAfterExport;
        UpdatePreviews();
    }

    private AppSettings Collect() => new()
    {
        SellerName = _sellerName.Text.Trim(),
        SellerLines = Lines(_sellerLines.Text),
        HomeState = _homeState.Text.Trim(),
        Title = _title.Text.Trim(),
        BuyerLabel = _buyerLabel.Text.Trim(),
        DateFormat = _dateFormat.Text.Trim(),
        DiscountColumn = DiscountModes[Math.Max(0, _discount.SelectedIndex)].Mode,
        DefaultUnit = _unit.Text.Trim(),
        Units = _units.Text.Split([',', '\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
        RefPattern = _refPattern.Text.Trim(),
        DefaultGstMode = GstModes[Math.Max(0, _gstMode.SelectedIndex)].Mode,
        GstPercent = _gst.Value,
        DefaultTerms = _terms.Text.Trim(),
        TermsAndConditions = Lines(_tc.Text),
        SignOff = Lines(_signOff.Text),
        ArchiveFolder = string.IsNullOrWhiteSpace(_archive.Text) ? null : _archive.Text.Trim(),
        FileNamePattern = string.IsNullOrWhiteSpace(_fileName.Text) ? FileNames.DefaultPattern : _fileName.Text.Trim(),
        AskForCopyLocation = _askCopy.Checked,
        OpenPdfAfterExport = _open.Checked,
        PdfFolder = _original.PdfFolder,
    };

    private static List<string> Lines(string text) =>
        text.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

    private void LoadNumbering()
    {
        try
        {
            _loadedNextNumber = _register.NextNumber(_fy);
            _nextNumber.Value = Math.Clamp(_loadedNextNumber, (int)_nextNumber.Minimum, (int)_nextNumber.Maximum);
            var last = _register.LastUsedNumber(_fy);
            _lastUsed.Text = last == 0
                ? $"No number used yet in {Numbering.FyLabel(_fy)}."
                : $"Last used: {Numbering.Format(_original.RefPattern, _fy, last)}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _nextNumber.Enabled = false;
            _lastUsed.Text = "The number register cannot be read: " + ex.Message;
        }
        UpdatePreviews();
    }

    private void UpdatePreviews()
    {
        var pattern = _refPattern.Text.Trim();
        var problem = Numbering.Validate(pattern);
        var nextRef = problem ?? Numbering.Format(pattern, _fy, (int)_nextNumber.Value);
        _refPreview.Text = nextRef;
        _refPreview.ForeColor = problem is null ? SystemColors.ControlText : Color.Firebrick;

        try
        {
            _datePreview.Text = "Today: " + DateTime.Today.ToString(_dateFormat.Text.Trim(), CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            _datePreview.Text = "Not a valid date format";
        }

        var sample = new Quotation { RefNo = problem is null ? nextRef : "T/2627/00001", Title = _title.Text, Buyer = new Party { Name = "BuyersHub" }, Date = "03 Nov 2026" };
        _fileNamePreview.Text = "Example: " + FileNames.Make(_fileName.Text, sample);
    }

    // ---------- actions ----------

    private void OnSave()
    {
        var s = Collect();
        if (Numbering.Validate(s.RefPattern) is { } problem)
        {
            MessageBox.Show(this, problem, "Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            _ = DateTime.Today.ToString(s.DateFormat, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            MessageBox.Show(this, $"\"{s.DateFormat}\" is not a valid date format.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_nextNumber.Enabled && (int)_nextNumber.Value != _loadedNextNumber)
        {
            try
            {
                _register.SetNextNumber(_fy, (int)_nextNumber.Value, s.RefPattern);
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(this, ex.Message, "Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        Result = s;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void BrowseArchive()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Folder where every exported PDF is kept",
            UseDescriptionForTitle = true,
            InitialDirectory = string.IsNullOrWhiteSpace(_archive.Text) ? AppPaths.DefaultArchiveFolder : _archive.Text,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) _archive.Text = dialog.SelectedPath;
    }

    private void OpenArchiveFolder()
    {
        var folder = string.IsNullOrWhiteSpace(_archive.Text) ? AppPaths.DefaultArchiveFolder : _archive.Text.Trim();
        try
        {
            Directory.CreateDirectory(folder);
            MainForm.OpenFile(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(this, ex.Message, "Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ExportSettings()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Export settings",
            Filter = "Settings (*.json)|*.json",
            FileName = "Formater-settings.json",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            Collect().Save(dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Export settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ImportSettings()
    {
        using var dialog = new OpenFileDialog { Title = "Import settings", Filter = "Settings (*.json)|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var s = AppSettings.FromJson(File.ReadAllText(dialog.FileName));
            s.PdfFolder = _original.PdfFolder;
            Fill(s);
            MessageBox.Show(this, "Settings loaded. Click Save to keep them. (The Ref No. register is not changed.)",
                "Import settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            MessageBox.Show(this, "That file could not be read: " + ex.Message, "Import settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
