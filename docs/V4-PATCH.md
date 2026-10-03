# V4 小型视觉修复补丁 · 4.0.0

日期：2026-10-01。继续使用原 V3 工程、应用管理、配置、诊断、快捷启动、关闭流程与交付结构。

## 已完成

- 550C 使用原作者保留在 mother-assets.js 的 SVG：6 条原始路径、原始 viewBox 和 despike 滤镜。恢复红白分区、断口及 0/C 交叠，未使用截图贴图。关键帧已与用户截图人工核对。
- core.png 与 V3 完全相同，SHA256 为 51a26921618ac45089320b03ed1ae3bc76538bfbca5709ecd1fa4f18266d833c。图片已有透明通道，本次没有修改像素、主体、造型、比例或颜色。
- 入场链：扫描定位 → 遮罩显现 → 镜头点亮 → 小字淡入 → 短暂停留 → 机器人与文字粒子消散 → 原版 550C 聚合 → 全屏粒子 → 原始多窗口。
- 两行文字为“MOSS 核心接入中”和“550C JOINT CONTROL NODE”；以 1920×1080 为基准，中文 12px、英文 9px，加粗；文字位于机器人容器下方 100px，行间距 5px。随整体画布等比例缩放。
- 原生首帧等待 WebView 时显示低亮扫描定位，不再提前显示完整静止机器人；WebView 就绪后才接入渐进显现。
- 原生背景、WebView 默认背景、页面和动画容器统一纯黑。前两阶段移除矩形区域上的 CRT 覆盖层，进入原 HUD 后恢复；最终交接时恢复透明背景。
- 软件内部版本及更新日志升级至 4.0.0 / V4。没有改动应用识别、配置覆盖或日志导出逻辑。

## 最小验证

- 一轮无界面 Edge 离屏动画检查通过；含 12 阶段顺序、文字尺寸/位置、6 条原始 SVG 路径/颜色、互不重叠、9 个窗口/47 个节点、最终透明交接和资源释放。
- 8 张关键帧已保存到 images-v4；人工检查扫描显现、文字、550C 和原 HUD。机器人与标识两个阶段的宽高比例变化截图中，16 个空白背景采样点全部为 RGB(0,0,0)。
- 原 BOOT/APP 结构及换算后的原始 CSS 保留检查通过，32 个原作者文件记录保持可查。
- Controller、Animation 的 win-x64 自包含 Release 发布成功，版本为 4.0.0.0；Launcher 与语音工作进程沿用原脚本编译。
- 本轮没有显示真实桌面测试窗口；原生首帧的扫描表现仅完成代码核对和编译，未在桌面重放。历史 V3 全量测试结果保留为历史记录，不计作 V4 重跑结果。
- 初次工具启动受到系统临时目录只读权限限制，改用项目内临时目录后完成检查与编译；实际动画检查一次通过，没有重复运行。

## 修改文件

- `Themes/550W/js/intro.js`
- `Themes/550W/css/controller.css`
- `Themes/550W/css/page.css`
- `Themes/550W/js/controller.js`
- `src/550W.Controller/Views/FirstFrameWindow.cs`
- `src/550W.Animation/AnimationWindow.cs`
- `Directory.Build.props`
- `Themes/550W/theme.json`
- `package.json`
- `Build.ps1`
- `src/550W.Controller/Views/SettingsWindow.cs`
- `src/550W.Controller/Services/ProductChangelog.cs`
- `CHANGELOG.md`
- `README.md`
- `scripts/test-web-v4.cjs`
- `docs/V4-PATCH.md`
- `docs/release-verification-v4.json`
- `docs/web-test-v4-results.json`
- `docs/images-v4/*.png`
- `TEST-REPORT.md`

## 使用

解压 550W-AI-Controller-V4-Portable.zip，运行其中的 550W-AI-Controller.exe。保持 Themes、VoiceEngines 与程序文件在同一目录。WebView2 要求与 V3 相同。

原作者项目：https://github.com/yannicksong0106/dsh-550c-boot
