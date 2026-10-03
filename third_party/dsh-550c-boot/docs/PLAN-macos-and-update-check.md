# 评估：macOS 适配 + 设置里的「检查更新」

状态：**评估稿**（未实现）。所有"事实"都标了取证来源；没有来源的一律写成待确认。

---

## 一、macOS 适配

### 1.1 桌面壳在 macOS 上怎么起窗口（取自 `app.asar` 字符串取证）

```js
// 主窗口
process.platform === 'darwin'
  ? { titleBarStyle: 'hiddenInset', trafficLightPosition: { x: 16, y: 18 },
      vibrancy: 'sidebar', visualEffectState: 'active', backgroundColor: '#00000000' } : {}
process.platform === 'win32' && primary
  ? { titleBarStyle: 'hidden', titleBarOverlay: { height: 40, color: chromeFallbackFill(),
      symbolColor: dark ? '#f9fafb' : '#0f1115' } } : {}
```

- macOS **没有 `titleBarOverlay`**（Windows/Linux 专属），全库 `titlebar-area` **0 命中** →
  我们 Windows 那条 `env(titlebar-area-width)` 在 macOS 上**根本不存在**，必须靠常量/其它信号。
- 红绿灯位置是 **x:16, y:18**（欢迎窗是 21,21）。

### 1.2 preload 交给页面的事实（这是插件唯一能用的契约）

| 信号 | 平台 | 来源 |
|---|---|---|
| `html[data-platform="darwin"]` | macOS | preload；DSH 自己的 CSS 大量按它分叉（`html:not([data-platform=darwin]) …`） |
| `html[data-windows-titlebar]` + `--dsh-windows-titlebar-height:40px` | Windows | `syncWindowsAppearance()`，仅在 `win32` 下标记 |
| `html[data-fullscreen="true"]` | macOS **和** Windows | 主进程 `enter/leave-full-screen` → preload 写 `root.dataset.fullscreen` |

### 1.3 DSH 自己的 macOS 约定

- 展开列在 darwin 下开一条 **52px 顶部条带**避让 `hiddenInset` 红绿灯；条带与 logo 行自己标
  **`data-window-drag`**（官方可拖拽标记，Windows 的 caption 行同理）。
- `--dsw-specific-sidebar-fill` 既是 **Windows 标题条底色**（`[data-windows-titlebar] .frame{…background:var(--dsw-specific-…)}`），
  也是 **侧栏底色**（`dsh-client-ui-sidebar/SidebarRoot.module.css`）。

### 1.4 我们现在的 mac 行为逐条体检

| 项 | 现状 | 判断 |
|---|---|---|
| 窗口拖拽 | 本轮已修：`data-dsh-boot-splash` + `-webkit-app-region:initial !important` | ✅ 已按官方语义修好 |
| HUD 让位 | `:host([data-caption="darwin"]) #hud-top{padding-left:86px}` | ⚠️ 86px 是拍的：真实几何 = x(16) + 三灯簇(≈52) + 间隙 ≈ **76px**；且**全屏时不塌**（macOS 全屏会藏红绿灯） |
| 标题栏换色 | `adaptCaption()` 在 darwin 也注入 `:root{--dsw-specific-sidebar-fill;--dsw-alias-label-primary}` | ⚠️ macOS 上没有 OS 条带可换 → 死代码；副作用是短暂改掉**全局主文字色**与**侧栏底色**（被遮罩盖住时不可见，但不该做） |
| 顶部条带可拖 | 未标 `data-window-drag` | 🔧 对齐官方约定，让 HUD 条带在片头期间可拖 |
| 全屏 | 完全没读 `html[data-fullscreen]` | 🔧 Windows 的 `env()` 会自己塌，darwin 的固定 padding 不会 |
| vibrancy / 透明窗 | 遮罩是不透明底色，渐出时透出 vibrancy | ✅ 符合预期 |

### 1.5 建议改动（都很小）

- **M1 让位与全屏**：JS 把平台与全屏状态镜像到宿主元素（`data-caption` 已有，新增 `data-fullscreen`），
  用 `MutationObserver` 盯 `<html>` 属性变化（片头 16 秒里用户完全可能切全屏）；
  CSS 改成 `padding-left:76px`，`[data-fullscreen]` 时塌成 0。常量在注释里写清推导（16+52+8）。
- **M2 只在 win32 注入标题栏换色**：darwin 只保留 `data-caption` 属性与让位规则，不再改全局 token。
- **M3 HUD 条带标 `data-window-drag`**（完整档有 `#hud-top` 时）。
- **M4 真机验证**：本机是 Windows，**无法验证 macOS 真实窗口行为**（拖拽、红绿灯、全屏）。
  缓解：改动全部对齐官方常量与标记，harness 用 `data-platform="darwin"` + `data-fullscreen` 断言；
  真机确认请交给有 mac 的人（例如 dsh-web 的维护者，他显然在 mac 上）。

