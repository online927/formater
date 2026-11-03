// Prints a page made by formater-html to PDF with headless Chromium, the way the Windows app does
// with WebView2: A4, backgrounds on, no margins, after the embedded fonts have loaded.
// usage: node tools/print-pdf.cjs <in.html> <out.pdf> [more.html more.pdf ...]
const path = require("path");
const { chromium } = require("playwright");

(async () => {
  const args = process.argv.slice(2);
  if (args.length < 2 || args.length % 2) {
    console.error("usage: node tools/print-pdf.cjs <in.html> <out.pdf> [...]");
    process.exit(2);
  }
  const browser = await chromium.launch();
  // Device scale 2 turns on fractional glyph positioning in Linux Chromium (at scale 1 every glyph
  // is rounded to a whole pixel and long lines drift by points). The PDF itself is the same size.
  const page = await browser.newPage({ deviceScaleFactor: 2 });
  for (let i = 0; i < args.length; i += 2) {
    await page.goto("file://" + path.resolve(args[i]));
    await page.waitForFunction(() => ["rendered", "font-error"].includes(document.body.dataset.state));
    const state = await page.evaluate(() => document.body.dataset.state);
    if (state !== "rendered") throw new Error(`${args[i]}: fonts did not load (${state})`);
    const pages = await page.evaluate(() => document.body.dataset.pages);
    await page.pdf({ path: args[i + 1], format: "A4", printBackground: true,
                     margin: { top: "0", right: "0", bottom: "0", left: "0" } });
    console.log(`${args[i + 1]}: ${pages} page(s)`);
  }
  await browser.close();
})().catch((e) => { console.error(e.message || e); process.exit(1); });
