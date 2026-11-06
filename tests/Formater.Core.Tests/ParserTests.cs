using Formater.Core;

public class ParserTests
{
    private static readonly DateOnly Today = new(2026, 11, 3);
    private static ParseResult Parse(string text) => QuotationTextParser.Parse(text, new AppSettings(), Today);

    [Fact]
    public void Minimal_text_company_ref_and_item()
    {
        var r = Parse("""
            Company: BuyersHub
            Ref: 100186
            Vernier Hook Depth Gauge, 10%, 5, 12011
            """);

        Assert.True(r.IsValid, string.Join("\n", r.Errors));
        var q = r.Quotation;
        Assert.Equal("BuyersHub", q.Buyer.Name);
        Assert.Equal("Buyer/Consignee", q.Buyer.Label);
        Assert.Empty(q.Buyer.Lines);
        Assert.Equal("100186", q.RefNo);
        Assert.Equal("03 Nov 2026", q.Date);
        Assert.Equal("", q.Terms);
        Assert.Equal("Quotation/Proforma", q.Title);
        Assert.Equal(["Regards,", "Name,", "Tsaifuddin and Company"], q.SignOff);
        Assert.Equal("T.SAIFUDDIN & CO", q.Seller.Name);
        Assert.Equal(2, q.TermsAndConditions.Count);

        var item = Assert.Single(q.Items);
        Assert.Equal("Vernier Hook Depth Gauge", item.Name);
        Assert.Equal(10m, item.DiscountPercent);
        Assert.Equal(5m, item.Qty);
        Assert.Equal(12011m, item.Rate);
        Assert.Equal("Nos", item.Unit);
        Assert.Empty(item.Desc);

        // No GSTIN or address: IGST.
        Assert.Equal("IGST18 (18%)", Assert.Single(q.Taxes).Label);
    }

    [Fact]
    public void Full_text_reproduces_the_reference_buyer_block()
    {
        var r = Parse("""
            Company: BuyersHub
            Address: Plot 27 , PRT Colony , Karmanghat
            Address: Hyderabad Telangana 500079
            Address: India
            GSTIN: 36AAZFB2304B1ZQ
            Phone: +919182706754
            Email: sales@buyershub.org.in
            Ref: 100186
            Other Ref: R
            Date: 02 Nov 2026

            Vernier Hook Depth Gauge, 0%, 5, 12011
              Make - Insize
              Model : 1234-2001
            """);

        Assert.True(r.IsValid, string.Join("\n", r.Errors));
        var q = r.Quotation;
        Assert.Equal(
            ["Plot 27 , PRT Colony , Karmanghat", "Hyderabad Telangana 500079", "India",
             "GSTIN 36AAZFB2304B1ZQ", "+919182706754", "sales@buyershub.org.in"],
            q.Buyer.Lines);
        Assert.Equal("R", q.OtherRef);
        Assert.Equal("02 Nov 2026", q.Date);
        Assert.Equal(["Make - Insize", "Model : 1234-2001"], q.Items[0].Desc);
        Assert.Equal("IGST18 (18%)", Assert.Single(q.Taxes).Label);

        var m = Calculator.ToRenderModel(q);
        Assert.False(m.ShowDiscount);
        Assert.Equal("₹70,864.90", m.Totals[^1].Value);
    }

    [Fact]
    public void Karnataka_gstin_gives_cgst_and_sgst()
    {
        var r = Parse("Company: Local Traders\nGSTIN: 29ABCDE1234F1Z5\nRef: 7\nBolt, 0, 10, 5");
        Assert.Equal(["CGST9 (9%)", "SGST9 (9%)"], r.Quotation.Taxes.Select(t => t.Label));
        Assert.Contains("automatic", r.GstNote);
    }

    [Fact]
    public void Without_gstin_a_karnataka_address_gives_cgst_and_sgst()
    {
        var r = Parse("Company: Local Traders\nAddress: Mysuru, Karnataka 570001\nRef: 7\nBolt, 0, 10, 5");
        Assert.Equal(["CGST9 (9%)", "SGST9 (9%)"], r.Quotation.Taxes.Select(t => t.Label));
    }

    [Fact]
    public void Gstin_wins_over_the_address()
    {
        var r = Parse("Company: X\nAddress: Branch office, Karnataka\nGSTIN 36AAZFB2304B1ZQ\nRef: 7\nBolt, 0, 10, 5");
        Assert.Equal("IGST18 (18%)", Assert.Single(r.Quotation.Taxes).Label);
    }

