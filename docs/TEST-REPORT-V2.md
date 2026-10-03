# 550W AI Controller V2 / B2 交付测试记录

日期：2026-09-30～2026-10-01。版本：2.0.0。保留同一 Solution 和原 550C 上游目录；V1 已在修改前备份。最终便携包使用 Windows x64 自包含 .NET 8.0.31，构建 SDK 8.0.425，WebView2 SDK 1.0.4258.31。

## 结果与证据

| 检查层 | 结果 | 证据 |
|---|---|---|
| .NET 单元测试 | 41 / 41 PASS，0 skipped | docs/unit-tests/V2-Final.trx |
| 正式网页动画 / 音频 | 28 / 28 PASS | docs/web-test-v2-results.json、scripts/test-web-v2.cjs |
| Windows 生命周期场景 | 14 / 14 当前场景 PASS，分轮验证 | docs/release-verification.json、docs/native-v2/ |
| 原生补充检查 | 6 / 6 PASS | cancel-assets-caption、voice-release |
| 最小化修复专项回归 | 6 / 6 PASS，含三次最小化循环 | focused-after-minimize-fix |
| 实际产品确认后预加载路径 | 2 次观察均顺序正确 | prepared-confirmation-a / b |
| 原视觉母版保留核验 | BOOT/APP 原字符串一致，CSS 仅统一画布换算，32 原文件 SHA256 | docs/source-preservation.json |
| 一键源码编译脚本 | Restore → 41 tests → 两个自包含 publish → Launcher / VoiceWorker 编译成功 | Build.ps1 实际执行，未仅检查语法 |
| 最终换屏保护编译后的组合复测 | 2 / 2 PASS（本机相同屏幕路径） | shortcut-pair-final；跨屏保护分支没有双屏实测 |

共 89 项上述自动检查 / 场景（41+28+14+6），额外的重复回归和人工确认观察不计入这个总数。Windows 测试对象是本工程 ExternalFixture，不是真实 ChatGPT/DeepSeek/Claude 的账号会话。窗口元数据、WMI、WinEvent、WM_CLOSE、最大化、WebView2 和语音播放是实际 Windows 调用。

本次编译恢复阶段 NuGet 在线漏洞数据查询不可达，出现 NU1900 警告；依赖已从缓存恢复，编译与测试成功。这份记录不把源码边界审计等同于独立第三方安全认证或完整在线依赖漏洞扫描。

## 生命周期场景

| 场景 | 结果 | Boot 次数 | 事后 Shutdown 次数 | 证据轮次 |
|---|---|---:|---:|---|
| 被动启动与普通退出 | PASS | 1 | 0 | main-before-minimize-fix |
| 最小化 / 恢复 | PASS | 1 | 0 | focused-after-minimize-fix |
| 三个辅助进程去重 | PASS | 1 | 0 | main-before-minimize-fix |
| 15 秒无响应窗口的 Ready 判断 | PASS | 1 | 0 | main-before-minimize-fix |
| 标准 300 ms 优化快捷方式 / 最大化 | PASS | 1 | 0 | shortcut-timing |
| 开启优化关闭后直接普通退出，仍不补播 | PASS | 1 | 0 | main-before-minimize-fix |
| 快速确认 / 原生关闭保底 / 四次重复请求 | PASS | 1 | 0 | main-before-minimize-fix |
| 异常进程退出不补播 | PASS | 1 | 0 | focused-after-minimize-fix |
| 缺少 WebView2 时释放 | PASS | 1 | 0 | focused-after-minimize-fix |
| 卡住启动的 Watchdog | PASS | 1 | 0 | main-before-minimize-fix |
| 隐藏托盘 / 重新打开 | PASS | 2 | 0 | focused-after-minimize-fix |
| 两应用同时启动 / 串行动画 | PASS | 2 | 0 | main-before-minimize-fix |
| 0 ms 同时启动 / 最大化 | PASS | 1 | 0 | shortcut-pair |
| 优化启动与优化关闭共同开启 | PASS | 1 | 0 | shortcut-pair |

