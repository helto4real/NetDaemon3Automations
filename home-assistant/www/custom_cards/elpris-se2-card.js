const MINUTE = 60 * 1000;
const QUARTER = 15 * MINUTE;
const HOUR = 60 * MINUTE;
const WINDOW = 24 * HOUR;
const timeZone = "Europe/Stockholm";
const svgNs = "http://www.w3.org/2000/svg";

const clock = new Intl.DateTimeFormat("sv-SE", { timeZone, hour: "2-digit", minute: "2-digit" });
const hourOnly = new Intl.DateTimeFormat("sv-SE", { timeZone, hour: "2-digit" });
const dateKey = new Intl.DateTimeFormat("en-CA", { timeZone, year: "numeric", month: "2-digit", day: "2-digit" });
const longDay = new Intl.DateTimeFormat("sv-SE", { timeZone, weekday: "long", day: "numeric", month: "short" });
const weekday = new Intl.DateTimeFormat("sv-SE", { timeZone, weekday: "long" });
const dayMonth = new Intl.DateTimeFormat("sv-SE", { timeZone, day: "numeric", month: "long" });
const shortDay = new Intl.DateTimeFormat("sv-SE", { timeZone, weekday: "short", day: "numeric", month: "short" });
const price = new Intl.NumberFormat("sv-SE", { minimumFractionDigits: 1, maximumFractionDigits: 1 });
const tick = new Intl.NumberFormat("sv-SE", { maximumFractionDigits: 1 });
const percent = new Intl.NumberFormat("sv-SE", { maximumFractionDigits: 0 });

