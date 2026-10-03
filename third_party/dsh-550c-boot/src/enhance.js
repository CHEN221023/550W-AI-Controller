/**
 * dsh-550c-boot — content enhancement layer.
 *
 * The extractor ports the original animation VERBATIM; this file is where the
 * content is upgraded, deliberately kept out of the extraction so the port stays
 * a faithful copy of what the author tuned. Nothing here reaches into the
 * generated code: it adds a stylesheet and observes the mounted stage, so the
 * show can be re-extracted at any time without losing these changes.
 *
 * The three axes of this pass:
 *
 *   终端质感  a monospace CJK stack (Windows has no CJK mono font, but NSimSun
 *             is fixed-pitch per glyph, which is what makes mixed text line up),
 *             character-cell progress bars, a block cursor, a real clock stamp on
 *             every log line, inverted key lines.
 *   数据自洽  the popup logs' hardcoded [14:22:07] stamps are remapped onto the
 *             real clock with their relative spacing preserved; ELAPSED becomes
 *             the real elapsed time; the T-00:03:41 window ticks for real.
 *   面板深度  firmware injection gains a line-number/address gutter, a live
 *             write-progress + CRC32 + hex-tail footer; every swarm node gains
 *             signal bars and a firmware version.
 */

/** Terminal texture + the new panel furniture. Injected after the ported CSS. */
const ENHANCE_CSS = `
/* ── 终端质感 ─────────────────────────────────────────────────────────── */
/* Cascadia Mono for Latin, NSimSun for CJK: the Windows fixed-pitch pair. */
:host{font-family:"Cascadia Mono",Consolas,NSimSun,"新宋体","Microsoft YaHei UI",monospace}
#mainBody .ln[data-ts]::before{content:attr(data-ts);color:var(--phos-dim);margin-right:1ch}
#mainBody::after{content:"▊";display:block;color:var(--phos);animation:blink 1.06s steps(1) infinite}
/* Progress bars as character cells rather than a smooth gradient. */
#hud-bot .prog{background-image:repeating-linear-gradient(90deg,var(--phos-off) 0 4px,transparent 4px 7px)}
#hud-bot .prog .fill{background-image:repeating-linear-gradient(90deg,var(--phos) 0 4px,transparent 4px 7px);box-shadow:none}
.wp-prog{background-image:repeating-linear-gradient(90deg,var(--phos-off) 0 4px,transparent 4px 7px)}
.wp-prog .f{background-image:repeating-linear-gradient(90deg,var(--phos) 0 4px,transparent 4px 7px);box-shadow:none}
.win-popup.danger .wp-prog .f{background-image:repeating-linear-gradient(90deg,var(--phos-lit) 0 4px,transparent 4px 7px)}

/* ── 面板深度：固件注入 ───────────────────────────────────────────────── */
#codeBody .cln::before{content:attr(data-ln) " " attr(data-addr);color:var(--phos-off);margin-right:1ch}
#w-code .w-head .tag{font-variant-numeric:tabular-nums}
.w-foot{flex:none;display:flex;align-items:center;gap:12px;padding:3px 8px;
  border-top:1px solid var(--phos-off);background:var(--bg-win);
  font-size:9px;letter-spacing:.1em;color:var(--phos-off);font-variant-numeric:tabular-nums;white-space:nowrap;overflow:hidden}
.w-foot b{color:var(--phos-dim);font-weight:400}
.w-foot .hex{color:var(--phos-dim);letter-spacing:.06em}

/* ── 面板深度：集群节点 ───────────────────────────────────────────────── */
.uav .sig{display:flex;align-items:flex-end;gap:1px;height:9px;margin-bottom:1px}
.uav .sig i{width:2px;background:var(--phos-off);opacity:.85}
.uav.done .sig i{background:var(--phos)}
.uav.writing .sig i{background:var(--phos)}
.uav .ver{font-size:5.5px;color:var(--text-faint);letter-spacing:.02em;margin-left:2px}

/* ── 配色：默认即原作，方案可选 ────────────────────────────────────────
   The port's palette was never the problem. The author's amber CRT IS the
   terminal look, and four rounds of "improving" it only carried it further away.
   So the default here overrides NOTHING: the texture and panel work borrow the
   ported tokens (the --phos* ladder below resolves to the original amber), and
   every structural colour the port hardcoded is lifted into a variable whose
   default is its original literal. The default rendering is the author's own.

   The schemes at the bottom are opt-in and chosen in Settings. Each is one block
   of variable overrides — which is the whole point of the indirection: a colour
   scheme is data now, not another rewrite of the animation.

   NOTE: never put a backtick in this block. The whole sheet is one template
   literal, so a single backtick ends it early; scripts/build.mjs refuses to
   emit a bundle that does not parse, which is what caught this. */
/* The texture rules speak in --phos*. With no scheme selected these ARE the
   ported ladder, so none of them changes a thing. */
:host{
  --phos:var(--amber);
  --phos-lit:var(--amber-b);
  --phos-dim:var(--text-dim);
  --phos-off:var(--text-faint);
  /* The Desktop window's caption strip, in this scheme. Literals on purpose:
     client.js reads them off :host and pushes them into the two DSH tokens the
     Desktop preload measures, and a var() chain would come back unresolved. */
  --caption-fill:#141008; --caption-symbol:#e8a020;
  /* Structural colours the port hardcoded, as variables whose defaults are the
     port's own literals. --glow-rgb is the one triple every tint derives from. */
  --glow-rgb:232,160,32;
  --head-top:#141008; --head-top-2:#161006;
  --title-top:#1a1208; --title-bottom:#0e0a06;
  --btn-top:#3a2408; --btn-bottom:#1a1005;
  --uav-glow:drop-shadow(0 0 3px rgba(224,80,48,.6));
  --uav-done-glow:drop-shadow(0 0 6px rgba(184,200,64,.8));
  --uav-write-glow:drop-shadow(0 0 7px rgba(255,192,67,1));
}
/* The port's own rules, restated through those variables: identical output. */
#hud-top{background:linear-gradient(180deg,var(--head-top),var(--bg-panel))}
.w{box-shadow:inset 0 0 30px rgba(var(--glow-rgb),.03)}
.w-head{background:linear-gradient(180deg,var(--head-top-2),var(--bg-panel))}
.w-body{scrollbar-color:rgba(var(--glow-rgb),.3) transparent}
.w-body::-webkit-scrollbar-thumb{background:rgba(var(--glow-rgb),.25)}
#hud-bot{background:linear-gradient(180deg,var(--bg-win),var(--bg))}
.win-popup{box-shadow:0 24px 70px rgba(0,0,0,.78),0 0 0 1px rgba(0,0,0,.9),0 0 50px rgba(var(--glow-rgb),.1),inset 0 0 24px rgba(var(--glow-rgb),.02)}
.wp-title{background:linear-gradient(180deg,var(--title-top),var(--title-bottom))}
.wp-title .wp-btn:hover{background:rgba(var(--glow-rgb),.15);color:var(--amber-b);border-color:var(--amber-d)}
.wp-body::-webkit-scrollbar-thumb{background:rgba(var(--glow-rgb),.25)}
.wp-row{border-bottom:1px dotted rgba(var(--glow-rgb),.12)}
.wp-note{background:rgba(var(--glow-rgb),.06)}
.wp-log::-webkit-scrollbar-thumb{background:rgba(var(--glow-rgb),.2)}
.wp-btn-b{background:linear-gradient(180deg,var(--title-top),var(--bg-panel))}
.wp-btn-b:hover{background:rgba(var(--glow-rgb),.15);border-color:var(--amber);color:var(--amber-b);box-shadow:0 0 14px rgba(var(--glow-rgb),.22)}
.wp-btn-b.primary{background:linear-gradient(180deg,var(--btn-top),var(--btn-bottom))}
.wp-btn-b.primary:hover{background:rgba(var(--glow-rgb),.25);box-shadow:0 0 18px rgba(var(--glow-rgb),.42)}
.wp-meter .big{text-shadow:0 0 14px rgba(var(--glow-rgb),.5)}
.wp-wave i{background:linear-gradient(180deg,var(--amber),var(--amber-d))}
.uav svg{filter:var(--uav-glow)}
.uav.done svg{filter:var(--uav-done-glow)}
.uav.writing svg{filter:var(--uav-write-glow)}
#final{background:var(--bg-panel);border-top-color:var(--amber);border-bottom-color:var(--amber);
  box-shadow:0 0 80px rgba(var(--glow-rgb),.55),0 0 180px rgba(var(--glow-rgb),.22),inset 0 0 80px rgba(var(--glow-rgb),.08);
  text-shadow:0 0 14px rgba(var(--glow-rgb),.95),0 0 40px rgba(var(--glow-rgb),.6)}
#final::before,#final::after{background:var(--amber-b);box-shadow:0 0 20px 4px rgba(var(--glow-rgb),.9)}

/* ── 配色方案（可选）────────────────────────────────────────────────────
   Opt-in, chosen in Settings, applied as data-scheme on the overlay host.
   Amber is the author's own and deliberately has no block: selecting it clears
   the attribute and the defaults above take over again. */
:host([data-scheme="green"]){
  --amber:#4ad46a; --amber-b:#a6ffbb; --amber-d:#1d5a2c; --amber-fade:rgba(74,212,106,.3);
  --text:#8fe0a4; --text-dim:#4e8a5e; --text-faint:#27452f;
  --bg:#010601; --bg-panel:#041007; --bg-win:#06160c;
  --red:#ff7043; --red-b:#ffa07a; --green:#a6ffbb; --cyan:#7fe0d0;
  --glow-rgb:74,212,106;
  --caption-fill:#08180c; --caption-symbol:#4ad46a;
  --head-top:#08180c; --head-top-2:#0a1c0f; --title-top:#0b1e12; --title-bottom:#05120a;
  --btn-top:#123a1c; --btn-bottom:#08180c;
  --uav-glow:drop-shadow(0 0 3px rgba(255,112,67,.6));
  --uav-done-glow:drop-shadow(0 0 6px rgba(166,255,187,.8));
  --uav-write-glow:drop-shadow(0 0 7px rgba(166,255,187,1));
}
:host([data-scheme="cyan"]){
  --amber:#3fc8dc; --amber-b:#a6f0ff; --amber-d:#17515e; --amber-fade:rgba(63,200,220,.3);
  --text:#8fd4e2; --text-dim:#4d8291; --text-faint:#26454e;
  --bg:#010608; --bg-panel:#041013; --bg-win:#06161a;
  --red:#ff5c7a; --red-b:#ff8fa3; --green:#7ff0d0; --cyan:#a6f0ff;
  --glow-rgb:63,200,220;
  --caption-fill:#08181d; --caption-symbol:#3fc8dc;
  --head-top:#08181d; --head-top-2:#0a1c22; --title-top:#0b1e24; --title-bottom:#051216;
  --btn-top:#12383f; --btn-bottom:#08181d;
  --uav-glow:drop-shadow(0 0 3px rgba(255,92,122,.6));
  --uav-done-glow:drop-shadow(0 0 6px rgba(127,240,208,.8));
  --uav-write-glow:drop-shadow(0 0 7px rgba(166,240,255,1));
}
:host([data-scheme="white"]){
  --amber:#c9c9c9; --amber-b:#ffffff; --amber-d:#4a4a4a; --amber-fade:rgba(255,255,255,.25);
  --text:#c0c0c0; --text-dim:#7a7a7a; --text-faint:#3d3d3d;
  --bg:#010101; --bg-panel:#070707; --bg-win:#0c0c0c;
  --red:#ff5a5a; --red-b:#ff8f8f; --green:#dcdcdc; --cyan:#d0d0d0;
  --glow-rgb:255,255,255;
  --caption-fill:#141414; --caption-symbol:#c9c9c9;
  --head-top:#141414; --head-top-2:#161616; --title-top:#1a1a1a; --title-bottom:#0e0e0e;
  --btn-top:#3a3a3a; --btn-bottom:#1a1a1a;
  --uav-glow:drop-shadow(0 0 3px rgba(255,90,90,.6));
  --uav-done-glow:drop-shadow(0 0 6px rgba(255,255,255,.8));
  --uav-write-glow:drop-shadow(0 0 7px rgba(255,255,255,1));
}
/* ── 桌面窗口的原生按钮（Desktop 专属）──────────────────────────────────
   The Desktop shell hands Electron titleBarStyle:'hidden' plus a 40px
   titleBarOverlay, and the OS draws the caption buttons ─ □ ✕ in the window's
   top-right corner ABOVE the page — no z-index in this document can reach them,
   which is why the splash adopts them instead of covering them:

     footprint  the strip is reserved inside #hud-top, so the HUD's own
                TIME / ● REC are not shoved under the buttons;
     palette    the two caption tokens above are pushed (by client.js) into the
                exact DSH tokens the Desktop preload measures and forwards to
                setTitleBarOverlay, so the OS repaints the strip in this
                splash's colours.

   env(titlebar-area-width) IS Chromium's reported strip geometry (~138px of
   Windows caption buttons at 100%); where the overlay is absent the fallback
   collapses the reservation to zero. A browser tab never sets data-caption at
   all: its window buttons live outside the viewport. */
:host([data-mode="simple"]){--caption-fill:#050403}
:host([data-caption="windows"]) #hud-top{padding-right:calc(100% - env(titlebar-area-width, 100%))}
:host([data-caption="darwin"]) #hud-top{padding-left:86px}
`

