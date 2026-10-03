#!/usr/bin/env node
/**
 * Headless verification harness for the plugin.
 *
 * Drives a real browser over the DevTools protocol so the animation can be
 * checked at an exact moment, in an exact mode, against a running `dsh web`.
 * A plain `--screenshot` run cannot do this: the mode lives in localStorage and
 * a first-run profile puts an onboarding gate in front of the whole shell, so
 * the plugin's slot does not exist until that gate is dismissed.
 *
 * Uses Node's global WebSocket (Node >= 22) — no dependencies.
 *
 * Usage:
 *   node scripts/verify.mjs --url <url> [--mode simple|full|off] [--at 2600]
 *                           [--out shot.png] [--keep]
 *
 * Options:
 *   --url    the running dsh web URL, token included
 *   --mode   localStorage value to seed before the reload (default: simple)
 *   --at     milliseconds after load to capture (default: 2600)
 *   --out    screenshot path (default: .verify/<mode>-<at>.png)
 *   --keep   leave the browser open (debugging)
 */
import { spawn } from 'node:child_process'
import { mkdirSync, writeFileSync, existsSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const root = resolve(here, '..')

const EDGE_CANDIDATES = [
  `${process.env['ProgramFiles(x86)']}\\Microsoft\\Edge\\Application\\msedge.exe`,
  `${process.env.ProgramFiles}\\Microsoft\\Edge\\Application\\msedge.exe`,
  `${process.env.ProgramFiles}\\Google\\Chrome\\Application\\chrome.exe`,
  `${process.env['ProgramFiles(x86)']}\\Google\\Chrome\\Application\\chrome.exe`,
]

function parseArgs(argv) {
  const out = {}
  for (let i = 0; i < argv.length; i += 2) {
    const key = argv[i]
    if (!key.startsWith('--')) continue
    const next = argv[i + 1]
    out[key.slice(2)] = next === undefined || next.startsWith('--') ? true : next
  }
  return out
}

const args = parseArgs(process.argv.slice(2))
if (typeof args.url !== 'string') {
  console.error('verify: --url is required')
  process.exit(2)
}
const mode = typeof args.mode === 'string' ? args.mode : 'simple'
const at = Number(typeof args.at === 'string' ? args.at : 2600)
const outPath = resolve(root, typeof args.out === 'string' ? args.out : `.verify/${mode}-${at}.png`)
const port = 9333

const browser = EDGE_CANDIDATES.find((candidate) => existsSync(candidate))
if (browser === undefined) {
  console.error('verify: no Edge/Chrome found')
  process.exit(2)
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

async function getJson(path) {
  const response = await fetch(`http://127.0.0.1:${port}${path}`)
  return response.json()
}

/** Minimal CDP client: one socket, id-matched replies, event fan-out. */
function connect(wsUrl) {
  return new Promise((resolvePromise, rejectPromise) => {
    const socket = new WebSocket(wsUrl)
    const pending = new Map()
    const listeners = new Set()
    let nextId = 0

    socket.addEventListener('message', (event) => {
      const message = JSON.parse(event.data)
      if (message.id !== undefined) {
        const entry = pending.get(message.id)
        if (entry === undefined) return
        pending.delete(message.id)
        if (message.error) entry.reject(new Error(JSON.stringify(message.error)))
        else entry.resolve(message.result)
        return
      }
      for (const listener of listeners) listener(message)
    })
    socket.addEventListener('error', rejectPromise)
    socket.addEventListener('open', () =>
      resolvePromise({
        send(method, params) {
          const id = ++nextId
          return new Promise((res, rej) => {
            pending.set(id, { resolve: res, reject: rej })
            socket.send(JSON.stringify({ id, method, params: params ?? {} }))
          })
        },
        on(listener) {
          listeners.add(listener)
        },
        close() {
          socket.close()
        },
      }),
    )
  })
}

async function main() {
  const profileDir = resolve(root, '.verify/browser-profile')
  mkdirSync(profileDir, { recursive: true })

  const child = spawn(
    browser,
    [
      '--headless=new',
      '--disable-gpu',
      '--no-first-run',
      '--no-default-browser-check',
      '--hide-scrollbars',
      `--remote-debugging-port=${port}`,
      `--user-data-dir=${profileDir}`,
      '--window-size=1600,900',
      'about:blank',
    ],
    { stdio: 'ignore' },
  )

  const cleanup = () => {
    try {
      child.kill()
    } catch (error) {
      /* already gone */
    }
  }

  try {
    // Wait for the debugging endpoint, then open a page target.
    for (let attempt = 0; attempt < 60; attempt++) {
      try {
        await getJson('/json/version')
        break
      } catch (error) {
        await sleep(250)
      }
    }
    const target = await fetch(`http://127.0.0.1:${port}/json/new?about:blank`, { method: 'PUT' }).then((r) => r.json())
    const cdp = await connect(target.webSocketDebuggerUrl)

    const logs = []
    cdp.on((message) => {
      if (message.method === 'Runtime.consoleAPICalled') {
        logs.push(`[${message.params.type}] ` + message.params.args.map((a) => a.value ?? a.description ?? a.type).join(' '))
      }
      if (message.method === 'Log.entryAdded') logs.push(`[log:${message.params.entry.level}] ${message.params.entry.text}`)
    })

    await cdp.send('Page.enable')
    await cdp.send('Runtime.enable')
    await cdp.send('Log.enable')

    const evaluate = async (expression) => {
      const result = await cdp.send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true })
      return result.result?.value
    }

    /**
     * Pre-injection probe (--probe). Runs before any page script, so it can time
     * the race that matters: DSH's own "Loading plugins…" boot card versus our
     * splash. Without this the browser is already past both by the time the
     * harness can evaluate anything.
     */
    if (args.probe === true) {
      await cdp.send('Page.addScriptToEvaluateOnNewDocument', {
        source: `(() => {
          const probe = { t0: Math.round(performance.now()), bootCardAdded: null, bootCardGone: null, splashAdded: null };
          window.__dsh550cProbe = probe;
          const check = () => {
            const card = document.querySelector('[data-dsh-boot]') !== null;
            const splash = document.querySelector('.dsh550c-host') !== null;
            if (probe.bootCardAdded === null && card) probe.bootCardAdded = Math.round(performance.now());
            if (probe.bootCardAdded !== null && probe.bootCardGone === null && !card) probe.bootCardGone = Math.round(performance.now());
            if (probe.splashAdded === null && splash) probe.splashAdded = Math.round(performance.now());
          };
          // At document-start there is no documentElement yet: observe the
          // document itself, and keep a slow poll as a belt-and-braces fallback
          // in case an insertion happens without a mutation we can see.
          try { new MutationObserver(check).observe(document, { childList: true, subtree: true }); } catch (error) { /* fall through to the poll */ }
          document.addEventListener('DOMContentLoaded', check);
          const poll = setInterval(check, 25);
          setTimeout(() => clearInterval(poll), 20000);
          check();
        })()`,
      })
    }

    const origin = new URL(args.url).origin
    // 1. Land on the origin so localStorage is addressable.
    await cdp.send('Page.navigate', { url: args.url })
    await sleep(1500)

    // 2. Seed the mode and the colour scheme the plugin reads.
    await evaluate(`window.localStorage.setItem('dsh-550c-boot:mode', ${JSON.stringify(mode)})`)
    const scheme = typeof args.scheme === 'string' ? args.scheme : 'amber'
    await evaluate(`window.localStorage.setItem('dsh-550c-boot:scheme', ${JSON.stringify(scheme)})`)

    /**
     * Dismiss the first-run gates. A fresh profile chains several of them (beta
     * notice → API key → …) and each renders in front of the whole shell, so the
     * plugin's slot is either absent or covered until they are gone. They persist
     * once answered, so this runs to exhaustion before the measured reload.
     */
    const dismissGates = async () => {
      let clicked = 0
      for (let round = 0; round < 8; round++) {
        const result = await evaluate(`(() => {
          const labels = ['继续', '稍后配置', '跳过', '我知道了', 'Skip', 'Later', 'Continue'];
          const buttons = Array.from(document.querySelectorAll('button'));
          const gate = buttons.find((b) => labels.includes(b.textContent.trim()));
          if (gate === undefined) return 'none';
          const label = gate.textContent.trim();
          gate.click();
          return label;
        })()`)
        if (result === 'none') break
        clicked++
        await sleep(700)
      }
      return clicked
    }

    const dismissed = await dismissGates()

    await cdp.send('Page.reload', { ignoreCache: true })
    // A timeline run has to start sampling before the shell boots, or the boot
    // card is already gone by the first sample.
    await sleep(args.timeline === true ? 150 : 1200)
    const dismissedAfterReload = await dismissGates()
    await sleep(at)

    /**
     * Settings tour (--settings). The row is lazy: it renders only once its
     * section is open, so registration cannot be read from the DOM without
     * walking there. Each step reports what it actually found rather than
     * assuming markup, because the panel's DOM is the shell's, not ours.
     */
    let settings = null
    if (args.settings === true) {
      const clickByText = (labels, exact) =>
        evaluate(`(() => {
          const wanted = ${JSON.stringify(labels)};
          const nodes = Array.from(document.querySelectorAll('button, [role="button"], a, [role="tab"], li'));
          const hit = nodes.find((el) => {
            const text = (el.textContent || '').trim();
            return wanted.some((w) => ${exact ? 'text === w' : 'text.includes(w)'});
          });
          if (hit === undefined) return 'none';
          hit.click();
          return 'clicked:' + (hit.textContent || '').trim().slice(0, 24);
        })()`)
      const steps = []
      steps.push({ openSettings: await clickByText(['设置', 'Settings'], false) })
      await sleep(1200)
      steps.push({ openGeneral: await clickByText(['通用', 'General'], true) })
      await sleep(1200)
      settings = {
        steps,
        rowFound: await evaluate(`document.querySelector('.dsh550c-row') !== null`),
        rowTitle: await evaluate(
          `(document.querySelector('.dsh550c-row-title') || {}).textContent || null`,
        ),
        segments: await evaluate(
          `Array.from(document.querySelectorAll('.dsh550c-seg button')).map((b) => b.textContent.trim())`,
        ),
        activeSegment: await evaluate(
          `(document.querySelector('.dsh550c-seg button.on') || {}).textContent || null`,
        ),
        previewButton: await evaluate(
          `(document.querySelector('.dsh550c-preview') || {}).textContent || null`,
        ),
      }
    }

    /**
     * Skip check (--skip): the overlay must be gone shortly after Esc, and the
     * app must be usable again. The host element only exists while the overlay
     * is active, so its disappearance is the assertion.
     */
    let skip = null
    if (args.skip === true) {
      const before = await evaluate(`document.querySelector('.dsh550c-host') !== null`)
      await evaluate(`window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))`)
      await sleep(900)
      const afterEsc = await evaluate(`document.querySelector('.dsh550c-host') !== null`)
      skip = { hostBefore: before, hostAfterEsc: afterEsc }
    }

    /**
     * Timeline sampling (--timeline): one run, polled, so the show's pacing can
     * be judged from data instead of from a handful of screenshots. Records the
     * phase banner, the progress bar, how fast log lines accumulate and how many
     * nodes have flipped to ONLINE, until the overlay unmounts.
     */
    let timeline = null
    if (args.timeline === true) {
      const sample = () =>
        evaluate(`(() => {
          const host = document.querySelector('.dsh550c-host');
          const shadow = host === null ? null : host.shadowRoot;
          if (shadow === null) return { gone: true, sawBootCard: host === null ? null : host.dataset.sawBootCard };
          const text = (sel) => { const el = shadow.querySelector(sel); return el === null ? null : el.textContent; };
          return {
            stage: text('#b-stage'),
            pct: text('#b-pct'),
            node: text('#b-node'),
            logs: shadow.querySelectorAll('#mainBody .ln').length,
            online: shadow.querySelectorAll('#nodeGrid .uav.done').length,
            writing: shadow.querySelectorAll('#nodeGrid .uav.writing').length,
            windows: shadow.querySelectorAll('.win-popup').length,
            winNames: Array.from(shadow.querySelectorAll('.wp-name')).map((n) => n.textContent).join(','),
            elapsed: (() => {
              for (const row of shadow.querySelectorAll('.wp-row')) {
                const key = row.querySelector('.k');
                if (key !== null && key.textContent.trim() === 'ELAPSED') return row.querySelector('.v').textContent;
              }
              return null;
            })(),
            final: shadow.querySelector('#final.show') !== null,
            boot: shadow.querySelector('#boot') !== null,
            bootCard: document.querySelector('[data-dsh-boot]') !== null,
            sawBootCard: host.dataset.sawBootCard,
          };
        })()`)
      timeline = []
      const started = Date.now()
      // Keep sampling through the pre-shell phase: the overlay is absent until
      // the shell mounts, so "absent" only ends the run AFTER it was once seen.
      let seenOverlay = false
      for (let i = 0; i < 60; i++) {
        const sampleRow = await sample()
        timeline.push({ ms: Date.now() - started, ...sampleRow })
        if (sampleRow.gone !== true) seenOverlay = true
        else if (seenOverlay) break
        await sleep(400)
      }
    }

    const state = await evaluate(`(() => {
      const host = document.querySelector('.dsh550c-host');
      const shadow = host === null ? null : host.shadowRoot;
      const style = host === null ? null : getComputedStyle(host);
      const top = document.elementFromPoint(Math.round(innerWidth / 2), Math.round(innerHeight / 2));
      const boot = shadow === null ? null : shadow.querySelector('#boot');
      const count = (sel) => (shadow === null ? 0 : shadow.querySelectorAll(sel).length);
      const textOf = (sel) => { const el = shadow === null ? null : shadow.querySelector(sel); return el === null ? null : el.textContent; };
      const attrOf = (sel, name) => { const el = shadow === null ? null : shadow.querySelector(sel); return el === null ? null : el.dataset[name]; };
      const elapsedRow = () => {
        if (shadow === null) return null;
        for (const row of shadow.querySelectorAll('.wp-row')) {
          const key = row.querySelector('.k');
          if (key !== null && key.textContent.trim() === 'ELAPSED') return row.querySelector('.v').textContent;
        }
        return null;
      };
      return {
        enhance: shadow === null ? null : {
          stampedLines: count('#mainBody .ln[data-ts]'),
          sampleStamp: attrOf('#mainBody .ln[data-ts]', 'ts'),
          gutters: count('#codeBody .cln[data-ln]'),
          sampleGutter: attrOf('#codeBody .cln[data-ln]', 'ln') + ' @ ' + attrOf('#codeBody .cln[data-ln]', 'addr'),
          headTag: textOf('#w-code .w-head .tag'),
          foot: textOf('#w-code .w-foot'),
          nodeBars: count('#nodeGrid .uav .sig'),
          nodeVersions: count('#nodeGrid .uav .ver'),
          remappedLogs: count('.wp-log .ll[data-remapped]'),
          sampleLog: textOf('.wp-log .ll'),
          elapsed: elapsedRow(),
          deadlineRaw: shadow === null || shadow.querySelector('#mainBody') === null
            ? null
            : /T-00:03:41/.test(shadow.querySelector('#mainBody').textContent),
          deadlineLines: shadow === null ? null : Array.from(shadow.querySelectorAll('#mainBody .ln'))
            .filter((el) => /时间窗口/.test(el.textContent))
            .map((el) => el.textContent),
        },
        host: host !== null,
        hostZ: style === null ? null : style.zIndex,
        hostOpacity: style === null ? null : style.opacity,
        hostSize: host === null ? null : host.getBoundingClientRect().width + 'x' + host.getBoundingClientRect().height,
        topAtCenter: top === null ? null : (top.className || top.tagName),
        hasShadow: shadow !== null,
        bootInShadow: boot !== null,
        bootOpacity: boot === null ? null : getComputedStyle(boot).opacity,
        appInShadow: shadow !== null && shadow.querySelector('#app') !== null,
        finalShown: shadow !== null && shadow.querySelector('#final.show') !== null,
        logLines: shadow === null ? 0 : shadow.querySelectorAll('#mainBody .ln').length,
        nodesOnline: shadow === null ? 0 : shadow.querySelectorAll('#nodeGrid .uav.done').length,
        mode: window.localStorage.getItem('dsh-550c-boot:mode'),
      };
    })()`)

    const probe = args.probe === true ? await evaluate('window.__dsh550cProbe ?? null') : null

    const shot = await cdp.send('Page.captureScreenshot', { format: 'png' })
    mkdirSync(dirname(outPath), { recursive: true })
    writeFileSync(outPath, Buffer.from(shot.data, 'base64'))

    if (timeline !== null) {
      console.log('   ms  stage      pct    node        logs  on  wr  wins  elapsed  final  bootCard  overlay  sawCardAtMount')
      for (const row of timeline) {
        if (row.gone === true) {
          console.log(`${String(row.ms).padStart(5)}  overlay absent${row.sawBootCard === undefined ? '' : '   sawBootCard=' + row.sawBootCard}`)
          continue
        }
        console.log(
          `${String(row.ms).padStart(5)}  ` +
            `${String(row.stage ?? '—').padEnd(10)}${String(row.pct ?? '—').padEnd(6)}${String(row.node ?? '—').padEnd(11)}` +
            `${String(row.logs).padStart(4)}${String(row.online).padStart(4)}${String(row.writing).padStart(4)}` +
            `${String(row.windows).padStart(6)}  ${String(row.elapsed ?? '—').padEnd(8)}` +
            `${String(row.final).padStart(6)}  ${String(row.bootCard).padEnd(9)}  yes      ${row.sawBootCard ?? ''}`,
        )
      }
    }

    console.log(
      JSON.stringify(
        { dismissed, dismissedAfterReload, at, mode, scheme, state, settings, skip, probe, logs: logs.slice(-25), screenshot: outPath },
        null,
        2,
      ),
    )
    if (args.keep !== true) cdp.close()
  } finally {
    if (args.keep !== true) cleanup()
  }
}

main().catch((error) => {
  console.error('verify failed:', error)
  process.exit(1)
})
