#!/usr/bin/env node
/**
 * Emit the OMDSH Workshop submission manifest for this repository.
 *
 * The Hub's intake wants a `omdsh-workshop-submission/v2` document pinned to a
 * full 40-character commit that is already public, and it refuses manifests
 * whose facts do not match the pinned commit — so this generator reads the
 * facts from the working tree (package.json, git HEAD) instead of asking anyone
 * to retype them, and refuses to run on a dirty tree.
 *
 * Zero dependencies on purpose: the same reason scripts/build.mjs has none.
 * Schema validation is the Hub's own job (`scripts/intake.mjs validate` in
 * omdsh-dev/dsh-hub-workshop); .verify/validate-json.mjs does a local
 * ajv-based check as scaffolding.
 *
 * Usage:
 *   node scripts/make-submission.mjs                 # print the manifest
 *   node scripts/make-submission.mjs --out sub.json  # write it to a file
 *   node scripts/make-submission.mjs --ref <sha>     # override the pinned commit
 *   node scripts/make-submission.mjs --channel stable
 *
 * See docs/PUBLISHING.md for the whole release path.
 */
import { execFileSync } from 'node:child_process'
import { readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const argv = process.argv.slice(2)

function option(name, fallback) {
  const at = argv.indexOf(`--${name}`)
  return at === -1 ? fallback : argv[at + 1]
}

const git = (...args) => execFileSync('git', args, { cwd: root, encoding: 'utf8' }).trim()

const pkg = JSON.parse(readFileSync(resolve(root, 'package.json'), 'utf8'))
const workshop = pkg.dshWorkshop
if (workshop === undefined) throw new Error('make-submission: package.json#dshWorkshop is missing')

const dirty = git('status', '--porcelain') !== ''
if (dirty && option('ref', undefined) === undefined) {
  throw new Error(
    'make-submission: working tree is dirty — the Hub only accepts facts that are already committed.\n' +
      '  Commit (or stash) first, then re-run. Nothing uncommitted may enter a submission.',
  )
}

const ref = option('ref', git('rev-parse', 'HEAD'))
if (!/^[0-9a-f]{40}$/.test(ref)) throw new Error(`make-submission: --ref must be a full 40-char commit, got "${ref}"`)

const repository = (pkg.repository?.url ?? '').replace(/^git\+/, '').replace(/\.git$/, '')
if (!/^https:\/\/github\.com\/[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+$/.test(repository)) {
  throw new Error(`make-submission: package.json#repository.url must be a GitHub https URL, got "${repository}"`)
}
const slug = repository.replace('https://github.com/', '')

const submission = {
  schema: 'omdsh-workshop-submission/v2',
  operation: 'create-project',
  project: {
    id: pkg.name,
    displayName: '550C 开机动画 / 550C boot splash',
    summary:
      '启动 DSH 时全屏播放 550C 片头（简易 4s / 完整 16s，可跳过，四套磷光配色）。首帧由宿主半边注入，' +
      "DSH 自己的 Loading 卡片不会露脸；桌面窗口的原生标题栏按钮会被收编成终端配色。",
    kind: 'ui',
    category: 'interface',
    tags: ['boot', 'splash', 'startup', 'animation', 'theme', 'web-ui', 'deepseek-harness'],
    repository,
    path: null,
    author: {
      name: slug.split('/')[0],
      url: `https://github.com/${slug.split('/')[0]}`,
    },
    license: pkg.license ?? 'MIT',
    media: {
      cover: 'docs/preview-full.png',
      screenshots: ['docs/preview-simple.png', 'docs/preview-cyan.png'],
    },
  },
  release: {
    version: pkg.version,
    ref,
    updatedAt: new Date().toISOString(),
    channel: option('channel', 'beta'),
    compatibility:
      `实测于 @deepseek-ai/dsh@${(workshop.compatibility?.dshVersions ?? []).join(' / ') || '未声明'}；` +
      'Hub 公布的公开基线（official-baseline.json）尚未复验，因此只声明实测版本，不声明兼容区间。',
    changelog:
      '首发：宿主半边用 webserver/index-inject 注入首帧（DSH 的 Loading 卡片不再露脸）；' +
      '客户端遮罩模块求值即挂载、同帧退役首帧；桌面原生标题栏按钮改为让位 + 换色；' +
      '内容增强层（终端质感 / 数据自洽 / 面板深度）与四套配色。',
    capabilities: {
      requiresFabric: false,
      deepHook: false,
      restartRequired: workshop.lifecycle?.activation === 'restart-profile',
    },
    profileBundle: {
      packageName: pkg.name,
      // Pinned to the same commit the submission is pinned to: installable
      // straight from GitHub without an npm publication. Swap for the bare
      // version ("0.1.0") if the package is published to npm instead.
      spec: `github:${slug}#${ref}`,
    },
    updateFrom: null,
  },
  management: {
    method: 'profile-bundle',
    protocol: workshop.integration?.protocol ?? 'harness-profile',
    label: '550C 开机动画',
    instructions:
      'dsh plugin --profile <profile> add github:' +
      `${slug}#${ref}` +
      '\n装完重启一次 DSH（bundle 层在启动时装配）；升级后在新窗口 Ctrl+Shift+R 硬刷新。' +
      '\n模式开关在 设置 → 通用 → 550C 开机动画。',
    // Null for this route on purpose: the submission schema only carries a
    // `source` coordinate for `repository-plugin` (github:owner/repo), and
    // rejects a non-null one for a `profile-bundle`. The pinned coordinate for
    // this submission lives in release.profileBundle.spec instead.
    source: null,
  },
  declarations: {
    permissions:
      '权限：web:index-inject（向 index 响应注入一条 style + 一条同步 script，即首帧）；' +
      'dom:overlay（在 document.body 挂全屏 shadow DOM 遮罩，含一张 <style> 与一个 caption 换色样式表）；' +
      'browser:local-storage（dsh-550c-boot:mode / :scheme 两个偏好）；' +
      'desktop:titlebar-overlay（改写 --dsw-specific-sidebar-fill 与 --dsw-alias-label-primary，' +
      '桌面 preload 会据此重设 Electron 标题栏叠层颜色）。' +
      '无网络请求、无文件系统访问、无子进程、无安装期脚本、无外部副作用。',
    testing:
      '作者侧：scripts/verify.mjs（CDP 驱动真实浏览器，按时刻截图并读遮罩内部状态）在本机 web profile 回放' +
      '简易/完整/关闭三档与设置行；.verify/ 的 harness 用 DSH 自己的 index 行渲染器套上本插件产出的行，' +
      '在无头 Edge 中断言首帧先于任何插件代码、同帧退役、caption 换色与让位规则；' +
      'npm run build 幂等，CI 校验 lib/client.js 与源码同步。' +
      '尚未由 Hub 适配器在官方基线上复验。',
    trustedPublisherRequested: false,
    installScriptsMustRemainDisabled: true,
  },
  packageManifest: workshop,
}

const text = JSON.stringify(submission, null, 2) + '\n'
const out = option('out', undefined)
if (out === undefined) {
  process.stdout.write(text)
} else {
  writeFileSync(resolve(root, out), text, 'utf8')
  process.stderr.write(`make-submission: wrote ${out} (${String(text.length)} chars, ref ${ref})\n`)
}
