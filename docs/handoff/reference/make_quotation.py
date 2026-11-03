"""
T.SAIFUDDIN & CO - Quotation generator
--------------------------------------
Edit the DATA section below, then run:

    python make_quotation.py

It writes  quotation.html  (the layout)  and  Quotation-<ref>.pdf  next to this file.

One-time setup:
    pip install playwright
    playwright install chromium

Fonts are loaded from ../fonts (the package-level fonts folder).
"""

from pathlib import Path
from decimal import Decimal, ROUND_HALF_UP

# =====================================================================
# DATA - change these for each quotation
# =====================================================================
TITLE      = "Quotation/Proforma"
REF_NO     = "100186"
OTHER_REF  = "R"                 # set to "" to hide the line
DATE       = "02 Nov 2026"
TERMS      = ""                  # set e.g. "Against Delivery" to show a Terms line under the date

SELLER_NAME  = "T.SAIFUDDIN & CO"
SELLER_LINES = ["No.72 N.R.Road", "Bangalore", "Karnataka 560002", "+917019740620",
                "+919739394657", "GSTIN 29AADFT8885H1ZF"]

BUYER_LABEL = "Buyer/Consignee"
BUYER_NAME  = "BuyersHub"
BUYER_LINES = ["Plot 27 , PRT Colony , Karmanghat", "Hyderabad Telangana 500079", "India",
               "GSTIN 36AAZFB2304B1ZQ", "+919182706754", "sales@buyershub.org.in"]

# Each item: name, list of small description lines, qty, unit, rate
ITEMS = [
    {"name": "Vernier Hook Depth Gauge",
     "desc": ["Make - Insize", "Model : 1234-2001"],
     "qty": "5", "unit": "Nos", "rate": "12011"},
]

# Taxes applied on the sub total: (label, percent)
# Inter-state:  [("IGST18 (18%)", 18)]
# Intra-state:  [("CGST9 (9%)", 9), ("SGST9 (9%)", 9)]
TAXES = [("IGST18 (18%)", 18)]

TERMS_AND_CONDITIONS = [
    "1. Payment: 100% Against Proforma",
    "2. Packing and forwarding charges extra",
]

# Sign-off printed under the terms & conditions (set to [] to hide)
SIGN_OFF = [
    "Regards,",
    "Name,",
    "Tsaifuddin and Company",
]

# =====================================================================
# Below this line is the layout - you normally don't need to touch it
# =====================================================================
HERE = Path(__file__).resolve().parent
PAGE_W, PAGE_H = 595.42, 841.69          # A4 in points
INK, BLACK, WHITE = "#333333", "#000000", "#FFFFFF"
MAROON, LOGO_MAROON = "#800A08", "#8A0000"
DARK, TABLE_HEAD, RULE = "#1C201F", "#3C3D3A", "#ADADAD"


def money(value: Decimal) -> str:
    """Indian digit grouping: 1,23,456.78"""
    value = value.quantize(Decimal("0.01"), ROUND_HALF_UP)
    whole, frac = f"{value:.2f}".split(".")
    neg = whole.startswith("-")
    whole = whole.lstrip("-")
    if len(whole) > 3:
        head, tail = whole[:-3], whole[-3:]
        groups = []
        while len(head) > 2:
            groups.insert(0, head[-2:])
            head = head[:-2]
        if head:
            groups.insert(0, head)
        whole = ",".join(groups + [tail])
    return ("-" if neg else "") + whole + "." + frac


def esc(s: str) -> str:
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


elements = []


def text(x, top, s, size=8, bold=False, color=INK, align="left"):
    """Place one line of text. x is the left edge (or right edge when align='right')."""
    y = top - 0.06 * size
    style = f"top:{y:.2f}pt;font-size:{size}pt;color:{color};"
    if bold:
        style += "font-weight:700;"
    if align == "right":
        style += f"right:{PAGE_W - x:.2f}pt;text-align:right;"
    else:
        style += f"left:{x:.2f}pt;"
    elements.append(f'<div class="t" style="{style}">{esc(s)}</div>')


def lines(x, top, items, step=9.22, size=8):
    for i, s in enumerate(items):
        text(x, top + i * step, s, size)


