using Formater.Core;

namespace Formater.App;

/// <summary>The details that are the same on every quotation.</summary>
internal sealed class SettingsForm : Form
{
    private readonly TextBox _title = new();
    private readonly TextBox _sellerName = new();
    private readonly TextBox _sellerLines = Multi(6);
    private readonly TextBox _homeState = new();
    private readonly NumericUpDown _gst = new() { DecimalPlaces = 2, Maximum = 100, Increment = 0.5m, Width = 90 };
    private readonly TextBox _buyerLabel = new();
    private readonly TextBox _unit = new();
    private readonly TextBox _terms = new();
    private readonly TextBox _tc = Multi(4);
    private readonly TextBox _signOff = Multi(3);
    private readonly CheckBox _open = new() { Text = "Open the PDF after saving it", AutoSize = true };
    private readonly AppSettings _original;

    public AppSettings Result { get; private set; }

    public SettingsForm(AppSettings settings)
    {
        _original = settings;
        Result = settings;
        Text = "Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 460));

        Row(grid, "Document title", _title, "Shown at the top right, e.g. Quotation or Proforma Invoice");
        Row(grid, "Your company name", _sellerName, null);
        Row(grid, "Your address lines", _sellerLines, "One per line; put your GSTIN on the last line");
        Row(grid, "Your state", _homeState, "Buyers in this state get CGST + SGST when they have no GSTIN");
        Row(grid, "GST rate %", _gst, "Used unless the text has a line like  GST: 12%");
        Row(grid, "Buyer heading", _buyerLabel, null);
        Row(grid, "Default unit", _unit, "Used when an item line has no unit");
        Row(grid, "Default Terms", _terms, "Shown as  Terms :  under the date; leave empty to hide it (as in the reference)");
        Row(grid, "Terms & Conditions", _tc, "One per line, numbered as you want them; used unless the text has T&C lines");
        Row(grid, "Sign-off", _signOff, "Printed under the terms, one per line, e.g. Regards, / your name / Tsaifuddin and Company");
        grid.Controls.Add(new Label(), 0, grid.RowCount);
        grid.Controls.Add(_open, 1, grid.RowCount++);

        var ok = new Button { Text = "Save", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var reset = new Button { Text = "Reset to defaults", AutoSize = true };
        reset.Click += (_, _) => Fill(new AppSettings { PdfFolder = _original.PdfFolder });
        ok.Click += (_, _) => Result = Collect();
        AcceptButton = ok;
        CancelButton = cancel;

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Bottom, Padding = new Padding(0, 10, 0, 0) };
        buttons.Controls.AddRange([cancel, ok, reset]);

        Controls.Add(grid);
        Controls.Add(buttons);
        Fill(settings);
    }

    private static TextBox Multi(int lines) => new()
    {
        Multiline = true,
        AcceptsReturn = true,
        ScrollBars = ScrollBars.Vertical,
        Height = lines * 20 + 8,
    };

    private static void Row(TableLayoutPanel grid, string label, Control control, string? hint)
    {
        var row = grid.RowCount++;
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, 6, 12, 0) }, 0, row);
        if (control is TextBox) control.Width = 450;
        if (hint is null)
        {
            grid.Controls.Add(control, 1, row);
            return;
        }
        var panel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        panel.Controls.Add(control);
        panel.Controls.Add(new Label { Text = hint, AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(3, 0, 3, 6) });
        grid.Controls.Add(panel, 1, row);
    }

    private void Fill(AppSettings s)
    {
        _title.Text = s.Title;
        _sellerName.Text = s.SellerName;
        _sellerLines.Text = string.Join("\r\n", s.SellerLines);
        _homeState.Text = s.HomeState;
        _gst.Value = Math.Clamp(s.GstPercent, 0, 100);
        _buyerLabel.Text = s.BuyerLabel;
        _unit.Text = s.DefaultUnit;
        _terms.Text = s.DefaultTerms;
        _tc.Text = string.Join("\r\n", s.TermsAndConditions);
        _signOff.Text = string.Join("\r\n", s.SignOff);
        _open.Checked = s.OpenPdfAfterExport;
    }

    private AppSettings Collect() => new()
    {
        Title = _title.Text.Trim(),
        SellerName = _sellerName.Text.Trim(),
        SellerLines = Lines(_sellerLines.Text),
        HomeState = _homeState.Text.Trim(),
        GstPercent = _gst.Value,
        BuyerLabel = _buyerLabel.Text.Trim(),
        DefaultUnit = _unit.Text.Trim(),
        DefaultTerms = _terms.Text.Trim(),
        TermsAndConditions = Lines(_tc.Text),
        SignOff = Lines(_signOff.Text),
        OpenPdfAfterExport = _open.Checked,
        PdfFolder = _original.PdfFolder,
    };

    private static List<string> Lines(string text) =>
        text.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
}
