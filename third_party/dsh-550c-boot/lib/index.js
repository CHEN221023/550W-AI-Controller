/**
 * Host half of dsh-550c-boot.
 *
 * The animation itself is browser-side (lib/client.js). Its FIRST FRAME cannot
 * be: DSH's boot card ("HARNESS / Loading plugins…") is drawn by the shell
 * before any client plugin is materialised — measured on this machine, card at
 * 67 ms, this plugin's bundle evaluated at 338 ms, card disposed at 517 ms — so
 * a plugin that only paints from its own JS always leaves the card on screen
 * for a few hundred milliseconds, at any z-index.
 *
 * The one surface that exists earlier than the shell is the served document, and
 * DSH exposes it: `webserver/index-inject` collects a table of index rows, which
 * the Web carrier renders into index.html immediately after `<head>` and the
 * Desktop carrier applies page-side before it settles `__DSH_BOOT_READY__` —
 * both ahead of the shell kernel that builds the card. A `style` row plus one
 * synchronous `script` row is therefore on the glass while the document is still
 * parsing, and the card is never visible in the first place.
 *
 * Two contracts with src/client.js:
 *
 *   MODE_KEY   the same localStorage key and the same default; the script below
 *              bows out for 'off' so that setting still boots straight through
 *              to DSH with no black frame.
 *   FIRST_FRAME_GLOBAL
 *              `window.__dsh550cFirstFrame.end()` is how the splash retires the
 *              cover once its shadow root is on screen — same task, one paint,
 *              no seam. The boot watch and the timeout below are the fallbacks:
 *              a client bundle that fails to load must never leave a black
 *              window behind.
 *
 * @module dsh-550c-boot
 */

export const name = 'boot-550c'

/** src/client.js MODE_KEY / DEFAULT_MODE — keep in sync. */
const MODE_KEY = 'dsh-550c-boot:mode'
/** src/client.js FIRST_FRAME_GLOBAL — keep in sync. */
const FIRST_FRAME_GLOBAL = '__dsh550cFirstFrame'
/** Painted under the splash's own --bg, so the handoff is invisible. */
const FIRST_FRAME_BG = '#050403'
/** Absolute ceiling (ms): past this the cover yields whatever is on the page. */
const FIRST_FRAME_MAX_MS = 12000
/** The splash's own :host z-index (src/client.js HOST_CSS) minus one. */
const FIRST_FRAME_Z = 2147482000

/**
 * The cover itself: the port's own background colour, above anything the page
 * paints and below the splash that replaces it. pointer-events stays off — the
 * card it covers is not interactive either.
 */
const FIRST_FRAME_CSS =
  'html.dsh550c-first::before{content:"";position:fixed;inset:0;background:' +
  FIRST_FRAME_BG +
  ';z-index:' +
  String(FIRST_FRAME_Z) +
  ';pointer-events:none}\n' +
  'html.dsh550c-first{background:' +
  FIRST_FRAME_BG +
  ';--dsw-specific-sidebar-fill:#141008;--dsw-alias-label-primary:#e8a020}'

/**
 * The class marker plus the handshake, the boot watch and the timeout.
 *
 * Deliberately tiny and synchronous: it runs while <head> is being parsed, so
 * the cover is applied before the first paint. The two DSH tokens it also sets
 * are the ones the Desktop preload measures into `setTitleBarOverlay` (see
 * src/client.js adaptCaption), which paints the OS caption strip black-and-amber
 * for the same window instead of leaving a grey Windows bar above a terminal.
 *
 * The cover must not outlive the reason for it, so a 250 ms watch ends it as
 * soon as the boot card is gone (the kernel removes the card exactly when the
 * application mounts) — or as soon as the card drops its spinner, which is how
 * the card renders its own failure state, since a broken page must be readable
 * rather than hidden. Nothing here is on the critical path: the splash retires
 * the cover itself, synchronously, the moment its shadow root is on screen.
 */
const FIRST_FRAME_SCRIPT =
  '(function(){' +
  'var mode=null;' +
  'try{mode=window.localStorage.getItem(' +
  JSON.stringify(MODE_KEY) +
  ')}catch(error){}' +
  'if(mode==="off")return;' +
  'var root=document.documentElement;' +
  'var watch=null;' +
  'var end=function(){' +
  'if(watch!==null){window.clearInterval(watch);watch=null}' +
  'root.classList.remove("dsh550c-first")' +
  '};' +
  'window.' +
  FIRST_FRAME_GLOBAL +
  '={end:end};' +
  'root.classList.add("dsh550c-first");' +
  'var seen=false;' +
  'watch=window.setInterval(function(){' +
  'var card=document.querySelector("[data-dsh-boot]");' +
  'if(card===null){if(seen)end();return}' +
  'seen=true;' +
  'if(card.querySelector("[data-dsh-boot-spinner]")===null)end()' +
  '},250);' +
  'window.setTimeout(end,' +
  String(FIRST_FRAME_MAX_MS) +
  ');' +
  '})()'

/**
 * Contribute the first frame to every index response.
 *
 * No service is injected: `webserver/index-inject` is a plain composition event
 * that the carrier emits per response, and a table is only rendered when a page
 * is actually served — so this costs one row and nothing when nobody asks.
 *
 * @param ctx - the plugin context.
 */
export function apply(ctx) {
  ctx.on('webserver/index-inject', (table) => {
    table.push({ kind: 'style', text: FIRST_FRAME_CSS })
    table.push({ kind: 'script', placement: 'head', text: FIRST_FRAME_SCRIPT })
  })
}
