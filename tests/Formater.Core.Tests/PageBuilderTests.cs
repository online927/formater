using Formater.Core;

public class PageBuilderTests
{
    [Fact]
    public void Page_embeds_fonts_logos_script_and_data()
    {
        var q = SpecJson.Parse(File.ReadAllText("sample_data.json"));
        var html = PageBuilder.Page(Calculator.ToRenderModel(q));

        Assert.Contains("data:font/ttf;base64,AAEAAA", html);
        Assert.Contains("viewBox=\"409 48 400 106\"", html);
        Assert.Contains("viewBox=\"44 37 212 194\"", html);
        Assert.Contains("window.formater", html);
        Assert.Contains("\"refLine\":\"Ref No. 100186\"", html);
        Assert.DoesNotContain("{{", html);
        Assert.True(html.Length < 1_900_000, "WebView2 NavigateToString accepts up to 2 MB");
    }

    [Fact]
    public void Data_cannot_close_the_script_tag()
    {
        var q = new Quotation { RefNo = "</script><b>x</b>" };
        var html = PageBuilder.Page(Calculator.ToRenderModel(q));
        Assert.DoesNotContain("</script><b>", html);
    }

    [Fact]
    public void Empty_page_has_no_data()
    {
        Assert.Contains("<script id=\"data\" type=\"application/json\"></script>", PageBuilder.EmptyPage());
    }
}
