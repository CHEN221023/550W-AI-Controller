# 550W AI Controller V2

在现有第一版 Windows 工程上完成的 V2 增量版本。Controller 常驻托盘，动画由独立的 Animation 进程按需创建。目标 AI 客户端继续使用自己的程序、账号和网络连接。

## 运行

1. 先从旧版 550W 托盘菜单选择「退出」。同一数据目录只允许一个 Controller，旧版仍在后台时，新版启动请求会交给旧版。
2. 解压整个 `550W-AI-Controller-V2-Portable.zip`，保持 EXE、DLL、Themes 和 licenses 的相对位置。双击 `550W-AI-Controller.exe`。
3. 托盘双击打开设置。正常打开 AI 应用后，使用「自动发现 AI 应用」「从当前进程添加」或「从原 Shortcut 添加」，确认进程名、启动目标后保存。
4. 想取得更快首帧，在应用设置里创建一个新的「优化快捷方式」，从这个快捷方式启动。保留原快捷方式即可同时保留原入口。
5. 想播放关闭动画，单独开启该应用的「优化关闭动画」，保存后使用托盘「关闭应用（550W）」或设置里的 Managed Close。默认开启关闭确认。

支持 Windows 10/11 x64。便携包自带 .NET 8 运行库，无需另装 .NET。动画需要 Microsoft Edge WebView2 Evergreen Runtime。缺失时会提示并释放遮罩，应用仍可正常使用；[微软官方下载页](https://developer.microsoft.com/microsoft-edge/webview2/#download-section)可获取 Runtime。程序没有自动下载安装器。发行文件未进行商业代码签名。

可选离线机器语音的 eSpeak NG DLL 还依赖 Microsoft Visual C++ v14 x64 运行库；本机已安装并通过播报测试。若干净电脑没有该组件，可从[微软运行库页面](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist)安装官方 x64 Redistributable。该组件没有被复制进便携包；缺失时优先尝试已安装的系统声音，再静默继续，音效和动画仍可使用。

配置保存在 `%LOCALAPPDATA%\550W AI Controller\settings.json`。升级保留已有应用、声音设置和自定义路径，并迁移 Schema 1 到 Schema 2。写配置前保留 `.bak`；损坏配置另存 `settings.invalid-时间.json`。可另外复制该 JSON 自行备份，退出 Controller 后放回恢复。第一次运行的自启动默认开启，设置「常规」可关闭；使用当前用户 Run 项，不需要管理员权限。

## 已恢复的启动画面

正式主题重新使用上游 `assets/550C-source.html` 提取出的 DOM、CSS、SVG 和完整演出。保留九段 Boot/AUTH/LINK/TUNNEL/ISOLATE/REWRITE/VERIFY/DISARM/COMPLETE、九个前后错位的弹窗、47 个节点、扫描线、边框与脉冲。1920×1080 母版统一缩放，分辨率变化不把它重排成整齐仪表盘。V1 的仪表盘保留为单独 Debug 页面，可从「动画」中的调试入口查看。

默认 Movie Red 使用 `#e05030`、`#ff7050`、`#ff2d2d`；还可选 Amber/Green/Cyan/White，全局和应用可分别配置。原版 SVG 550C 标志保留，启动系统文字为 550W。

第一眼是原创金属机械核心，原生窗口先绘制核心，WebView2 随后接替相同的 PNG。充能约 280 ms、展开默认 900 ms，再进入原版演出。设置可选 PNG/SVG/WebP 核心，转换后的 PNG 缓存在数据目录 `CoreAssets`；原生首帧与网页使用同一缓存。

**4 秒是最低时长，不是原版全部动画的总时长。** 原版完整演出约 11 秒，加核心展开和 WebView2 冷启动，本机 Standard 实测完整交接约 16–17 秒。3/4/5 秒或自定义最低时长不能截断原版演出。交接同时等待完整编排、最低时长和目标窗口 Ready；目标慢时保持最后的 Waiting Loop。Quick/Cinematic 只改变演出速度，最低时长仍按真实毫秒计算。默认遮罩超时为 20 秒，可在设置调整；Cinematic 会自动给完整演出留出时间。Escape 可退出遮罩，点击跳过默认关闭。

Ready 依据进程、主窗口可见、响应与稳定时间；程序不读取目标页面内容或账号状态，因此不声称已经判断 AI 服务端完成登录。正在运行的应用作为监控基线，不在 Controller 启动时补播 Boot。最小化不视为退出；隐藏到托盘后重新打开可开始新会话。

## 优化启动与窗口

每个应用独立选择原入口被动检测 / 优化 Shortcut。新快捷方式默认命名为「原应用名称（550W）」并继承应用图标，支持 EXE、原 `.lnk`、AppUserModelID、URI 和 Shell AppsFolder。也可自行选择 PNG/ICO/EXE/LNK 图标。原快捷方式不会被覆盖，更新或删除只处理 550W 自己创建且目标校验通过的链接。

默认动画优先 300 ms，支持同时、600/1000 ms、自定义或 App 先行。一个 managed launch session 关联之后的进程/窗口事件，重复点击和辅助进程不会造成第二次 Boot。目标窗口默认在稳定后最大化；可改保持、居中、填满工作区，或改为最低时长过半 / 交接前调整。被动检测的窗口调整需要另外勾选。

首帧记录为 `FIRST_VISUAL`，网页准备完成记录为 `WEB_READY`。前者包括触发进入控制器后的原生绘制和 DWM 同步；它不等于鼠标硬件点击到进程创建的全链路时间。Launcher 入口时间会传递到 Controller。两个应用同时启动时共享一个动画队列，排队的第二个首帧会延后。

## 优化关闭

默认关闭，和优化启动完全独立。关闭功能未启用时，目标正常关闭，不播放 Shutdown。即使开启，直接点击不可靠的原 X、进程退出、隐藏、崩溃或外部强制结束也不会事后补播。

Managed Close 的流程：用户发起关闭 → 出现小确认框，同时隐藏、静音预加载 Shutdown → 确认后原生全屏画面先绘制 → 已准备的网页开始 Shutdown → 向目标发送标准 WM_CLOSE → 目标在遮罩后关闭 → 坍缩到机械核心、CRT 横线 / 白点、黑屏 → 释放动画进程。

取消会恢复 RUNNING、保留目标，取消隐藏预加载，没有全屏动画或关闭音效。重复请求只保留一个确认会话、一个动画和一次 WM_CLOSE。资源尚未准备好而立即确认时，使用原生静音坍缩作为快速保护画面；动画/Runtime 异常也会尽快正常关闭目标。正常流程不强制 Kill AI。WM_CLOSE 会尊重应用自身的未保存提示和关闭到托盘策略，550W 不保证把选择驻留托盘的应用强行结束。

关闭动画为独立 `550W Collapse`，默认 2.6 秒，加约 0.3 秒退场。外围模块逐步关闭，HUD 收向机械核心，核心熄灭；具有自己的状态、文字和声音。

确认时重新选择覆盖屏幕。若等待确认期间目标换到另一显示器，不激活旧屏幕的预加载窗口，而在当前屏幕使用原生坍缩保底，保证关闭请求仍在覆盖画面之后发送。

「自动代理关闭按钮」为可选入口。目前只接受 Windows 标准标题栏中经过矩形/可见性检查的 WPF 与 Notepad 窗口，使用 WM_GETTITLEBARINFOEX 和物理屏幕坐标，随移动、尺寸变化、最大化更新。ChatGPT 等自定义标题栏通常会拒绝代理，设置显示 Managed Close 支持说明。**当前没有经过真实 ChatGPT/DeepSeek/Claude X 按钮代理兼容认证。** 对这些应用请使用 Managed Close，原 X 保持正常行为。程序不拦截 Alt+F4、注销、系统关机、崩溃和任务管理器强制结束。

## 声音

默认原创低频机械音效开启、音量 70，机器语音关闭、音量 60。声音、语音、启动播报、关闭播报独立开关，支持静音、测试声音、测试语音。声音由 CORE_INITIALIZING、HUD_VISIBLE、自检项、PROCESS_DETECTED、TARGET_READY、SHUTDOWN_BEGIN、MODULE_OFFLINE、CRT_COLLAPSE、POWER_OFF 等状态事件触发。自检和 Waiting Loop 使用一次性事件键，避免重复播放。

默认 11 段 WAV 使用项目脚本自行合成，没有电影原声、演员录音或未经许可采样。语音名称留空时使用随包的 eSpeak NG 离线通用机器合成，支持中文和英文，不要求系统另装声音。填写已安装 Windows SAPI 语音名称时优先尝试该声音，失败则回到离线合成。可以修改句子，`{app}` 替换应用名称，`{system}` 替换系统名称，例如 `{app} interface ready`。语音顺序排队；Boot 的最终 Ready 会等待短播报完成（最多额外等待 4 秒，Escape 和 Watchdog 仍有效）。动画结束时全部停止，因此 2.6 秒关闭动画建议配短句或短语音片段，长句不会拖住退出。

离线语音只在 Animation 请求播报时启动瞬时 VoiceWorker，生成 WAV 后立刻退出，交给网页 HTML Audio 按独立语音音量播放。Worker 与 eSpeak NG 使用 GPL-3.0-or-later，许可证、引擎完整源代码、构建脚本和 Worker 源码随 `VoiceEngines/espeak-ng/` 分发；Controller、Animation 等本项目代码保留各自 MIT 许可。没有打包 Microsoft 系统语音库。

本地 WAV/MP3/OGG 可以按事件键替换音效或语音；资源缺失、损坏、音频被系统禁用时静默继续。文件仅由当前 Animation 进程加载，不在 Controller 后台播放。对应设置为 `audio.audioFiles` / `audio.voiceFiles`。主题的 `theme.json` 提供 `audioEnabled`、`voiceEnabled`、`audioFiles`、`voiceProfile`（可包含 `files`），资源放在主题的 `audio/`。

## 真实数据与主题装饰

母版中只替换八个原有文本位置：CPU、内存、GPU、专用显存总量、DNS、外网连通性、VPN 适配器、目标 AI 状态。完整检测另在设置查看，不扩大母版布局。CPU 是短时负载，内存来自 Windows，GPU/64 位显存来自 DXGI；集成显卡显示共享内存，无法测量时显示 UNKNOWN，显存占用没有被测量。

网络采用活动网卡、DNS 和可配置 HTTPS HEAD 端点，每个端点三次采样，报告成功数、中位耗时与 max-min 抖动。默认每轮最长约 2 秒，无 Cookie、登录凭据或响应正文读取。状态失败是检测结果，不会阻塞目标客户端。VPN 是适配器识别与外网检测的组合，不能证明某个请求走过指定 VPN，HTTP 代理不会被假装成 VPN。

**原母版里的无人机、权限覆写、坐标、节点状态、代码流是保留的剧情装饰，不是当前计算机的权限或安全审计结果。** 只有上述八个明确标签来自检测；测试/预览数据标记 DEMO。动画完全发生在 550W 自己的 WebView，装饰文字不会执行其中展示的代码。

## 文件与故障排查

数据目录还包含 Logs、Icons、CoreAssets、WebView2 临时会话缓存。日志最多 8 个轮转文件，每个约 512 KB。动画结束后清理自己的 WebView2 子进程和缓存；极短的文件锁可能令缓存延迟到下次清理。目标软件安装目录、登录配置和浏览器数据不被修改。

如果软件关闭后突然出现 Shutdown，请确认正在运行的是 V2，并退出旧版监控器。V2 的普通 Process/Window stop 分支没有关闭动画调用。

如果应用不能通过优化快捷方式启动，先在配置确认目标类型和原快捷方式 / EXE 路径，Store 应用优先用 AppUserModelID。卸载或移动原应用后需要重新选择。URI 启动依赖 Windows 已注册协议。

常驻内存仍有改进空间：本机 GDI 首帧版本退出动画后的私有内存约 31–53 MB，包含共享运行库的工作集约 94–125 MB。打开设置或可选透明代理会增加内存。详见 TEST-REPORT.md；没有通过清空工作集来人为压低数值。

## 编译与测试

源码包含同一 `550W-AI-Controller.sln`：Core、Controller、Animation、Tests、ExternalFixture、IntegrationHarness。使用 Windows x64、.NET 8 SDK（交付构建为 8.0.425）和系统自带 .NET Framework C# 编译器。Visual Studio 可直接打开 solution；命令行执行 `powershell -File .\Build.ps1`。脚本恢复 NuGet、运行单元测试、发布两个自包含程序，再编译瞬时 Launcher 与单独的离线 VoiceWorker。不需要电影素材或官方 AI 客户端源码。

网页回归：安装 Node 20+，在源码目录 `npm install` 后 `npm run verify-web`，使用已安装 Edge（或设置 EDGE_PATH）。28 项结果和帧图写到 docs。`scripts/verify-web.cjs` 与 docs/images、web-test-results.json 保留为第一版历史，V2 不使用该旧测试作为结果。

Windows 集成测试使用本工程 ExternalFixture，不连接 AI 账号或服务。源码解压后先编译 `tests/550W.Fixture/550W.Fixture.csproj` 和 `tests/550W.IntegrationHarness/550W.IntegrationHarness.csproj`（Release），将环境变量 `550W_TEST_ROOT` 设为源码目录的绝对路径、`550W_TEST_BINARY` 设为便携发布目录，再运行 Harness EXE。证据写入源码内的 `work/desktop-v2-evidence`。`550W_TEST_SOURCE` 可另外指定源码位置。自动回归会创建/结束自己的测试进程，请使用测试目录；原始日志、JSON、TRX、视觉对照和当前实际测试范围见 docs 与 TEST-REPORT.md。
