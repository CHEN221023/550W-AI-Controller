# 构建与验证

[← 回到 README](../README.md)

## 开发

```sh
npm run build              # extract + build（只重写 lib/client.js）
node --check lib/client.js
node --check lib/index.js  # 宿主半边是手写的，同样要过一遍
```

构建是**幂等**的：`npm run build` 后再 `git diff --exit-code -- lib/client.js` 必须是空的，
CI 就在盯这件事。

## 首帧 / 标题栏（不需要起 GUI）

`.verify/build-harness.mjs` + `.verify/run-harness.mjs` 把**真实的 index.html**（从 app.asar 里取出
的 web-frontend dist）用 **DSH 自己的行渲染器**（`.verify/dsh-render-rows.mjs`，逐字复制自
`@deepseek-ai/dsh-host-webserver`）套上**插件自己产出的行**，再用无头 Edge 跑：

```sh
node .verify/build-harness.mjs                      # 收集行 + 渲染 harness 页
node .verify/serve.mjs 3499                         # 静态服务器（后台）
node .verify/run-harness.mjs 3499                   # 跑六组探针 + 四张截图（含 darwin 拖拽守卫）
```

| 断言 | 结果 |
|---|---|
| 首帧早于任何插件代码（`coverApplied`） | true（控件页同页面为 false） |
| 首帧退役与遮罩挂载同一任务（`coverAfterModule`） | false（类已移除）、握手全局已消费 |
| 桌面平台下挂载配色（`data-platform=win32`） | `data-caption=windows`，`<head>` 有了换色样式表 |
| 配色随档位/方案 | 简易 `#050403`、完整 `#141008`，符号色 `#e8a020` |
| 预留规则存在、浏览器里塌成 0 | true / `padding-right: 0px` |
| 关闭档 | 不作画、不挂载、不定义握手全局 |
| macOS 拖拽守卫（`data-platform=darwin`） | 宿主带 `data-dsh-boot-splash`；computed `-webkit-app-region`：有守卫 `none`、抽掉守卫 `no-drag`（对照）、插回 `none`；官方 `[data-window-drag]` 行仍为 `drag`；点击后遮罩正常卸载 |

截图在 `.verify/shots/`：同样的页面，**有行**时是纯黑首帧，**抽掉行**时 HARNESS 卡片就在那儿。

## 真实 GUI

`scripts/verify.mjs` 用 DevTools 协议驱动真实浏览器，可以在**精确时刻**、**指定模式**下截图并读取
遮罩层的内部状态。普通 `--screenshot` 做不到：模式存在 localStorage 里，而且新 profile 的引导弹窗
挡在整个 shell 前面，不点掉它插件的槽位根本不存在。

```sh
node scripts/verify.mjs --url 'http://127.0.0.1:3080/?token=…' --mode simple --at 2600
node scripts/verify.mjs --url '…' --mode full  --at 7000
node scripts/verify.mjs --url '…' --mode off   --at 1000 --settings   # 设置行巡检
node scripts/verify.mjs --url '…' --mode full  --at 3000 --skip       # 跳过验证
```

已实测通过的项（web profile，`link:` 安装）：

| 项 | 证据 |
|---|---|
| 简易模式播放 | 2.6s 截图：logo 正在逐路径书写，红 0 已发光，`550C SYSTEM BOOT` 打字机 |
| 简易模式收尾 | 7s 时 `host: false`，屏幕顶层元素变回 DSH 输入框 |
| 完整模式播放 | 7s 时 `appInShadow: true`、63 行日志、47 节点全部 `done` |
| 完整模式收尾 | 15s 时 `host: false`，遮罩已卸载 |
| 关闭档 | `--mode off` 时 `host: false`，遮罩从不挂载 |
| 通用设置行 | `rowTitle: "550C 开机动画"`，三档 `["关闭","简易","完整"]`，当前档高亮，`预览` 按钮在 |
| Esc 跳过 | 3s 时 `hostBefore: true` → Esc 后 `hostAfterEsc: false` |

截图存在 `.verify/shots/`；README 顶部那三张对外预览在 [`docs/`](.)。
`.verify/` 整个目录已 gitignore —— 里面有从 app.asar 取出的 DSH 包文件，不该进仓库。

> web profile 首次启动会串联几个引导弹窗（内测声明 → API Key），它们盖在整个 shell 之上，
> 所以全新 profile 下动画会被弹窗挡住。桌面 profile 的引导已经走完，不存在这个问题。