主发布候选轮曾有一项最小化失败：1 秒最小化期间，被暂时没有可见窗口误判为目标消失。修复为最小化窗口优先保留存在性、延长短暂无窗口保护，再通过专项三次最小化循环及其他相关回归。原失败日志仍在 main-before-minimize-fix；本表以修复后的结果替换相同场景，不把旧一轮描述为全通过。

## Optimized Shutdown 的 14 项要求覆盖

| 要求 | 已验证 / 实际范围 |
|---|---|
| 禁用优化关闭 | 普通退出零事后 Shutdown；源码 Stop 两层防护 |
| 开启后的确认框 | 两次真实产品确认框观察、重复请求只一会话 |
| 等待确认时预加载 | PRELOAD_READY 在确认之前；隐藏且无 SHUTDOWN_BEGIN |
| 取消 | 原生 IPC 取消入口与按钮使用同一 Cancel 回调；应用继续、进程释放、无 WM_CLOSE / 全屏 / 关闭声音；取消按钮鼠标点击本身未做自动化认证 |
| 覆盖先于关闭 | 实际原生绘制回执 + DwmFlush，OverlayVisible → ShutdownBegin → WM_CLOSE，且只有一次 |
| 避免桌面先暴露 | 上述顺序已验证；未取得全桌面逐帧录像，未对实际 ChatGPT 提供帧级认证 |
| 重复点击 | 3 / 4 次请求去重，重复取消去重，网页重复 activate 去重 |
| 100 / 125 / 150 / 200% DPI | 真实 Windows 只测 100%；四组物理坐标单元测试和四组浏览器 DPI 回归通过，其余原生 DPI 待实机验证 |
| 最大化 | 实际目标窗口 IsZoomed=true；原生标题栏 normal/moved/resized/maximized 全部可可靠定位 |
| 多显示器 | 实现按目标 / 光标 / 主屏 / 指定屏选择及负坐标换算；本机只有一个物理显示器，未完成混合 DPI 双屏实测 |
| UIA / 代理失败 | 只启用已知标准标题栏且全部检查通过的代理；自定义标题栏为 Managed Close，未宣称真实 AI 的 X 已支持 |
| 动画加载失败 | 缺失 WebView2 和快速确认的原生静音 Collapse 通过，目标仍收到正常关闭 |
| Task Manager Kill / 崩溃 | 用 fixture 异常退出模拟强制结束；零补播；未操作用户的 Task Manager 或进行真实系统注销 / 关机实验 |
| 与优化 Shortcut 共存 | 真实 .lnk 启动、完整 Boot、最大化、Managed Close、一次 WM_CLOSE、释放，全部通过 |

不拦截 Alt+F4、系统注销、Windows 关机、外部 Kill。应用自己的 WM_CLOSE 可以拒绝关闭或进入托盘，550W 不用 Kill 改变该行为。

## 延迟和占用

| 指标 | 当前实测 |
|---|---|
| 被动事件进入 Controller → 已绘制首帧 | 多次约 64–81 ms |
| 标准 +300 ms Shortcut，Launcher 入口 → 首帧 | 最新两次 104.9 / 96.0 ms |
| 标准 Shortcut，Windows Shell 发起 .lnk → 首帧 | 两次 314.8 / 178.1 ms；不包含真人鼠标硬件输入延迟 |
| 同时启动 Shortcut，Shell 发起 → 首帧 | 一次 438.7 ms；其中 Launcher 入口 → 首帧 224.4 ms |
| 完整 WebView 页面准备 | 冷初始化约 3.9–4.3 秒，单独 WEB_READY 指标 |
| Standard 完整 Boot（语音关闭） | 约 16–17 秒，含核心 / 冷 WebView / 原版完整约 11 秒编排 |
| Boot 最终 Ready 语音开启 | 额外等待短句完成，最多等 4 秒；当前完整播报轮约 20 秒结束 |
| 关闭确认之后原生覆盖 | 两次已预加载观察 157 / 168 ms |
| 关闭演出 | 独立约 2.6 秒 + 约 0.3 秒退场 |
| Controller 退出动画后的私有内存 | 约 31–53 MB |
| Controller 工作集（包括共享运行库） | 多场景约 94–125 MB；没有达到 20–50 MB 的工作集目标 |
| 空闲 CPU | 2 秒短采样，大多 0；单核口径约 0–2.34%，本机 16 逻辑处理器，总 CPU 约 0–0.15%；不是长期性能压力报告 |
| 结束资源 | 所有测试场景无残留 550W.Animation；语音测试无残留 550W.VoiceWorker |

