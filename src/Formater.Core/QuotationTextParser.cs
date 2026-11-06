using System.Globalization;
using System.Text.RegularExpressions;

namespace Formater.Core;

public sealed record ParseMessage(int Line, string Text)
{
    public override string ToString() => Line > 0 ? $"Line {Line}: {Text}" : Text;
}

public sealed class ParseResult
{
    public required Quotation Quotation { get; init; }
    public List<ParseMessage> Errors { get; } = [];
    public List<ParseMessage> Warnings { get; } = [];

    /// <summary>How the GST was chosen, for display, e.g. "IGST 18% (buyer GSTIN state code 36 ≠ seller 29)".</summary>
    public string GstNote { get; set; } = "";

    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Reads pasted quotation text. Labelled lines (Company:, Ref:, Address:, GSTIN:, …) fill the header;
/// item lines are "item name, discount, qty, rate" with optional unit and description after the rate.
/// Indented or "-" lines under an item are its small description lines.
/// </summary>
public static partial class QuotationTextParser
{
    private enum Field
    {
        BuyerName, Address, Gstin, Phone, Email, Ref, OtherRef, Date, Terms, Gst, Tc, SignOff, Title, Items,
    }

    private static readonly Dictionary<string, Field> Labels = new(StringComparer.Ordinal)
    {
        ["company"] = Field.BuyerName, ["companyname"] = Field.BuyerName, ["customer"] = Field.BuyerName,
        ["customername"] = Field.BuyerName, ["buyer"] = Field.BuyerName, ["buyername"] = Field.BuyerName,
        ["party"] = Field.BuyerName, ["partyname"] = Field.BuyerName, ["client"] = Field.BuyerName,
        ["clientname"] = Field.BuyerName, ["to"] = Field.BuyerName, ["ms"] = Field.BuyerName,
        ["name"] = Field.BuyerName, ["firm"] = Field.BuyerName,

        ["address"] = Field.Address, ["addr"] = Field.Address, ["add"] = Field.Address,
        ["city"] = Field.Address, ["state"] = Field.Address, ["pin"] = Field.Address,
        ["pincode"] = Field.Address, ["country"] = Field.Address, ["area"] = Field.Address,

        ["gstin"] = Field.Gstin, ["gstno"] = Field.Gstin, ["gstinno"] = Field.Gstin, ["gstn"] = Field.Gstin,
        ["gstnumber"] = Field.Gstin, ["gstinuin"] = Field.Gstin, ["buyergstin"] = Field.Gstin,

        ["phone"] = Field.Phone, ["phoneno"] = Field.Phone, ["ph"] = Field.Phone, ["mobile"] = Field.Phone,
        ["mobileno"] = Field.Phone, ["mob"] = Field.Phone, ["tel"] = Field.Phone, ["telephone"] = Field.Phone,
        ["contact"] = Field.Phone, ["contactno"] = Field.Phone, ["cell"] = Field.Phone, ["whatsapp"] = Field.Phone,

        ["email"] = Field.Email, ["emailid"] = Field.Email, ["mail"] = Field.Email,

        ["ref"] = Field.Ref, ["refno"] = Field.Ref, ["reference"] = Field.Ref, ["referenceno"] = Field.Ref,
        ["refnumber"] = Field.Ref, ["quotationno"] = Field.Ref, ["quotationnumber"] = Field.Ref,
        ["quoteno"] = Field.Ref, ["qno"] = Field.Ref, ["qtnno"] = Field.Ref, ["quotationref"] = Field.Ref,

        ["otherref"] = Field.OtherRef, ["otherrefno"] = Field.OtherRef, ["otherreference"] = Field.OtherRef,
        ["yourref"] = Field.OtherRef, ["yourrefno"] = Field.OtherRef, ["enquiryref"] = Field.OtherRef,
        ["enqref"] = Field.OtherRef, ["enquiryno"] = Field.OtherRef, ["enqno"] = Field.OtherRef,

        ["date"] = Field.Date, ["quotationdate"] = Field.Date,
        ["terms"] = Field.Terms, ["paymentterms"] = Field.Terms,
        ["gst"] = Field.Gst, ["tax"] = Field.Gst, ["gsttype"] = Field.Gst, ["taxtype"] = Field.Gst,
        ["gstrate"] = Field.Gst,

        ["t&c"] = Field.Tc, ["tc"] = Field.Tc, ["terms&conditions"] = Field.Tc,
        ["termsandconditions"] = Field.Tc, ["termsconditions"] = Field.Tc, ["conditions"] = Field.Tc,
        ["condition"] = Field.Tc, ["note"] = Field.Tc, ["notes"] = Field.Tc, ["remark"] = Field.Tc,
        ["remarks"] = Field.Tc,

        ["signoff"] = Field.SignOff, ["signature"] = Field.SignOff, ["closing"] = Field.SignOff,

        ["title"] = Field.Title, ["document"] = Field.Title,
        ["items"] = Field.Items, ["item"] = Field.Items, ["products"] = Field.Items, ["particulars"] = Field.Items,
    };