# ---------- logo (vector) ----------
ICON = f'''<g fill="{LOGO_MAROON}">
 <polygon points="129.3,38 146.8,38 146.8,52.1 184.5,52.1 166.9,77 146.8,77 146.8,135.8 187.1,200 148,200 129.3,165.5"/>
 <polygon points="167,98.8 182.1,121.8 167,144.7 151.4,121.8"/>
 <polygon points="127.2,168.1 143,196.5 110.5,196.5"/>
</g>
<g stroke="{LOGO_MAROON}" stroke-width="STROKE" fill="none" stroke-linecap="square">
 <polyline points="129.3,96.6 87.3,96.6 87.3,199.4 213,199.4 213,96.6 168,96.6"/>
</g>'''


def watermark_svg():
    icon = ICON.replace('stroke-width="STROKE"', 'stroke-width="1.15"')
    return f'''<svg class="wm" viewBox="44 37 212 194" xmlns="http://www.w3.org/2000/svg">{icon}
<text x="44.3" y="230" textLength="211.7" lengthAdjust="spacingAndGlyphs" font-family="Mont"
 font-weight="500" font-size="23.8" fill="{LOGO_MAROON}">T.SAIFUDDIN &amp; CO</text></svg>'''


def banner_svg():
    s = 97 / 162
    tx, ty = 413.5 - 87.3 * s, 52.5 - 38 * s
    icon = ICON.replace('stroke-width="STROKE"', 'stroke-width="1.4" stroke-opacity="0.6"')
    return f'''<svg class="banner" viewBox="409 48 400 106" xmlns="http://www.w3.org/2000/svg">
<g transform="translate({tx:.2f},{ty:.2f}) scale({s:.4f})">{icon}</g>
<text x="501" y="130" textLength="304" lengthAdjust="spacingAndGlyphs" font-family="Mont"
 font-weight="700" font-size="34.6" fill="{MAROON}">T.SAIFUDDIN &amp; CO</text></svg>'''