首帧与完整 HUD 是不同指标。原生首帧能覆盖冷 WebView 等待，完整原版编排仍然保留。没有把 Launcher 内部的 96–105 ms 当作 Shell / 真人点击的全链路延迟；同时启动场景仍有改善空间。

Shutdown 后台准备在本机冷 WebView 条件下约 4 秒，不能承诺几十到几百毫秒完成全部网页初始化。确认过快时已有原生全屏母版坍缩保底，并在遮罩之后正常关闭；这条原生保底路径是静音的。已预加载的网页路径具有独立模块、CRT、状态事件和完整音效。

最终编译还加入确认时的屏幕矩形校验：等待确认期间目标换屏时，旧屏预加载窗口不被激活，在确认时的当前屏幕使用原生覆盖与坍缩。这一保护不等同于已完成双显示器实测。

## 视觉、音频与数据

正式 Boot 保留原 BOOT/APP DOM 标记、SVG、原框位置 / 尺寸 / 错位 / 重叠、九阶段、九个弹窗与 47 节点；上层仅增加同图机械核心、统一画布、配色、门控及状态事件。时间线记录和帧图见 images-v2；更换了八个原文字槽内容，不重新创建监控卡片。Movie Red 的主色 #e05030 / 高光 #ff7050 来自原 CSS 的 --red / --red-b，#ff2d2d 来自原 SVG 标志的 .red。衍生暗色与 Glow 在附加 CSS 中定义。

八个真实槽：CPU CORE、MEMORY、GPU、VRAM TOTAL、DNS、EXTERNAL ROUTE、VPN ADAPTER、TARGET AI。显存是总量，不声称测了占用。无人机、权限覆写、证书、坐标和代码流为原作者科幻剧情装饰。预览与 fixture 数据不是实际 AI 服务认证。

原生补充检查包含取消、SVG、WebP、标准标题栏四种位置、离线中英语音生成、实际 WebView2 HTML Audio 播放和释放。最新原生语音轮 5 次 VOICE_PLAYING，测试增益 0 避免打扰桌面；音量独立性另外由网页 42% / 37% 回归验证。这证明解码 / 播放调用和生命周期，没有进行主观听感评分。

本机没有可用的 Windows SAPI 声音，最初系统语音探测失败；默认改用通用 eSpeak NG 离线机器合成，并通过上述验证。它需要已安装 VC++ v14 x64 Runtime；本机版本 v14.51.36247.00。引擎 / Worker 的 GPL 许可、完整源码与构建流程随包，没有电影或演员音频。原生默认 11 段音效为本工程合成。

## 尚未认证的范围

当前证据不足以声称真实 ChatGPT/DeepSeek/Claude 自定义 X 代理、MSIX/URI 实际启动、125/150/200% 原生桌面、混合 DPI 双屏、Windows 11 或系统关机全过程都已逐项实机通过。相应代码路径已实现，默认使用保守的 Managed Close，错误放行目标正常使用。这些范围和 RAM / 冷初始化 / 快速关闭静音保底限制均保留在交付记录中。

V1 的便携目录与修改前 ZIP 不被 V2 覆盖；V2 独立目录可运行，升级前应先退出旧托盘进程。请保留整包 EXE / DLL / Themes / VoiceEngines / licenses 的相对位置。
