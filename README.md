# Formater: Quotation PDF (T.SAIFUDDIN & CO)

A Windows app that turns pasted text into a one-page A4 quotation PDF. The PDF uses the T.SAIFUDDIN & CO layout in
[`docs/handoff/reference/Quotation-100186.pdf`](docs/handoff/reference/Quotation-100186.pdf).

- **One file:** `Formater.exe` is self-contained, so the PC doesn't need .NET installed. It uses the Chromium engine
  already built into Windows 10/11 (Microsoft Edge WebView2) to make the PDF.
- **Same fonts and logos:** Ubuntu fonts and the vector logos from the handoff package are built into the exe.
- **Live preview:** the page updates as you type. Export with **Export PDF** or **Ctrl+E**.

## Download

Every push builds the exe on GitHub Actions. Open **Actions → Build → latest run → Artifacts → Formater-win-x64**.
A version tag such as `v1.0.0` also attaches `Formater.exe` to a GitHub release.

The first time you run it, Windows SmartScreen may warn about an unknown publisher. Click
**More info → Run anyway**.

## How to use

Paste the quotation text on the left. Only **Company**, **Ref** and **at least one item** are required. Everything
else is optional.

```
Company: BuyersHub
Address: Plot 27 , PRT Colony , Karmanghat
Address: Hyderabad Telangana 500079
Address: India
GSTIN: 36AAZFB2304B1ZQ
Phone: +919182706754
Email: sales@buyershub.org.in
Ref: 100186
Other Ref: R

Vernier Hook Depth Gauge, 10%, 5, 12011
  Make - Insize
  Model : 1234-2001
Digital Caliper 150mm, 0%, 2, 3450
```

- **Item lines** are `item name, discount, qty, rate`.
  - The discount can be written `10%`, `10`, `0` or left empty.
  - A rate can have thousand separators (`12,011` or `1,23,456.50`).
  - You can add a unit and small description lines after the rate:
    `Gauge, 10%, 5, 12011, Pcs, Make - Insize | Model : 1234-2001`. Without a unit, the item gets **Nos**.
  - Lines indented (or starting with `-`) under an item are its small description lines.
  - Rows copied from Excel (tab separated) also work.
- **Address, GSTIN, phone, e-mail:** each is optional. Lines without a label under the company name are also read as
  its address. A GSTIN, phone number or e-mail on such a line is recognised.
- **Date** is today's date (`03 Nov 2026`). Override it with `Date: 02/11/2026`.
- **Terms** default to *Against Delivery*. Override with `Terms: 30 days credit`.
- **Terms & Conditions** default to the two lines in Settings. Replace them with `T&C: ...` lines, or a
  `Terms & Conditions:` line followed by the lines. An empty `Terms & Conditions:` section hides the block.
- **GST is automatic.**
  - A buyer GSTIN starting with the seller's state code (29, Karnataka) gives **CGST 9% + SGST 9%**.
  - Without a GSTIN, a buyer address mentioning Karnataka gives **CGST 9% + SGST 9%**.
  - Anything else gives **IGST 18%**.
  - Override with `GST: IGST`, `GST: CGST+SGST`, `GST: 12%` or `GST: None`.
- **Discount:** when any item has a discount, the table gets a **Disc %** column.
  - Rate is the list rate, and Amount = qty × rate × (1 − disc%).
  - With no discounts, the table is exactly the reference layout.
- **What the app understood:** the box under the text shows each item's maths, the GST choice and the totals, plus
  any line it could not read. Export is blocked until the errors are fixed.

**Settings** holds the details that are the same on every quotation:
- your company name and address
- the document title
- your state and GST rate
- the default unit
- the default Terms and Terms & Conditions
- whether the PDF opens after saving

They are stored in `%APPDATA%\Formater\settings.json`. The text you were working on is kept in `draft.txt` there.

### Command line

```
Formater.exe --export quotation.txt Quotation-100186.pdf [--date 2026-11-02]
```

This makes the PDF without opening a window. Exit code 0 means saved and 1 means the text has errors.
JSON in the SPEC §8 format (`docs/handoff/sample_data.json`) is accepted as input as well as text.

## Layout

[`docs/handoff/SPEC.md`](docs/handoff/SPEC.md) is the source of truth.
[`src/Formater.Core/Resources/layout.js`](src/Formater.Core/Resources/layout.js) implements it: absolutely positioned
text in an HTML page, printed by Chromium. All maths and number formatting are in C# `decimal`.

Beyond the spec (§9):

- **Disc % column:** shown only when an item has a discount. Qty and Rate move left to make room; everything else
  stays in place.
- **Wide numbers** (lakhs and crores): when they would touch the next column, the columns move left. Normal
  quotations keep the reference positions.
- **Wrapping:** long item names, description lines, address lines and terms wrap instead of running off the page.
  The rows below move down.
- **Long blocks:** a long buyer or seller block pushes the table down.
- **More than one page:**
  - Rows continue on the next page under a repeated title and table header.
  - Totals and Terms & Conditions go on the last page.
  - Each page is numbered.
- **Missing fonts:** if the fonts fail to load, export is blocked instead of silently using another font.

## Development

Requires the .NET 10 SDK.

```bash
dotnet test                                           # parser, maths, formatting, page tests
dotnet publish src/Formater.App -c Release -r win-x64 -p:DebugType=none -o publish   # Formater.exe
tools/visual-check.sh                                 # renders PDFs and compares with the reference
```

`tools/visual-check.sh` needs Node with Playwright (`npm install --prefix tools`), `pdftoppm`, and Python 3 with Pillow and NumPy. It
prints `docs/handoff/sample_data.json` with Chromium and compares it at 150 dpi with the reference PDF. Everything
matches except the logo lettering: the reference used live Montserrat text, while the app uses the outlined
`logo-banner.svg` that SPEC.md asks for. That difference is about 2,400 pixels, all inside the banner, and can't be
seen by eye. It also renders the edge cases in `tests/visual/` and the examples:
- 3 items
- CGST + SGST
- empty Other Ref
- empty Terms & Conditions
- crore amounts
- discount with wrapping
- 24 items over 3 pages

| Path | Contents |
|---|---|
| `src/Formater.Core` | Text parser, GST rule, decimal maths, Indian number format, page template and layout script, fonts, logos |
| `src/Formater.App` | Windows app (WinForms + WebView2): paste box, live preview, Export PDF, Settings, `--export` |
| `src/Formater.Cli` | `formater-html`: writes the same page as HTML, for tests and printing with Playwright |
| `tests/` | Unit tests and visual-check inputs |
| `docs/handoff/` | The handoff package: SPEC.md, sample data, reference PDF/PNG and the Python reference implementation |

Fonts: Ubuntu (Ubuntu Font Licence). The logo lettering is outlined from Montserrat (SIL Open Font Licence).