const template = document.createElement("template");
template.innerHTML = `
  <style>
    :host {
      --helto-bg: #181825;
      --helto-border: #313244;
      --helto-border-strong: #45475a;
      --helto-text: #cdd6f4;
      --helto-text-dim: #a6adc8;
      --helto-text-faint: #7f849c;
      --helto-accent: #fab387;
      --helto-accent-strong: #fddcc4;
      --helto-focus: #89b4fa;
      --helto-focus-ring: 0 0 0 3px rgba(137, 180, 250, 0.28);
      --helto-ok: #a6e3a1;
      --helto-ok-border: #4f7050;
      --helto-ok-bg: #223423;
      --helto-danger: #f38ba8;
      --helto-radius-sm: 5px;
      --helto-radius: 6px;
      --helto-radius-lg: 10px;
      --helto-shadow-pop: 0 12px 32px rgba(0, 0, 0, 0.5);
      --helto-transition: 0.12s ease;
      --helto-font-sans: system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;

      /* Bar colours validated for colour-vision deficiency on the card surface. */
      --bar: #4a8fd0;
      --bar-cheap: #4fa860;
      --bar-selected: var(--helto-accent);
      --text: var(--primary-text-color, var(--helto-text));
      --text-dim: var(--secondary-text-color, var(--helto-text-dim));
      --text-faint: var(--helto-text-faint);
      --hairline: color-mix(in srgb, var(--text) 9%, transparent);
      --well: color-mix(in srgb, var(--text) 3.5%, transparent);

      display: block;
      margin-inline: auto;
      max-inline-size: 1120px;
      padding: clamp(0px, 2vw, 16px);
      box-sizing: border-box;
      color: var(--text);
      font: 14px/1.4 var(--helto-font-sans);
    }
    *, *::before, *::after { box-sizing: border-box; }
    [hidden] { display: none !important; }
    h1, h2, p, dl, dd, dt { margin: 0; }

    ha-card {
      display: block;
      overflow: hidden;
      border-radius: var(--ha-card-border-radius, var(--helto-radius-lg));
      background: var(--ha-card-background, var(--card-background-color, #1e1e2e));
      border: 1px solid var(--ha-card-border-color, var(--helto-border));
    }
    .section { padding: clamp(16px, 3.4vw, 28px); }
    .section + .section { border-top: 1px solid var(--hairline); }

    header { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
    h1 { font-size: 15px; font-weight: 600; letter-spacing: 0.01em; color: var(--text-dim); }
    h2 { font-size: 13px; font-weight: 600; color: var(--text-dim); }
    .pill {
      display: inline-flex; align-items: center; gap: 6px;
      height: 24px; padding-inline: 10px; border-radius: 999px;
      border: 1px solid var(--helto-ok-border); background: var(--helto-ok-bg);
      color: var(--helto-ok); font-size: 12px; font-weight: 600; white-space: nowrap;
    }
    .pill::before { content: ""; width: 7px; height: 7px; border-radius: 50%; background: currentColor; }

    .hero {
      display: grid;
      grid-template-columns: minmax(0, 1fr) minmax(260px, 380px);
      gap: 20px 32px;
      align-items: end;
      margin-top: 14px;
    }
    .now-value { display: flex; align-items: baseline; gap: 10px; flex-wrap: wrap; }
    .figure { font-size: clamp(48px, 9vw, 68px); font-weight: 600; line-height: 1; letter-spacing: -0.02em; }
    .unit { font-size: 16px; color: var(--text-dim); }
    .now-meta { display: flex; flex-wrap: wrap; align-items: center; gap: 4px 14px; margin-top: 10px; color: var(--text-dim); }
    .delta { display: inline-flex; align-items: center; gap: 4px; font-weight: 600; }
    .delta svg { width: 14px; height: 14px; fill: none; stroke: currentColor; stroke-width: 1.8; stroke-linecap: round; stroke-linejoin: round; }
    .delta.below { color: var(--helto-ok); }
    .delta.above { color: var(--helto-danger); }

    .next {
      position: relative;
      padding: 14px 16px 14px 20px;
      border-radius: var(--helto-radius);
      background: color-mix(in srgb, var(--bar-cheap) 10%, transparent);
      border: 1px solid color-mix(in srgb, var(--bar-cheap) 28%, transparent);
    }
    .next::before {
      content: ""; position: absolute; inset-block: 12px; inset-inline-start: 8px;
      width: 3px; border-radius: 2px; background: var(--bar-cheap);
    }
    .next.none { background: var(--well); border-color: var(--hairline); }
    .next.none::before { background: var(--text-faint); }
    .next dt { font-size: 12px; color: var(--text-dim); }
    .next .when { font-size: 22px; font-weight: 600; margin-top: 2px; }
    .next .detail { color: var(--text-dim); margin-top: 2px; }

    .stats {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: 1px;
      margin-top: 24px;
      border-radius: var(--helto-radius);
      overflow: hidden;
      background: var(--hairline);
    }
    .stats > div { padding: 12px 14px; background: var(--ha-card-background, var(--card-background-color, #1e1e2e)); }
    .stats > div > * { display: block; }
    .stats dt { font-size: 12px; color: var(--text-dim); }
    .stats dd { font-size: 20px; font-weight: 600; margin-top: 2px; }
    .stats .sub { font-size: 12px; color: var(--text-faint); font-weight: 400; }
    .stats .unit-small { font-size: 13px; color: var(--text-dim); font-weight: 400; }

    .chart-head { display: flex; flex-wrap: wrap; align-items: baseline; justify-content: space-between; gap: 6px 16px; margin-bottom: 14px; }
    .legend { display: flex; flex-wrap: wrap; gap: 4px 16px; color: var(--text-dim); font-size: 12px; }
    .legend span { display: inline-flex; align-items: center; gap: 6px; }
    .legend i { width: 10px; height: 10px; border-radius: 2px; background: var(--bar); }
    .legend .cheap { background: var(--bar-cheap); }
    .legend .missing { background: var(--well); outline: 1px solid var(--hairline); }

    .chart { position: relative; }
    svg.plot {
      display: block; width: 100%; height: clamp(210px, 32vw, 300px);
      overflow: visible; touch-action: pan-y; cursor: crosshair;
      border-radius: var(--helto-radius);
      user-select: none; -webkit-user-select: none;
    }
    svg.plot:focus { outline: none; }
    svg.plot:focus-visible { box-shadow: var(--helto-focus-ring); outline: 1px solid var(--helto-focus); }
    .grid line { stroke: var(--hairline); stroke-width: 1; shape-rendering: crispEdges; }
    .grid line.zero { stroke: color-mix(in srgb, var(--text) 30%, transparent); }
    .axis text { fill: var(--text-faint); font-size: 11px; font-variant-numeric: tabular-nums; }
    .axis text.day { fill: var(--text-dim); font-weight: 600; }
    .band { fill: color-mix(in srgb, var(--bar-cheap) 11%, transparent); }
    .missing rect { fill: var(--well); }
    .missing text { fill: var(--text-faint); font-size: 12px; }
    .bars path { fill: var(--bar); transition: fill var(--helto-transition), opacity var(--helto-transition); }
    .bars path.cheap { fill: var(--bar-cheap); }
    .bars.dimmed path { opacity: 0.55; }
    .bars path.selected { fill: var(--bar-selected); opacity: 1; }
    .marker line { stroke-width: 1; shape-rendering: crispEdges; }
    .marker.now line { stroke: var(--text); }
    .marker.now circle { fill: var(--text); }
    .marker.midnight line { stroke: color-mix(in srgb, var(--text) 22%, transparent); }
    .marker.now text { fill: var(--text); font-size: 11px; font-weight: 600; }

    .tooltip {
      position: absolute; top: 0; left: 0; z-index: 1;
      padding: 8px 10px; min-width: 132px;
      border-radius: var(--helto-radius);
      background: var(--helto-bg);
      border: 1px solid var(--helto-border-strong);
      box-shadow: var(--helto-shadow-pop);
      pointer-events: none;
      white-space: nowrap;
    }
    .tooltip .value { font-size: 16px; font-weight: 600; color: var(--helto-text); }
    .tooltip .value small { font-size: 12px; font-weight: 400; color: var(--helto-text-dim); }
    .tooltip .time { font-size: 12px; color: var(--helto-text-dim); }
    .tooltip .tag { display: flex; align-items: center; gap: 6px; margin-top: 4px; font-size: 12px; color: var(--helto-text-dim); }
    .tooltip .tag::before { content: ""; width: 12px; height: 2px; border-radius: 1px; background: var(--bar-cheap); }

    .periods { display: grid; gap: 6px; margin-top: 10px; padding: 0; list-style: none; }
    .periods li {
      display: grid; grid-template-columns: auto minmax(0, 1fr) auto auto; align-items: center; gap: 14px;
      min-height: 40px; padding: 8px 12px; border-radius: var(--helto-radius);
      background: var(--well); border: 1px solid transparent;
      cursor: default; transition: border-color var(--helto-transition), background var(--helto-transition);
    }
    .periods li:hover, .periods li.active { border-color: color-mix(in srgb, var(--bar-cheap) 45%, transparent); }
    .periods li::before { content: ""; width: 3px; height: 20px; border-radius: 2px; background: var(--bar-cheap); }
    .periods .range { font-weight: 600; }
    .periods .length, .periods .avg { color: var(--text-dim); font-variant-numeric: tabular-nums; text-align: end; }
    .periods .empty-row { display: block; color: var(--text-dim); cursor: auto; }
    .periods .empty-row::before { display: none; }
    .periods .live { color: var(--helto-ok); font-weight: 600; margin-inline-start: 8px; font-size: 12px; }

    details { margin-top: 18px; }
    summary {
      display: inline-flex; align-items: center; gap: 6px; cursor: pointer;
      height: 24px; padding-inline: 10px; border-radius: var(--helto-radius-sm);
      border: 1px solid var(--helto-border-strong);
      background: linear-gradient(180deg, #45475a, #313244);
      color: var(--helto-text); font-size: 12px; list-style: none;
      transition: border-color var(--helto-transition);
    }
    summary::-webkit-details-marker { display: none; }
    summary:hover { border-color: #6c7086; }
    summary:focus-visible { outline: none; border-color: var(--helto-focus); box-shadow: var(--helto-focus-ring); }
    summary svg { width: 12px; height: 12px; fill: none; stroke: currentColor; stroke-width: 1.8; stroke-linecap: round; stroke-linejoin: round; transition: transform var(--helto-transition); }
    details[open] summary svg { transform: rotate(90deg); }
    table { width: 100%; margin-top: 12px; border-collapse: collapse; font-variant-numeric: tabular-nums; font-size: 13px; }
    th { text-align: end; font-weight: 600; font-size: 12px; color: var(--text-dim); padding: 6px 10px; border-bottom: 1px solid var(--hairline); }
    td { text-align: end; padding: 5px 10px; border-bottom: 1px solid var(--hairline); }
    th:first-child, td:first-child { text-align: start; }
    tr.day th { text-align: start; padding-top: 14px; color: var(--text); font-size: 13px; }
    .cheap-key { display: inline-flex; align-items: center; gap: 6px; vertical-align: top; height: 1.4em; margin-inline-start: 12px; font-size: 12px; color: var(--text-dim); }
    .cheap-key::before { content: ""; width: 12px; height: 2px; border-radius: 1px; background: var(--bar-cheap); }

    .empty-state { padding: 40px 0 8px; color: var(--text-dim); }

    @media (max-width: 720px) {
      .hero { grid-template-columns: 1fr; }
      .stats { grid-template-columns: repeat(2, minmax(0, 1fr)); }
      .periods li { grid-template-columns: auto minmax(0, 1fr) auto; }
      .periods .length { display: none; }
    }
    @media (prefers-reduced-motion: reduce) {
      * { transition: none !important; }
    }
  </style>
  <ha-card>
    <div class="section">
      <header>
        <h1>Elpris SE2</h1>
        <span class="pill" hidden></span>
      </header>
      <p class="empty-state" hidden>Inväntar kvartpriser från NetDaemon.</p>
      <div class="content" hidden>
        <div class="hero">
          <div>
            <div class="now-value"><span class="figure"></span><span class="unit">öre/kWh</span></div>
            <p class="now-meta"><span class="now-range"></span><span class="delta"></span></p>
          </div>
          <dl class="next"><dt></dt><dd class="when"></dd><dd class="detail"></dd></dl>
        </div>
        <dl class="stats">
          <div><dt>Lägst</dt><dd class="low"></dd><dd class="sub low-at"></dd></div>
          <div><dt>Högst</dt><dd class="high"></dd><dd class="sub high-at"></dd></div>
          <div><dt>Snitt 24 h</dt><dd class="avg"></dd><dd class="sub">spot exkl. moms</dd></div>
          <div><dt>Priser t.o.m.</dt><dd class="until"></dd><dd class="sub until-day"></dd></div>
        </dl>
      </div>
    </div>
    <div class="section content" hidden>
      <div class="chart-head">
        <h2>Kommande 24 timmar</h2>
        <div class="legend">
          <span><i></i>Kvartpris</span>
          <span><i class="cheap"></i>Billig period</span>
          <span class="legend-missing" hidden><i class="missing"></i>Ej publicerat</span>
        </div>
      </div>
      <div class="chart">
        <svg class="plot" tabindex="0" role="slider" aria-label="Kvartpriser kommande 24 timmar"></svg>
        <div class="tooltip" hidden>
          <div class="value"></div>
          <div class="time"></div>
          <div class="tag" hidden>Billig period</div>
        </div>
      </div>
    </div>
    <div class="section content" hidden>
      <h2>Billiga perioder</h2>
      <ul class="periods"></ul>
      <details>
        <summary><svg viewBox="0 0 12 12" aria-hidden="true"><path d="M4 2.5 7.5 6 4 9.5"/></svg>Timpriser</summary>
        <table>
          <thead><tr><th>Timme</th><th>Snitt</th><th>Lägst</th><th>Högst</th></tr></thead>
          <tbody></tbody>
        </table>
      </details>
    </div>
  </ha-card>
`;

