# 更新日志

遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)；版本号用 [语义化版本](https://semver.org/lang/zh-CN/)。

## [0.1.3] - 2026-09-30

### 修复

- **片头到对话页的交接改成两段式**。原来整屏一次性 cross-fade，而片头结尾是一枚放大的高对比度
  logo，那半秒里会有一枚灰色 logo 幽灵盖在已经加载好的对话页上（用真实时间的逐帧截图确认过）。
  现在：内容先淡进片头自己的底色（`--bg`，由遮罩自己绘制）→ 一屏干净的黑 → 再淡出黑底露出对话页。
  总时长 620ms（内容 200ms，黑底延迟 200ms 后 420ms 淡出）。

## [0.1.2] - 2026-09-30

### 变更

- **桌面标题栏条带改为透明**：片头播放期间，右上角那几个原生按钮**浮在动画上**，不再压着一条
  不透明的色带。做法是把 preload 探针元素的 `background-color` 设成 `transparent`、符号色设成当前
  配色 —— 探针这两个 computed 值就是 `setTitleBarOverlay({color, symbolColor})` 的来源。
- 两处只在真机暴露的坑修掉了：颜色必须写在探针元素**自己**身上（app 把 token 定义在 `body` 上，
  `:root` 上的 `!important` 因"最近祖先"规则压不过它，整段片头条带都不变）；读配色要等增强层样式表
  挂上之后再读，否则静默落到兜底值。

## [0.1.1] - 2026-09-29

### 修复

- **macOS：片头播放期间窗口拖不动**。官方基础样式表把每个 body 直接子元素都算成
  `-webkit-app-region: no-drag`，铺满视口的遮罩会把整扇窗口从 macOS 可拖拽区域里减掉，
  双击缩放也失效。现在宿主元素带 `data-dsh-boot-splash`（`dsh-web-all` 全家桶豁免的标记之一），
  并自带一条 `-webkit-app-region: initial !important` 覆盖没装全家桶的纯 DSH。
  点击 / `Esc` 跳过不受影响。

### 文档

- README 重排为「是什么 / 装 / 怎么用 / 配色 / 兼容与限制 / 文档索引」，工程细节拆进 `docs/`：
  `ARCHITECTURE.md`、`ENHANCEMENTS.md`、`DESKTOP-CHROME.md`、`VERIFICATION.md`。
- 新增 `docs/PLAN-macos-and-update-check.md`：macOS 适配待办与设置里「检查更新」的方案评估。
- 新增 README 顶部实时徽章（Stars / Downloads / 最近提交 / 许可 / topic / 访客）。

## [0.1.0] - 2026-09-28

### 新增

- 550C 开机片头：简易档约 4 秒、完整档约 16 秒，点击画面或 `Esc` 跳过，播完渐出。
- 首帧由宿主半边经 `webserver/index-inject` 在文档解析阶段注入 —— DSH 自己的
  `HARNESS / Loading plugins…` 卡片不会露脸；卡片消失或进入失败态时首帧自行退役，另有 12s 上限。
- 通用设置里的设置行：三档（关闭 / 简易 / 完整）、四套配色（琥珀 / 绿 / 青 / 白）与「预览」按钮。
- 桌面窗口右上角原生按钮的收编：`#hud-top` 按 `env(titlebar-area-width)` 让位，
  条带底色与符号色跟随当前配色（走 `dsh-desktop:windows-appearance`）。
- 内容增强层：等宽字体栈、字符单元进度条、块光标与真实时钟戳、固件注入页脚（行号 / 地址 / CRC32）、
  集群节点信号条。

[0.1.1]: https://github.com/yannicksong0106/dsh-550c-boot/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/yannicksong0106/dsh-550c-boot/releases/tag/v0.1.0
