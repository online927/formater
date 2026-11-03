using Formater.Core;

public class CalculatorTests
{
    [Fact]
    public void Sample_data_gives_the_expected_totals()
    {
        var q = SpecJson.Parse(File.ReadAllText("sample_data.json"));
        var m = Calculator.ToRenderModel(q);

        Assert.Equal("60,055.00", m.Items[0].Amount);
        Assert.Equal("5.00", m.Items[0].Qty);
        Assert.Equal("12,011.00", m.Items[0].Rate);
        Assert.Equal(["Sub Total", "IGST18 (18%)", "Total"], m.Totals.Select(t => t.Label));
        Assert.Equal(["60,055.00", "10,809.90", "₹70,864.90"], m.Totals.Select(t => t.Value));
        Assert.True(m.Totals[^1].Bold);
        Assert.False(m.ShowDiscount);
        Assert.Equal("Ref No. 100186", m.RefLine);
        Assert.Equal("Other Ref: R", m.OtherRefLine);
        Assert.Equal("Quotation/Proforma", m.Title);
        Assert.Equal("Buyer/Consignee", m.Buyer.Label);
        Assert.Equal("", m.Terms);
        Assert.Equal(["Regards,", "Name,", "Tsaifuddin and Company"], m.SignOff);
        Assert.Equal(["No.72 N.R.Road", "Bangalore", "Karnataka 560002", "+917019740620", "+919739394657",
                      "GSTIN 29AADFT8885H1ZF"], m.Seller.Lines);
    }

    [Fact]
    public void Discount_reduces_the_amount_and_shows_the_column()
    {
        var q = new Quotation
        {
            Items = [new LineItem { Name = "Gauge", Qty = 5, Rate = 12011, DiscountPercent = 10, Unit = "Nos" }],
            Taxes = Gst.Taxes(false, 18),
        };
        var m = Calculator.ToRenderModel(q);

        Assert.True(m.ShowDiscount);
        Assert.Equal("10%", m.Items[0].Discount);
        Assert.Equal("12,011.00", m.Items[0].Rate);
        Assert.Equal("54,049.50", m.Items[0].Amount);
        Assert.Equal(["54,049.50", "9,728.91", "₹63,778.41"], m.Totals.Select(t => t.Value));
    }

    [Fact]
    public void Cgst_and_sgst_are_each_rounded_half_up()
    {
        var q = new Quotation
        {
            Items = [new LineItem { Name = "A", Qty = 1, Rate = 100.05m }],
            Taxes = Gst.Taxes(true, 18),
        };
        var t = Calculator.Compute(q);
        Assert.Equal(9.00m, t.TaxAmounts[0]);   // 9.0045 → 9.00
        Assert.Equal(9.00m, t.TaxAmounts[1]);
        Assert.Equal(118.05m, t.Total);
    }

    [Fact]
    public void Crore_amounts_use_indian_grouping()
    {
        var q = new Quotation { Items = [new LineItem { Name = "Machine", Qty = 3, Rate = 4567890.25m }] };
        var m = Calculator.ToRenderModel(q);
        Assert.Equal("1,37,03,670.75", m.Items[0].Amount);
        Assert.Equal("₹1,37,03,670.75", m.Totals[^1].Value);
    }

    [Fact]
    public void Empty_other_ref_hides_the_line()
    {
        var m = Calculator.ToRenderModel(new Quotation { RefNo = "1" });
        Assert.Equal("", m.OtherRefLine);
    }
}
