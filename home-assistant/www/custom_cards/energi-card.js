const MINUTE = 60 * 1000;
const HOUR = 60 * MINUTE;
const timeZone = "Europe/Stockholm";
const svgNs = "http://www.w3.org/2000/svg";
const prefix = "sensor.solis_inverter_ballstavagen_78_";
const idleWatts = 30;

const defaults = {
  pv_power: `${prefix}inverter_dc_power`,
  battery_power: `${prefix}battery_power`,
  battery_soc: `${prefix}battery_soc`,
  grid_power: `${prefix}grid_active_power`,
  home_power: `${prefix}home_load_power`,
  pv_today: `${prefix}inverter_generation_today`,
  home_today: `${prefix}home_load_today`,
  grid_import_today: `${prefix}grid_import_today`,
  grid_export_today: `${prefix}grid_export_today`,
  battery_charge_today: `${prefix}battery_charge_today`,
  battery_discharge_today: `${prefix}battery_discharge_today`,
  price: "sensor.nasta_billiga_elperiod",
  sun: "sun.sun",
  // Signs: battery_power > 0 means charging, grid_power > 0 means exporting.
  invert_battery: false,
  invert_grid: false,
  day_image: "/local/energy/house-day.webp",
  night_image: "/local/energy/house-night.webp",
  price_path: "/elpris-se2/priser",
};

// Flow routes in the background image's pixel space (1664 × 936).
const routes = {
  grid: "M207 214 Q380 345 550 376 L575 386 L1246 461 Q1248 470 1248 516",
  pv: "M1040 300 C1150 318 1276 352 1276 404 L1276 516",
  battery: "M1266 598 L1266 652 Q1266 668 1282 668 L1312 668",
  home: "M1228 560 C1180 560 1120 575 1062 590",
};
const flowColors = { pv: "#f0b85a", battery: "#5fd6bd", grid: "#9ab0ff", home: "#ffffff" };

