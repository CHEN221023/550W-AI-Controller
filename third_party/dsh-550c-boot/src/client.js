/**
 * dsh-550c-boot — browser half.
 *
 * Two surfaces:
 *
 *   the splash            mounted IMPERATIVELY from apply(), not through a slot
 *                         — see mountOverlay() for why that matters
 *   settings.general.item  the 关闭 / 简易 / 完整 row, next to Appearance
 *
 * The row is a plain React element (createElement, no JSX: the client loader
 * hands this module `require`, and `react` is one of the externals it resolves —
 * the same shape tsdown emits for the published client plugins). The splash is
 * plain DOM in a shadow root, so the original page's generic class names
 * (.w, .ln, .dt) cannot leak into DSH's own UI.
 *
 * The show plays once per client boot. `#app`'s markup is only mounted in full
 * mode, and the splash never waits for anything: it plays to the end and then
 * fades, whatever state the app behind it is in.
 */

const React = require('react')

const MODE_KEY = 'dsh-550c-boot:mode'
const MODE_VALUES = ['off', 'simple', 'full']
const DEFAULT_MODE = 'simple'
const SCHEME_KEY = 'dsh-550c-boot:scheme'
/** Amber is the original author's palette and the default: choosing it clears
 *  the data-scheme attribute entirely, so nothing is overridden. */
const SCHEME_VALUES = ['amber', 'green', 'cyan', 'white']
const DEFAULT_SCHEME = 'amber'
/** Skip token: thrown through the show's awaits when the user skips. */
const CANCELLED = Symbol('dsh-550c-cancelled')
/** How long the whole hand-off takes: the content fade (200 ms) plus the delay
 *  and the backdrop fade (200 + 420 ms). Must match :host(.dsh550c-out). */
const FADE_MS = 620
const ROW_STYLE_ID = 'dsh-550c-boot-row-style'
/** Handshake with the host half's first frame (see endFirstFrame + lib/index.js). */
const FIRST_FRAME_GLOBAL = '__dsh550cFirstFrame'
/** The host-document sheet that makes the Desktop caption strip see-through. */
const CAPTION_STYLE_ID = 'dsh-550c-boot-caption'
/** The host-document sheet that keeps the overlay out of the macOS drag region. */
const DRAG_GUARD_STYLE_ID = 'dsh-550c-boot-drag-guard'

/** The one live splash; the boot trigger and the preview button share it. */
let liveOverlay = null

function readMode() {
  try {
    const raw = window.localStorage.getItem(MODE_KEY)
    return MODE_VALUES.indexOf(raw) >= 0 ? raw : DEFAULT_MODE
  } catch (error) {
    return DEFAULT_MODE
  }
}

function writeMode(mode) {
  try {
    window.localStorage.setItem(MODE_KEY, mode)
  } catch (error) {
    /* private mode: the choice simply does not persist */
  }
}

function readScheme() {
  try {
    const raw = window.localStorage.getItem(SCHEME_KEY)
    return SCHEME_VALUES.indexOf(raw) >= 0 ? raw : DEFAULT_SCHEME
  } catch (error) {
    return DEFAULT_SCHEME
  }
}

function writeScheme(scheme) {
  try {
    window.localStorage.setItem(SCHEME_KEY, scheme)
  } catch (error) {
    /* private mode: the choice simply does not persist */
  }
}

/** The overlay's own sheet: host geometry + the extracted animation styles. */
const HOST_CSS = `
:host{position:fixed;inset:0;display:block;box-sizing:border-box;z-index:2147483000;background:var(--bg,#050403)}
/* The hand-off is deliberately two-phase, because a single cross-fade is ugly:
   the port ends on a big, bright logo, and dissolving that straight into the
   conversation page stamps a grey ghost of it over the UI for half a second
   (measured frame by frame). So the CONTENT goes first — the logo and panels
   fade into the splash's own background, which the host now paints itself — and
   only then does that clean backdrop fade away to reveal the app. The backdrop
   is read from the animation's own --bg, so both phases agree on the colour. */
:host(.dsh550c-out){opacity:0;transition:opacity 420ms cubic-bezier(.4,0,.2,1) 200ms}
:host(.dsh550c-out) .dsh550c-stage{opacity:0;transition:opacity 200ms ease-out}
/* Height only — deliberately NOT position:fixed. A fixed stage would create a
   stacking context and trap #boot's z-index 2000 inside it, which lets the
   ported vignette (z-index 899) and scanlines paint OVER the logo. In the
   original page #boot was a direct child of body and nothing wrapped it. */
.dsh550c-stage{height:100%}
`

