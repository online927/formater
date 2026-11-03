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