const pad2 = (value) => String(value).padStart(2, '0')

function clockText(date) {
  return pad2(date.getHours()) + ':' + pad2(date.getMinutes()) + ':' + pad2(date.getSeconds())
}

function secondsOf(stamp) {
  const [h, m, s] = stamp.split(':').map(Number)
  return h * 3600 + m * 60 + s
}

function elapsedText(seconds) {
  const clamped = Math.max(0, Math.floor(seconds))
  return (
    pad2(Math.floor(clamped / 3600)) + ':' + pad2(Math.floor((clamped % 3600) / 60)) + ':' + pad2(clamped % 60)
  )
}

/* CRC32 over the streamed source, so the footer's checksum is really computed
 * from what has been written rather than invented. */
let crcTable = null
function crc32(text, seed) {
  if (crcTable === null) {
    crcTable = new Uint32Array(256)
    for (let i = 0; i < 256; i++) {
      let value = i
      for (let bit = 0; bit < 8; bit++) value = value & 1 ? 0xedb88320 ^ (value >>> 1) : value >>> 1
      crcTable[i] = value >>> 0
    }
  }
  let crc = (seed ^ 0xffffffff) >>> 0
  for (let i = 0; i < text.length; i++) crc = (crcTable[(crc ^ text.charCodeAt(i)) & 0xff] ^ (crc >>> 8)) >>> 0
  return (crc ^ 0xffffffff) >>> 0
}