/** The General-settings row sheet; DSH tokens so it matches either theme. */
const ROW_CSS = `
.dsh550c-row{display:flex;align-items:center;gap:16px;padding:10px 0;flex-wrap:wrap}
.dsh550c-row-text{flex:1;min-width:220px}
.dsh550c-row-title{font-size:13px;line-height:1.5;color:var(--dsw-alias-label-primary,#191919)}
.dsh550c-row-desc{margin-top:2px;font-size:12px;line-height:1.5;color:var(--dsw-alias-label-secondary,#666)}
.dsh550c-row-ctrl{display:flex;align-items:center;gap:8px}
.dsh550c-seg{display:inline-flex;border:1px solid var(--dsw-alias-border-l2,rgba(127,127,127,.35));border-radius:8px;overflow:hidden}
.dsh550c-seg button{border:0;background:transparent;color:var(--dsw-alias-label-secondary,#666);font-family:inherit;font-size:12.5px;line-height:1.4;padding:5px 14px;cursor:pointer}
.dsh550c-seg button+button{border-left:1px solid var(--dsw-alias-border-l2,rgba(127,127,127,.35))}
.dsh550c-seg button:hover{background:var(--dsw-alias-interactive-bg-hover,rgba(127,127,127,.14))}
.dsh550c-seg button.on{background:var(--dsw-alias-brand-primary,#4d6bfe);color:#fff}
.dsh550c-preview{border:1px solid var(--dsw-alias-border-l2,rgba(127,127,127,.35));background:transparent;color:var(--dsw-alias-label-primary,#191919);font-family:inherit;font-size:12.5px;line-height:1.4;padding:5px 14px;border-radius:8px;cursor:pointer}
.dsh550c-preview:hover{background:var(--dsw-alias-interactive-bg-hover,rgba(127,127,127,.14))}
`

function ensureRowStyle() {
  if (document.getElementById(ROW_STYLE_ID) !== null) return
  const style = document.createElement('style')
  style.id = ROW_STYLE_ID
  style.textContent = ROW_CSS
  document.head.appendChild(style)
}

/**
 * macOS window-drag guard.
 *
 * DSH's official base stylesheet turns every DIRECT body child into a
 * `-webkit-app-region: no-drag` region — its selector spares only the app's own
 * root element. A body-level element that spans the viewport therefore subtracts
 * the whole window from the macOS draggable region: while the splash is up the
 * window cannot be dragged by its title area, and macOS no longer runs the
 * system double-click action (zoom) there. `pointer-events: none` does not exempt
 * an element from that computation; only a declaration of its own does.
 *
 * `data-dsh-boot-splash` is the marker the dsh-web family bundle exempts
 * (packages/dsh-web-all/src/client/index.ts), so carrying it is what keeps this
 * plugin a good citizen there; this sheet is the same declaration for installs
 * that ship no such bundle. `initial` is the initial value (`none`), which takes
 * the element out of the app-region computation instead of turning the whole
 * overlay into a drag handle — click-to-skip keeps working. `!important` is
 * required because the official selector outranks this one.
 */
const DRAG_GUARD_CSS = `
html[data-platform="darwin"] body>.dsh550c-host{-webkit-app-region:initial !important}
`

function ensureDragGuard() {
  if (document.getElementById(DRAG_GUARD_STYLE_ID) !== null) return
  const style = document.createElement('style')
  style.id = DRAG_GUARD_STYLE_ID
  style.textContent = DRAG_GUARD_CSS
  document.head.appendChild(style)
}

/**
 * Retire the host half's first frame.
 *
 * `lib/index.js` contributes an index-injection row that covers the screen with
 * the splash's own opening colour *before the shell exists* — that is the only
 * way to keep DSH's boot card off the glass, because a client plugin is
 * materialised long after the card is painted (measured: card 67 ms, this
 * bundle 338 ms, card disposed 517 ms). The splash paints the same colour
 * itself, so calling this on the same task as mounting the overlay swaps them
 * inside one frame: no seam, no flash, nothing left behind. The injected script
 * watches the boot card and carries its own timeout, so a client half that never
 * arrives cannot leave a black window behind.
 */
function endFirstFrame() {
  const handle = window[FIRST_FRAME_GLOBAL]
  if (handle === undefined || handle === null) return
  delete window[FIRST_FRAME_GLOBAL]
  try {
    handle.end()
  } catch (error) {
    console.error('[dsh-550c-boot] first frame refused to end', error)
  }
}