const arrowDown = "M7 2.5v9M3 7.5l4 4 4-4";
const arrowUp = "M7 11.5v-9M3 6.5l4-4 4 4";

function parseInterval(value) {
  const start = Date.parse(value?.start);
  const end = Date.parse(value?.end);
  return Number.isFinite(start) && end > start ? { start, end } : null;
}

function svg(name, attributes = {}, parent) {
  const element = document.createElementNS(svgNs, name);
  for (const [key, value] of Object.entries(attributes)) element.setAttribute(key, String(value));
  parent?.append(element);
  return element;
}

function dayPrefix(instant, now) {
  const offset = (Date.parse(dateKey.format(instant)) - Date.parse(dateKey.format(now))) / (24 * HOUR);
  if (offset === 0) return "";
  if (offset === 1) return "imorgon ";
  return shortDay.format(instant) + " ";
}

function range(start, end, now) {
  return `${dayPrefix(start, now)}${clock.format(start)}–${clock.format(end)}`;
}

function duration(ms) {
  const minutes = Math.round(ms / MINUTE);
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  if (!hours) return `${rest} min`;
  return rest ? `${hours} h ${rest} min` : `${hours} h`;
}

function niceTicks(low, high) {
  const roughStep = (high - low || 4) / 4;
  const magnitude = 10 ** Math.floor(Math.log10(roughStep));
  const step = [1, 2, 2.5, 5, 10].map(value => value * magnitude).find(value => value >= roughStep);
  const bottom = Math.floor(low / step);
  const top = Math.max(bottom + 1, Math.ceil(high / step));
  return Array.from({ length: top - bottom + 1 }, (_, index) => (bottom + index) * step);
}