def build_html() -> str:
    elements.clear()
    shapes = []

    # Seller block (top left)
    text(46.4, 137.7, SELLER_NAME, 9, True)
    lines(46.4, 148.2, SELLER_LINES)

    # Title block (top right)
    text(559.9, 139.9, TITLE, 24, align="right")
    text(559.8, 165.5, f"Ref No. {REF_NO}", 10, True, align="right")
    if OTHER_REF:
        text(559.8, 178.5, f"Other Ref: {OTHER_REF}", 10, True, align="right")

    # Buyer block
    text(46.4, 227.8, BUYER_LABEL, 10)
    text(46.4, 239.2, BUYER_NAME, 9, True)
    lines(46.4, 249.7, BUYER_LINES)

    # Date / terms
    text(460.3, 280.2, "Date :", 10, align="right")
    text(559.9, 281.0, DATE, 8, align="right")
    if TERMS:
        text(460.3, 299.2, "Terms :", 10, align="right")
        text(559.8, 300.1, TERMS, 8, align="right")

    # Item table header
    head_top = 328.8
    shapes.append(f'<rect x="45.6" y="{head_top}" width="515" height="24" fill="{TABLE_HEAD}"/>')
    for x, label, al in [(50.9, "S.No", "left"), (86.3, "Item & Description", "left"),
                         (419.2, "Qty", "right"), (475.9, "Rate", "right"), (553.1, "Amount", "right")]:
        text(x, head_top + 8.1, label, 9, color=WHITE, align=al)

    # Item rows
    row_top = head_top + 24
    sub_total = Decimal("0")
    for n, item in enumerate(ITEMS, 1):
        qty, rate = Decimal(item["qty"]), Decimal(item["rate"])
        amount = qty * rate
        sub_total += amount
        y = row_top + 8.8
        text(57.8, y, str(n), 9, color=BLACK)
        text(86.3, y, item["name"], 9, color=BLACK)
        text(419.2, y, f"{qty:.2f}", 9, color=BLACK, align="right")
        text(419.2, y + 10.1, item.get("unit", ""), 7, align="right")
        text(475.9, y, money(rate), 9, color=BLACK, align="right")
        text(553.1, y, money(amount), 9, color=BLACK, align="right")
        desc = item.get("desc", [])
        for i, d in enumerate(desc):
            text(86.3, row_top + 18.9 + 12.3 * i, d, 7)
        last = row_top + 18.9 + 12.3 * (max(len(desc), 1) - 1)
        rule_y = last + 14.9
        shapes.append(f'<rect x="45.6" y="{rule_y:.1f}" width="515" height="0.8" fill="{RULE}"/>')
        row_top = rule_y + 0.8

    # Totals
    y = row_top + 9.2
    text(451.8, y, "Sub Total", 9, color=BLACK, align="right")
    text(553.1, y, money(sub_total), 9, color=BLACK, align="right")
    total = sub_total
    for label, pct in TAXES:
        y += 25.4
        tax = (sub_total * Decimal(str(pct)) / 100).quantize(Decimal("0.01"), ROUND_HALF_UP)
        total += tax
        text(451.8, y, label, 9, color=BLACK, align="right")
        text(553.1, y, money(tax), 9, color=BLACK, align="right")
    y += 25.4
    text(451.8, y, "Total", 9, True, BLACK, "right")
    text(553.1, y, "₹" + money(total), 9, True, BLACK, "right")

    # Terms & conditions + sign-off, stacked upward from the footer rule
    bottom = 772.5                       # top of the last line of this block
    if SIGN_OFF:
        so_top = bottom - 10.5 * (len(SIGN_OFF) - 1)
        lines(46.4, so_top, SIGN_OFF, step=10.5)
        bottom = so_top - 18.0           # gap between T&C and sign-off
    else:
        bottom -= 10.5 * 2               # original position when there is no sign-off
    if TERMS_AND_CONDITIONS:
        tc_top = bottom - 10.5 * (len(TERMS_AND_CONDITIONS) - 1)
        text(46.4, tc_top - 14.0, "Terms & Conditions", 10)
        lines(46.4, tc_top, TERMS_AND_CONDITIONS, step=10.5)

    # Page number
    text(562.8, 801.0, "1", 6, color="#AAAAAA", align="right")

    return f'''<!doctype html><html><head><meta charset="utf-8"><style>
@font-face{{font-family:U;src:url(../fonts/Ubuntu-Regular.ttf);font-weight:400}}
@font-face{{font-family:U;src:url(../fonts/Ubuntu-Bold.ttf);font-weight:700}}
@font-face{{font-family:Mont;src:url(../fonts/Montserrat.ttf);font-weight:100 900}}
@page{{size:{PAGE_W}pt {PAGE_H}pt;margin:0}}
*{{margin:0;padding:0;box-sizing:border-box}}
html,body{{width:{PAGE_W}pt;height:{PAGE_H}pt}}
body{{position:relative;overflow:hidden;background:#fff;font-family:U,sans-serif}}
.t{{position:absolute;line-height:1;white-space:nowrap}}
.wm{{position:absolute;left:83pt;top:212pt;width:430pt;height:393.4pt;opacity:.065}}
.banner{{position:absolute;left:14pt;top:46pt;width:264.15pt;height:70pt}}
.frame{{position:absolute;left:0;top:0}}
</style></head><body>
{watermark_svg()}
<svg class="frame" width="{PAGE_W}pt" height="{PAGE_H}pt" viewBox="0 0 {PAGE_W} {PAGE_H}">
  <rect x="0" y="0" width="{PAGE_W}" height="17" fill="{DARK}"/>
  <polygon points="494.5,17 {PAGE_W},17 {PAGE_W},44 525,44" fill="{MAROON}"/>
  <polygon points="0,813 0,{PAGE_H} 82,{PAGE_H}" fill="{MAROON}"/>
  <rect x="0" y="838.6" width="{PAGE_W}" height="3.1" fill="{DARK}"/>
  <rect x="39.6" y="791.3" width="527" height="0.7" fill="{RULE}"/>
  {"".join(shapes)}
</svg>
{banner_svg()}
{"".join(elements)}
</body></html>'''


def render_pdf(html_path: Path, pdf_path: Path):
    from playwright.sync_api import sync_playwright
    with sync_playwright() as pw:
        try:
            browser = pw.chromium.launch()                 # Chromium installed by Playwright
        except Exception:
            browser = pw.chromium.launch(channel="chrome")  # fall back to installed Google Chrome
        page = browser.new_page()
        page.goto(html_path.as_uri())
        page.wait_for_load_state("networkidle")
        page.evaluate("document.fonts.ready")
        page.pdf(path=str(pdf_path), format="A4",
                 print_background=True, margin={"top": "0", "bottom": "0", "left": "0", "right": "0"})
        browser.close()


if __name__ == "__main__":
    html_file = HERE / "quotation.html"
    pdf_file = HERE / f"Quotation-{REF_NO}.pdf"
    html_file.write_text(build_html(), encoding="utf-8")
    render_pdf(html_file, pdf_file)
    print(f"Saved {pdf_file}")