/**
 * Make the Desktop window's caption buttons belong to the splash.
 *
 * On Windows the shell hands Electron `titleBarStyle: 'hidden'` plus a
 * `titleBarOverlay` 40 px tall and ~138 px wide, and the buttons are drawn by the
 * browser process ABOVE the page: no z-index in this document can reach them
 * (the ported UI keeps its own ─ □ ✕ inside the fake window titles, which is
 * where the design wants them). Two things stop them reading as foreign chrome:
 *
 *   footprint  the stylesheet reserves the strip inside #hud-top, so the HUD's
 *              TIME / ● REC are not pushed under the buttons (data-caption).
 *   strip      the Desktop preload measures a probe element's computed
 *              `background-color` / `color` and pushes them to
 *              `setTitleBarOverlay({color, symbolColor})` over IPC, re-measuring
 *              whenever <head> mutates. Both values accept alpha, so the strip is
 *              made TRANSPARENT while the splash plays: the buttons float on the
 *              animation instead of sitting on an opaque bar, and the symbols are
 *              painted in the splash's accent so they stay legible on it.
 *
 * The colours are declared ON THE PROBE ELEMENT, not on :root. The probe is a
 * body-level `<span>` whose inline style reads the app's two tokens, and the app
 * defines them on `body`: custom properties resolve from the CLOSEST ancestor, so
 * a `:root` override never reaches it — `!important` on `html` loses to a plain
 * declaration on `body` (measured on the real window: the strip stayed opaque for
 * the whole splash while the sheet looked correct). Declaring both colours on the
 * probe sidesteps inheritance and leaves the app's own tokens — and its text
 * colour — untouched. `transparent` is a real value here: the preload normalises
 * it through a canvas to `rgba(0, 0, 0, 0)`, which the shell's own colour check
 * accepts, so the strip really does become see-through.
 *
 * Removing the sheet afterwards puts the app's own colours back, because that
 * mutation of <head> is itself the re-measure trigger.
 *
 * A browser tab has neither the strip nor `data-platform` (the Electron preload
 * never runs), so this is a no-op outside the Desktop app.
 *
 * @returns a disposer, or null when there is nothing to adapt
 */
function adaptCaption(host) {
  const platform = document.documentElement.dataset.platform
  if (platform === undefined || platform === '') return null

  const darwin = platform === 'darwin'
  host.dataset.caption = darwin ? 'darwin' : 'windows'

  // The accent follows the scheme for free: `--caption-symbol` is a literal on
  // :host (see ENHANCE_CSS), and the host's computed style is the scheme's answer.
  const symbol = getComputedStyle(host).getPropertyValue('--caption-symbol').trim() || '#e8a020'

  const style = document.createElement('style')
  style.id = CAPTION_STYLE_ID
  const probe = 'body>span[style*="dsw-specific-sidebar-fill"]'
  style.textContent =
    probe + '{background-color:transparent !important;color:' + symbol + ' !important}'
  // Appending to <head> is itself the trigger: the preload's MutationObserver
  // re-measures the probe and repaints the strip.
  document.head.appendChild(style)

  return () => {
    style.remove()
    delete host.dataset.caption
  }
}

/**
 * The splash, mounted imperatively into document.body.
 *
 * NOT through the `shell.overlay` slot: the shell — and with it every slot —
 * renders only after the client module system has loaded every plugin, which is
 * also the moment DSH's own boot card (`[data-dsh-boot]`, "HARNESS / Loading
 * plugins…") is disposed. A slot-based splash therefore always comes AFTER that
 * card, leaving a visible gap. apply() runs during the load phase instead, so
 * this is as early as a client plugin can draw.
 *
 * Even that is ~271 ms too late for the card (67 ms card, 338 ms this bundle,
 * 517 ms card disposal), which is why the frame on screen before this module
 * ever runs comes from the host half (lib/index.js) — this mount retires it in
 * the same task (endFirstFrame), so the two read as one animation.
 *
 * Every exit path — played out, skipped, or a throw inside the show — ends in
 * the same fade, so a broken animation can never trap anyone behind a black
 * screen.
 *
 * @param force - play even when the stored mode is 'off' (the preview button)
 * @returns the live overlay record, or null when nothing was mounted
 */
