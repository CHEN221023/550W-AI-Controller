# 桌面标题栏与 macOS 拖拽

[← 回到 README](../README.md)

## Windows：那三个原生按钮

桌面壳给 Electron 的是 `titleBarStyle:'hidden'` + `titleBarOverlay`（高 40，宽约 3×46=138，
`color: chromeFallbackFill()` 暗色 `#1b1b1c`，`symbolColor: #f9fafb`），也就是说
**─ □ ✕ 是浏览器进程画在网页之上的**：文档里任何 z-index 都碰不到它，插件也删不掉它（原作自己的
─ □ ✕ 在假窗口标题栏里，那是设计的一部分，与本条无关）。

插件不试图盖它，而是**收编它**：

- **让位**：`#hud-top` 按 `env(titlebar-area-width)` 预留出条带宽度，HUD 自己的 `TIME / ● REC`
  不再被压在按钮底下；普通浏览器标签页没有这个 env，预留自然塌成 0。
- **条带透明**：桌面 preload 会建一个探针元素，把它的 computed `background-color` / `color` 经
  `dsh-desktop:windows-appearance` 交给 `setTitleBarOverlay({color, symbolColor})`，并且 `<head>`
  一变就重量一次。两个值都接受 alpha，所以片头挂载时把探针的底色设成 **`transparent`**
  （preload 用 canvas 归一化成 `rgba(0, 0, 0, 0)`，主进程的颜色校验接受它），符号色设成当前配色 ——
  那几个按钮就**浮在动画上**，不再压着一条不透明的色带；卸载时移除这张样式表，`<head>` 的这次变更
  本身就是重量触发，主题自动还原。

两个实现上的坑（都在真机上量到过）：

1. 颜色必须写在**探针元素自己**身上，不能写 `:root`。探针是 body 级 `<span>`，内联样式读的是
   `var(--dsw-specific-sidebar-fill)` / `var(--dsw-alias-label-primary)`，而 app 把这两个变量定义在
   `body` 上 —— 自定义属性按**最近祖先**解析，`html` 上的 `!important` 压不过 `body` 上的普通声明，
   所以 `:root` 方案在真机上完全没生效（整段片头条带都没变）。
2. 读配色要等增强层样式表挂上之后再读：`--caption-symbol` 由 `src/enhance.js` 定义，早读会静默
   落到兜底值，条带就一直是错的颜色。

## macOS：不吞掉窗口拖拽

官方基础样式表把**每个 body 直接子元素**都算成 `-webkit-app-region: no-drag`（选择器只放过应用
自己的根元素），于是铺满视口的 body 级元素会**把整扇窗口从 macOS 可拖拽区域里减掉**：片头在放的
时候，标题栏拖不动、双击缩放也失效。`pointer-events: none` 不豁免这件事，只有元素自己声明才行。

所以遮罩挂载时同时做两件事：

- 宿主元素带 `data-dsh-boot-splash` —— 这是 `dsh-web` 全家桶（`dsh-web-all`）专门豁免的三个标记之一；
- 自己往 `<head>` 塞一张 `html[data-platform="darwin"] body>.dsh550c-host{-webkit-app-region:initial !important}`
  —— 没装全家桶的纯 DSH 靠这一条。

`initial` 是初始值（`none`），作用是让元素**退出** app-region 计算，而不是把整块遮罩变成拖拽把手，
所以点击跳过照旧有效；`!important` 是必需的，官方那条选择器权重更高。看得到的区别：加了这条，
遮罩的 computed `-webkit-app-region` 是 `none`，官方 `[data-window-drag]` 标题栏行保持 `drag`；
去掉它，遮罩变成 `no-drag`（就是被吃掉的 bug）。`.verify/run-harness.mjs` 的
`darwin · window drag guard` 用例把这四个值都断言了。顶部 40px 条带在这段时间归窗口拖动，
点击跳过请点别处或按 `Esc`。

## macOS 上还没做的（评估稿）

`hiddenInset` + `trafficLightPosition{x:16,y:18}`、没有 `titleBarOverlay`（所以
`env(titlebar-area-*)` 在 macOS 上不存在）、全屏时 preload 会打 `html[data-fullscreen]`——
据此还有三处待改（让位常量与全屏塌陷、darwin 不再注入标题栏换色、HUD 条带标 `data-window-drag`），
详见 [PLAN-macos-and-update-check.md](PLAN-macos-and-update-check.md)。
