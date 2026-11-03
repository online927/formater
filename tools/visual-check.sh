#!/usr/bin/env bash
# Renders the reference fixture and the edge cases to PDF with Chromium and compares the fixture
# with docs/handoff/reference/Quotation-100186.pdf (SPEC §10). Needs: dotnet, node + playwright,
# poppler-utils (pdftoppm) and Python 3 with Pillow and NumPy.
# usage: tools/visual-check.sh [out-dir]
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${1:-$ROOT/out}"
mkdir -p "$OUT"
DATE=2026-11-03
LIMIT=6000   # differing pixels at 150 dpi; the outlined banner lettering accounts for ~2,500

dotnet build "$ROOT/src/Formater.Cli" -c Release -v q -nologo > /dev/null
CLI=(dotnet "$ROOT/src/Formater.Cli/bin/Release/net10.0/formater-html.dll")

pairs=()
"${CLI[@]}" "$ROOT/docs/handoff/sample_data.json" "$OUT/sample_data.html"
pairs+=("$OUT/sample_data.html" "$OUT/sample_data.pdf")
for f in "$ROOT"/examples/*.txt "$ROOT"/tests/visual/*.txt; do
  n="$(basename "$f" .txt)"
  "${CLI[@]}" "$f" "$OUT/$n.html" --date "$DATE"
  pairs+=("$OUT/$n.html" "$OUT/$n.pdf")
done
node "$ROOT/tools/print-pdf.cjs" "${pairs[@]}"

compare_to_reference() { # <pdf> <label>
  pdftoppm -r 150 -png -singlefile "$1" "$OUT/$2"
  pdftoppm -r 150 -png -singlefile "$ROOT/docs/handoff/reference/Quotation-100186.pdf" "$OUT/reference" 2>/dev/null
  echo "$2 compared with the reference (limit $LIMIT):"
  if ! "${PYTHON:-python3}" "$ROOT/tools/compare.py" "$OUT/$2.png" "$OUT/reference.png" --limit "$LIMIT" --side "$OUT/$2-vs-reference.png"; then
    echo "Fonts in $1:"; pdffonts "$1" || true
    echo "Word positions in $1 (x, y in pt):"
    pdftotext -bbox "$1" - | sed -n 's/.*xMin="\([0-9.]*\)" yMin="\([0-9.]*\)".*>\(.*\)<\/word>/\1 \2 \3/p' | head -80 || true
    return 1
  fi
}

compare_to_reference "$OUT/sample_data.pdf" sample_data
if [ -n "${WINDOWS_PDF:-}" ]; then compare_to_reference "$WINDOWS_PDF" windows_sample_data; fi
echo "OK: PDFs in $OUT"