function mountOverlay(force) {
  if (liveOverlay !== null) return liveOverlay

  const stored = readMode()
  if (!force && stored === 'off') return null
  const mode = stored === 'full' ? 'full' : 'simple'

  if (document.body === null) {
    // The factory can be evaluated while the document is still parsing; the
    // splash needs a body to attach to, so it waits for one.
    document.addEventListener('DOMContentLoaded', () => mountOverlay(force), { once: true })
    return null
  }

  const host = document.createElement('div')
  host.className = 'dsh550c-host'
  // Body-level overlays are subtracted from the macOS draggable region unless
  // they declare otherwise: the marker is what the dsh-web family bundle
  // exempts, ensureDragGuard() is the same exemption for installs without it.
  host.dataset.dshBootSplash = ''
  ensureDragGuard()
  // The scheme is applied as data on the host, which is what the stylesheet's
  // :host([data-scheme=…]) blocks key on. Amber sets nothing on purpose.
  const scheme = readScheme()
  if (scheme !== DEFAULT_SCHEME) host.dataset.scheme = scheme
  // Recorded for the stylesheet: only the full mode mounts #hud-top, and the
  // caption strip's fill has to match whichever surface is at the top.
  host.dataset.mode = mode
  // Recorded before anything else: did the splash beat DSH's own boot card to
  // the screen? (Read by scripts/verify.mjs.)
  host.dataset.sawBootCard = String(document.querySelector('[data-dsh-boot]') !== null)
  document.body.appendChild(host)

  const record = { host: host, show: null, enhance: null, caption: null, finished: false, fadeTimer: null, watchdog: null, dispose: null }

  const skip = () => {
    if (record.show !== null) record.show.cancel()
  }
  const onKey = (event) => {
    if (event.key === 'Escape') skip()
  }
  const dispose = () => {
    if (record.fadeTimer !== null) window.clearTimeout(record.fadeTimer)
    if (record.watchdog !== null) window.clearTimeout(record.watchdog)
    if (record.show !== null) record.show.cancel()
    if (record.enhance !== null) record.enhance()
    if (record.caption !== null) record.caption()
    host.removeEventListener('click', skip)
    window.removeEventListener('keydown', onKey, true)
    host.remove()
    if (liveOverlay === record) liveOverlay = null
  }
  const finish = () => {
    if (record.finished) return
    record.finished = true
    if (record.watchdog !== null) {
      window.clearTimeout(record.watchdog)
      record.watchdog = null
    }
    host.classList.add('dsh550c-out')
    record.fadeTimer = window.setTimeout(dispose, FADE_MS + 40)
  }
  record.dispose = dispose

  // Absolute watchdog. The show has its own end, the click/Esc skip and a catch
  // around startup, but this is the one guarantee that matters on a daily-driver
  // install: the splash can never outlive its own animation and lock the user
  // out of Settings — which is where the switch that disables it lives.
  record.watchdog = window.setTimeout(() => {
    console.error('[dsh-550c-boot] watchdog fired; dismissing the splash')
    finish()
  }, mode === 'full' ? 30000 : 12000)

  try {
    const shadow = host.attachShadow({ mode: 'open' })
    const style = document.createElement('style')
    style.textContent = HOST_CSS + CSS_550C
    shadow.appendChild(style)

    const stage = document.createElement('div')
    stage.className = 'dsh550c-stage'
    stage.innerHTML = mode === 'full' ? BOOT_MARKUP + APP_MARKUP : BOOT_MARKUP
    shadow.appendChild(stage)

    // Content upgrades (terminal texture, self-consistent data, panel depth)
    // live outside the ported code, so re-extracting never loses them.
    record.enhance = enhanceShow(stage, { mode: mode })

    // After enhanceShow: `--caption-fill` / `--caption-symbol` are defined by the
    // enhancement sheet, so reading them any earlier silently falls back to the
    // built-in default and the strip keeps the wrong colour for the whole splash.
    record.caption = adaptCaption(host)

    record.show = createShow(stage, { mode: mode, cancelled: CANCELLED })
    host.addEventListener('click', skip)
    window.addEventListener('keydown', onKey, true)
    record.show.start().then(finish, finish)
    // Last statement of the same task that put the overlay on screen: the host
    // half's first frame and this shadow root swap inside one paint.
    endFirstFrame()
  } catch (error) {
    console.error('[dsh-550c-boot] show failed to start', error)
    finish()
  }

  liveOverlay = record
  return record
}

