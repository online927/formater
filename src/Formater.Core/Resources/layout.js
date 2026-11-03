// Quotation page layout. Coordinates are PDF points from the top-left corner (docs/handoff/SPEC.md).
// All maths and number formatting happen in C#; this script only places pre-formatted text.
(function () {
  "use strict";

  var PW = 595.42, PH = 841.69;
  var INK = "#333333", BLACK = "#000000", WHITE = "#FFFFFF";
  var MAROON = "#800A08", DARK = "#1C201F", TABLE_HEAD = "#3C3D3A", RULE = "#ADADAD", PAGE_NO = "#AAAAAA";
  var SVG_NS = "http://www.w3.org/2000/svg";

  // Column right edges. With a Disc % column, Qty and Rate move left to make room (Amount stays put).
  var COLS = { qty: 419.2, rate: 475.9, disc: null, amount: 553.1 };
  var COLS_DISC = { qty: 380.0, rate: 440.0, disc: 478.0, amount: 553.1 };

  var HEAD_TOP = 328.8;       // table header band on page 1 (pushed down by a long buyer block)
  var CONT_HEAD_TOP = 200.0;  // table header band on continuation pages
  var ROW_LIMIT = 780.0;      // rows must end above this (footer rule at 791.3)
  var LINE = 9.22;            // address line step
  var PARTY_WIDTH = 320;      // address lines wrap at this width
  var TC_WIDTH = 515;

  var measurer = null;
  var fontsOk = false;
  var lastData = null;

  function measure(s, size, bold) {
    measurer.style.fontSize = size + "pt";
    measurer.style.fontWeight = bold ? "700" : "400";
    measurer.textContent = s;
    return measurer.getBoundingClientRect().width * 0.75; // CSS px → pt
  }

  // Greedy word wrap. A single word wider than the limit stays on its own line.
  function wrap(s, size, bold, maxW) {
    s = String(s == null ? "" : s);
    if (!s || measure(s, size, bold) <= maxW) return [s];
    var words = s.split(/\s+/), out = [], cur = "";
    for (var i = 0; i < words.length; i++) {
      var next = cur ? cur + " " + words[i] : words[i];
      if (cur && measure(next, size, bold) > maxW) { out.push(cur); cur = words[i]; }
      else cur = next;
    }
    if (cur) out.push(cur);
    return out;
  }

  function wrapAll(list, size, bold, maxW) {
    var out = [];
    (list || []).forEach(function (s) { out = out.concat(wrap(s, size, bold, maxW)); });
    return out;
  }

  // One line of text. T is the reference "top"; baseline = T + 0.85 × size.
  function text(pg, x, T, s, size, bold, color, align) {
    if (s == null || s === "") return;
    var d = document.createElement("div");
    d.className = "t";
    d.style.top = (T - 0.06 * size).toFixed(2) + "pt";
    d.style.fontSize = size + "pt";
    d.style.color = color || INK;
    if (bold) d.style.fontWeight = "700";
    if (align === "right") { d.style.right = (PW - x).toFixed(2) + "pt"; d.style.textAlign = "right"; }
    else d.style.left = x.toFixed(2) + "pt";
    d.textContent = s;
    pg.el.appendChild(d);
  }

  function shape(pg, tag, attrs) {
    var e = document.createElementNS(SVG_NS, tag);
    for (var k in attrs) e.setAttribute(k, attrs[k]);
    pg.frame.appendChild(e);
  }

  function rect(pg, x, y, w, h, fill) {
    shape(pg, "rect", { x: x, y: +y.toFixed(2), width: w, height: h, fill: fill });
  }

  function cloneSvg(id, cls) {
    var svg = document.getElementById(id).querySelector("svg").cloneNode(true);
    svg.setAttribute("class", cls);
    svg.removeAttribute("width");
    svg.removeAttribute("height");
    return svg;
  }

  // Static artwork in SPEC §4 order; dynamic shapes go into the same frame afterwards.
  function newPage(container) {
    var el = document.createElement("div");
    el.className = "page";
    el.appendChild(cloneSvg("tpl-wm", "wm"));

    var frame = document.createElementNS(SVG_NS, "svg");
    frame.setAttribute("class", "frame");
    frame.setAttribute("width", PW + "pt");
    frame.setAttribute("height", PH + "pt");
    frame.setAttribute("viewBox", "0 0 " + PW + " " + PH);
    el.appendChild(frame);

    var pg = { el: el, frame: frame };
    rect(pg, 0, 0, PW, 17, DARK);
    shape(pg, "polygon", { points: "494.5,17 " + PW + ",17 " + PW + ",44 525,44", fill: MAROON });
    shape(pg, "polygon", { points: "0," + (PH - 28.7).toFixed(2) + " 0," + PH + " 82," + PH, fill: MAROON });
    rect(pg, 0, PH - 3.1, PW, 3.1, DARK);
    rect(pg, 39.6, 791.3, 527, 0.7, RULE);

    container.appendChild(el);
    pg.banner = cloneSvg("tpl-banner", "banner");
    return pg;
  }

  function finishPage(pg) {
    // The banner logo draws above the frame shapes, and text draws above both.
    pg.el.insertBefore(pg.banner, pg.frame.nextSibling);
  }

  function titleBlock(pg, d) {
    text(pg, 559.9, 139.9, d.title, 24, false, INK, "right");
    text(pg, 559.8, 165.5, d.refLine, 10, true, INK, "right");
    if (d.otherRefLine) text(pg, 559.8, 178.5, d.otherRefLine, 10, true, INK, "right");
  }

  function tableHead(pg, top, cols) {
    rect(pg, 45.6, top, 515, 24, TABLE_HEAD);
    var T = top + 8.1;
    text(pg, 50.9, T, "S.No", 9, false, WHITE);
    text(pg, 86.3, T, "Item & Description", 9, false, WHITE);
    text(pg, cols.qty, T, "Qty", 9, false, WHITE, "right");
    text(pg, cols.rate, T, "Rate", 9, false, WHITE, "right");
    if (cols.disc) text(pg, cols.disc, T, "Disc %", 9, false, WHITE, "right");
    text(pg, cols.amount, T, "Amount", 9, false, WHITE, "right");
  }

  // Page 1 header; returns the top of the table header band.
  function firstPageHeader(pg, d) {
    var seller = wrapAll(d.seller.lines, 8, false, PARTY_WIDTH);
    text(pg, 46.4, 137.7, d.seller.name, 9, true);
    seller.forEach(function (s, i) { text(pg, 46.4, 148.2 + LINE * i, s, 8); });
    titleBlock(pg, d);

    // A seller block longer than the reference's 6 lines pushes the buyer block down.
    var sellerLast = 148.2 + LINE * (Math.max(seller.length, 1) - 1);
    var shift = Math.max(0, sellerLast - 194.3);
    var buyer = wrapAll(d.buyer.lines, 8, false, PARTY_WIDTH);
    text(pg, 46.4, 227.8 + shift, d.buyer.label, 10);
    text(pg, 46.4, 239.2 + shift, d.buyer.name, 9, true);
    buyer.forEach(function (s, i) { text(pg, 46.4, 249.7 + shift + LINE * i, s, 8); });

    var buyerLast = buyer.length ? 249.7 + shift + LINE * (buyer.length - 1) : 239.2 + shift;
    var headTop = Math.max(HEAD_TOP, buyerLast + 33.0);

    // Date and Terms sit just above the table, as in the reference.
    text(pg, 460.3, headTop - 48.6, "Date :", 10, false, INK, "right");
    text(pg, 559.9, headTop - 47.8, d.date, 8, false, INK, "right");
    if (d.terms) {
      text(pg, 460.3, headTop - 29.6, "Terms :", 10, false, INK, "right");
      text(pg, 559.8, headTop - 28.7, d.terms, 8, false, INK, "right");
    }
    return headTop;
  }

  // Keep the reference column positions unless wide numbers (lakhs and crores) would touch the
  // column to their left; then move the columns left, right to left from the fixed Amount column.
  function fitColumns(base, items) {
    var GAP = 9;
    function widest(key, label) {
      var w = measure(label, 9, false);
      items.forEach(function (it) { w = Math.max(w, measure(it[key] || "", 9, false)); });
      return w;
    }
    var cols = { qty: base.qty, rate: base.rate, disc: base.disc, amount: base.amount };
    var right = cols.amount - widest("amount", "Amount");
    if (cols.disc) {
      cols.disc = Math.min(cols.disc, right - GAP);
      right = cols.disc - widest("discount", "Disc %");
    }
    cols.rate = Math.min(cols.rate, right - GAP);
    right = cols.rate - widest("rate", "Rate");
    cols.qty = Math.min(cols.qty, right - GAP);
    return cols;
  }

  // Measure an item row: wrapped name and description lines, and its height (SPEC §6).
  function prepareRow(item, nameWidth) {
    var name = wrap(item.name, 9, false, nameWidth);
    var desc = wrapAll(item.desc, 7, false, nameWidth);
    var extra = 11 * (name.length - 1);
    var last = 18.9 + extra + 12.3 * (Math.max(desc.length, 1) - 1);
    return { item: item, name: name, desc: desc, extra: extra, height: last + 14.9 + 0.8 };
  }

  function placeRow(pg, row, rowTop, cols) {
    var it = row.item, y = rowTop + 8.8;
    text(pg, 57.8, y, it.n, 9, false, BLACK);
    row.name.forEach(function (s, i) { text(pg, 86.3, y + 11 * i, s, 9, false, BLACK); });
    text(pg, cols.qty, y, it.qty, 9, false, BLACK, "right");
    text(pg, cols.qty, rowTop + 18.9, it.unit, 7, false, INK, "right");
    text(pg, cols.rate, y, it.rate, 9, false, BLACK, "right");
    if (cols.disc) text(pg, cols.disc, y, it.discount, 9, false, BLACK, "right");
    text(pg, cols.amount, y, it.amount, 9, false, BLACK, "right");
    row.desc.forEach(function (s, i) { text(pg, 86.3, rowTop + 18.9 + row.extra + 12.3 * i, s, 7); });
    var ruleY = rowTop + row.height - 0.8;
    rect(pg, 45.6, ruleY, 515, 0.8, RULE);
    return ruleY + 0.8;
  }

  function render(d) {
    if (!fontsOk) return { ok: false, pages: 0, error: "fonts" };
    lastData = d;
    var container = document.getElementById("pages");
    container.innerHTML = "";

    var cols = fitColumns(d.showDiscount ? COLS_DISC : COLS, d.items || []);
    var nameWidth = cols.qty - 45 - 86.3;
    var rows = (d.items || []).map(function (it) { return prepareRow(it, nameWidth); });
    var totals = d.totals || [];
    var tc = wrapAll(d.termsAndConditions, 8, false, TC_WIDTH);
    var tcTop = 752.0 - 10.5 * (tc.length - 2);
    var totalsHeight = 9.2 + 25.4 * Math.max(totals.length - 1, 0) + 9;
    var lastLimit = tc.length ? tcTop - 14 - 12 : ROW_LIMIT;

    // Page 1 header first: its height decides where the table starts.
    var pages = [];
    var first = newPage(container);
    var headTop = firstPageHeader(first, d);

    // Split rows into pages; totals and terms go on the last page.
    var plan = [{ start: headTop, rows: [] }];
    var top = headTop + 24;
    rows.forEach(function (r) {
      var cur = plan[plan.length - 1];
      if (cur.rows.length && top + r.height > ROW_LIMIT) {
        plan.push({ start: CONT_HEAD_TOP, rows: [] });
        top = CONT_HEAD_TOP + 24;
      }
      plan[plan.length - 1].rows.push(r);
      top += r.height;
    });
    if (top + totalsHeight > lastLimit) {
      var cur = plan[plan.length - 1];
      // Carry the last row over so the totals never sit alone on a page.
      var moved = cur.rows.length > 1 ? [cur.rows.pop()] : [];
      plan.push({ start: CONT_HEAD_TOP, rows: moved });
    }

    plan.forEach(function (p, index) {
      var pg = index === 0 ? first : newPage(container);
      if (index > 0) titleBlock(pg, d);
      var rowTop = p.start + 24;
      if (p.rows.length || index === 0) tableHead(pg, p.start, cols);
      else rowTop = p.start;
      p.rows.forEach(function (r) { rowTop = placeRow(pg, r, rowTop, cols); });

      if (index === plan.length - 1) {
        var y = rowTop + 9.2;
        totals.forEach(function (t, i) {
          if (i > 0) y += 25.4;
          text(pg, 451.8, y, t.label, 9, !!t.bold, BLACK, "right");
          text(pg, 553.1, y, t.value, 9, !!t.bold, BLACK, "right");
        });
        if (tc.length) {
          text(pg, 46.4, tcTop - 14.0, "Terms & Conditions", 10);
          tc.forEach(function (s, i) { text(pg, 46.4, tcTop + 10.5 * i, s, 8); });
        }
      }
      text(pg, 562.8, 801.0, String(index + 1), 6, false, PAGE_NO, "right");
      finishPage(pg);
      pages.push(pg);
    });

    fit();
    document.body.setAttribute("data-state", "rendered");
    document.body.setAttribute("data-pages", String(pages.length));
    return { ok: true, pages: pages.length };
  }

  // Screen preview only: scale the pages to the window width.
  function fit() {
    var z = Math.max(0.3, Math.min(2.5, (window.innerWidth - 28) / (PW * 4 / 3)));
    document.getElementById("pages").style.setProperty("--z", z.toFixed(4));
  }

  function post(msg) {
    if (window.chrome && window.chrome.webview) window.chrome.webview.postMessage(JSON.stringify(msg));
  }

  window.formater = {
    render: render,
    get fontsOk() { return fontsOk; },
  };

  window.addEventListener("resize", fit);

  function init() {
    measurer = document.getElementById("measure");
    Promise.all([document.fonts.load("400 12px U"), document.fonts.load("700 12px U")])
      .then(function (r) { return r.every(function (faces) { return faces.length > 0; }); },
            function () { return false; })
      .then(function (ok) {
        fontsOk = ok;
        if (!ok) {
          document.body.classList.add("font-error");
          document.body.setAttribute("data-state", "font-error");
          post({ type: "font-error" });
          return;
        }
        document.body.setAttribute("data-state", "ready");
        var el = document.getElementById("data");
        var json = el && el.textContent.trim();
        if (json) render(JSON.parse(json));
        post({ type: "ready" });
      });
  }

  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
  else init();
})();