function average(quarters) {
  return quarters.reduce((sum, quarter) => sum + quarter.price, 0) / quarters.length;
}

function barPath(x, width, from, to) {
  const height = Math.abs(to - from);
  const radius = Math.min(3, width / 2, height);
  const up = to < from;
  const tip = to;
  const curve = up ? radius : -radius;
  return [
    `M${x},${from}`,
    `V${tip + curve}`,
    `Q${x},${tip} ${x + radius},${tip}`,
    `H${x + width - radius}`,
    `Q${x + width},${tip} ${x + width},${tip + curve}`,
    `V${from}`,
    "Z",
  ].join("");
}

class ElprisSe2Card extends HTMLElement {
  #entity = "sensor.nasta_billiga_elperiod";
  #hass;
  #attributes;
  #data;
  #layout;
  #selected = -1;
  #pinned = false;
  #hoverPeriod = -1;
  #timer;
  #resize;
  #view;

  constructor() {
    super();
    const root = this.attachShadow({ mode: "open" });
    root.append(template.content.cloneNode(true));
    const pick = selector => root.querySelector(selector);
    this.#view = {
      root,
      pill: pick(".pill"),
      empty: pick(".empty-state"),
      content: [...root.querySelectorAll(".content")],
      figure: pick(".figure"),
      nowRange: pick(".now-range"),
      delta: pick(".delta"),
      next: pick(".next"),
      low: pick(".low"), lowAt: pick(".low-at"),
      high: pick(".high"), highAt: pick(".high-at"),
      avg: pick(".avg"),
      until: pick(".until"), untilDay: pick(".until-day"),
      legendMissing: pick(".legend-missing"),
      plot: pick("svg.plot"),
      tooltip: pick(".tooltip"),
      periods: pick(".periods"),
      table: pick("tbody"),
    };