/** The General-settings row: mode segmented control + a preview button. */
function SettingsRow() {
  const [mode, setMode] = React.useState(readMode)

  React.useEffect(() => {
    ensureRowStyle()
  }, [])

  const choose = React.useCallback((next) => {
    writeMode(next)
    setMode(next)
  }, [])

  const options = [
    { value: 'off', label: '关闭' },
    { value: 'simple', label: '简易' },
    { value: 'full', label: '完整' },
  ]

  return React.createElement(
    'div',
    { className: 'dsh550c-row' },
    React.createElement(
      'div',
      { className: 'dsh550c-row-text' },
      React.createElement('div', { className: 'dsh550c-row-title' }, '550C 开机动画'),
      React.createElement(
        'div',
        { className: 'dsh550c-row-desc' },
        '启动 DSH 时播放 550C 片头。简易模式只播 logo 加载动画；完整模式会播完整的覆写流程，可用点击或 Esc 跳过。',
      ),
    ),
    React.createElement(
      'div',
      { className: 'dsh550c-row-ctrl' },
      React.createElement(
        'div',
        { className: 'dsh550c-seg' },
        options.map((option) =>
          React.createElement(
            'button',
            {
              key: option.value,
              type: 'button',
              className: mode === option.value ? 'on' : '',
              onClick: () => choose(option.value),
            },
            option.label,
          ),
        ),
      ),
      React.createElement(
        'button',
        { type: 'button', className: 'dsh550c-preview', onClick: () => mountOverlay(true) },
        '预览',
      ),
    ),
  )
}

/**
 * The colour-scheme row. Amber is the original author's palette — the default,
 * and the only one that overrides nothing at all.
 */
function SchemeRow() {
  const [scheme, setScheme] = React.useState(readScheme)

  React.useEffect(() => {
    ensureRowStyle()
  }, [])

  const choose = React.useCallback((next) => {
    writeScheme(next)
    setScheme(next)
  }, [])

  const options = [
    { value: 'amber', label: '琥珀' },
    { value: 'green', label: '绿' },
    { value: 'cyan', label: '青' },
    { value: 'white', label: '白' },
  ]

  return React.createElement(
    'div',
    { className: 'dsh550c-row' },
    React.createElement(
      'div',
      { className: 'dsh550c-row-text' },
      React.createElement('div', { className: 'dsh550c-row-title' }, '开机动画配色'),
      React.createElement(
        'div',
        { className: 'dsh550c-row-desc' },
        '琥珀是原作的配色，也是默认值。点「预览」可以立刻看效果。',
      ),
    ),
    React.createElement(
      'div',
      { className: 'dsh550c-row-ctrl' },
      React.createElement(
        'div',
        { className: 'dsh550c-seg' },
        options.map((option) =>
          React.createElement(
            'button',
            {
              key: option.value,
              type: 'button',
              className: scheme === option.value ? 'on' : '',
              onClick: () => choose(option.value),
            },
            option.label,
          ),
        ),
      ),
    ),
  )
}

/**
 * Mount both surfaces.
 *
 * Slot names are inlined as literals on purpose: the injector's pre-flight
 * check reads register() calls statically and cannot follow a constant.
 */
function apply(ctx) {
  // The settings rows are ordinary slot contributions: they belong to a panel
  // and should live and die with it.
  ctx.slots.inject('settings.general.item', () =>
    ctx.slots.register({ name: 'settings.general.item', id: 'boot-550c', order: 26 }, SettingsRow),
  )
  ctx.slots.inject('settings.general.item', () =>
    ctx.slots.register({ name: 'settings.general.item', id: 'boot-550c-scheme', order: 27 }, SchemeRow),
  )

  // The splash is deliberately NOT a slot contribution — mountOverlay() explains
  // why. Registered as an effect so disabling the plugin takes the splash down.
  ctx.effect(
    () => () => {
      if (bootSplash !== null) bootSplash.dispose()
      if (liveOverlay !== null) liveOverlay.dispose()
    },
    'dsh-550c-boot: splash teardown',
  )
}

/**
 * Mount the boot splash at MODULE EVALUATION time.
 *
 * This is as early as a client plugin can possibly draw: the factory below runs
 * while the module system is still draining, before cordis has resolved the
 * service graph and called apply(). Every millisecond here is a millisecond of
 * DSH's own "Loading plugins…" card that the splash covers instead.
 *
 * The lifecycle is still owned by the plugin: apply() registers the teardown
 * effect, so disabling the plugin removes whatever this started.
 */
const bootSplash = mountOverlay(false)

exports.name = 'boot-550c'
exports.inject = ['slots']
exports.apply = apply