const clock = new Intl.DateTimeFormat("sv-SE", { timeZone, hour: "2-digit", minute: "2-digit" });
const hourOnly = new Intl.DateTimeFormat("sv-SE", { timeZone, hour: "2-digit" });
const dateKey = new Intl.DateTimeFormat("en-CA", { timeZone, year: "numeric", month: "2-digit", day: "2-digit" });
const one = new Intl.NumberFormat("sv-SE", { minimumFractionDigits: 1, maximumFractionDigits: 1 });
const whole = new Intl.NumberFormat("sv-SE", { maximumFractionDigits: 0 });
const axis = new Intl.NumberFormat("sv-SE", { maximumFractionDigits: 1 });

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
      --helto-focus: #89b4fa;
      --helto-focus-ring: 0 0 0 3px rgba(137, 180, 250, 0.28);
      --helto-ok: #a6e3a1;
      --helto-ok-border: #4f7050;
      --helto-ok-bg: #223423;
      --helto-info: #74c7ec;
      --helto-info-border: #3e6478;
      --helto-info-bg: #16303d;
      --helto-radius: 6px;
      --helto-radius-lg: 10px;
      --helto-shadow-pop: 0 12px 32px rgba(0, 0, 0, 0.5);
      --helto-transition: 0.12s ease;
      --helto-font-sans: system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;

      /* Series colours validated together for colour-vision deficiency on the card surface. */
      --pv: #bd8520;
      --battery: #2fa38c;
      --grid: #6a86e0;
      --home: #c9ced6;
      --text: var(--primary-text-color, var(--helto-text));
      --text-dim: var(--secondary-text-color, var(--helto-text-dim));
      --text-faint: var(--helto-text-faint);
      --surface: var(--ha-card-background, var(--card-background-color, #1e1e2e));
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
      background: var(--surface);
      border: 1px solid var(--ha-card-border-color, var(--helto-border));
    }
    .section { padding: clamp(16px, 3.4vw, 28px); }
    .section + .section { border-top: 1px solid var(--hairline); }
    header { display: flex; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 14px; }
    h1 { font-size: 15px; font-weight: 600; color: var(--text-dim); }
    h2 { font-size: 13px; font-weight: 600; color: var(--text-dim); margin-bottom: 10px; }
    .pill {
      display: inline-flex; align-items: center; gap: 6px;
      height: 24px; padding-inline: 10px; border-radius: 999px;
      font-size: 12px; font-weight: 600; white-space: nowrap;
      border: 1px solid var(--helto-ok-border); background: var(--helto-ok-bg); color: var(--helto-ok);
    }
    .pill.export { border-color: var(--helto-info-border); background: var(--helto-info-bg); color: var(--helto-info); }
    .pill::before { content: ""; width: 7px; height: 7px; border-radius: 50%; background: currentColor; }

    .stage {
      position: relative;
      container-type: inline-size;
      aspect-ratio: 1664 / 936;
      border-radius: var(--helto-radius);
      overflow: hidden;
      background: #222123;
    }
    .stage img, .stage svg { position: absolute; inset: 0; width: 100%; height: 100%; display: block; }
    .stage img { object-fit: cover; transition: opacity 1.5s ease; }
    .stage img.night { opacity: 0; }
    .stage.is-night img.night { opacity: 1; }
    .routes path { fill: none; stroke-width: 4; stroke-linecap: round; stroke-linejoin: round; opacity: 0; transition: opacity 0.6s ease; }
    .routes path.active { opacity: 0.32; }
    .routes .pv { stroke: var(--pv); }
    .routes .battery { stroke: var(--battery); }
    .routes .grid { stroke: var(--grid); }
    .routes .home { stroke: var(--home); }

    .chip {
      position: absolute; transform: translate(-50%, -50%);
      display: flex; gap: 0.6em; padding: 0.45em 0.8em 0.45em 0.55em;
      font-size: clamp(9px, 1.25cqi, 14px);
      border-radius: 0.6em;
      background: rgba(17, 17, 27, 0.8);
      backdrop-filter: blur(6px);
      border: 1px solid rgba(255, 255, 255, 0.08);
      box-shadow: 0 6px 18px rgba(0, 0, 0, 0.45);
      white-space: nowrap;
      transition: opacity 0.4s ease;
    }
    .chip i { width: 0.3em; border-radius: 2px; }
    .chip small { display: block; font-size: 0.78em; color: #a6adc8; }
    .chip b { display: block; font-size: 1.12em; font-weight: 600; color: #f2f2f2; }
    .chip.quiet b { color: #a6adc8; font-weight: 500; }
    .soc { width: 5em; height: 0.36em; border-radius: 1em; margin-top: 0.35em; background: rgba(255, 255, 255, 0.12); overflow: hidden; }
    .soc div { height: 100%; border-radius: 1em; background: var(--battery); transition: width 0.6s ease; }

    .mix-bar { display: flex; gap: 2px; height: 28px; }
    .mix-bar div {
      display: flex; align-items: center; padding-inline: 8px; min-width: 0;
      border-radius: 4px; font-size: 12px; font-weight: 600; color: #fff; overflow: hidden;
    }
    .legend { display: flex; flex-wrap: wrap; gap: 4px 16px; margin-top: 8px; color: var(--text-dim); font-size: 12px; }
    .legend span { display: inline-flex; align-items: center; gap: 6px; }
    .legend .swatch { width: 10px; height: 10px; border-radius: 2px; }
    .legend .line { width: 14px; height: 2px; border-radius: 1px; }

    .stats {
      display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 1px; margin-top: 22px;
      border-radius: var(--helto-radius); overflow: hidden; background: var(--hairline);
    }
    .stats > div { padding: 12px 14px; background: var(--surface); }
    .stats dt { font-size: 12px; color: var(--text-dim); }
    .stats dd { font-size: 20px; font-weight: 600; margin-top: 2px; }
    .stats .unit { font-size: 13px; font-weight: 400; color: var(--text-dim); }
    .stats .sub { font-size: 12px; font-weight: 400; color: var(--text-faint); }

    .chart-head { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: baseline; gap: 6px 16px; margin-bottom: 12px; }
    .chart-head h2 { margin: 0; }
    .chart-head .legend { margin: 0; }
    .chart { position: relative; }
    svg.plot {
      display: block; width: 100%; height: clamp(190px, 28vw, 260px); overflow: visible;
      touch-action: pan-y; cursor: crosshair; border-radius: var(--helto-radius);
    }
    svg.plot:focus { outline: none; }
    svg.plot:focus-visible { box-shadow: var(--helto-focus-ring); outline: 1px solid var(--helto-focus); }
    .grid-lines line { stroke: var(--hairline); shape-rendering: crispEdges; }
    .grid-lines line.zero { stroke: color-mix(in srgb, var(--text) 30%, transparent); }
    .axis text { fill: var(--text-faint); font-size: 11px; font-variant-numeric: tabular-nums; }
    .series path { fill: none; stroke-width: 2; stroke-linejoin: round; stroke-linecap: round; }
    .series .area { stroke: none; fill: color-mix(in srgb, var(--pv) 14%, transparent); }
    .now line { stroke: var(--text); stroke-width: 1; shape-rendering: crispEdges; opacity: 0.6; }
    .cursor line { stroke: var(--text); stroke-width: 1; opacity: 0.45; shape-rendering: crispEdges; }
    .cursor circle { stroke: var(--surface); stroke-width: 2; }
    .chart-empty { padding: 32px 0; color: var(--text-dim); }

    .tooltip {
      position: absolute; top: 0; left: 0; z-index: 1; min-width: 170px;
      padding: 8px 10px; border-radius: var(--helto-radius);
      background: var(--helto-bg); border: 1px solid var(--helto-border-strong);
      box-shadow: var(--helto-shadow-pop); pointer-events: none; white-space: nowrap;
    }
    .tooltip .time { font-size: 12px; color: var(--helto-text-dim); margin-bottom: 4px; }
    .tooltip .row { display: grid; grid-template-columns: 14px auto 1fr; align-items: center; column-gap: 8px; row-gap: 2px; padding-block: 1px; font-size: 12px; color: var(--helto-text-dim); }
    .tooltip .row i { height: 2px; border-radius: 1px; }
    .tooltip .row b { justify-self: end; padding-inline-start: 14px; color: var(--helto-text); font-weight: 600; font-size: 13px; font-variant-numeric: tabular-nums; }

    .price {
      display: flex; flex-wrap: wrap; align-items: center; gap: 6px 28px;
      padding: 12px 14px; border-radius: var(--helto-radius); background: var(--well);
      border: 1px solid transparent; color: inherit; text-decoration: none; cursor: pointer;
      transition: border-color var(--helto-transition);
    }
    .price:hover { border-color: var(--helto-border-strong); }
    .price:focus-visible { outline: none; border-color: var(--helto-focus); box-shadow: var(--helto-focus-ring); }
    .price dt { font-size: 12px; color: var(--text-dim); }
    .price dd { font-size: 16px; font-weight: 600; }
    .price dd .unit { font-size: 12px; font-weight: 400; color: var(--text-dim); }
    .price .go { margin-inline-start: auto; color: var(--text-dim); display: inline-flex; }
    .price .go svg { width: 16px; height: 16px; fill: none; stroke: currentColor; stroke-width: 1.8; stroke-linecap: round; stroke-linejoin: round; }

    @media (max-width: 720px) {
      .stats { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    }
    @media (prefers-reduced-motion: reduce) {
      .routes path.active { opacity: 0.7; }
      .dots { display: none; }
      * { transition: none !important; }
    }
  </style>
  <ha-card>
    <div class="section">
      <header>
        <h1>Energi</h1>
        <span class="pill" hidden></span>
      </header>
      <div class="stage">
        <img class="day" alt="">
        <img class="night" alt="">
        <svg viewBox="0 0 1664 936" preserveAspectRatio="xMidYMid slice" aria-hidden="true">
          <defs>
            <filter id="glow" x="-50%" y="-50%" width="200%" height="200%">
              <feGaussianBlur stdDeviation="5" result="blur"/>
              <feMerge><feMergeNode in="blur"/><feMergeNode in="SourceGraphic"/></feMerge>
            </filter>
          </defs>
          <g class="routes"></g>
          <g class="dots" filter="url(#glow)"></g>
        </svg>
        <div class="chip" data-node="grid" style="left: 14%; top: 11%"><i style="background: var(--grid)"></i><div><small>Elnät</small><b></b></div></div>
        <div class="chip" data-node="pv" style="left: 74%; top: 11%"><i style="background: var(--pv)"></i><div><small>Sol</small><b></b></div></div>
        <div class="chip" data-node="home" style="left: 50%; top: 90%"><i style="background: var(--home)"></i><div><small>Huset</small><b></b></div></div>
        <div class="chip" data-node="battery" style="left: 86%; top: 89%"><i style="background: var(--battery)"></i><div><small></small><b></b><div class="soc"><div></div></div></div></div>
      </div>
    </div>
    <div class="section">
      <h2 class="mix-title">Varifrån kom husets el idag</h2>
      <div class="mix-bar" role="img"></div>
      <div class="legend">
        <span><i class="swatch" style="background: var(--pv)"></i>Sol direkt</span>
        <span><i class="swatch" style="background: var(--battery)"></i>Batteri</span>
        <span><i class="swatch" style="background: var(--grid)"></i>Nät</span>
      </div>
      <dl class="stats">
        <div><dt>Solproduktion</dt><dd class="pv-today"></dd><dd class="sub pv-peak"></dd></div>
        <div><dt>Självförsörjning</dt><dd class="self"></dd><dd class="sub">av husets förbrukning</dd></div>
        <div><dt>Köpt / sålt</dt><dd class="traded"></dd><dd class="sub net"></dd></div>
        <div><dt>Batteri</dt><dd class="cycled"></dd><dd class="sub">laddat / urladdat</dd></div>
      </dl>
    </div>
    <div class="section">
      <div class="chart-head">
        <h2>Idag</h2>
        <div class="legend">
          <span><i class="swatch" style="background: color-mix(in srgb, var(--pv) 45%, transparent); outline: 2px solid var(--pv); outline-offset: -2px"></i>Sol</span>
          <span><i class="line" style="background: var(--home)"></i>Förbrukning</span>
          <span><i class="line" style="background: var(--battery)"></i>Batteri, + laddar</span>
          <span><i class="line" style="background: var(--grid)"></i>Nät, + köper</span>
        </div>
      </div>
      <div class="chart">
        <svg class="plot" tabindex="0" role="slider" aria-label="Effekt under dagen"></svg>
        <p class="chart-empty" hidden>Ingen historik för idag än.</p>
        <div class="tooltip" hidden><div class="time"></div><div class="rows"></div></div>
      </div>
    </div>
    <div class="section price-section" hidden>
      <a class="price" tabindex="0" role="link">
        <dl><dt>Elpris nu</dt><dd class="price-now"></dd></dl>
        <dl><dt class="cheap-label"></dt><dd class="cheap-when"></dd></dl>
        <span class="go"><svg viewBox="0 0 16 16" aria-hidden="true"><path d="M6 3.5 10.5 8 6 12.5"/></svg></span>
      </a>
    </div>
  </ha-card>
`;

function svg(name, attributes = {}, parent) {
  const element = document.createElementNS(svgNs, name);
  for (const [key, value] of Object.entries(attributes)) element.setAttribute(key, String(value));
  parent?.append(element);
  return element;
}

function power(watts) {
  const magnitude = Math.abs(watts);
  return magnitude < 1000 ? `${whole.format(magnitude)} W` : `${one.format(magnitude / 1000)} kW`;
}

function niceTicks(low, high) {
  const roughStep = (high - low || 2) / 4;
  const magnitude = 10 ** Math.floor(Math.log10(roughStep));
  const step = [1, 2, 2.5, 5, 10].map(value => value * magnitude).find(value => value >= roughStep);
  const bottom = Math.floor(low / step);
  const top = Math.max(bottom + 1, Math.ceil(high / step));
  return Array.from({ length: top - bottom + 1 }, (_, index) => (bottom + index) * step);
}

function startOfDay(now) {
  // Midnight in the market time zone, robust to DST.
  const [year, month, day] = dateKey.format(now).split("-").map(Number);
  let guess = Date.UTC(year, month - 1, day);
  for (let i = 0; i < 2; i++) {
    const hour = Number(hourOnly.format(guess));
    const minute = Number(new Intl.DateTimeFormat("sv-SE", { timeZone, minute: "2-digit" }).format(guess));
    guess -= ((hour === 24 ? 0 : hour) * 60 + minute) * MINUTE;
  }
  return guess;
}

class EnergiCard extends HTMLElement {
  #config = { ...defaults };
  #hass;
  #view;
  #flowKeys = {};
  #history;
  #historyDay;
  #historyAt = 0;
  #loading = false;
  #layout;
  #cursor = -1;
  #pinned = false;
  #resize;
  #timer;

  constructor() {
    super();
    const root = this.attachShadow({ mode: "open" });
    root.append(template.content.cloneNode(true));
    const pick = selector => root.querySelector(selector);
    this.#view = {
      pill: pick(".pill"),
      stage: pick(".stage"),
      day: pick("img.day"),
      night: pick("img.night"),
      routes: pick(".routes"),
      dots: pick(".dots"),
      chips: Object.fromEntries([...root.querySelectorAll(".chip")].map(chip => [chip.dataset.node, chip])),
      mixTitle: pick(".mix-title"),
      mixBar: pick(".mix-bar"),
      pvToday: pick(".pv-today"), pvPeak: pick(".pv-peak"),
      self: pick(".self"),
      traded: pick(".traded"), net: pick(".net"),
      cycled: pick(".cycled"),
      plot: pick("svg.plot"),
      chartEmpty: pick(".chart-empty"),
      tooltip: pick(".tooltip"),
      priceSection: pick(".price-section"),
      priceLink: pick(".price"),
      priceNow: pick(".price-now"),
      cheapLabel: pick(".cheap-label"),
      cheapWhen: pick(".cheap-when"),
    };

    for (const [name, d] of Object.entries(routes)) {
      svg("path", { d, class: name, id: `route-${name}` }, this.#view.routes);
    }

    const plot = this.#view.plot;
    plot.addEventListener("pointermove", event => {
      if (event.pointerType === "mouse" || this.#pinned) this.#hover(event);
    });
    plot.addEventListener("pointerdown", event => {
      this.#pinned = event.pointerType !== "mouse";
      this.#hover(event);
    });
    plot.addEventListener("pointerleave", event => {
      if (event.pointerType === "mouse" && !this.#pinned) this.#showCursor(-1);
    });
    plot.addEventListener("focus", () => {
      if (this.#cursor < 0 && this.#history) this.#showCursor(this.#history.times.length - 1);
    });
    plot.addEventListener("blur", () => this.#showCursor(-1));
    plot.addEventListener("keydown", event => this.#key(event));
    root.addEventListener("pointerdown", event => {
      if (this.#pinned && !event.composedPath().includes(plot)) {
        this.#pinned = false;
        this.#showCursor(-1);
      }
    });

    const navigate = () => {
      history.pushState(null, "", this.#config.price_path);
      window.dispatchEvent(new CustomEvent("location-changed", { detail: { replace: false } }));
    };
    this.#view.priceLink.addEventListener("click", navigate);
    this.#view.priceLink.addEventListener("keydown", event => {
      if (event.key === "Enter" || event.key === " ") {
        event.preventDefault();
        navigate();
      }
    });
  }

  setConfig(config) {
    const merged = { ...defaults, ...(config ?? {}) };
    for (const key of ["pv_power", "battery_power", "grid_power", "home_power"]) {
      if (typeof merged[key] !== "string" || !merged[key].includes(".")) {
        throw new Error(`energi-card: '${key}' must be an entity id.`);
      }
    }
    this.#config = merged;
    this.#view.day.src = merged.day_image;
    this.#view.night.hidden = !merged.night_image;
    if (merged.night_image) this.#view.night.src = merged.night_image;
    this.#history = undefined;
    this.#render();
  }

  set hass(hass) {
    this.#hass = hass;
    this.#render();
  }

  getCardSize() {
    return 14;
  }

  connectedCallback() {
    this.#timer = setInterval(() => this.#loadHistory(), 60 * 1000);
    this.#resize = new ResizeObserver(() => this.#drawChart());
    this.#resize.observe(this.#view.plot);
    this.#render();
  }

  disconnectedCallback() {
    clearInterval(this.#timer);
    this.#resize?.disconnect();
  }

  #state(key) {
    return this.#hass?.states?.[this.#config[key]];
  }

  #number(key) {
    const value = Number.parseFloat(this.#state(key)?.state);
    return Number.isFinite(value) ? value : undefined;
  }

  #watts(key) {
    const entity = this.#state(key);
    const value = Number.parseFloat(entity?.state);
    if (!Number.isFinite(value)) return undefined;
    const unit = entity.attributes?.unit_of_measurement;
    return unit === "kW" ? value * 1000 : unit === "MW" ? value * 1e6 : value;
  }

  #kwh(key) {
    const entity = this.#state(key);
    const value = Number.parseFloat(entity?.state);
    if (!Number.isFinite(value)) return undefined;
    const unit = entity.attributes?.unit_of_measurement;
    return unit === "Wh" ? value / 1000 : unit === "MWh" ? value * 1000 : value;
  }

  #flows() {
    const sign = flag => (this.#config[flag] ? -1 : 1);
    const pv = Math.max(0, this.#watts("pv_power") ?? 0);
    const battery = (this.#watts("battery_power") ?? 0) * sign("invert_battery");
    const grid = -(this.#watts("grid_power") ?? 0) * sign("invert_grid");
    const home = Math.max(0, this.#watts("home_power") ?? 0);
    // battery > 0 charging, grid > 0 importing.
    return { pv, battery, grid, home };
  }

  #render() {
    if (!this.#hass || !this.#view) return;
    const flows = this.#flows();
    this.#renderStage(flows);
    this.#renderToday();
    this.#renderPrice();
    this.#loadHistory();
  }

  #renderStage({ pv, battery, grid, home }) {
    const view = this.#view;
    view.stage.classList.toggle("is-night", Boolean(this.#config.night_image) && this.#state("sun")?.state === "below_horizon");

    const setChip = (node, label, value, quiet) => {
      const chip = view.chips[node];
      if (label !== undefined) chip.querySelector("small").textContent = label;
      chip.querySelector("b").textContent = value;
      chip.classList.toggle("quiet", Boolean(quiet));
    };
    setChip("pv", undefined, pv > idleWatts ? power(pv) : "Ingen produktion", pv <= idleWatts);
    setChip("home", undefined, power(home));
    setChip("grid", undefined,
      grid > idleWatts ? `Köper ${power(grid)}` : grid < -idleWatts ? `Säljer ${power(grid)}` : "Ingen handel",
      Math.abs(grid) <= idleWatts);
    const soc = this.#number("battery_soc");
    setChip("battery", soc === undefined ? "Batteri" : `Batteri · ${whole.format(soc)} %`,
      battery > idleWatts ? `Laddar ${power(battery)}` : battery < -idleWatts ? `Ger ström ${power(battery)}` : "Vila",
      Math.abs(battery) <= idleWatts);
    view.chips.battery.querySelector(".soc div").style.width = `${Math.max(0, Math.min(100, soc ?? 0))}%`;

    const exporting = grid < -idleWatts;
    const selfPowered = !exporting && grid <= idleWatts;
    view.pill.hidden = !exporting && !selfPowered;
    view.pill.classList.toggle("export", exporting);
    view.pill.textContent = exporting ? "Säljer överskott" : selfPowered ? "Självförsörjande" : "";

    this.#flow("pv", pv, false);
    this.#flow("battery", battery, battery < 0);
    this.#flow("grid", grid, grid < 0);
    this.#flow("home", home, false);
  }

  #flow(name, watts, reverse) {
    const magnitude = Math.abs(watts);
    const active = magnitude > idleWatts;
    const kw = magnitude / 1000;
    const count = active ? Math.min(6, 2 + Math.round(kw * 1.2)) : 0;
    const duration = active ? Math.round(Math.max(1.4, 4.2 - kw * 0.5) * 5) / 5 : 0;
    const key = `${count}:${duration}:${reverse}`;
    this.#view.routes.querySelector(`.${name}`).classList.toggle("active", active);
    if (this.#flowKeys[name] === key) return;
    this.#flowKeys[name] = key;

    this.#view.dots.querySelectorAll(`[data-flow="${name}"]`).forEach(dot => dot.remove());
    for (let index = 0; index < count; index++) {
      const dot = svg("circle", { r: 6, fill: flowColors[name], "data-flow": name }, this.#view.dots);
      const motion = svg("animateMotion", {
        dur: `${duration}s`, repeatCount: "indefinite", begin: `${(-index * duration / count).toFixed(2)}s`,
        ...(reverse ? { keyPoints: "1;0", keyTimes: "0;1", calcMode: "linear" } : {}),
      }, dot);
      svg("mpath", { href: `#route-${name}` }, motion);
    }
  }

  #renderToday() {
    const view = this.#view;
    const home = this.#kwh("home_today");
    const gridIn = this.#kwh("grid_import_today");
    const gridOut = this.#kwh("grid_export_today");
    const charged = this.#kwh("battery_charge_today");
    const discharged = this.#kwh("battery_discharge_today");
    const pvToday = this.#kwh("pv_today");
    const kwhText = value => (value === undefined ? "–" : one.format(value));

    if (home > 0) {
      // The battery may be charged from the grid at night, so count what the house got from it first.
      // Grid imports serve the house before charging the battery; the remainder came straight from the sun.
      const fromBattery = Math.min(home, discharged ?? 0);
      const fromGrid = Math.min(home - fromBattery, gridIn ?? 0);
      const fromSun = Math.max(0, home - fromBattery - fromGrid);
      const parts = [["pv", fromSun, "Sol direkt"], ["battery", fromBattery, "Batteri"], ["grid", fromGrid, "Nät"]];
      view.mixTitle.textContent = `Varifrån kom husets el idag · ${one.format(home)} kWh`;
      view.mixBar.replaceChildren(...parts.filter(([, value]) => value > 0).map(([name, value]) => {
        const share = value / home;
        const block = document.createElement("div");
        block.style.flex = String(share);
        block.style.background = `var(--${name})`;
        block.title = `${parts.find(part => part[0] === name)[2]}: ${one.format(value)} kWh`;
        if (share >= 0.1) block.textContent = `${whole.format(share * 100)} %`;
        return block;
      }));
      view.mixBar.setAttribute("aria-label", parts.map(([, value, label]) => `${label} ${whole.format(value / home * 100)} %`).join(", "));
      view.self.replaceChildren(whole.format((1 - fromGrid / home) * 100), this.#unit(" %"));
    } else {
      view.mixTitle.textContent = "Varifrån kom husets el idag";
      view.mixBar.replaceChildren();
      view.self.textContent = "–";
    }

    view.pvToday.replaceChildren(kwhText(pvToday), this.#unit(" kWh"));
    view.traded.replaceChildren(kwhText(gridIn), this.#unit(` / ${kwhText(gridOut)} kWh`));
    if (gridIn !== undefined && gridOut !== undefined) {
      const net = gridOut - gridIn;
      view.net.textContent = `netto ${net >= 0 ? "+" : "−"}${one.format(Math.abs(net))} kWh`;
    } else {
      view.net.textContent = "";
    }
    view.cycled.replaceChildren(kwhText(charged), this.#unit(` / ${kwhText(discharged)} kWh`));
    const peak = this.#history?.peakPv;
    view.pvPeak.textContent = peak > 0 ? `topp ${one.format(peak)} kW` : "";
  }

  #unit(text) {
    const unit = document.createElement("span");
    unit.className = "unit";
    unit.textContent = text;
    return unit;
  }

  #renderPrice() {
    const view = this.#view;
    const attributes = this.#state("price")?.attributes;
    const now = Date.now();
    const quarters = attributes?.prices_24h ?? [];
    const current = quarters.find(quarter => Date.parse(quarter.start) <= now && now < Date.parse(quarter.end));
    view.priceSection.hidden = !current;
    if (!current) return;

    const unit = this.#unit(" öre/kWh");
    view.priceNow.replaceChildren(one.format(current.price_ore_per_kwh), unit);
    const periods = (attributes.periods_24h ?? [])
      .map(period => ({ start: Date.parse(period.start), end: Date.parse(period.end) }))
      .filter(period => period.end > now)
      .sort((a, b) => a.start - b.start);
    const active = periods.find(period => period.start <= now);
    const next = periods.find(period => period.start > now);
    if (active) {
      view.cheapLabel.textContent = "Billig period pågår";
      view.cheapWhen.textContent = `till ${clock.format(active.end)}`;
    } else if (next) {
      const tomorrow = dateKey.format(next.start) !== dateKey.format(now) ? "imorgon " : "";
      view.cheapLabel.textContent = "Nästa billiga period";
      view.cheapWhen.textContent = `${tomorrow}${clock.format(next.start)}–${clock.format(next.end)}`;
    } else {
      view.cheapLabel.textContent = "Nästa billiga period";
      view.cheapWhen.textContent = "ingen inom 24 h";
    }
  }

  async #loadHistory() {
    if (!this.#hass || this.#loading) return;
    const now = Date.now();
    const day = startOfDay(now);
    if (this.#historyDay === day && now - this.#historyAt < 5 * MINUTE) return;
    this.#loading = true;
    try {
      const ids = ["pv_power", "home_power", "battery_power", "grid_power"].map(key => this.#config[key]);
      const result = await this.#hass.callWS({
        type: "recorder/statistics_during_period",
        start_time: new Date(day).toISOString(),
        end_time: new Date(now).toISOString(),
        statistic_ids: ids,
        period: "5minute",
        types: ["mean"],
        units: { power: "kW" },
      });
      const series = ids.map(id => new Map((result?.[id] ?? []).map(point => {
        const start = typeof point.start === "number" ? point.start : Date.parse(point.start);
        return [start, point.mean];
      })));
      const times = [...new Set(series.flatMap(map => [...map.keys()]))].sort((a, b) => a - b);
      const sign = flag => (this.#config[flag] ? -1 : 1);
      const rows = times.map(time => {
        const [pv, home, battery, grid] = series.map(map => map.get(time));
        return {
          pv: pv === undefined ? undefined : Math.max(0, pv),
          home,
          battery: battery === undefined ? undefined : battery * sign("invert_battery"),
          grid: grid === undefined ? undefined : -grid * sign("invert_grid"),
        };
      });
      this.#history = {
        day, times, rows,
        peakPv: Math.max(0, ...rows.map(row => row.pv ?? 0)),
      };
      this.#historyDay = day;
      this.#historyAt = now;
      this.#drawChart();
      this.#renderToday();
    } catch (error) {
      console.warn("energi-card: could not load statistics", error);
      this.#historyAt = now;
    } finally {
      this.#loading = false;
    }
  }

  #drawChart() {
    const view = this.#view;
    const plot = view.plot;
    const history = this.#history;
    const hasData = Boolean(history?.times.length);
    view.chartEmpty.hidden = hasData || !history;
    plot.style.visibility = hasData ? "" : "hidden";
    if (!hasData) return;
    const width = plot.clientWidth;
    const height = plot.clientHeight;
    if (!width || !height) return;

    const compact = width < 520;
    const margin = { top: 22, right: 4, bottom: 24, left: compact ? 30 : 38 };
    const innerWidth = width - margin.left - margin.right;
    const innerHeight = height - margin.top - margin.bottom;
    const start = history.day;
    const end = start + 24 * HOUR;
    const values = history.rows.flatMap(row => [row.pv, row.home, row.battery, row.grid]).filter(Number.isFinite);
    const ticks = niceTicks(Math.min(0, ...values), Math.max(0.5, ...values));
    const low = ticks[0];
    const high = ticks.at(-1);
    const x = time => margin.left + (time - start) / (end - start) * innerWidth;
    const y = value => margin.top + (high - value) / (high - low) * innerHeight;
    const step = 5 * MINUTE;

    plot.replaceChildren();
    plot.setAttribute("viewBox", `0 0 ${width} ${height}`);
    const grid = svg("g", { class: "grid-lines" }, plot);
    const labels = svg("g", { class: "axis" }, plot);
    for (const value of ticks) {
      const position = Math.round(y(value)) + 0.5;
      svg("line", { class: value === 0 ? "zero" : "", x1: margin.left, x2: width - margin.right, y1: position, y2: position }, grid);
      const label = svg("text", { x: margin.left - 8, y: position, "text-anchor": "end", "dominant-baseline": "middle" }, labels);
      label.textContent = axis.format(value);
    }
    const unit = svg("text", { x: margin.left - 8, y: 0, "text-anchor": "end", "dominant-baseline": "hanging" }, labels);
    unit.textContent = "kW";
    for (let hour = 0; hour <= 24; hour += compact ? 6 : 3) {
      const label = svg("text", { x: x(start + hour * HOUR), y: height - 6, "text-anchor": hour === 0 ? "start" : hour === 24 ? "end" : "middle" }, labels);
      label.textContent = `${String(hour % 24).padStart(2, "0")}:00`;
    }

    const series = svg("g", { class: "series" }, plot);
    const line = key => {
      let d = "";
      let previous;
      history.times.forEach((time, index) => {
        const value = history.rows[index][key];
        if (!Number.isFinite(value)) { previous = undefined; return; }
        const gap = previous === undefined || time - previous > step * 3;
        d += `${gap ? "M" : "L"}${x(time + step / 2).toFixed(1)},${y(value).toFixed(1)}`;
        previous = time;
      });
      return d;
    };
    const pvLine = line("pv");
    if (pvLine) {
      const segments = pvLine.split("M").filter(Boolean).map(segment => {
        const points = segment.split("L");
        const first = points[0].split(",")[0];
        const last = points.at(-1).split(",")[0];
        return `M${first},${y(0)}L${segment}L${last},${y(0)}Z`;
      });
      svg("path", { class: "area", d: segments.join("") }, series);
    }
    const colors = { pv: "var(--pv)", home: "var(--home)", battery: "var(--battery)", grid: "var(--grid)" };
    for (const key of ["grid", "battery", "home", "pv"]) {
      const d = line(key);
      if (d) svg("path", { d, style: `stroke: ${colors[key]}` }, series);
    }

    const now = Math.min(end, Date.now());
    const nowGroup = svg("g", { class: "now" }, plot);
    const nowX = Math.round(x(now)) + 0.5;
    svg("line", { x1: nowX, x2: nowX, y1: margin.top, y2: margin.top + innerHeight }, nowGroup);

    this.#layout = { x, y, margin, width, innerWidth, start, end, step, top: margin.top, bottom: margin.top + innerHeight };
    plot.setAttribute("aria-valuemin", "1");
    plot.setAttribute("aria-valuemax", String(history.times.length));
    if (this.#cursor >= 0) this.#showCursor(Math.min(this.#cursor, history.times.length - 1));
  }

  #hover(event) {
    const layout = this.#layout;
    const history = this.#history;
    if (!layout || !history?.times.length) return;
    const bounds = this.#view.plot.getBoundingClientRect();
    const offset = (event.clientX - bounds.left) * (layout.width / bounds.width);
    const time = layout.start + (offset - layout.margin.left) / layout.innerWidth * (layout.end - layout.start);
    let best = 0;
    history.times.forEach((candidate, index) => {
      if (Math.abs(candidate + layout.step / 2 - time) < Math.abs(history.times[best] + layout.step / 2 - time)) best = index;
    });
    if (time > history.times.at(-1) + layout.step * 3) best = -1;
    if (best !== this.#cursor) this.#showCursor(best);
  }

  #showCursor(index) {
    const view = this.#view;
    this.#cursor = index;
    view.plot.querySelector(".cursor")?.remove();
    const history = this.#history;
    const layout = this.#layout;
    if (index < 0 || !history || !layout) {
      view.tooltip.hidden = true;
      view.plot.removeAttribute("aria-valuetext");
      return;
    }
    const time = history.times[index];
    const row = history.rows[index];
    const cx = layout.x(time + layout.step / 2);
    const cursor = svg("g", { class: "cursor" }, view.plot);
    svg("line", { x1: Math.round(cx) + 0.5, x2: Math.round(cx) + 0.5, y1: layout.top, y2: layout.bottom }, cursor);
    const entries = [
      ["pv", "Sol", row.pv, value => one.format(value) + " kW"],
      ["home", "Förbrukning", row.home, value => one.format(value) + " kW"],
      ["battery", "Batteri", row.battery, value => `${value > 0.03 ? "laddar " : value < -0.03 ? "ger " : ""}${one.format(Math.abs(value))} kW`],
      ["grid", "Nät", row.grid, value => `${value > 0.03 ? "köper " : value < -0.03 ? "säljer " : ""}${one.format(Math.abs(value))} kW`],
    ];
    for (const [key, , value] of entries) {
      if (Number.isFinite(value)) svg("circle", { cx, cy: layout.y(value), r: 4, style: `fill: var(--${key})` }, cursor);
    }

    const [timeLabel, rows] = view.tooltip.children;
    timeLabel.textContent = `${clock.format(time)}–${clock.format(time + layout.step)}`;
    rows.replaceChildren(...entries.filter(([, , value]) => Number.isFinite(value)).map(([key, label, value, format]) => {
      const item = document.createElement("div");
      item.className = "row";
      const keyLine = document.createElement("i");
      keyLine.style.background = `var(--${key})`;
      const name = document.createElement("span");
      name.textContent = label;
      const amount = document.createElement("b");
      amount.textContent = format(value);
      item.append(keyLine, name, amount);
      return item;
    }));
    view.tooltip.hidden = false;
    const bounds = view.plot.getBoundingClientRect();
    const scale = bounds.width / layout.width || 1;
    const tipWidth = view.tooltip.offsetWidth;
    const center = cx * scale;
    const left = center + 14 + tipWidth > bounds.width ? center - 14 - tipWidth : center + 14;
    view.tooltip.style.transform = `translate(${Math.max(0, left)}px, ${layout.top * scale}px)`;
    view.plot.setAttribute("aria-valuenow", String(index + 1));
    view.plot.setAttribute("aria-valuetext", `${timeLabel.textContent}: ${entries.filter(([, , value]) => Number.isFinite(value)).map(([, label, value, format]) => `${label} ${format(value)}`).join(", ")}`);
  }

  #key(event) {
    const count = this.#history?.times.length ?? 0;
    if (!count) return;
    const current = this.#cursor < 0 ? count - 1 : this.#cursor;
    const moves = {
      ArrowLeft: current - 1, ArrowDown: current - 1,
      ArrowRight: current + 1, ArrowUp: current + 1,
      PageDown: current - 12, PageUp: current + 12,
      Home: 0, End: count - 1,
    };
    if (event.key === "Escape") {
      this.#showCursor(-1);
      return;
    }
    if (!Object.hasOwn(moves, event.key)) return;
    event.preventDefault();
    this.#showCursor(Math.max(0, Math.min(count - 1, moves[event.key])));
  }
}

if (!customElements.get("energi-card")) customElements.define("energi-card", EnergiCard);

window.customCards = window.customCards ?? [];
if (!window.customCards.some(card => card.type === "energi-card")) {
  window.customCards.push({ type: "energi-card", name: "Energi", description: "Energiflöden för sol, batteri och elnät" });
}
