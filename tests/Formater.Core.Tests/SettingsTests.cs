using Formater.Core;

public class SettingsTests
{
    [Fact]
    public void Defaults_match_the_reference()
    {
        var s = new AppSettings();
        var sample = SpecJson.Parse(File.ReadAllText("sample_data.json"));
        Assert.Equal(sample.Title, s.Title);
        Assert.Equal(sample.Seller.Name, s.SellerName);
        Assert.Equal(sample.Seller.Lines, s.SellerLines);
        Assert.Equal(sample.Buyer.Label, s.BuyerLabel);
        Assert.Equal(sample.Terms, s.DefaultTerms);
        Assert.Equal(sample.TermsAndConditions, s.TermsAndConditions);
        Assert.Equal(sample.SignOff, s.SignOff);
    }

    [Fact]
    public void Settings_saved_by_the_first_version_move_to_the_new_format()
    {
        const string old = """
            { "Title": "Quotation", "SellerName": "T.SAIFUDDIN & CO",
              "SellerLines": ["No.72", "N.R.Road", "Bangalore", "560002 Karnataka", "India", "GSTIN 29AADFT8885H1ZF"],
              "BuyerLabel": "Buyer", "DefaultTerms": "Against Delivery", "GstPercent": 12, "PdfFolder": "D:\\PDF" }
            """;
        var s = AppSettings.FromJson(old);
        Assert.Equal("Quotation/Proforma", s.Title);
        Assert.Equal("Buyer/Consignee", s.BuyerLabel);
        Assert.Equal("", s.DefaultTerms);
        Assert.Equal("Karnataka 560002", s.SellerLines[2]);
        Assert.Equal(["Regards,", "Name,", "Tsaifuddin and Company"], s.SignOff);
        Assert.Equal(12m, s.GstPercent);         // the user's own choices stay
        Assert.Equal("D:\\PDF", s.PdfFolder);
        Assert.Equal(AppSettings.CurrentFormatVersion, s.FormatVersion);
    }

    [Fact]
    public void Values_the_user_changed_are_kept()
    {
        var s = AppSettings.FromJson("""{ "Title": "Proforma Invoice", "BuyerLabel": "Customer", "DefaultTerms": "Advance" }""");
        Assert.Equal("Proforma Invoice", s.Title);
        Assert.Equal("Customer", s.BuyerLabel);
        Assert.Equal("Advance", s.DefaultTerms);
    }

    [Fact]
    public void Current_settings_are_read_as_saved()
    {
        var saved = new AppSettings { Title = "Quotation", SignOff = [] };
        var path = Path.Combine(Path.GetTempPath(), $"formater-{Guid.NewGuid():N}.json");
        try
        {
            saved.Save(path);
            var s = AppSettings.Load(path);
            Assert.Equal("Quotation", s.Title); // a current-format file is never migrated
            Assert.Empty(s.SignOff);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public class SettingsOptionTests
{
    private static readonly DateOnly Today = new(2026, 11, 3);

    [Fact]
    public void Date_format()
    {
        var s = new AppSettings { DateFormat = "dd/MM/yyyy" };
        Assert.Equal("03/11/2026", QuotationTextParser.Parse("Company: X\nA, 0, 1, 1", s, Today).Quotation.Date);
        Assert.Equal("02/11/2026", QuotationTextParser.Parse("Company: X\nDate: 2 Nov 2026\nA, 0, 1, 1", s, Today).Quotation.Date);
    }

    [Fact]
    public void Default_gst_mode()
    {
        var s = new AppSettings { DefaultGstMode = GstMode.None };
        var r = QuotationTextParser.Parse("Company: X\nA, 0, 1, 1", s, Today);
        Assert.Empty(r.Quotation.Taxes);
        Assert.Contains("Settings", r.GstNote);
        var igst = QuotationTextParser.Parse("Company: X\nGST: IGST\nA, 0, 1, 1", s, Today);
        Assert.Single(igst.Quotation.Taxes);
    }

    [Fact]
    public void Units_come_from_settings()
    {
        var s = new AppSettings { Units = ["Nos", "Tin"] };
        var item = QuotationTextParser.Parse("Company: X\nPaint, 0, 2, 100, Tin", s, Today).Quotation.Items[0];
        Assert.Equal("Tin", item.Unit);
        var other = QuotationTextParser.Parse("Company: X\nPaint, 0, 2, 100, Pcs", s, Today).Quotation.Items[0];
        Assert.Equal("Nos", other.Unit);
        Assert.Equal(["Pcs"], other.Desc);
    }

    [Fact]
    public void Discount_column_modes()
    {
        var q = new Quotation { Items = [new LineItem { Name = "A", Qty = 2, Rate = 100, DiscountPercent = 10 }, new LineItem { Name = "B", Qty = 1, Rate = 50 }] };
        var never = Calculator.ToRenderModel(q, DiscountColumn.Never);
        Assert.False(never.ShowDiscount);
        Assert.Equal("90.00", never.Items[0].Rate);   // rate after discount
        Assert.Equal("180.00", never.Items[0].Amount);
        Assert.True(Calculator.ToRenderModel(new Quotation { Items = [q.Items[1]] }, DiscountColumn.Always).ShowDiscount);
        Assert.Equal("100.00", Calculator.ToRenderModel(q).Items[0].Rate);
    }

    [Fact]
    public void Settings_round_trip_with_enums_as_words()
    {
        var path = Path.Combine(Path.GetTempPath(), $"formater-{Guid.NewGuid():N}.json");
        try
        {
            new AppSettings { DefaultGstMode = GstMode.CgstSgst, DiscountColumn = DiscountColumn.Never, RefPattern = "Q/{N:4}" }.Save(path);
            Assert.Contains("\"CgstSgst\"", File.ReadAllText(path));
            var s = AppSettings.Load(path);
            Assert.Equal(GstMode.CgstSgst, s.DefaultGstMode);
            Assert.Equal(DiscountColumn.Never, s.DiscountColumn);
            Assert.Equal("Q/{N:4}", s.RefPattern);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Ref_lines_are_removed_when_copying_text()
    {
        var text = "Company: X\r\nRef: T/2627/00001\r\nRef No. 55\r\nA, 0, 1, 1";
        Assert.Equal("Company: X\r\nA, 0, 1, 1", QuotationTextParser.RemoveRefLines(text));
    }
}
