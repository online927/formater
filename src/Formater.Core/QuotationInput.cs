namespace Formater.Core;

/// <summary>One entry point for pasted text or SPEC JSON.</summary>
public static class QuotationInput
{
    public static ParseResult Read(string input, AppSettings settings, DateOnly today)
    {
        var trimmed = input.TrimStart();
        if (trimmed.StartsWith('{'))
        {
            try
            {
                return new ParseResult { Quotation = SpecJson.Parse(trimmed), GstNote = "Taxes from JSON" };
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or FormatException)
            {
                var r = new ParseResult { Quotation = new Quotation() };
                r.Errors.Add(new ParseMessage(0, "JSON could not be read: " + ex.Message));
                return r;
            }
        }
        return QuotationTextParser.Parse(input, settings, today);
    }
}
