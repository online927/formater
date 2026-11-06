using Formater.Core;

public class NumberingTests
{
    [Theory]
    [InlineData(2026, 11, 3, 2026)]
    [InlineData(2027, 3, 31, 2026)]
    [InlineData(2027, 4, 1, 2027)]
    [InlineData(2030, 1, 15, 2029)]
    public void Financial_year_starts_in_april(int y, int m, int d, int fy) =>
        Assert.Equal(fy, Numbering.FyStart(new DateOnly(y, m, d)));

    [Fact]
    public void Default_format_is_T_slash_fy_slash_five_digits()
    {
        Assert.Equal("T/2627/00001", Numbering.Format(Numbering.DefaultPattern, 2026, 1));
        Assert.Equal("T/2627/00152", Numbering.Format(Numbering.DefaultPattern, 2026, 152));
        Assert.Equal("T/2728/123456", Numbering.Format(Numbering.DefaultPattern, 2027, 123456));
        Assert.Equal("T/9900/00001", Numbering.Format(Numbering.DefaultPattern, 2099, 1));
    }

    [Fact]
    public void Other_tokens()
    {
        Assert.Equal("TSQ/2026-27/7", Numbering.Format("TSQ/{FY-}/{N}", 2026, 7));
        Assert.Equal("Q-2026-0042", Numbering.Format("Q-{YYYY}-{N:4}", 2026, 42));
    }

    [Theory]
    [InlineData("T/{FY}/{N:5}", true)]
    [InlineData("{N}", true)]
    [InlineData("T/{FY}", false)]
    [InlineData("T/{N}/{N}", false)]
    [InlineData("T/{YEAR}/{N}", false)]
    [InlineData("", false)]
    public void Validate(string pattern, bool ok) => Assert.Equal(ok, Numbering.Validate(pattern) is null);

    [Fact]
    public void File_names_replace_characters_windows_does_not_allow()
    {
        var q = new Quotation { RefNo = "T/2627/00001", Title = "Quotation/Proforma", Buyer = new Party { Name = "A:B Ltd" } };
        Assert.Equal("Quotation-T-2627-00001.pdf", FileNames.Make(FileNames.DefaultPattern, q));
        Assert.Equal("T-2627-00001 - A-B Ltd.pdf", FileNames.Make("{Ref} - {Company}", q));
        Assert.Equal("Quotation-Proforma T-2627-00001.pdf", FileNames.Make("{Title} {Ref}.pdf", q));
    }
}