function hex(value, width) {
  return value.toString(16).toUpperCase().padStart(width, '0')
}

/**
 * Install the enhancement layer over one mounted show.
 *
 * @param stage - the shadow-root container holding the ported markup
 * @param options.mode - 'simple' or 'full'; simple mode has no HUD, so the
 *   panel work is skipped entirely
 * @returns a disposer that disconnects the observer and clears pending timers
 */
function enhanceShow(stage, options) {
  const startedAt = Date.now()
  const full = options.mode === 'full'
  const root = stage.getRootNode()

  const style = document.createElement('style')
  style.textContent = ENHANCE_CSS
  root.appendChild(style)

  /* ── 数据自洽 ──────────────────────────────────────────────────────── */
  // The popup logs carry stamps frozen at authoring time ([03:41:22] in the
  // intercept log, [14:22:07] in the anomaly logs). Remap them onto the real
  // clock PER LOG CONTAINER: each incident's log starts when its window appears
  // and advances in real seconds. A single global origin would instead splice
  // two unrelated fictional timelines together and land hours away from now.
  const timelines = new WeakMap()
  function remapStamp(container, original) {
    let timeline = timelines.get(container)
    if (timeline === undefined) {
      timeline = { originSeconds: null, originRealMs: Date.now(), map: new Map() }
      timelines.set(container, timeline)
    }
    const existing = timeline.map.get(original)
    if (existing !== undefined) return existing
    const seconds = secondsOf(original)
    if (timeline.originSeconds === null) timeline.originSeconds = seconds
    const mapped = clockText(new Date(timeline.originRealMs + (seconds - timeline.originSeconds) * 1000))
    timeline.map.set(original, mapped)
    return mapped
  }

  const DEADLINE = /T-00:03:41/g

  /* ── 面板深度：固件注入 ────────────────────────────────────────────── */
  const IMAGE_BYTES = 0x0012c000
  const BASE_ADDRESS = 0x08000000
  const LINE_BYTES = 32
  let codeLines = 0
  let crc = 0
  let codeFoot = null
  let writeAddress = null

  function buildCodeFooter() {
    const panel = stage.querySelector('#w-code')
    if (panel === null) return
    const head = panel.querySelector('.w-head')
    if (head !== null && head.querySelector('.tag') === null) {
      const tag = document.createElement('span')
      tag.className = 'tag'
      tag.textContent = '0x' + hex(BASE_ADDRESS, 8) + ' · ' + hex(IMAGE_BYTES, 8) + ' B'
      head.appendChild(tag)
    }
    codeFoot = document.createElement('div')
    codeFoot.className = 'w-foot'
    codeFoot.innerHTML =
      '<span>WROTE <b data-wrote>0x00000000</b> / 0x' +
      hex(IMAGE_BYTES, 8) +
      '</span><span>CRC32 <b data-crc>00000000</b></span><span class="hex" data-hex></span>'
    panel.appendChild(codeFoot)
    writeAddress = codeFoot.querySelector('[data-wrote]')
  }

  function noteCodeLine(text) {
    codeLines++
    crc = crc32(text, crc)
    if (codeFoot === null) return
    const written = Math.min(IMAGE_BYTES, codeLines * LINE_BYTES)
    writeAddress.textContent = '0x' + hex(written, 8)
    codeFoot.querySelector('[data-crc]').textContent = hex(crc, 8)
    // Eight bytes of digest for the line just pushed. Hashing rather than
    // slicing the text: a source line starts with indentation, so slicing gave
    // eight 0x20s and the field read as broken.
    const digest = crc32(text, crc)
    const tail = []
    for (let shift = 24; shift >= 0; shift -= 8) tail.push(hex((digest >>> shift) & 0xff, 2))
    for (let shift = 24; shift >= 0; shift -= 8) tail.push(hex((crc >>> shift) & 0xff, 2))
    codeFoot.querySelector('[data-hex]').textContent = tail.join(' ')
  }

  /* ── decorators ────────────────────────────────────────────────────── */
  function stampLine(el) {
    // The finale countdown lines are already real; a second stamp would only add
    // noise to the one moment the screen should read as a single beat.
    if (el.classList.contains('t') || el.dataset.ts !== undefined) return
    el.dataset.ts = clockText(new Date())
  }

  function gutterLine(el) {
    if (el.dataset.ln !== undefined) return
    codeLines++
    const address = BASE_ADDRESS + (codeLines - 1) * LINE_BYTES
    el.dataset.ln = String(codeLines).padStart(4, '0')
    el.dataset.addr = hex(address, 8)
    noteCodeLine(el.textContent ?? '')
  }

  function deepenNode(el) {
    if (el.dataset.deep !== undefined) return
    el.dataset.deep = '1'
    const idEl = el.querySelector('.id')
    const index = Number((idEl?.textContent ?? '').replace(/\D/g, '')) || 1

    const bars = document.createElement('div')
    bars.className = 'sig'
    // Deterministic per node: the same node always shows the same link, which is
    // what a real matrix does — only its state colour changes.
    const strength = 2 + ((index * 7 + 3) % 4)
    for (let i = 0; i < 5; i++) {
      const bar = document.createElement('i')
      bar.style.height = (i < strength ? 3 + i * 1.5 : 2) + 'px'
      bar.style.opacity = i < strength ? '0.9' : '0.22'
      bars.appendChild(bar)
    }
    el.insertBefore(bars, idEl ?? null)

    if (idEl !== null && idEl.querySelector('.ver') === null) {
      const version = document.createElement('span')
      version.className = 'ver'
      version.textContent = 'v1.14'
      idEl.appendChild(version)
    }
  }

  function remapLogLine(el) {
    if (el.dataset.remapped !== undefined) return
    const container = el.parentElement
    if (container === null) return
    el.dataset.remapped = '1'
    el.textContent = (el.textContent ?? '').replace(
      /^\[(\d{2}:\d{2}:\d{2})\]/,
      (match, stamp) => '[' + remapStamp(container, stamp) + ']',
    )
  }

  function fixRow(el) {
    if (el.dataset.fixed !== undefined) return
    const key = el.querySelector('.k')
    const value = el.querySelector('.v')
    if (key === null || value === null) return
    if (key.textContent.trim() !== 'ELAPSED') return
    el.dataset.fixed = '1'
    value.textContent = 'T-' + elapsedText((Date.now() - startedAt) / 1000)
  }

  function decorate(node) {
    if (node.nodeType !== 1) return
    const el = node
    const cls = el.classList
    if (cls.contains('ln')) stampLine(el)
    else if (cls.contains('cln')) gutterLine(el)
    else if (cls.contains('uav')) deepenNode(el)
    else if (cls.contains('ll')) remapLogLine(el)
    else if (cls.contains('wp-row')) fixRow(el)
    for (const child of el.children) decorate(child)
  }

  /* ── the ticking deadline ──────────────────────────────────────────── */
  // T-00:03:41 is a deadline frozen at authoring time. Rewrite it once, at the
  // moment the full pattern exists: type() appends in order, so anything still
  // untyped after that point is the sentence's final character and lands after
  // the rewrite unharmed.
  function tickDeadline(el) {
    if (el.dataset.deadline !== undefined) return
    const text = el.textContent ?? ''
    if (!DEADLINE.test(text)) {
      DEADLINE.lastIndex = 0
      return
    }
    DEADLINE.lastIndex = 0
    el.dataset.deadline = '1'
    const remaining = Math.max(0, 221 - (Date.now() - startedAt) / 1000)
    el.textContent = text.replace(DEADLINE, 'T-' + elapsedText(remaining))
  }

  /* ── observer ──────────────────────────────────────────────────────── */
  // type() writes with `el.textContent += chunk`, which REPLACES the text node
  // rather than mutating it — so progress arrives as childList mutations, not
  // characterData ones. Both are handled: the childList branch is the one that
  // actually fires for the typed log lines.
  const observer = new MutationObserver((records) => {
    for (const record of records) {
      if (record.type === 'characterData') {
        const parent = record.target.parentElement
        if (parent !== null && parent.classList.contains('ln')) tickDeadline(parent)
        continue
      }
      for (const added of record.addedNodes) {
        if (added.nodeType === 1) {
          decorate(added)
        } else if (added.nodeType === 3) {
          const parent = added.parentElement
          if (parent !== null && parent.classList.contains('ln')) tickDeadline(parent)
        }
      }
    }
  })
  observer.observe(stage, { childList: true, subtree: true, characterData: true })

  // The static markup is already mounted: decorate it once up front.
  decorate(stage)
  if (full) buildCodeFooter()

  return function stop() {
    observer.disconnect()
  }
}