    [GeneratedRegex(@"^(?<k>[A-Za-z][A-Za-z0-9 .&/_'-]{0,30}?)\s*[:=]\s*(?<v>.*)$")]
    private static partial Regex LabelRegex();

    [GeneratedRegex(@"^ref(?:erence)?\b\.?\s*(?:no\b\.?|number\b|#)?\s*[#.-]?\s*(?<v>[^,\t]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex RefNoColonRegex();

    [GeneratedRegex(@"^(?:gstin|gst\s*no\.?|gstn)\s*[#.-]?\s*(?<v>[0-9A-Za-z]{15})$", RegexOptions.IgnoreCase)]
    private static partial Regex GstinNoColonRegex();

    [GeneratedRegex(@"^[^\s@,]+@[^\s@,]+\.[^\s@,]+$")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^\+?\d[\d\s()-]{7,}\d$")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"^(?:disc(?:ount)?\.?\s*)?(?<v>\d+(?:\.\d+)?)\s*%?(?:\s*(?:disc(?:ount)?|off))?$", RegexOptions.IgnoreCase)]
    private static partial Regex DiscountRegex();

    [GeneratedRegex(@"^(?<v>\d+(?:\.\d+)?)\s*(?<u>[A-Za-z][A-Za-z.]{0,9})?$")]
    private static partial Regex QtyRegex();

    [GeneratedRegex(@"^(?:rs\.?|inr|₹)?\s*(?<v>(?:0|[1-9]\d*)(?:\.\d+)?)\s*(?:/-)?$", RegexOptions.IgnoreCase)]
    private static partial Regex RateRegex();

    [GeneratedRegex(@"^\d{2,3}(?:\.\d+)?(?:\s*/-)?$")]
    private static partial Regex DigitGroupRegex();

    [GeneratedRegex(@"^\d{1,3}\s*[.)]\s+")]
    private static partial Regex LeadingNumberRegex();

    [GeneratedRegex(@"\d")]
    private static partial Regex AnyDigitRegex();

    private static readonly string[] DateFormats =
    [
        "dd MMM yyyy", "d MMM yyyy", "dd MMMM yyyy", "d MMMM yyyy", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy",
        "d-M-yyyy", "dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd", "dd-MMM-yyyy", "d-MMM-yyyy", "dd/MM/yy", "d/M/yy",
    ];

    public static string FormatDate(DateOnly date, string format = "dd MMM yyyy")
    {
        try
        {
            return date.ToString(string.IsNullOrWhiteSpace(format) ? "dd MMM yyyy" : format, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>The text without its Ref lines, so a copied quotation gets a new number.</summary>
    public static string RemoveRefLines(string text)
    {
        var lines = (text ?? "").Replace("\r\n", "\n").Split('\n')
            .Where(l => !(TryLabel(l.Trim(), out var f, out _) && f is Field.Ref));
        return string.Join("\r\n", lines);
    }

    public static ParseResult Parse(string text, AppSettings settings, DateOnly today)
    {
        var q = new Quotation
        {
            Title = settings.Title,
            Date = FormatDate(today, settings.DateFormat),
            Terms = settings.DefaultTerms,
            Seller = new Party { Name = settings.SellerName, Lines = [.. settings.SellerLines] },
            Buyer = new Party { Label = settings.BuyerLabel },
        };
        var result = new ParseResult { Quotation = q };

        var address = new List<string>();
        string? gstin = null, phone = null, email = null;
        List<string>? tc = null;
        List<string>? signOff = null;
        var gstMode = settings.DefaultGstMode;
        var gstFromText = false;
        var units = new HashSet<string>(settings.Units.Select(u => u.Trim()).Where(u => u.Length > 0),
            StringComparer.OrdinalIgnoreCase);
        var gstPercent = settings.GstPercent;
        LineItem? lastItem = null;
        Field? section = null; // set by a label with no value, e.g. "Address:" or "Terms & Conditions:"

        var lines = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var lineNo = i + 1;
            var raw = lines[i];
            var line = raw.Trim();
            if (line.Length == 0)
            {
                if (section is Field.Tc or Field.SignOff or Field.Address) section = null;
                continue;
            }
            if (line.StartsWith('#') || line.StartsWith("//", StringComparison.Ordinal)) continue;

            var indented = char.IsWhiteSpace(raw[0]);
            var bullet = line[0] is '-' or '•' or '*' or '>' or '–';

            // Labelled line, e.g. "Company: BuyersHub".
            if (TryLabel(line, out var field, out var value))
            {
                if (value.Length == 0)
                {
                    section = field;
                    if (field == Field.Tc) tc ??= [];
                    if (field == Field.SignOff) signOff ??= [];
                    if (field == Field.Items) lastItem = null;
                    continue;
                }
                section = null;
                switch (field)
                {
                    case Field.BuyerName:
                        if (q.Buyer.Name.Length > 0)
                            result.Warnings.Add(new(lineNo, $"Company given twice; using \"{value}\"."));
                        q.Buyer.Name = value;
                        break;
                    case Field.Address: address.Add(value); break;
                    case Field.Gstin:
                        gstin = value.ToUpperInvariant().Replace(" ", "");
                        if (!Gst.IsGstin(gstin))
                            result.Warnings.Add(new(lineNo, $"\"{value}\" does not look like a 15-character GSTIN."));
                        break;
                    case Field.Phone: phone = value; break;
                    case Field.Email: email = value; break;
                    case Field.Ref: q.RefNo = value; break;
                    case Field.OtherRef: q.OtherRef = value; break;
                    case Field.Date: q.Date = ParseDate(value, settings.DateFormat); break;
                    case Field.Terms: q.Terms = value; break;
                    case Field.Gst: ParseGst(value, lineNo, result, ref gstMode, ref gstPercent); gstFromText = true; break;
                    case Field.Tc: (tc ??= []).Add(value); break;
                    case Field.SignOff: (signOff ??= []).Add(value); break;
                    case Field.Title: q.Title = value; break;
                }
                continue;
            }

            // Item line, e.g. "Vernier Hook Depth Gauge, 10%, 5, 12011".
            var item = TryItem(line, settings, units, out var itemError);
            if (item is not null)
            {
                if (item.DiscountPercent > 100)
                    result.Errors.Add(new(lineNo, $"Discount {Money.Percent(item.DiscountPercent)}% is more than 100%."));
                if (item.Qty == 0)
                    result.Warnings.Add(new(lineNo, $"Qty is 0 for \"{item.Name}\"."));
                if (item.Rate == 0)
                    result.Warnings.Add(new(lineNo, $"Rate is 0 for \"{item.Name}\"."));
                q.Items.Add(item);
                lastItem = item;
                section = null;
                continue;
            }
            if (itemError is not null && !(lastItem is not null && (indented || bullet)))
            {
                result.Errors.Add(new(lineNo, itemError));
                continue;
            }

            if (section == Field.Tc)
            {
                tc!.Add(line);
                continue;
            }
            if (section == Field.SignOff)
            {
                signOff!.Add(line);
                continue;
            }
            if (section == Field.Address)
            {
                AddPartyLine(line, address, ref gstin, ref phone, ref email);
                continue;
            }

            // Small description line under the last item ("Make - Insize").
            if (lastItem is not null)
            {
                lastItem.Desc.Add(bullet ? line.TrimStart('-', '•', '*', '>', '–', ' ') : line);
                continue;
            }

            // Unlabelled header lines: the first is the company, the rest are its address.
            if (q.Buyer.Name.Length == 0)
            {
                if (line.Equals("to", StringComparison.OrdinalIgnoreCase) ||
                    line.Equals("to,", StringComparison.OrdinalIgnoreCase)) continue;
                q.Buyer.Name = line;
                continue;
            }
            AddPartyLine(line, address, ref gstin, ref phone, ref email);
        }

        // Buyer block in the reference order: address, GSTIN, phone, e-mail.
        q.Buyer.Lines.AddRange(address);
        if (!string.IsNullOrWhiteSpace(gstin)) q.Buyer.Lines.Add($"GSTIN {gstin}");
        if (!string.IsNullOrWhiteSpace(phone)) q.Buyer.Lines.Add(phone);
        if (!string.IsNullOrWhiteSpace(email)) q.Buyer.Lines.Add(email);

        q.TermsAndConditions = tc ?? [.. settings.TermsAndConditions];
        q.SignOff = signOff ?? [.. settings.SignOff];

        // GST
        var sellerGstin = Gst.FindGstin(settings.SellerLines);
        var buyerGstin = gstin is not null && Gst.IsGstin(gstin) ? gstin : null;
        var (intra, reason) = Gst.Decide(sellerGstin, settings.HomeState, buyerGstin, address);
        var p = Money.Percent(gstPercent);
        var source = gstFromText ? "set in text" : "default in Settings";
        switch (gstMode)
        {
            case GstMode.None:
                result.GstNote = $"No GST ({source})";
                break;
            case GstMode.Igst:
                q.Taxes = Gst.Taxes(false, gstPercent);
                result.GstNote = $"IGST {p}% ({source})";
                break;
            case GstMode.CgstSgst:
                q.Taxes = Gst.Taxes(true, gstPercent);
                result.GstNote = $"CGST + SGST {p}% ({source})";
                break;
            default:
                q.Taxes = Gst.Taxes(intra, gstPercent);
                result.GstNote = (intra ? $"CGST + SGST {p}%" : $"IGST {p}%") + $" (automatic: {reason})";
                break;
        }
        if (gstMode != GstMode.None && gstPercent <= 0) result.GstNote = "No GST (rate 0%)";

        if (q.Buyer.Name.Length == 0)
            result.Errors.Add(new(0, "Company name is missing. Add a line like:  Company: BuyersHub"));
        if (q.Items.Count == 0)
            result.Errors.Add(new(0, "No items found. Item lines look like:  Item name, discount, qty, rate"));

        return result;
    }

    private static bool TryLabel(string line, out Field field, out string value)
    {
        field = default;
        value = "";

        var m = LabelRegex().Match(line);
        if (m.Success && Labels.TryGetValue(NormalizeKey(m.Groups["k"].Value), out field))
        {
            value = m.Groups["v"].Value.Trim();
            return true;
        }

        m = RefNoColonRegex().Match(line);
        if (m.Success)
        {
            field = Field.Ref;
            value = m.Groups["v"].Value.Trim();
            return value.Length > 0;
        }

        m = GstinNoColonRegex().Match(line);
        if (m.Success)
        {
            field = Field.Gstin;
            value = m.Groups["v"].Value.Trim();
            return true;
        }
        return false;
    }

    private static string NormalizeKey(string key)
    {
        var chars = key.ToLowerInvariant().Where(c => char.IsLetter(c) || c == '&').ToArray();
        var k = new string(chars);
        if (k.StartsWith("address", StringComparison.Ordinal) || k.StartsWith("addr", StringComparison.Ordinal))
            return "address";
        return k;
    }

    private static void AddPartyLine(string line, List<string> address, ref string? gstin, ref string? phone, ref string? email)
    {
        var compact = line.Replace(" ", "");
        if (gstin is null && compact.Length == 15 && Gst.IsGstin(compact)) gstin = compact.ToUpperInvariant();
        else if (email is null && EmailRegex().IsMatch(line)) email = line;
        else if (phone is null && PhoneRegex().IsMatch(line)) phone = line;
        else address.Add(line);
    }

    private static string ParseDate(string value, string format)
    {
        return DateTime.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var d)
            ? FormatDate(DateOnly.FromDateTime(d), format)
            : value;
    }

    private static void ParseGst(string value, int lineNo, ParseResult result, ref GstMode mode, ref decimal percent)
    {
        var v = value.ToLowerInvariant();
        var number = Regex.Match(v, @"\d+(?:\.\d+)?");
        if (number.Success)
            percent = decimal.Parse(number.Value, CultureInfo.InvariantCulture);

        if (Regex.IsMatch(v, @"\b(none|nil|no|without|exempt|exempted)\b") || (number.Success && percent == 0))
            mode = GstMode.None;
        else if (v.Contains("cgst") || v.Contains("sgst") || v.Contains("intra") || v.Contains("local"))
            mode = GstMode.CgstSgst;
        else if (v.Contains("igst") || v.Contains("inter"))
            mode = GstMode.Igst;
        else if (v.Contains("auto") || number.Success)
            mode = GstMode.Auto;
        else
            result.Warnings.Add(new(lineNo, $"GST \"{value}\" not understood; use IGST, CGST+SGST, None or a rate like 18%."));
    }

    /// <summary>
    /// "name, discount, qty, rate[, unit][, description | description]". Returns null when the line is not
    /// an item; <paramref name="error"/> is set when it looks like a broken item line.
    /// </summary>
    private static LineItem? TryItem(string line, AppSettings settings, HashSet<string> units, out string? error)
    {
        error = null;
        var tabs = line.Contains('\t');
        var fields = (tabs ? line.Split('\t') : line.Split(',')).Select(f => f.Trim()).ToList();
        if (tabs)
        {
            // Spreadsheet paste: drop empty edge cells and a leading S.No column; a cell may hold "12,011.00".
            while (fields.Count > 0 && fields[0].Length == 0) fields.RemoveAt(0);
            while (fields.Count > 0 && fields[^1].Length == 0) fields.RemoveAt(fields.Count - 1);
            if (fields.Count >= 5 && Regex.IsMatch(fields[0], @"^\d{1,3}\.?$")) fields.RemoveAt(0);
            for (var k = 1; k < fields.Count; k++)
                if (Regex.IsMatch(fields[k], @"^[₹\d,.\s%/-]+$|^(rs\.?|inr)", RegexOptions.IgnoreCase))
                    fields[k] = fields[k].Replace(",", "");
        }

        for (var i = 1; i + 2 < fields.Count; i++)
        {
            var disc = ParseDiscount(fields[i]);
            var qm = QtyRegex().Match(fields[i + 1]);
            var rm = RateRegex().Match(fields[i + 2]);
            if (disc is null || !qm.Success || !rm.Success) continue;

            var name = string.Join(", ", fields.Take(i)).Trim();
            if (name.Length == 0) continue;
            name = LeadingNumberRegex().Replace(name, "");

            // Rate typed with thousand separators, e.g. "12,011" or "1,23,456.50".
            var rateText = rm.Groups["v"].Value;
            var next = i + 3;
            if (!tabs && Regex.IsMatch(rateText, @"^\d{1,3}$"))
            {
                var run = 0;
                while (next + run < fields.Count && DigitGroupRegex().IsMatch(fields[next + run])) run++;
                // Use the longest run that ends in a 3-digit group, with decimals only on the last group.
                for (var len = run; len > 0; len--)
                {
                    var groups = fields.Skip(next).Take(len).Select(g => g.Replace("/-", "").Trim()).ToList();
                    var last = groups[^1];
                    var lastWhole = last.Split('.')[0];
                    if (lastWhole.Length != 3) continue;
                    if (groups.Take(len - 1).Any(g => g.Contains('.'))) continue;
                    rateText += string.Concat(groups);
                    next += len;
                    break;
                }
            }

            var item = new LineItem
            {
                Name = name,
                DiscountPercent = disc.Value,
                Qty = decimal.Parse(qm.Groups["v"].Value, CultureInfo.InvariantCulture),
                Rate = decimal.Parse(rateText, CultureInfo.InvariantCulture),
                Unit = qm.Groups["u"].Success ? qm.Groups["u"].Value : "",
            };

            var extras = fields.Skip(next).Where(f => f.Length > 0).ToList();
            if (extras.Count > 0 && units.Contains(extras[0]))
            {
                if (item.Unit.Length == 0) item.Unit = extras[0];
                extras.RemoveAt(0);
            }
            if (item.Unit.Length == 0) item.Unit = settings.DefaultUnit;
            if (extras.Count > 0)
            {
                item.Desc.AddRange(string.Join(", ", extras)
                    .Split('|')
                    .Select(d => d.Trim())
                    .Where(d => d.Length > 0));
            }
            return item;
        }

        // Looks like an item (a name followed by numbers) but could not be read.
        var numeric = fields.Skip(1).Count(f => f.Length > 0 && AnyDigitRegex().IsMatch(f) &&
                                                Regex.IsMatch(f, @"^[₹\d.,%\s/-]+$|^(rs\.?|inr)", RegexOptions.IgnoreCase));
        if (fields.Count >= 3 && numeric >= 2)
            error = $"Could not read this item. Use:  item name, discount, qty, rate   (got \"{line}\")";
        return null;
    }

    private static decimal? ParseDiscount(string field)
    {
        var f = field.Trim();
        if (f.Length == 0 || f == "-" || f == "–" || f.Equals("nil", StringComparison.OrdinalIgnoreCase) ||
            f.Equals("no", StringComparison.OrdinalIgnoreCase) || f.Equals("none", StringComparison.OrdinalIgnoreCase))
            return 0m;
        var m = DiscountRegex().Match(f);
        return m.Success ? decimal.Parse(m.Groups["v"].Value, CultureInfo.InvariantCulture) : null;
    }
}