    [Theory]
    [InlineData("GST: None", new string[0])]
    [InlineData("GST: 0%", new string[0])]
    [InlineData("GST: IGST", new[] { "IGST18 (18%)" })]
    [InlineData("GST: CGST+SGST", new[] { "CGST9 (9%)", "SGST9 (9%)" })]
    [InlineData("GST: 12%", new[] { "IGST12 (12%)" })]
    [InlineData("GST: CGST SGST 5%", new[] { "CGST2.5 (2.5%)", "SGST2.5 (2.5%)" })]
    public void Gst_line_overrides_the_automatic_choice(string gstLine, string[] labels)
    {
        var r = Parse($"Company: X\nRef: 1\n{gstLine}\nBolt, 0, 10, 5");
        Assert.Equal(labels, r.Quotation.Taxes.Select(t => t.Label));
    }

    [Fact]
    public void Unlabelled_header_lines_become_company_and_address()
    {
        var r = Parse("""
            BuyersHub
            Plot 27 , PRT Colony , Karmanghat
            Hyderabad Telangana 500079
            36AAZFB2304B1ZQ
            sales@buyershub.org.in
            +91 91827 06754
            Ref No. 100186
            Gauge, 5%, 2, 100
            """);
        Assert.True(r.IsValid, string.Join("\n", r.Errors));
        var q = r.Quotation;
        Assert.Equal("BuyersHub", q.Buyer.Name);
        Assert.Equal("100186", q.RefNo);
        Assert.Equal(
            ["Plot 27 , PRT Colony , Karmanghat", "Hyderabad Telangana 500079",
             "GSTIN 36AAZFB2304B1ZQ", "+91 91827 06754", "sales@buyershub.org.in"],
            q.Buyer.Lines);
    }

    [Theory]
    [InlineData("Gauge, 10%, 5, 12,011", 10, 5, 12011)]
    [InlineData("Gauge, 10, 5, 1,23,456.50", 10, 5, 123456.50)]
    [InlineData("Gauge, 0%, 2, 1,500", 0, 2, 1500)]
    [InlineData("Gauge, , 5, 12011", 0, 5, 12011)]
    [InlineData("Gauge, -, 5, 12011", 0, 5, 12011)]
    [InlineData("Gauge, 12.5%, 0.5, Rs. 99.90", 12.5, 0.5, 99.90)]
    [InlineData("Gauge, disc 5%, 3, ₹250/-", 5, 3, 250)]
    public void Item_numbers(string line, double disc, double qty, double rate)
    {
        var r = Parse($"Company: X\nRef: 1\n{line}");
        Assert.True(r.IsValid, string.Join("\n", r.Errors));
        var item = Assert.Single(r.Quotation.Items);
        Assert.Equal("Gauge", item.Name);
        Assert.Equal((decimal)disc, item.DiscountPercent);
        Assert.Equal((decimal)qty, item.Qty);
        Assert.Equal((decimal)rate, item.Rate);
    }

    [Fact]
    public void Item_name_may_contain_commas_and_numbering()
    {
        var r = Parse("Company: X\nRef: 1\n1. Digital Caliper, 0-150 mm, IP67, 10%, 2, 3450");
        var item = Assert.Single(r.Quotation.Items);
        Assert.Equal("Digital Caliper, 0-150 mm, IP67", item.Name);
        Assert.Equal(3450m, item.Rate);
    }

    [Fact]
    public void Unit_and_description_after_the_rate()
    {
        var r = Parse("Company: X\nRef: 1\nGauge, 10%, 5, 12011, Pcs, Make - Insize | Model : 1234-2001");
        var item = Assert.Single(r.Quotation.Items);
        Assert.Equal("Pcs", item.Unit);
        Assert.Equal(["Make - Insize", "Model : 1234-2001"], item.Desc);
    }

    [Fact]
    public void Unit_written_with_the_qty()
    {
        var item = Assert.Single(Parse("Company: X\nRef: 1\nTape, 0, 3 Rolls, 45").Quotation.Items);
        Assert.Equal(3m, item.Qty);
        Assert.Equal("Rolls", item.Unit);
    }

    [Fact]
    public void Description_lines_under_an_item()
    {
        var r = Parse("Company: X\nRef: 1\nGauge, 10%, 5, 12011\n- Make - Insize\nModel : 1234-2001\nScrew, 0, 100, 2");
        Assert.Equal(["Make - Insize", "Model : 1234-2001"], r.Quotation.Items[0].Desc);
        Assert.Empty(r.Quotation.Items[1].Desc);
    }

