# 550W AI Controller V2 源码与边界审计

## 上游取得与保留

上游为 [yannicksong0106/dsh-550c-boot](https://github.com/yannicksong0106/dsh-550c-boot)，检查的 main 提交为 `bddc507d7c717fe7cda440cec83f330e8b3e5004`，package 版本 0.1.3。原始 HTML 动画贡献者 Voidpoket，插件工程作者 Ziyang Song。上游 MIT LICENSE 随源码和便携包保留。完整上游文件在 `third_party/dsh-550c-boot`，不包括 `.git`。

审计对象包含 README、package.json、assets/550C-source.html、src/assets.js、src/show.js、src/client.js、src/index.js、src/enhance.js 和构建/提取脚本。上游是 DSH 插件，client inject、local storage、网页/titlebar overlay 属于它的 DSH 插件集成声明。550W Windows 程序不执行这个插件安装流程，只使用独立动画 HTML/CSS/SVG 和演出代码。

`scripts/port-theme.mjs` 读取上游资源，产出 `mother-assets.js` / `mother-show.js`。DOM 标记和 SVG 保留，vw/vh 换算为固定 1920×1080 母版单位，再统一缩放。演出新增状态事件、可取消计时器、真实帧率计数和最终 Ready 门控。九段原编排、九个错位弹窗、47 个节点保留。Movie Red 在附加 CSS 中覆盖色值；真实检测只写八个原有文本槽。V1 面板单独作为 DebugDiagnostics 页面保留。视觉对照见 docs/visual-audit-v1 与 docs/images-v2。

## 进程 / 窗口监听

ProcessMonitor 使用 WMI Win32_ProcessStartTrace / Win32_ProcessStopTrace。WindowMonitor 使用 WinEvent 的 `WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS`，不向目标加载回调 DLL。5 秒低频轮询补偿漏失事件。WindowProbe 先按目标 PID 过滤，再读取公开窗口元数据，排除 Controller 自己的窗口，避免跨线程标题查询死锁。

监控不读取目标进程内存、不调用 WriteProcessMemory / VirtualAllocEx / CreateRemoteThread、不安装 WH_CBT/鼠标键盘系统钩子、不下载驱动。没有修改 AI 可执行文件、安装目录、DOM、Cookies、Token、登录文件或浏览器资料。目标匹配依据名称、可选完整路径和可选窗口标题。

Minimize、隐藏和最后一个进程退出分别作为状态；最小化不触发 Stop。普通 Stop 只发 TargetStopped，并取消仍在启动的动画。Core.AppLifecycle 的 Stop 分支不产生 AnimationKind.Shutdown，AnimationCoordinator 正式分支还会拒绝非 Demo Shutdown 请求，形成两道检查。

## Optimized Launch

只创建用户选择的新 `.lnk`。WScript.Shell 用于快捷方式元数据与创建，没有用其 Run 执行拼接脚本。EXE 参数通过 ProcessStartInfo，URI/Store 交给 Windows Shell。瞬时 .NET Framework Launcher 通过同用户命名管道交给已运行 Controller；失败时启动 Controller 正常命令行。启动入口、配置文件与命名管道均在当前用户范围。图标从目标 exe/shortcut/Store logo 或手选资源取得，缓存到自己的数据目录。

为防止原链接被覆盖，Create 检查新路径；Update/Delete 校验 TargetPath、550W Launcher/Controller 以及匹配的 `--launch-profile`。不对原 AI 快捷方式自动覆写。快捷方式改动不会改变客户端权限、启动用户或登录状态。

## Optimized Shutdown

Managed Close 只在用户启用/使用入口后创建关闭会话。CloseConfirmation、Preparing、PreloadReady、OverlayVisible、ShutdownBegin、WM_CLOSE 有独立日志。预加载窗口透明、不激活、不可点、不是 Topmost，只有确认后切换。确认之前没有播放音频和语音。确认后原生覆盖窗口完成绘制并同步 DWM，再激活已预加载的网页；收到 SHUTDOWN_BEGIN 后才正常请求关闭。

标准关闭使用 PostMessage(WM_CLOSE)。[微软 WM_CLOSE 文档](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-close)描述窗口自行处理正常关闭，允许应用提示未保存。目标拒绝关闭或把自己收进托盘时，550W 不用 Kill 强行结束它。取消恢复 Running，并取消 own Animation 资源。异常、超时、加载失败会放行标准关闭，系统退出不被等待动画阻塞。

可选 X 代理只接受已知标准标题栏类 HwndWrapper / Notepad，要求 WS_CAPTION、未最小化、前台、关闭按钮可见可用，矩形位于窗口右上角且大小合理。按 [WM_GETTITLEBARINFOEX](https://learn.microsoft.com/en-us/windows/win32/menurc/wm-gettitlebarinfoex) 和 [TITLEBARINFOEX](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-titlebarinfoex) 的关闭按钮元数据取得物理屏幕坐标；排除 invisible/offscreen/unavailable 状态。移动/前台/尺寸事件会隐藏旧代理并更新，点击时再核对前台、最新矩形和光标，避免使用陈旧坐标。没有 UIA 全树常驻扫描。对于自定义标题栏直接使用 Managed Close，并明确显示支持说明。

原生窗口代理在跨应用、混合 DPI、无标准标题栏的情况下仍需要兼容实测。本次真实单显示器 100% 标准 WPF 标题栏验证及四组物理坐标单元测试不能替代所有 AI 客户端测试。因此默认 Managed Close，并保持原 X 的正常操作。

## WebView2、IPC 与资源释放

Animation 单独运行，Controller 常驻部分不创建 WebView2。原生 GDI 核心覆盖冷启动等待，动画网页仅在 own WebView2 内运行。页面限于 `https://550w.local/` 本地虚拟映射，阻止外部导航/新窗口，禁用默认上下文菜单、DevTools 和权限请求；HTML 使用 CSP。

音效/语音替换只开放配置列出的本地 WAV/MP3/OGG（最大 64 MB），核心仅是自己的 PNG（最大 16 MB），不提供任意路径 URL 读取服务。SVG/WebP 的转换在瞬时 own Animation 子进程中处理，本地固定 image 请求、图片 CSP，不执行 SVG 内的网页脚本。自备主题/声音仍是用户自己选择的本地代码与媒体，主题工程应像本地程序一样由用户信任。

每次动画有随机 32 位十六进制 Session ID 和 CurrentUserOnly 命名管道。Job 对象、Process.Kill(tree) 的清理范围仅为 **550W 自己创建的 Animation 与其 WebView2 子进程**，没有用于正常关闭 AI。CoreAsset 转换也有 own job 与超时。关闭目标只调用 WM_CLOSE。会话缓存删除先校验 Session ID 与绝对根目录，避免递归清理落到其他目录。

预加载失败不阻塞正常关闭；确认过快时原生静音 Collapse 保证已有画面，再发送关闭。Controller 退出时不等待长动画。关闭会话重复请求、重复取消和重复 WM_CLOSE 均通过状态机去重。日志轮转，配置原子写入与备份；程序不向外发送日志。

确认之前若目标换屏，预加载记录的屏幕矩形与确认时的矩形必须相同才允许激活 WebView；否则改用确认时当前屏幕的原生覆盖。该保护避免旧屏动画被激活后，目标所在的新屏先露出桌面；本机没有双屏设备，因此保护分支尚未做实际换屏认证。

## 数据诚实性与网络

GetSystemTimes 计算短时 CPU 负载，GlobalMemoryStatusEx 读取内存，DXGI 读取显卡与 64 位专用显存总量。不会把 WMI AdapterRAM 32 位结果误报成大显存，不声称显存占用。图形硬件接口失败时展示 UNKNOWN/UNAVAILABLE。

DNS 和 HTTPS HEAD 使用用户可配置端点，默认 Microsoft/Cloudflare/Baidu/QQ，仅进行可用性采样。HttpClient 不启用 Cookie、默认凭据和自动跳转，不读响应正文，不访问 AI 私有 API，不验证用户 Token，也不操纵目标网络请求。每端点三次样本，中位数与 max-min 抖动；超时显示 TIMEOUT。活动 VPN 适配器不等于实际请求路由，主题有明确说明。

原动画的无人机、根证书、权限覆写和展示代码是科幻剧情图形，未作为系统检测结果或执行逻辑。原版装饰中的地点/坐标来自上游固定 HTML，与当前用户位置无关。真实数据标签与剧情分开说明，Demo 数据标记 DEMO。

## 声音与版权

11 个默认 WAV 来自本工程合成脚本；机械核心为本工程原创 SVG/PNG。没有影片配乐/演员录音。默认通用机器语音使用随包的 eSpeak NG 1.52.0，句子长度限制 180 字符，作为纯 UTF-8 文本交给独立瞬时 VoiceWorker，生成 WAV 后退出，不接触目标客户端。Worker 与引擎保持 GPL-3.0-or-later 许可，完整源码、语音数据生成流程与构建脚本随包。可选已安装 Windows SAPI，句子作为 plain text，不解析 XML。声音与语音独立音量和开关，Missing/Decode/Permission/VoiceWorker 失败静默继续。Waiting Loop、自检、重复激活用事件键去重。默认机器语音关闭。

MIT 原动画代码授权与电影作品/商标/演员录音授权是不同范围。公开包只包含上述代码和原创生成资源，没有提供电影音频或把自己标识为官方 AI 客户端。默认音效授权文本和完整上游/Microsoft 组件许可见 licenses。审计记录反映源代码检查与当前测试，未做独立第三方安全认证。