    const plot = this.#view.plot;
    plot.addEventListener("pointermove", event => {
      if (event.pointerType === "mouse" || this.#pinned) this.#hover(event);
    });
    plot.addEventListener("pointerdown", event => {
      this.#pinned = event.pointerType !== "mouse";
      this.#hover(event);
    });
    plot.addEventListener("pointerleave", event => {
      if (event.pointerType === "mouse" && !this.#pinned) this.#select(-1);
    });
    plot.addEventListener("focus", () => {
      if (this.#selected < 0) this.#select(this.#data?.currentIndex ?? 0);
    });
    plot.addEventListener("blur", () => this.#select(-1));
    plot.addEventListener("keydown", event => this.#key(event));
    root.addEventListener("pointerdown", event => {
      if (!event.composedPath().includes(plot) && this.#pinned) {
        this.#pinned = false;
        this.#select(-1);
      }
    });
  }

  setConfig(config) {
    this.#entity = config?.entity ?? "sensor.nasta_billiga_elperiod";
    this.#attributes = undefined;
    this.#refresh();
  }

  set hass(hass) {
    this.#hass = hass;
    const attributes = hass?.states?.[this.#entity]?.attributes;
    if (attributes !== this.#attributes) {
      this.#attributes = attributes;
      this.#refresh();
    }
  }

  getCardSize() {
    return 12;
  }

  connectedCallback() {
    this.#timer = setInterval(() => this.#refresh(), 30 * 1000);
    this.#resize = new ResizeObserver(() => this.#drawChart());
    this.#resize.observe(this.#view.plot);
    this.#refresh();
  }

  disconnectedCallback() {
    clearInterval(this.#timer);
    this.#resize?.disconnect();
  }

  #refresh() {
    if (!this.#hass) return;
    const now = Date.now();
    const start = Math.floor(now / QUARTER) * QUARTER;
    const end = start + WINDOW;
    const attributes = this.#attributes ?? {};

    const quarters = (attributes.prices_24h ?? []).flatMap(value => {
      const interval = parseInterval(value);
      const amount = value?.price_ore_per_kwh;
      return interval && Number.isFinite(amount) && interval.end > start && interval.start < end
        ? [{ ...interval, price: amount }]
        : [];
    }).sort((a, b) => a.start - b.start);
    const periods = (attributes.periods_24h ?? []).map(parseInterval)
      .filter(period => period && period.end > now && period.start < end)
      .sort((a, b) => a.start - b.start);
    for (const quarter of quarters) {
      quarter.cheap = periods.some(period => period.start < quarter.end && quarter.start < period.end);
    }

    const previousStart = this.#data?.quarters[this.#selected]?.start;
    this.#data = {
      now, start, end, quarters, periods,
      until: Date.parse(attributes.prices_until),
      currentIndex: Math.max(0, quarters.findIndex(quarter => quarter.start <= now && now < quarter.end)),
    };

    const hasData = quarters.length > 0;
    this.#view.empty.hidden = hasData;
    for (const element of this.#view.content) element.hidden = !hasData;
    if (!hasData) {
      this.#view.pill.hidden = true;
      return;
    }

    this.#renderSummary();
    this.#renderPeriods();
    this.#renderTable();
    this.#drawChart();
    const keep = quarters.findIndex(quarter => quarter.start === previousStart);
    this.#select(this.#selected >= 0 ? keep : -1);
  }

  #renderSummary() {
    const { now, quarters, periods, until } = this.#data;
    const view = this.#view;
    const current = quarters.find(quarter => quarter.start <= now && now < quarter.end);
    const mean = average(quarters);

    view.figure.textContent = current ? price.format(current.price) : "–";
    view.nowRange.textContent = current ? `Nu · ${clock.format(current.start)}–${clock.format(current.end)}` : "Inget pris för just nu";
    view.delta.replaceChildren();
    view.delta.className = "delta";
    if (current && Math.abs(mean) > 0.05) {
      const change = (current.price - mean) / Math.abs(mean);
      const below = change < 0;
      view.delta.classList.add(below ? "below" : "above");
      const icon = svg("svg", { viewBox: "0 0 14 14", "aria-hidden": "true" });
      svg("path", { d: below ? arrowDown : arrowUp }, icon);
      view.delta.append(icon, `${percent.format(Math.abs(change) * 100)} % ${below ? "under" : "över"} snittet`);
    }

    const active = periods.find(period => period.start <= now && now < period.end);
    const upcoming = periods.find(period => period.start > now);
    view.pill.hidden = !active;
    view.pill.textContent = active ? "Billigt nu" : "";

    const [label, when, detail] = view.next.children;
    const shown = active ?? upcoming;
    view.next.classList.toggle("none", !shown);
    if (shown) {
      const from = Math.max(shown.start, now);
      const inside = quarters.filter(quarter => quarter.start < shown.end && from < quarter.end);
      const meanInside = inside.length ? ` · snitt ${price.format(average(inside))} öre` : "";
      label.textContent = active ? "Billig period pågår" : `Nästa billiga period om ${duration(Math.ceil((shown.start - now) / MINUTE) * MINUTE)}`;
      when.textContent = active ? `Till ${clock.format(shown.end)}` : range(shown.start, shown.end, now);
      detail.textContent = `${duration(shown.end - from)}${active ? " kvar" : ""}${meanInside}`;
    } else {
      label.textContent = "Nästa billiga period";
      when.textContent = "Ingen inom 24 h";
      detail.textContent = "";
    }

    const low = quarters.reduce((best, quarter) => quarter.price < best.price ? quarter : best);
    const high = quarters.reduce((best, quarter) => quarter.price > best.price ? quarter : best);
    this.#stat(view.low, low.price);
    view.lowAt.textContent = range(low.start, low.end, now);
    this.#stat(view.high, high.price);
    view.highAt.textContent = range(high.start, high.end, now);
    this.#stat(view.avg, mean);
    if (until > now) {
      view.until.textContent = weekday.format(until - 1);
      view.untilDay.textContent = dayMonth.format(until - 1);
    } else {
      view.until.textContent = "Saknas";
      view.untilDay.textContent = "";
    }
  }

  #stat(element, value) {
    const unit = document.createElement("span");
    unit.className = "unit-small";
    unit.textContent = " öre";
    element.replaceChildren(price.format(value), unit);
  }

  #renderPeriods() {
    const { now, quarters, periods } = this.#data;
    const list = this.#view.periods;
    if (!periods.length) {
      const item = document.createElement("li");
      item.className = "empty-row";
      item.textContent = "Inga billiga perioder de kommande 24 timmarna.";
      list.replaceChildren(item);
      return;
    }

    list.replaceChildren(...periods.map((period, index) => {
      const item = document.createElement("li");
      const label = document.createElement("span");
      label.className = "range";
      label.textContent = range(Math.max(period.start, now), period.end, now);
      if (period.start <= now) {
        const live = document.createElement("span");
        live.className = "live";
        live.textContent = "pågår";
        label.append(live);
      }
      const length = document.createElement("span");
      length.className = "length";
      length.textContent = duration(period.end - Math.max(period.start, now));
      const avg = document.createElement("span");
      avg.className = "avg";
      const inside = quarters.filter(quarter => quarter.start < period.end && period.start < quarter.end);
      avg.textContent = inside.length ? `snitt ${price.format(average(inside))} öre` : "";
      item.append(label, length, avg);
      item.addEventListener("pointerenter", () => this.#highlightPeriod(index));
      item.addEventListener("pointerleave", () => this.#highlightPeriod(-1));
      return item;
    }));
  }

  #highlightPeriod(index) {
    this.#hoverPeriod = index;
    [...this.#view.periods.children].forEach((item, position) => item.classList.toggle("active", position === index));
    const bars = this.#layout?.bars;
    if (!bars) return;
    const period = this.#data.periods[index];
    bars.group.classList.toggle("dimmed", Boolean(period));
    bars.paths.forEach((path, position) => {
      const quarter = this.#data.quarters[position];
      path.style.opacity = period && quarter.start < period.end && period.start < quarter.end ? "1" : "";
    });
  }

  #renderTable() {
    const { quarters } = this.#data;
    const hours = new Map();
    for (const quarter of quarters) {
      const key = Math.floor(quarter.start / HOUR) * HOUR;
      if (!hours.has(key)) hours.set(key, []);
      hours.get(key).push(quarter);
    }
    let previousDay;
    const rows = [];
    for (const [hour, items] of hours) {
      const day = dateKey.format(hour);
      if (day !== previousDay) {
        const heading = document.createElement("tr");
        heading.className = "day";
        const cell = document.createElement("th");
        cell.colSpan = 4;
        cell.scope = "colgroup";
        const title = longDay.format(hour);
        cell.textContent = title.charAt(0).toUpperCase() + title.slice(1);
        heading.append(cell);
        rows.push(heading);
        previousDay = day;
      }
      const row = document.createElement("tr");
      const values = items.map(item => item.price);
      const cells = [`${hourOnly.format(hour)}–${hourOnly.format(hour + HOUR)}`, price.format(average(items)),
        price.format(Math.min(...values)), price.format(Math.max(...values))];
      row.append(...cells.map(text => {
        const cell = document.createElement("td");
        cell.textContent = text;
        return cell;
      }));
      if (items.some(item => item.cheap)) {
        const key = document.createElement("span");
        key.className = "cheap-key";
        key.textContent = "billig";
        row.firstChild.append(key);
      }
      rows.push(row);
    }
    this.#view.table.replaceChildren(...rows);
  }

  #drawChart() {
    const data = this.#data;
    const plot = this.#view.plot;
    if (!data?.quarters.length) return;
    const width = plot.clientWidth;
    const height = plot.clientHeight;
    if (!width || !height) return;

    const compact = width < 520;
    const margin = { top: 24, right: 4, bottom: 24, left: compact ? 30 : 40 };
    const innerWidth = width - margin.left - margin.right;
    const innerHeight = height - margin.top - margin.bottom;
    const { start, end, now, quarters, periods } = data;
    const values = quarters.map(quarter => quarter.price);
    const lowest = Math.min(0, ...values);
    const allTicks = niceTicks(lowest, Math.max(0, ...values));
    const high = allTicks.at(-1);
    const low = lowest < 0 ? Math.max(allTicks[0], lowest - (high - lowest) * 0.04) : 0;
    const ticks = allTicks.filter(value => value >= low);
    const x = instant => margin.left + (Math.max(start, Math.min(end, instant)) - start) / WINDOW * innerWidth;
    const y = value => margin.top + (high - value) / (high - low) * innerHeight;
    const slot = innerWidth / (WINDOW / QUARTER);
    const gap = slot >= 7 ? 2 : slot >= 3.5 ? 1 : 0.5;

    plot.replaceChildren();
    plot.setAttribute("viewBox", `0 0 ${width} ${height}`);

    const bands = svg("g", {}, plot);
    for (const period of periods) {
      svg("rect", { class: "band", x: x(period.start), y: margin.top, width: x(period.end) - x(period.start), height: innerHeight }, bands);
    }

    const lastEnd = quarters.at(-1).end;
    this.#view.legendMissing.hidden = lastEnd >= end;
    if (lastEnd < end) {
      const missing = svg("g", { class: "missing" }, plot);
      svg("rect", { x: x(lastEnd) + 1, y: margin.top, width: x(end) - x(lastEnd) - 1, height: innerHeight, rx: 4 }, missing);
      if (x(end) - x(lastEnd) > 300) {
        const label = svg("text", { x: (x(lastEnd) + x(end)) / 2, y: margin.top + innerHeight / 2, "text-anchor": "middle", "dominant-baseline": "middle" }, missing);
        label.textContent = Number(hourOnly.format(now)) < 13 ? "Morgondagens priser publiceras runt 13:00" : "Morgondagens priser är inte publicerade än";
      }
    }

    const grid = svg("g", { class: "grid" }, plot);
    const axis = svg("g", { class: "axis" }, plot);
    for (const value of ticks) {
      const position = Math.round(y(value)) + 0.5;
      const gridEnd = value === 0 ? width - margin.right : Math.min(width - margin.right, x(lastEnd));
      svg("line", { class: value === 0 ? "zero" : "", x1: margin.left, x2: gridEnd, y1: position, y2: position }, grid);
      const label = svg("text", { x: margin.left - 8, y: position, "text-anchor": "end", "dominant-baseline": "middle" }, axis);
      label.textContent = tick.format(value);
    }
    const unitLabel = svg("text", { x: margin.left - 8, y: 8, "text-anchor": "end", "dominant-baseline": "middle" }, axis);
    unitLabel.textContent = "öre";

    const every = compact ? 6 : 3;
    for (let hour = Math.ceil(start / HOUR) * HOUR; hour < end; hour += HOUR) {
      const local = Number(hourOnly.format(hour));
      if (local % every !== 0) continue;
      const position = x(hour);
      if (position - margin.left < 26) continue;
      const label = svg("text", { x: position, y: height - 6, "text-anchor": "middle" }, axis);
      label.textContent = `${hourOnly.format(hour)}:00`;
    }

    let midnight;
    for (let hour = Math.ceil(start / HOUR) * HOUR; hour < end; hour += HOUR) {
      if (hourOnly.format(hour) === "00" && hour > start) { midnight = hour; break; }
    }
    const dayLabel = (instant, position, anchor) => {
      const text = svg("text", { class: "day", x: position, y: 10, "text-anchor": anchor, "dominant-baseline": "middle" }, axis);
      text.textContent = compact ? shortDay.format(instant) : longDay.format(instant);
      return text;
    };
    if (midnight === undefined || x(midnight) - margin.left > (compact ? 90 : 150)) dayLabel(start, margin.left + 34, "start");
    if (midnight !== undefined) {
      const position = Math.round(x(midnight)) + 0.5;
      const marker = svg("g", { class: "marker midnight" }, plot);
      svg("line", { x1: position, x2: position, y1: margin.top - 6, y2: margin.top + innerHeight }, marker);
      dayLabel(midnight, position + 6, "start");
    }

    const zero = y(0);
    const group = svg("g", { class: "bars" }, plot);
    const paths = quarters.map(quarter => {
      const left = x(quarter.start) + gap / 2;
      const barWidth = Math.max(0.5, x(quarter.end) - x(quarter.start) - gap);
      let tip = y(quarter.price);
      if (Math.abs(zero - tip) < 1.5) tip = zero - (quarter.price < 0 ? -1.5 : 1.5);
      return svg("path", { d: barPath(left, barWidth, zero, tip), class: quarter.cheap ? "cheap" : "" }, group);
    });

    const nowX = Math.round(x(now)) + 0.5;
    const nowMarker = svg("g", { class: "marker now" }, plot);
    svg("line", { x1: nowX, x2: nowX, y1: margin.top - 6, y2: margin.top + innerHeight }, nowMarker);
    svg("circle", { cx: nowX, cy: margin.top - 6, r: 3 }, nowMarker);
    const nowLabel = svg("text", { x: nowX + 7, y: 10, "dominant-baseline": "middle" }, nowMarker);
    nowLabel.textContent = "Nu";

    this.#layout = { x, y, zero, margin, width, innerWidth, bars: { group, paths } };
    plot.setAttribute("aria-valuemin", "1");
    plot.setAttribute("aria-valuemax", String(quarters.length));
    if (this.#selected >= 0) this.#select(this.#selected);
    if (this.#hoverPeriod >= 0) this.#highlightPeriod(this.#hoverPeriod);
  }

  #hover(event) {
    const layout = this.#layout;
    if (!layout) return;
    const bounds = this.#view.plot.getBoundingClientRect();
    const offset = (event.clientX - bounds.left) * (layout.width / bounds.width);
    const instant = this.#data.start + (offset - layout.margin.left) / layout.innerWidth * WINDOW;
    const quarters = this.#data.quarters;
    let index = quarters.findIndex(quarter => quarter.start <= instant && instant < quarter.end);
    if (index < 0) index = instant < quarters[0].start ? 0 : -1;
    if (index !== this.#selected) this.#select(index);
  }

  #select(index) {
    const view = this.#view;
    const paths = this.#layout?.bars.paths ?? [];
    paths[this.#selected]?.classList.remove("selected");
    this.#selected = index;
    const quarter = this.#data?.quarters[index];
    if (!quarter || !this.#layout) {
      view.tooltip.hidden = true;
      view.plot.removeAttribute("aria-valuenow");
      view.plot.removeAttribute("aria-valuetext");
      return;
    }

    paths[index]?.classList.add("selected");
    const [value, time, tag] = view.tooltip.children;
    const unit = document.createElement("small");
    unit.textContent = " öre/kWh";
    value.replaceChildren(price.format(quarter.price), unit);
    time.textContent = range(quarter.start, quarter.end, this.#data.now);
    tag.hidden = !quarter.cheap;
    view.tooltip.hidden = false;

    const { x, y, zero } = this.#layout;
    const bounds = view.plot.getBoundingClientRect();
    const scale = bounds.width / this.#layout.width || 1;
    const center = (x(quarter.start) + x(quarter.end)) / 2 * scale;
    const top = Math.min(y(quarter.price), zero) * scale;
    const tipWidth = view.tooltip.offsetWidth;
    const tipHeight = view.tooltip.offsetHeight;
    const left = Math.max(0, Math.min(bounds.width - tipWidth, center - tipWidth / 2));
    const above = top - tipHeight - 10;
    view.tooltip.style.transform = `translate(${left}px, ${Math.max(0, above)}px)`;

    const label = `${time.textContent}, ${price.format(quarter.price)} öre per kWh${quarter.cheap ? ", billig period" : ""}`;
    view.plot.setAttribute("aria-valuenow", String(index + 1));
    view.plot.setAttribute("aria-valuetext", label);
  }

  #key(event) {
    const last = (this.#data?.quarters.length ?? 0) - 1;
    if (last < 0) return;
    const current = this.#selected < 0 ? this.#data.currentIndex : this.#selected;
    const moves = {
      ArrowLeft: current - 1, ArrowDown: current - 1,
      ArrowRight: current + 1, ArrowUp: current + 1,
      PageDown: current - 4, PageUp: current + 4,
      Home: 0, End: last,
    };
    if (event.key === "Escape") {
      this.#select(-1);
      return;
    }
    if (!Object.hasOwn(moves, event.key)) return;
    event.preventDefault();
    this.#select(Math.max(0, Math.min(last, moves[event.key])));
  }
}

if (!customElements.get("elpris-se2-card")) customElements.define("elpris-se2-card", ElprisSe2Card);

window.customCards = window.customCards ?? [];
if (!window.customCards.some(card => card.type === "elpris-se2-card")) {
  window.customCards.push({ type: "elpris-se2-card", name: "Elpris SE2", description: "Kvartpriser och billiga perioder i SE2" });
}
