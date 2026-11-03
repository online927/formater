namespace Formater.Core;

public sealed record Totals(
    IReadOnlyList<decimal> Amounts,
    decimal SubTotal,
    IReadOnlyList<decimal> TaxAmounts,
    decimal Total);

/// <summary>Quotation maths in decimal (SPEC §7). Never uses binary floating point.</summary>
public static class Calculator
{
    public static decimal Amount(LineItem item) =>
        Money.Round2(item.Qty * item.Rate * (100m - item.DiscountPercent) / 100m);

    public static Totals Compute(Quotation q)
    {
        var amounts = q.Items.Select(Amount).ToList();
        var sub = amounts.Sum();
        var taxes = q.Taxes.Select(t => Money.Round2(sub * t.Percent / 100m)).ToList();
        return new Totals(amounts, sub, taxes, sub + taxes.Sum());
    }

    /// <summary>Formats everything the page layout needs. The layout itself does no maths.</summary>
    public static RenderModel ToRenderModel(Quotation q)
    {
        var totals = Compute(q);
        var showDiscount = q.Items.Any(i => i.DiscountPercent != 0);

        var model = new RenderModel
        {
            Title = q.Title,
            RefLine = $"Ref No. {q.RefNo}".TrimEnd(),
            OtherRefLine = string.IsNullOrWhiteSpace(q.OtherRef) ? "" : $"Other Ref: {q.OtherRef.Trim()}",
            Date = q.Date,
            Terms = q.Terms,
            Seller = new RenderParty { Label = q.Seller.Label, Name = q.Seller.Name, Lines = [.. q.Seller.Lines] },
            Buyer = new RenderParty { Label = q.Buyer.Label, Name = q.Buyer.Name, Lines = [.. q.Buyer.Lines] },
            ShowDiscount = showDiscount,
            TermsAndConditions = [.. q.TermsAndConditions],
        };

        for (var i = 0; i < q.Items.Count; i++)
        {
            var item = q.Items[i];
            model.Items.Add(new RenderItem
            {
                N = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                Name = item.Name,
                Desc = [.. item.Desc],
                Qty = Money.Qty(item.Qty),
                Unit = item.Unit,
                Rate = Money.Format(item.Rate),
                Discount = Money.Percent(item.DiscountPercent) + "%",
                Amount = Money.Format(totals.Amounts[i]),
            });
        }

        model.Totals.Add(new RenderTotal { Label = "Sub Total", Value = Money.Format(totals.SubTotal) });
        for (var i = 0; i < q.Taxes.Count; i++)
            model.Totals.Add(new RenderTotal { Label = q.Taxes[i].Label, Value = Money.Format(totals.TaxAmounts[i]) });
        model.Totals.Add(new RenderTotal { Label = "Total", Value = "₹" + Money.Format(totals.Total), Bold = true });

        return model;
    }
}