    [Fact]
    public void Tab_separated_spreadsheet_rows()
    {
        var r = Parse("Company: X\nRef: 1\n1\tVernier Gauge\t10%\t5\t12,011.00\n2\tScrew\t\t100\t2.50");
        Assert.True(r.IsValid, string.Join("\n", r.Errors));
        Assert.Equal(["Vernier Gauge", "Screw"], r.Quotation.Items.Select(i => i.Name));
        Assert.Equal(12011m, r.Quotation.Items[0].Rate);
        Assert.Equal(0m, r.Quotation.Items[1].DiscountPercent);
        Assert.Equal(2.5m, r.Quotation.Items[1].Rate);
    }

    [Fact]
    public void Terms_and_conditions_replace_the_defaults()
    {
        var r = Parse("""
            Company: X
            Ref: 1
            Terms: 30 days credit
            Gauge, 0, 1, 10

            Terms & Conditions:
            1. Delivery: 2 weeks
            2. Prices are ex-works
            """);
        Assert.Equal("30 days credit", r.Quotation.Terms);
        Assert.Equal(["1. Delivery: 2 weeks", "2. Prices are ex-works"], r.Quotation.TermsAndConditions);
        Assert.Single(r.Quotation.Items);
    }

    [Fact]
    public void Single_tc_lines_with_a_label()
    {
        var r = Parse("Company: X\nRef: 1\nGauge, 0, 1, 10\nT&C: Validity 15 days");
        Assert.Equal(["Validity 15 days"], r.Quotation.TermsAndConditions);
    }

    [Fact]
    public void Date_is_reformatted()
    {
        Assert.Equal("02 Nov 2026", Parse("Company: X\nRef: 1\nDate: 02/11/2026\nA, 0, 1, 1").Quotation.Date);
        Assert.Equal("02 Nov 2026", Parse("Company: X\nRef: 1\nDate: 2026-11-02\nA, 0, 1, 1").Quotation.Date);
    }

    [Fact]
    public void Missing_company_and_items_are_errors()
    {
        var r = Parse("");
        Assert.False(r.IsValid);
        Assert.Equal(2, r.Errors.Count); // company and items; the Ref No. is given by the app
        Assert.True(Parse("Company: X\nA, 0, 1, 1").IsValid);
    }

    [Fact]
    public void Broken_item_line_is_reported()
    {
        var r = Parse("Company: X\nRef: 1\nGauge, 10%, 5\nScrew, 0, 1, 2");
        var e = Assert.Single(r.Errors);
        Assert.Equal(3, e.Line);
        Assert.Single(r.Quotation.Items);
    }

    [Fact]
    public void Discount_over_100_is_an_error()
    {
        Assert.False(Parse("Company: X\nRef: 1\nGauge, 120%, 5, 10").IsValid);
    }

    [Fact]
    public void Address_lines_with_commas_are_not_items()
    {
        var r = Parse("Company: X\nNo. 12, 3rd Cross, Bangalore 560002\nRef: 1\nGauge, 0, 1, 10");
        Assert.Equal(["No. 12, 3rd Cross, Bangalore 560002"], r.Quotation.Buyer.Lines);
        Assert.Single(r.Quotation.Items);
    }

    [Fact]
    public void Settings_supply_seller_title_unit_and_gst_rate()
    {
        var s = new AppSettings { Title = "Proforma Invoice", DefaultUnit = "Pcs", GstPercent = 12, SellerName = "ME" };
        var r = QuotationTextParser.Parse("Company: X\nRef: 1\nA, 0, 1, 1", s, Today);
        Assert.Equal("Proforma Invoice", r.Quotation.Title);
        Assert.Equal("Pcs", r.Quotation.Items[0].Unit);
        Assert.Equal("ME", r.Quotation.Seller.Name);
        Assert.Equal("IGST12 (12%)", r.Quotation.Taxes[0].Label);
    }

    [Fact]
    public void Sign_off_lines_replace_the_defaults()
    {
        var r = Parse("Company: X\nRef: 1\nA, 0, 1, 1\n\nSign-off:\nRegards,\nAhmed Saifuddin\nTsaifuddin and Company");
        Assert.Equal(["Regards,", "Ahmed Saifuddin", "Tsaifuddin and Company"], r.Quotation.SignOff);
        Assert.Equal(2, r.Quotation.TermsAndConditions.Count);
        Assert.Empty(r.Quotation.Items[0].Desc);
    }

    [Fact]
    public void Empty_sections_hide_the_terms_and_the_sign_off()
    {
        var r = Parse("Company: X\nRef: 1\nTerms: Against Delivery\nA, 0, 1, 1\n\nTerms & Conditions:\n\nSign-off:\n");
        Assert.Equal("Against Delivery", r.Quotation.Terms);
        Assert.Empty(r.Quotation.TermsAndConditions);
        Assert.Empty(r.Quotation.SignOff);
    }
}
