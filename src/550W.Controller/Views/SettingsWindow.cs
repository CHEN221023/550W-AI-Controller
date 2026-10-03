using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Controller550W.Controller.Services;
using Forms = System.Windows.Forms;

namespace Controller550W.Controller.Views;

public sealed class SettingsWindow : Window
{
    readonly ControllerSettings edit; readonly Action<ControllerSettings> save; readonly AppDiscoveryService discovery = new();
    readonly ListBox applications = new(); readonly ContentControl profileEditor = new(), page = new();
    readonly TextBlock status = Ui.Label("修改后保存，新的设置将在下一次动画生效。",12,Ui.Muted);
    readonly string dataDirectory,pipeName; Button applyAll=null!; int profilePage; string activePage="应用管理";
    public event Action<ControllerSettings,AppProfile,AnimationKind,string>? Preview;
    public event Action<AppProfile>? ManagedCloseRequested;
    public event Action<ControllerSettings,AppProfile>? DebugRequested;
    public SettingsWindow(ControllerSettings settings,Action<ControllerSettings> save,Func<ControllerSettings>? runtimeSettings=null,string dataDirectory="",string pipeName="")
    {
        this.dataDirectory=dataDirectory;this.pipeName=pipeName;edit=WireJson.Clone(settings);this.save=save;
        Title="550W AI Controller · 设置";Width=1320;Height=860;MinWidth=1080;MinHeight=680;Ui.Style(this);
        var root=new DockPanel{Margin=new Thickness(24)};
        var header=new DockPanel{Margin=new Thickness(4,0,0,22)};
        var badge=Ui.Label("V4.6 Beta  /  电影红",12,Ui.Muted);badge.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(badge,Dock.Right);header.Children.Add(badge);
        var branding=new StackPanel();branding.Children.Add(Ui.Label("550W AI Controller",27));branding.Children.Add(Ui.Label("轻量监控 · 真实自检 · 工业终端",12,Ui.Muted));header.Children.Add(branding);DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var footer=new DockPanel{Margin=new Thickness(0,18,0,0)};
        applyAll=Ui.Button("应用此设置到所有应用",ApplyToAll);
        var saveButton=Ui.Button("保存并应用",Save);saveButton.Background=Ui.Brush("#813442");saveButton.BorderBrush=Ui.Brush("#D97580");
        var buttons=Ui.Buttons(applyAll,saveButton,Ui.Button("关闭",Close));DockPanel.SetDock(buttons,Dock.Right);footer.Children.Add(buttons);status.VerticalAlignment=VerticalAlignment.Center;status.MaxWidth=350;footer.Children.Add(status);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        var main=new Grid();main.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(186)});main.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(20)});main.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        var rail=new DockPanel();var note=Ui.Label("550W\n独立、安全、轻量",11,Ui.Muted);note.Margin=new Thickness(12,18,0,8);DockPanel.SetDock(note,Dock.Bottom);rail.Children.Add(note);
        var nav=new ListBox();foreach(var (symbol,title) in new[]{("▦","应用管理"),("◈","动画"),("⌁","系统检测"),("◎","网络"),("♫","声音"),("⚙","常规"),("ⓘ","关于")})nav.Items.Add(new ListBoxItem{Tag=title,Content=Ui.Label(symbol+"    "+title,15)});
        nav.SelectionChanged+=(_,_)=>{if(nav.SelectedItem is ListBoxItem item)ShowPage((string)item.Tag);};rail.Children.Add(nav);main.Children.Add(Ui.Card(rail,8));
        Grid.SetColumn(page,2);main.Children.Add(page);root.Children.Add(main);Content=root;nav.SelectedIndex=0;
        if(runtimeSettings!=null){void Refresh(){var active=runtimeSettings();foreach(var p in edit.Profiles)p.RuntimeStatus=active.Profiles.FirstOrDefault(x=>x.Id==p.Id)?.RuntimeStatus??"待保存";}var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(3)};timer.Tick+=(_,_)=>Refresh();Closed+=(_,_)=>timer.Stop();Refresh();timer.Start();}
    }
    void ShowPage(string name)
    {
        activePage=name;applyAll.Visibility=name=="应用管理"?Visibility.Visible:Visibility.Collapsed;
        var panel=new DockPanel();var heading=new StackPanel{Margin=new Thickness(4,0,0,14)};heading.Children.Add(Ui.Label(name,26));
        heading.Children.Add(Ui.Label(name switch{"应用管理"=>"应用身份独立保存，动画与声音可选择跟随全局或单独配置。","动画"=>"全局默认模板 · 供所有“跟随全局”的应用使用。","声音"=>"全局声音模板 · 主音效与机器语音可以独立开关。","关于"=>"版本信息、更新记录与故障诊断。",_=>"为你的 550W 配置合适的默认行为。"},12,Ui.Muted));DockPanel.SetDock(heading,Dock.Top);panel.Children.Add(heading);
        panel.Children.Add(name switch{"应用管理"=>Applications(),"动画"=>Animation(),"系统检测"=>Hardware(),"网络"=>Network(),"声音"=>Sound(),"常规"=>General(),_=>About()});page.Content=panel;
        panel.BeginAnimation(OpacityProperty,new DoubleAnimation(.55,1,TimeSpan.FromMilliseconds(130)));
    }
    AppProfile Selected=>applications.SelectedItem as AppProfile??new AppProfile{DisplayName="演示应用"};
    void Test(AnimationKind kind,string audioTest="",bool global=false)
    {if(!CheckParticleTiming())return;edit.Validate();var p=WireJson.Clone(Selected);if(global)p.UseIndependentSettings=false;Preview?.Invoke(edit,p,kind,audioTest);}
    void Save()
    {
        if(HasValidationErrors(this)){status.Text="请先修正标红的数值，再保存。";return;}
        if(!CheckParticleTiming())return;
        try{edit.Validate();save(WireJson.Clone(edit));status.Text="已保存，后台监控和新动画将使用这些设置。";}catch(Exception e){status.Text="保存失败："+e.Message;}
    }
    bool CheckParticleTiming()
    {
        if(edit.ParticleTimingError() is not { } error)return true;
        status.Text=error;return false;
    }
    static bool HasValidationErrors(DependencyObject element)
    {if(Validation.GetHasError(element))return true;for(var i=0;i<VisualTreeHelper.GetChildrenCount(element);i++)if(HasValidationErrors(VisualTreeHelper.GetChild(element,i)))return true;return false;}
    void ApplyToAll()
    {
        if(applications.SelectedItem is not AppProfile p)return;
        if(HasValidationErrors(this)){status.Text="请先修正标红的数值。";return;}
        if(!CheckParticleTiming())return;
        var dialog=new Window{Title="应用到全部应用",Width=480,Height=280,ResizeMode=ResizeMode.NoResize,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false};Ui.Style(dialog);dialog.WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var body=Ui.Stack();body.Children.Add(Ui.Label("使用 "+p.DisplayName+" 的通用配置？",20));body.Children.Add(Ui.Label($"将当前动画、声音、窗口行为与通用交互设置复制给全部 {edit.Profiles.Count} 个应用，并保存。应用名称、路径、图标和识别规则保持各自独立。",13,Ui.Muted));body.Children.Add(Ui.Buttons(Ui.Button("取消",()=>dialog.DialogResult=false),Ui.Button("确认应用",()=>dialog.DialogResult=true)));dialog.Content=body;
        if(dialog.ShowDialog()!=true)return;
        try{edit.Validate();var count=ProfileSettingsResolver.ApplyToAll(edit,p);save(WireJson.Clone(edit));ShowProfile();status.Text=$"已应用并保存至 {count} 个应用。";}catch(Exception e){status.Text="应用失败："+e.Message;}
    }
    UIElement Applications()
    {
        // Detach reusable controls when changing the navigation page.
        if(applications.Parent is Panel oldList)oldList.Children.Remove(applications);if(profileEditor.Parent is Panel oldEditor)oldEditor.Children.Remove(profileEditor);
        var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(224)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(16)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        var listPanel=new DockPanel();var top=new StackPanel();top.Children.Add(Ui.Label("已配置的应用",14));var add=Ui.Button("＋ 添加应用",()=>ShowAddMenu());top.Children.Add(add);top.Margin=new Thickness(0,0,0,12);DockPanel.SetDock(top,Dock.Top);listPanel.Children.Add(top);
        applications.ItemsSource=edit.Profiles;
        var template=new DataTemplate(typeof(AppProfile));var card=new FrameworkElementFactory(typeof(StackPanel));
        var name=new FrameworkElementFactory(typeof(TextBlock));name.SetBinding(TextBlock.TextProperty,new Binding("DisplayName"));name.SetValue(TextBlock.FontSizeProperty,16d);name.SetValue(TextBlock.FontWeightProperty,FontWeights.SemiBold);card.AppendChild(name);
        var process=new FrameworkElementFactory(typeof(TextBlock));process.SetBinding(TextBlock.TextProperty,new Binding("ProcessNamesText"));process.SetValue(TextBlock.ForegroundProperty,Ui.Muted);process.SetValue(TextBlock.MarginProperty,new Thickness(0,6,0,0));process.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);card.AppendChild(process);
        var state=new FrameworkElementFactory(typeof(TextBlock));state.SetBinding(TextBlock.TextProperty,new Binding("RuntimeStatus"));state.SetValue(TextBlock.ForegroundProperty,Ui.Muted);state.SetValue(TextBlock.FontSizeProperty,11d);state.SetValue(TextBlock.MarginProperty,new Thickness(0,7,0,0));card.AppendChild(state);template.VisualTree=card;applications.ItemTemplate=template;
        applications.SelectionChanged-=OnProfileSelection;applications.SelectionChanged+=OnProfileSelection;listPanel.Children.Add(applications);grid.Children.Add(Ui.Card(listPanel,12));Grid.SetColumn(profileEditor,2);grid.Children.Add(profileEditor);
        if(edit.Profiles.Count>0&&applications.SelectedIndex<0)applications.SelectedIndex=0;ShowProfile();return grid;
    }
    void OnProfileSelection(object sender,SelectionChangedEventArgs e){profilePage=0;ShowProfile();}
    void ShowAddMenu()
    {
        var menu=new ContextMenu();foreach(var (title,action) in new (string,Action)[]{("自动发现 AI 应用",()=>_=Scan()),("从当前进程添加",()=>_=PickProcess()),("浏览可执行文件",BrowseExe),("从原快捷方式添加",BrowseShortcut)}){var item=new MenuItem{Header=title};item.Click+=(_,_)=>action();menu.Items.Add(item);}menu.IsOpen=true;
    }
    void ShowProfile()
    {
        applyAll.IsEnabled=applications.SelectedItem is AppProfile;
        if(applications.SelectedItem is not AppProfile p){profileEditor.Content=Ui.Card(Ui.Label("尚未添加应用\n\n点击“添加应用”，选择已安装或正在运行的 AI 软件。",16,Ui.Muted));return;}
        var layout=new DockPanel();var head=new DockPanel{Margin=new Thickness(0,0,0,12)};var enabled=Ui.Check("启用监控","Enabled",p);DockPanel.SetDock(enabled,Dock.Right);head.Children.Add(enabled);head.Children.Add(Ui.Label(p.DisplayName,22));DockPanel.SetDock(head,Dock.Top);layout.Children.Add(head);
        var sections=new ComboBox{ItemsSource=new[]{"概览","启动与窗口","动画与声音","优化关闭","高级识别"},SelectedIndex=profilePage,Margin=new Thickness(0,0,0,12)};sections.SelectionChanged+=(_,_)=>{profilePage=sections.SelectedIndex;ShowProfile();};DockPanel.SetDock(sections,Dock.Top);layout.Children.Add(sections);
        var fields=new StackPanel();
        switch(profilePage)
        {
            case 0:
                Ui.Field(fields,"显示名称",Ui.Input("DisplayName",p));Ui.Field(fields,"进程名称",Ui.Input("ProcessNamesText",p),"填写进程名，多个名称用逗号分隔。不知道填什么时，用“从当前进程添加”。");
                Ui.Field(fields,"可执行文件路径",Ui.Input("ExecutablePath",p));
                fields.Children.Add(Ui.Label(p.UseIndependentSettings?"动画与声音：此应用使用独立设置":"动画与声音：当前此应用使用全局设置",14,Ui.Amber));
                var effective=ProfileSettingsResolver.ResolveAnimation(edit,p);fields.Children.Add(Ui.Label($"启动动画至少 {effective.MinimumBootMs/1000.0:0.#} 秒  ·  {ColorName(effective.ColorProfile)}\n优化关闭：{(p.OptimizedShutdown?"已开启":"已关闭")}",13,Ui.Muted));
                fields.Children.Add(Ui.Buttons(Ui.Button("调整动画与声音",()=>{profilePage=2;ShowProfile();}),Ui.Button("重新选择进程",()=>_=PickProcess(p))));
                Ui.Fold(fields,"预览与应用操作",PreviewActions(p));break;
            case 1:BuildLaunch(fields,p);break;
            case 2:BuildOverrides(fields,p);break;
            case 3:BuildShutdown(fields,p);break;
            default:BuildIdentification(fields,p);break;
        }
        layout.Children.Add(Ui.Scroll(fields));profileEditor.Content=Ui.Card(layout);
    }
    UIElement PreviewActions(AppProfile p)
    {var s=new StackPanel();s.Children.Add(Ui.Buttons(Ui.Button("预览启动",()=>Test(AnimationKind.Boot)),Ui.Button("预览关闭",()=>Test(AnimationKind.Shutdown)),Ui.Button("删除应用",()=>{if(MessageBox.Show(this,"从监控列表移除 "+p.DisplayName+"？","删除应用",MessageBoxButton.OKCancel)==MessageBoxResult.OK){edit.Profiles.Remove(p);ShowProfile();}})));s.Children.Add(Ui.Label("预览会显示全屏动画。应用未运行时使用演示数据。",12,Ui.Muted));return s;}
    void BuildOverrides(StackPanel fields,AppProfile p)
    {
        fields.Children.Add(Ui.Label("动画配置模式",16));var choices=new WrapPanel();var follow=new RadioButton{Content="跟随全局设置",IsChecked=!p.UseIndependentSettings,GroupName="animationMode"};var own=new RadioButton{Content="使用独立设置",IsChecked=p.UseIndependentSettings,GroupName="animationMode"};
        follow.Checked+=(_,_)=>{p.UseIndependentSettings=false;ShowProfile();};own.Checked+=(_,_)=>{ProfileSettingsResolver.EnableIndependent(edit,p);ShowProfile();};choices.Children.Add(follow);choices.Children.Add(own);fields.Children.Add(choices);
        if(!p.UseIndependentSettings){fields.Children.Add(Ui.Label("当前此应用使用全局设置",18,Ui.Amber));fields.Children.Add(Ui.Label("动画和声音将自动跟随左侧“动画”“声音”页面的修改。启动路径、窗口方式和优化关闭仍由当前应用单独管理。",13,Ui.Muted));fields.Children.Add(Ui.Buttons(Ui.Button("前往全局动画",()=>Navigate("动画")),Ui.Button("前往全局声音",()=>Navigate("声音"))));return;}
        fields.Children.Add(Ui.Label("以下设置只对当前应用生效。全局静音仍可关闭所有声音。",12,Ui.Muted));
        var selector=new ComboBox{ItemsSource=new[]{"动画与粒子","音效与语音"},SelectedIndex=0,Margin=new Thickness(0,10,0,12)};var content=new ContentControl();void Update(){content.Content=selector.SelectedIndex==0?AnimationControls(p.AnimationOverrides):SoundControls(p.AudioOverrides,false);}selector.SelectionChanged+=(_,_)=>Update();fields.Children.Add(selector);fields.Children.Add(content);Update();
    }
    void Navigate(string name)
    {var nav=FindNavigation(this);if(nav!=null)nav.SelectedItem=nav.Items.Cast<ListBoxItem>().First(x=>(string)x.Tag==name);else ShowPage(name);}
    static ListBox? FindNavigation(DependencyObject root)
    {if(root is ListBox list&&list.Items.Count==7&&list.Items[0] is ListBoxItem)return list;for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var found=FindNavigation(VisualTreeHelper.GetChild(root,i));if(found!=null)return found;}return null;}
    void BuildLaunch(StackPanel fields,AppProfile p)
    {
        Ui.Field(fields,"启动入口",Ui.Select("LaunchMode",p,[new("原入口 · 被动检测",LaunchMode.PassiveDetection),new("优化快捷启动",LaunchMode.OptimizedShortcut)]),"使用优化快捷方式时，550W 先准备动画再启动应用。原快捷方式不会被修改。");
        Ui.Field(fields,"应用启动顺序",Ui.Select("AppLaunchOffsetMs",p,[new("应用先启动 300 毫秒",-300),new("同时启动",0),new("动画优先 300 毫秒（推荐）",300),new("动画优先 600 毫秒",600)]));
        fields.Children.Add(Ui.Buttons(Ui.Button("创建优化快捷方式",()=>CreateShortcut(p)),Ui.Button("更新",()=>UpdateShortcut(p)),Ui.Button("移除快捷方式",()=>RemoveShortcut(p))));
        Ui.Field(fields,"目标窗口显示",Ui.Select("WindowPresentation",p,[new("最大化（默认）",WindowPresentation.Maximize),new("保持原状",WindowPresentation.Preserve),new("居中",WindowPresentation.Center),new("填满工作区",WindowPresentation.FillWorkArea)]));
        var advanced=new StackPanel();Ui.Field(advanced,"调整窗口时机",Ui.Select("PresentationTiming",p,[new("窗口稳定后",PresentationTiming.AfterStable),new("最低时长过半后",PresentationTiming.Halfway),new("动画交接前",PresentationTiming.BeforeHandoff)]),"推荐窗口稳定后调整。应用出现较慢时，可以选择交接前，避免提前移动窗口。");advanced.Children.Add(Ui.Check("被动检测时也调整窗口","ApplyPresentationToPassive",p));Ui.Field(advanced,"启动延迟（毫秒）",Ui.Input("AppLaunchOffsetMs",p),"正数让动画先出现，负数让应用先启动。默认 300，支持 -1000～5000。");Ui.Fold(fields,"窗口与启动高级参数",advanced);
        var target=new StackPanel();Ui.Field(target,"启动目标类型",Ui.Select("LaunchKind",p,[new("可执行文件",LaunchTargetKind.Executable),new("原快捷方式（.lnk）",LaunchTargetKind.Shortcut),new("商店应用标识",LaunchTargetKind.AppUserModelId),new("协议链接",LaunchTargetKind.UriProtocol),new("系统应用文件夹",LaunchTargetKind.ShellAppsFolder)]));Ui.Field(target,"启动目标",Ui.Input("LaunchTarget",p));Ui.Field(target,"启动参数",Ui.Input("LaunchArguments",p),"仅可执行文件使用。普通应用通常留空。");Ui.Field(target,"工作目录",Ui.Input("WorkingDirectory",p));Ui.Field(target,"快捷方式路径",Ui.Input("OptimizedShortcutPath",p));target.Children.Add(Ui.Button("选择快捷方式图标",()=>ChooseIcon(p)));Ui.Fold(fields,"启动目标与快捷方式资源",target);
    }
    void BuildShutdown(StackPanel fields,AppProfile p)
    {
        Ui.Field(fields,"优化关闭动画",Ui.Select("OptimizedShutdown",p,[new("关闭（默认）",false),new("开启",true)]),"开启后先显示 550W 关闭画面，再正常关闭应用。关闭此功能时，应用正常退出且没有关闭动画。");fields.Children.Add(Ui.Check("关闭前确认（推荐）","CloseConfirmation",p));
        Ui.Field(fields,"关闭入口",Ui.Select("CloseEntry",p,[new("仅托盘托管关闭（推荐）",CloseEntry.ManagedClose),new("自动代理标准关闭按钮",CloseEntry.AutoCaptionProxy)]),"托管关闭使用托盘里的“关闭应用（550W）”。自动代理仅在关闭按钮可可靠定位时启用，否则退回托盘入口。");
        fields.Children.Add(Ui.Label("关闭动画：550W 中心坍缩\n\n确认框出现时会预加载资源；取消立即释放。普通退出、隐藏、崩溃和强制结束不会事后补播动画。",13,Ui.Muted));
        fields.Children.Add(Ui.Buttons(Ui.Button("检查关闭按钮支持",()=>{var identities=Controller550W.Controller.Native.WindowProbe.Processes(profiles:edit.Profiles);var hwnd=Controller550W.Controller.Native.WindowProbe.ForProfile(p,identities).FirstOrDefault(w=>w.Visible)?.Handle??0;status.Text=CaptionProxyService.SupportText(hwnd).Replace("Managed Close","托管关闭");}),Ui.Button("通过 550W 关闭应用",()=>{Save();ManagedCloseRequested?.Invoke(p);})));
    }
    void BuildIdentification(StackPanel fields,AppProfile p)
    {
        fields.Children.Add(Ui.Label("通常无需修改。识别规则仅属于当前应用，不会被“应用到全部应用”覆盖。",12,Ui.Muted));fields.Children.Add(Ui.Check("同时匹配完整可执行文件路径","MatchExecutablePath",p));Ui.Field(fields,"窗口标题包含",Ui.Input("WindowTitleMatch",p),"只在同一程序有多个窗口需要区分时填写，默认留空。不会读取窗口里的聊天内容。");
        Ui.Field(fields,"启动触发",Ui.Select("StartTrigger",p,[new("主窗口出现（推荐）",StartTrigger.WindowShow),new("进程启动",StartTrigger.ProcessStart)]));Ui.Field(fields,"停止状态来源",Ui.Select("StopTrigger",p,[new("主窗口关闭或隐藏",StopTrigger.WindowHide),new("所有匹配进程退出",StopTrigger.ProcessExit)]));
        var advanced=new StackPanel();Ui.Field(advanced,"窗口稳定时间（毫秒）",Ui.Input("ReadyDelayMs",p),"窗口持续可见且能响应后才交接。默认 350 毫秒，卡顿应用可适当增加。");Ui.Field(advanced,"重复触发间隔（毫秒）",Ui.Input("CooldownMs",p),"避免应用的辅助窗口反复触发动画，默认 650 毫秒。");advanced.Children.Add(Ui.Check("允许最小化窗口判为就绪","AllowMinimizedReady",p));advanced.Children.Add(Ui.Check("启用此应用的硬件检测","HardwareDiagnostics",p));advanced.Children.Add(Ui.Check("启用此应用的网络检测","NetworkDiagnostics",p));Ui.Fold(fields,"就绪与检测高级参数",advanced);
        var assets=new StackPanel();Ui.Field(assets,"启动主题",Ui.Select("BootTheme",p,Themes()));Ui.Field(assets,"关闭主题",Ui.Select("ShutdownTheme",p,Themes()));Ui.Fold(fields,"动画主题",assets);
    }
    static IEnumerable<Ui.Choice> Themes()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Themes");
        return Directory.Exists(root) ? Directory.GetDirectories(root).Where(x => File.Exists(Path.Combine(x, "theme.json"))).Select(x => new Ui.Choice(Path.GetFileName(x), Path.GetFileName(x))).ToArray() : [new("550W", "550W")];
    }
    void Add(string name, string exe, string path)
    {
        if (edit.Profiles.Any(p => p.ProcessNames.Any(x => x.Equals(exe, StringComparison.OrdinalIgnoreCase)) && (path.Length == 0 || p.ExecutablePath.Equals(path, StringComparison.OrdinalIgnoreCase)))) return;
        var p = new AppProfile { DisplayName = name, ProcessNames = [exe], ExecutablePath = path, LaunchTarget=path, MinimumBootTimeMs = edit.Animation.MinimumBootMs }; edit.Profiles.Add(p); applications.SelectedItem = p;
    }
    public async Task Scan()
    {
        status.Text = "正在扫描运行进程、已安装程序、开始菜单和 Store 应用…";
        var found = await Task.Run(discovery.DiscoverAsync); if (!IsVisible) return;
        if (found.Count == 0) { status.Text = "没有发现已安装或正在运行的 AI 应用。可从当前进程或 EXE 添加。"; return; }
        var picker = new SelectionWindow("发现的 AI 应用", found, [("名称", "DisplayName"), ("EXE", "ProcessName"), ("来源", "Source"), ("路径", "Path")]) { Owner = this };
        if (picker.ShowDialog() == true) foreach (var app in picker.Selected.Cast<DiscoveredApp>()){Add(app.DisplayName,app.ProcessName,app.Path);var p=edit.Profiles.FirstOrDefault(x=>x.ExecutablePath==app.Path);if(p!=null){p.LaunchKind=app.LaunchKind;p.LaunchTarget=app.LaunchTarget.Length>0?app.LaunchTarget:app.Path;p.IconSource=app.IconSource;p.SourceShortcut=app.SourceShortcut;}ShowProfile();}
        status.Text = $"扫描完成：发现 {found.Count} 项。添加后点击保存。";
    }
    async Task PickProcess(AppProfile? replace = null)
    {
        status.Text = "正在读取进程列表…"; var all = await Task.Run(discovery.CurrentProcesses); if (!IsVisible) return;
        var picker = new SelectionWindow("从当前进程添加", all, [("图标", "Icon"), ("程序", "DisplayName"), ("EXE", "ProcessName"), ("PID", "Pid"), ("可见主窗口", "Visible"), ("窗口标题", "WindowTitle"), ("路径", "Path")]) { Owner = this };
        if (picker.ShowDialog() == true) foreach (var p in picker.Selected.Cast<ProcessChoice>())
        {
            if (replace != null) { replace.ProcessNames = [p.ProcessName]; replace.ExecutablePath = p.Path; ShowProfile(); break; }
            Add(p.DisplayName, p.ProcessName, p.Path);
        }
        status.Text = "进程选择完成。受保护进程的路径可能不可读取。";
    }
    void BrowseExe()
    {
        var dialog = new OpenFileDialog { Filter = "Windows 应用 (*.exe)|*.exe", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) Add(Path.GetFileNameWithoutExtension(dialog.FileName), Path.GetFileName(dialog.FileName), dialog.FileName);
    }
    void BrowseShortcut(){var dialog=new OpenFileDialog{Filter="Windows 快捷方式|*.lnk",CheckFileExists=true};if(dialog.ShowDialog(this)!=true)return;try{var p=new AppProfile{DisplayName=Path.GetFileNameWithoutExtension(dialog.FileName),MinimumBootTimeMs=edit.Animation.MinimumBootMs};ShortcutService.ReadMetadata(p,dialog.FileName);edit.Profiles.Add(p);applications.SelectedItem=p;}catch(Exception e){status.Text="快捷方式读取失败："+e.Message;}}
    void CreateShortcut(AppProfile p){var dialog=new SaveFileDialog{Filter="Windows 快捷方式|*.lnk",FileName=p.DisplayName+" (550W).lnk",InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),OverwritePrompt=false};if(dialog.ShowDialog(this)!=true)return;try{new ShortcutService(dataDirectory,pipeName).Create(p,dialog.FileName);Save();ShowProfile();status.Text="已创建优化快捷方式："+dialog.FileName;}catch(Exception e){status.Text=e.Message;}}
    void UpdateShortcut(AppProfile p){if(p.OptimizedShortcutPath.Length==0){CreateShortcut(p);return;}try{new ShortcutService(dataDirectory,pipeName).Create(p,p.OptimizedShortcutPath);Save();status.Text="快捷方式已更新。";}catch(Exception e){status.Text=e.Message;}}
    void RemoveShortcut(AppProfile p){try{new ShortcutService(dataDirectory,pipeName).Remove(p);Save();ShowProfile();status.Text="优化快捷方式已删除。";}catch(Exception e){status.Text=e.Message;}}
    void ChooseIcon(AppProfile p){var dialog=new OpenFileDialog{Filter="应用图标|*.png;*.ico;*.exe;*.lnk",CheckFileExists=true};if(dialog.ShowDialog(this)!=true)return;p.IconSource=dialog.FileName;if(Path.GetExtension(dialog.FileName).Equals(".lnk",StringComparison.OrdinalIgnoreCase)){var metadata=new AppProfile();ShortcutService.ReadMetadata(metadata,dialog.FileName);p.IconSource=metadata.IconSource.Length>0?metadata.IconSource:metadata.ExecutablePath;}ShowProfile();}

    static string ColorName(string color)=>color switch{"movie_red"=>"电影红","amber"=>"原版琥珀","green"=>"绿磷光","cyan"=>"青磷光",_=>"白磷光"};
    static Ui.Choice[] Colors()=>[new("电影红（默认）","movie_red"),new("原版琥珀","amber"),new("绿磷光","green"),new("青磷光","cyan"),new("白磷光","white")];
    UIElement Animation()
    {
        var p=Ui.Stack();p.Children.Add(AnimationControls(edit.Animation));p.Children.Add(Ui.Buttons(Ui.Button("预览全局启动动画",()=>Test(AnimationKind.Boot,global:true)),Ui.Button("预览全局关闭动画",()=>Test(AnimationKind.Shutdown,global:true))));p.Children.Add(Ui.Label("预览会显示全屏动画。时长以毫秒为单位：1000 毫秒 = 1 秒。",12,Ui.Muted));return Ui.Card(Ui.Scroll(p),0);
    }
    UIElement AnimationControls(AnimationSettings a)
    {
        var p=new StackPanel();
        var basic=new StackPanel();Ui.Field(basic,"配色",Ui.Select("ColorProfile",a,Colors()));Ui.Field(basic,"最短启动时长（毫秒）",Ui.Input("MinimumBootMs",a),"即使目标应用已经打开，动画也至少播放这么久。推荐 3～5 秒，默认 4 秒；完整编排不会被截断。");Ui.Field(basic,"整体时间倍率",Ui.Select("DurationMultiplier",a,[new("快速 · 0.75 倍",.75),new("标准 · 1 倍",1d),new("舒缓 · 1.5 倍",1.5)]),"数值越大播放越慢。默认 1 倍。最短启动时长保持按真实时间计算。");basic.Children.Add(Ui.Check("等待目标应用就绪后再交接","WaitForReady",a));Ui.Fold(p,"基础动画设置",basic,true);
        var opening=new StackPanel();
        Ui.Field(opening,"中央粒子待机时长（毫秒）",Ui.Input("ParticleResidueMs",a),"控制中央粒子首次出现后保持轻微动态待机的时间。此时会并行准备后续动画资源。时间越短，越快开始重组；时间越长，预加载机会越多。默认 350，范围 0～10000 毫秒。");
        Ui.Field(opening,"粒子 → 550C 总时长（毫秒）",Ui.Input("ParticleToLogoMs",a),"从粒子首次出现到 550C 完整形成的总时间，已包含待机。实际重组时间 = 总时长 − 待机时长，总时长必须大于待机时长。例如待机 500、总时长 1000，则重组 500 毫秒。默认 1200，范围 1～20000 毫秒。");
        Ui.Field(opening,"550C 完整停留时长（毫秒）",Ui.Input("LogoHoldMs",a),"从 550C 已完整形成开始计时，停留结束即开始全屏打碎。不包含待机、重组或打碎时间。默认 450，范围 0～10000 毫秒。");
        opening.Children.Add(Ui.Label("以上三项统一受整体时间倍率缩放；示例与默认时长按 1 倍计算。总时长已包含待机，不会再次相加。",12,Ui.Muted));
        Ui.Fold(p,"中央粒子与 550C",opening);
        var particles=new StackPanel();Ui.Field(particles,"粒子数量",Ui.Input("ParticleCount",a),"默认 700。增大后更细密，也更消耗显卡。一般建议 400～1000。");Ui.Field(particles,"粒子大小",Ui.Input("ParticleSize",a),"以 1920×1080 画面为基准的像素大小，默认 2.2。显示器缩放会自动适配。");Ui.Field(particles,"全屏扩散强度",Ui.Input("ParticleSpread",a),"默认 1。增大后粒子向边缘扩散得更远，建议 0.8～1.4。");Ui.Field(particles,"全屏打碎时长（毫秒）",Ui.Input("LogoBurstMs",a),"550C 化为粒子并扩散到全屏，默认 900 毫秒。");Ui.Fold(p,"粒子效果",particles);
        var windows=new StackPanel();Ui.Field(windows,"多窗口交叉显现（毫秒）",Ui.Input("MultiWindowRevealMs",a),"粒子消散的同时，原 550C 多窗口画面逐渐出现。默认 850 毫秒。");Ui.Field(windows,"就绪状态停留（毫秒）",Ui.Input("ReadyMs",a),"目标应用就绪、完整编排结束后，保留确认画面的时间，默认 600 毫秒。");Ui.Field(windows,"系统显示名称",Ui.Input("SystemLabel",a));windows.Children.Add(Ui.Label("后半段保留原 550C 错位多窗口布局与出现顺序。等待应用时不会重复播放开场或音效。",12,Ui.Muted));Ui.Fold(p,"多窗口与就绪阶段",windows);
        var handoff=new StackPanel();Ui.Field(handoff,"左右压缩时长（毫秒）",Ui.Input("HandoffCompressMs",a),"当前画面从左右向中心收缩，露出真实应用。默认 420 毫秒。");Ui.Field(handoff,"透明淡出时长（毫秒）",Ui.Input("FadeMs",a),"与压缩同时进行，淡出的是当前画面。默认 300 毫秒。");Ui.Field(handoff,"关闭动画时长（毫秒）",Ui.Input("ShutdownMs",a),"独立的中心坍缩关闭编排，推荐 2000～3000，默认 2600 毫秒。");Ui.Fold(p,"最终交接与关闭",handoff);
        var advanced=new StackPanel();Ui.Field(advanced,"最大等待时间（毫秒）",Ui.Input("BootWatchdogMs",a),"防止动画长期挡住桌面。默认 20000 毫秒；慢速编排会自动留出必要时间，超时安全退出。");Ui.Field(advanced,"关闭超时上限（毫秒）",Ui.Input("ShutdownWatchdogMs",a),"关闭资源异常时的保护上限，默认 5000 毫秒，不会阻止应用正常退出。");advanced.Children.Add(Ui.Check("允许 Esc 紧急跳过","AllowEscape",a));advanced.Children.Add(Ui.Check("允许单击画面跳过","AllowClickSkip",a));Ui.Field(advanced,"动画显示位置",Ui.Select("Monitor",a,[new("目标窗口所在屏幕",MonitorTarget.TargetWindow),new("鼠标所在屏幕",MonitorTarget.Cursor),new("主显示器",MonitorTarget.Primary),new("指定显示器",MonitorTarget.Specified)]),"优先跟随目标窗口；暂时不知道目标位置时使用鼠标所在屏幕。");Ui.Field(advanced,"指定显示器",Ui.Select("MonitorDevice",a,Forms.Screen.AllScreens.Select(s=>new Ui.Choice(s.DeviceName+(s.Primary?" · 主屏":""),s.DeviceName))));Ui.Fold(p,"高级参数与显示器",advanced);return p;
    }
    UIElement Hardware()
    {var p=Ui.Stack();Ui.Section(p,"真实系统数据");p.Children.Add(Ui.Check("启用处理器 / 内存 / 显卡自检","HardwareDiagnostics",edit));p.Children.Add(Ui.Label("处理器：读取短时间系统负载和逻辑处理器数。\n\n内存：读取总量、已用、可用和占比。\n\n显卡：读取名称和专用显存总量，不推测显存实时占用。",14,Ui.Muted));p.Children.Add(Ui.Label("这些数据用于展示当前运行状态，不代表硬件健康诊断。检测失败不会阻止应用启动。",12,Ui.Muted));return Ui.Card(Ui.Scroll(p),0);}
    UIElement Network()
    {
        var p=Ui.Stack();Ui.Section(p,"并行网络检测");p.Children.Add(Ui.Check("启用网络自检","Enabled",edit.Network));Ui.Field(p,"检测超时（毫秒）",Ui.Input("TimeoutMs",edit.Network),"默认 2000 毫秒。网络较慢可增至 3000～5000；检测失败不会阻止应用启动。");Ui.Field(p,"虚拟专网识别",Ui.Select("VpnMode",edit.Network,[new("自动识别活动适配器",VpnMode.Auto),new("手动选择适配器",VpnMode.Manual),new("关闭识别",VpnMode.Disabled)]),"识别活动的 VPN / TUN / TAP 网卡，只说明网卡存在，不保证所有流量都经过它。");
        var endpoints=new StackPanel();Ui.Field(endpoints,"域名查询主机",Ui.Input("DnsHost",edit.Network));Ui.Field(endpoints,"国内检测网址\n每行一个 HTTPS 地址",Ui.Input("DomesticText",edit.Network,true));Ui.Field(endpoints,"外部检测网址\n每行一个 HTTPS 地址",Ui.Input("ExternalText",edit.Network,true));Ui.Fold(p,"自定义检测地址",endpoints);
        var adapters=new StackPanel();IEnumerable<Ui.Choice> options;try{options=NetworkDiagnostics.Adapters().Select(x=>new Ui.Choice(x.Name+" · "+x.Description,x.Id)).ToArray();}catch{options=[];}Ui.Field(adapters,"手动选择网卡",Ui.Select("VpnAdapterId",edit.Network,options));adapters.Children.Add(Ui.Label("每个检测网址采样 3 次，报告成功率、延迟中位数与抖动。不采集 AI 应用的网络内容。",12,Ui.Muted));Ui.Fold(p,"适配器与检测说明",adapters);return Ui.Card(Ui.Scroll(p),0);
    }
    UIElement Sound()=>Ui.Card(Ui.Scroll(new StackPanel{Margin=new Thickness(20),Children={SoundControls(edit.Audio,true)}}),0);
    UIElement SoundControls(AudioSettings audio,bool global)
    {
        var p=new StackPanel();var basic=new StackPanel();basic.Children.Add(Ui.Check(global?"全局静音（包括独立配置的应用）":"此应用静音","Muted",audio));basic.Children.Add(Ui.Check("启用主音效","SoundEnabled",audio));Ui.Field(basic,"音效音量",Ui.Volume("SoundVolume",audio));basic.Children.Add(Ui.Check("启用机器语音","VoiceEnabled",audio));Ui.Field(basic,"语音音量",Ui.Volume("VoiceVolume",audio));var voiceSwitches=new WrapPanel();voiceSwitches.Children.Add(Ui.Check("启动语音   ","BootVoice",audio));voiceSwitches.Children.Add(Ui.Check("关闭语音","ShutdownVoice",audio));basic.Children.Add(voiceSwitches);basic.Children.Add(Ui.Buttons(Ui.Button("测试声音",()=>Test(AnimationKind.Boot,"sfx",global)),Ui.Button("测试语音",()=>Test(AnimationKind.Boot,"voice",global))));basic.Children.Add(Ui.Label("测试将显示预览并临时解除静音。默认使用原创音效与通用合成机器语音。",12,Ui.Muted));Ui.Fold(p,"常用声音设置",basic,true);
        var phrases=new StackPanel();var phraseChoices=new[]{new Ui.Choice("系统启动","BOOT"),new("网络已连接","NETWORK"),new("进程已检测","PROCESS"),new("应用就绪","READY"),new("开始关闭","SHUTDOWN"),new("网络监控结束","NETWORK_CLOSED"),new("核心待机","STANDBY"),new("最终断电","POWER_OFF")};var choice=new ComboBox{ItemsSource=phraseChoices,DisplayMemberPath="Label",SelectedValuePath="Value",SelectedIndex=0};var editor=new ContentControl();void ShowPhrase(){var key=(string)((Ui.Choice)choice.SelectedItem).Value;audio.Phrases.TryAdd(key,"");var group=new StackPanel();Ui.Field(group,"播报内容",Ui.Input("Phrases["+key+"]",audio,true),"可使用 {app} 代入应用名称，{system} 代入系统名称。留空则不播报该句。");group.Children.Add(Ui.Buttons(Ui.Button("选择本地语音",()=>AudioFile(audio,key,true)),Ui.Button("恢复合成语音",()=>{audio.VoiceFiles.Remove(key);status.Text="已恢复合成语音，保存后生效。";})));editor.Content=group;}choice.SelectionChanged+=(_,_)=>ShowPhrase();Ui.Field(phrases,"语音事件",choice);phrases.Children.Add(editor);ShowPhrase();Ui.Fold(p,"自定义语音内容",phrases);
        var files=new StackPanel();var soundChoices=new[]{new Ui.Choice("550C 重组","CORE_EXPAND"),new("HUD 电子扫描","HUD_SCAN"),new("自检确认","CHECK_OK"),new("进程检测","PROCESS_DETECTED"),new("系统就绪","TARGET_READY"),new("交接解锁","UNLOCK"),new("降压关机","SHUTDOWN_BEGIN"),new("继电器关闭","RELAY_OFF"),new("显像管收束","CRT_COLLAPSE"),new("最终断电","POWER_OFF")};var soundChoice=new ComboBox{ItemsSource=soundChoices,DisplayMemberPath="Label",SelectedValuePath="Value",SelectedIndex=0};Ui.Field(files,"音效事件",soundChoice);files.Children.Add(Ui.Buttons(Ui.Button("选择本地音效",()=>AudioFile(audio,(string)((Ui.Choice)soundChoice.SelectedItem).Value,false)),Ui.Button("恢复主题音效",()=>{audio.AudioFiles.Remove((string)((Ui.Choice)soundChoice.SelectedItem).Value);status.Text="已恢复主题音效。";})));files.Children.Add(Ui.Label("支持本地 WAV / MP3 / OGG。文件缺失时静默继续；资源仅在动画运行期间加载。",12,Ui.Muted));Ui.Fold(p,"替换主题音效",files);
        var advanced=new StackPanel();Ui.Field(advanced,"系统语音名称（可选）",Ui.Input("VoiceName",audio),"留空使用内置离线机器语音。也可填写已经安装的 Windows 语音名称。");advanced.Children.Add(Ui.Label("公开版本不包含电影原声或演员语音。你可以在本地自行替换有权使用的音频。",12,Ui.Muted));Ui.Fold(p,"语音引擎与资源说明",advanced);return p;
    }
    void AudioFile(AudioSettings audio,string key,bool voice)
    {var dialog=new OpenFileDialog{Filter="本地音频|*.wav;*.mp3;*.ogg",CheckFileExists=true};if(dialog.ShowDialog(this)==true){(voice?audio.VoiceFiles:audio.AudioFiles)[key]=dialog.FileName;status.Text="已选择："+Path.GetFileName(dialog.FileName)+"；保存后生效。";}}
    UIElement General()
    {
        var p=Ui.Stack();Ui.Section(p,"安静地后台运行");p.Children.Add(Ui.Check("Windows 登录时自动运行 550W","StartWithWindows",edit));p.Children.Add(Ui.Check("暂停所有自动动画","Paused",edit));p.Children.Add(Ui.Label("关闭设置窗口后继续留在托盘；退出软件请使用托盘菜单。\n\n动画播放时才启动独立渲染进程，完成、跳过或异常后释放。",14,Ui.Muted));var advanced=new StackPanel();Ui.Field(advanced,"兼容扫描间隔（秒）",Ui.Input("FallbackPollingSeconds",edit),"主监听使用系统事件。这个低频扫描用于补漏，默认 5 秒。一般不需要调整。");advanced.Children.Add(Ui.Label("开机启动只使用当前用户设置。移动便携版目录后，请重新保存开机启动选项。",12,Ui.Muted));advanced.Children.Add(Ui.Button("打开诊断调试预览",()=>DebugRequested?.Invoke(edit,WireJson.Clone(Selected))));Ui.Fold(p,"高级后台设置",advanced);return Ui.Card(Ui.Scroll(p),0);
    }
    UIElement About()
    {
        var p=Ui.Stack();Ui.Section(p,"550W AI Controller "+ProductChangelog.CurrentVersion);p.Children.Add(Ui.Label("Windows 10 / 11 · 64 位\n独立外部程序，不修改或注入 AI 客户端，不读取聊天内容。",14,Ui.Muted));
        var export=Ui.Button("导出故障日志 / 诊断包",()=>_=ExportDiagnostics());p.Children.Add(Ui.Buttons(export));p.Children.Add(Ui.Label("包含运行事件、系统与显示器信息、脱敏配置。不会导出聊天内容、路径、启动参数或自定义语音文本。",12,Ui.Muted));
        var changes=new StackPanel();changes.Children.Add(Ui.Label("版本说明",13,Ui.Muted));changes.Children.Add(Ui.Label(ProductChangelog.BetaNotice,12,Ui.Muted));foreach(var entry in ProductChangelog.Entries){changes.Children.Add(Ui.Label(entry.Version+"  ·  "+entry.Date,15,Ui.Amber));foreach(var text in entry.Changes)changes.Children.Add(Ui.Label("• "+text,12,Ui.Muted));}Ui.Fold(p,"更新日志",changes,true);
        var credits=new StackPanel();credits.Children.Add(Ui.Label("原动画与 HTML：Voidpoket\n插件移植与增强：Ziyang Song / yannicksong0106\n原 550C 多窗口编排保留，前后过渡与控制器独立实现。\n\n原创合成音效、通用机器语音、AI 生成核心素材。完整许可与出处见软件目录中的第三方声明。",12,Ui.Muted));Ui.Fold(p,"鸣谢与开源许可",credits);return Ui.Card(Ui.Scroll(p),0);
    }
    async Task ExportDiagnostics()
    {
        var dialog=new SaveFileDialog{Filter="故障诊断包（ZIP）|*.zip",FileName="550W-诊断-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".zip"};if(dialog.ShowDialog(this)!=true)return;status.Text="正在收集脱敏诊断信息…";
        try{await DiagnosticExportService.ExportAsync(dialog.FileName,dataDirectory,edit);status.Text="诊断包已导出，可将 ZIP 交给开发者排查。";}catch(Exception e){status.Text="导出失败："+e.Message;}
    }
}