---

## 二、设置里的「检查更新」

### 2.1 DSH 现在有什么（取证）

- 设置里有插件页，但**都是只读**：
  `dsh-client-ui-settings-plugin-inventory`（"Read-only Cordis Loader inventory tab"）、
  `dsh-client-ui-settings-plugins`（"feature-owned tabs and configurable host-plane plugin cards"）；
  这两个包 + `dsh-host-plugin-inventory` 里 `checkUpdate|latestVersion|outdated|升级|检查更新` **0 命中**。
- 安装/升级走 **profile 工程**：`~/.dsh/profiles/<name>/package.json` 的 `dependencies` + `dsh.profile.bundles`，
  由包管理器装配（本机 web / desktop 两个 profile 都是；我们的是 `link:`）。
- 桌面 app 自己的更新是另一条线（`app-update.yml`、`preload-update-dialog.cjs`、`renderer/mandatory-update.*`），与插件无关。
- 宿主 webserver 只公开 `registerFallback` / `registerUpgrade`（精确/前缀路由表不是公开 API）→
  **第三方插件没有官方"加自己的 HTTP 路由"入口**（待确认；若确实没有，方案 B 就不能走路由）。
- 页面 `index.html` **无 CSP meta**，GitHub API 允许 CORS → 客户端直接 fetch 是可行的。

### 2.2 三个方案

**A. 纯客户端检查 + 引导（建议 v1）**

- 客户端半边在设置行加「检查更新」按钮 → `fetch('https://api.github.com/repos/yannicksong0106/dsh-550c-boot/releases/latest')`。
- 当前版本由**宿主半边**在 `webserver/index-inject` 时用 `global` 行注入（页面不用猜、也不用打接口）。
- 结果分支：有新版本 → 版本号 +「复制安装命令」+「打开 Release 页」（走官方 `open-in-app` 服务）+「重新检查」；
  网络失败/被墙 → 明确提示 + 给 Release 链接，不静默失败。
- 代价：新增一条诚实声明（例如 `network:github-releases`）；每次点击一次外网请求。
- 速率限制：未认证 GitHub API 60 次/小时/IP → 结果缓存（localStorage，例如 6 小时）足够。

**B. 宿主侧检查（可选增强）**

- 宿主半边在启动时（或按天缓存）用 node fetch 查一次，把结果随行注入页面；按钮只负责展示 + 打开链接。
- 优点：无 CORS/浏览器侧隐私面，web 与 desktop 行为一致；缺点：新鲜度 = 页面加载时刻（「重新检查」得刷新）。
- 前提：能确认宿主半边在启动阶段联网是可接受的（每次启动一次请求，可缓存 24h）。

**C. 真·一键更新（不建议 v1）**

- 需要宿主去改 profile 的 `package.json` 并跑包管理器（`pnpm add github:…#vX.Y.Z`），风险：profile 写坏、并发写、
  需要新权限、必须重启；DSH 也没给插件这个能力（只有只读清单）。
- 折中（v2）：宿主侧"改 profile + 提示重启"，但必须在 README 写清失败回滚，且默认关闭。

### 2.3 建议与验收

- **v1 = A**（+ 宿主注入版本号 + `open-in-app` 打开 Release + 复制命令）——这正是"辅助使用者更新"：
  用户点一下知道有没有新版、怎么升，**不替用户做静默写操作**。B 视需要再加。
- 验收：
  1. harness：按钮与状态渲染（未知/检查中/已是最新/有新版/失败五态）；
  2. 版本比较的单元测试（`0.1.10 > 0.1.9`、`-rc.1` 预发布语义、`v` 前缀）；
  3. 一次真实点击的截图（`scripts/verify.mjs --settings`）；
  4. 断网/被墙路径的手动验证（拔网线或改 hosts）。

### 2.4 工作量与风险

| 项 | 估时 | 主要风险 |
|---|---|---|
| M1–M3 mac 适配 | 1–2 小时（含 harness 用例） | 本机无法验证真实 mac 窗口行为 |
| A 检查更新 | 2–3 小时 | GitHub API 速率限制（用缓存化解） |
| B 宿主侧检查 | +1 小时 | 启动期多一次外网请求（缓存 24h） |
| C 一键更新 | 不建议 v1 | 写坏 profile 的代价远大于省下的手工步骤 |

---

## 三、待确认（需要你或维护者拍板）

1. 更新检查走 **A**（纯客户端）还是 **A+B**（宿主也查一次）？要不要在 README/manifest 里声明
   `network:github-releases`？
2. macOS 那三条改动（M1–M3）是否现在就做？真机验证是否需要找人（例如请 dsh-web 维护者代跑一次）？
3. 要不要顺带把「检查更新」也做成可配置（默认开启/关闭，存 localStorage，和现有偏好一致）？
