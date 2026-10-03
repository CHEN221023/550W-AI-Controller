using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;

namespace Controller550W.Controller.Views;

internal static class Ui
{
    public static Brush Bg = Brush("#0D1016"), Panel = Brush("#181D26"), Line = Brush("#343B48");
    public static Brush Text = Brush("#F1F2F6"), Muted = Brush("#9BA6B9"), Amber = Brush("#F16B6F");
    public static SolidColorBrush Brush(string color) => (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
    public static void Style(Window window)
    {
        window.Background = new LinearGradientBrush(Color.FromRgb(25,29,39), Color.FromRgb(10,13,19), 65);
        window.Foreground = Text; window.FontFamily = new FontFamily("Microsoft YaHei UI"); window.FontSize = 13;
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        window.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse("""
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
 <Style TargetType="Button">
  <Setter Property="Background" Value="#252B36"/><Setter Property="Foreground" Value="#F1F2F6"/><Setter Property="BorderBrush" Value="#47505F"/><Setter Property="BorderThickness" Value="1"/>
  <Setter Property="Padding" Value="15,9"/><Setter Property="Margin" Value="0,0,8,0"/><Setter Property="Cursor" Value="Hand"/>
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button"><Border x:Name="chrome" CornerRadius="12" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" Padding="{TemplateBinding Padding}"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="chrome" Property="Background" Value="#394252"/><Setter TargetName="chrome" Property="BorderBrush" Value="#B97C83"/></Trigger><Trigger Property="IsPressed" Value="True"><Setter TargetName="chrome" Property="Opacity" Value=".7"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter TargetName="chrome" Property="Opacity" Value=".4"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="chrome" Property="BorderBrush" Value="#F16B6F"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType="TextBox">
  <Setter Property="Background" Value="#10151D"/><Setter Property="Foreground" Value="#F1F2F6"/><Setter Property="BorderBrush" Value="#3A4352"/><Setter Property="BorderThickness" Value="1"/><Setter Property="Padding" Value="12,8"/><Setter Property="CaretBrush" Value="#F16B6F"/><Setter Property="SelectionBrush" Value="#8C394A"/>
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TextBox"><Border x:Name="chrome" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="10"><ScrollViewer x:Name="PART_ContentHost" Margin="0"/></Border><ControlTemplate.Triggers><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="chrome" Property="BorderBrush" Value="#CE606A"/></Trigger><Trigger Property="Validation.HasError" Value="True"><Setter TargetName="chrome" Property="BorderBrush" Value="#FF8A68"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType="ComboBox">
  <Setter Property="Foreground" Value="#F1F2F6"/><Setter Property="Background" Value="#151B24"/><Setter Property="BorderBrush" Value="#3A4352"/><Setter Property="Padding" Value="12,8"/><Setter Property="MinHeight" Value="38"/>
  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBox"><Grid><Border CornerRadius="10" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1"/><ContentPresenter ContentStringFormat="{TemplateBinding SelectionBoxItemStringFormat}" IsHitTestVisible="False" Margin="12,8,34,8" Content="{TemplateBinding SelectionBoxItem}" ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}" VerticalAlignment="Center"/><ToggleButton Focusable="False" IsChecked="{Binding IsDropDownOpen,RelativeSource={RelativeSource TemplatedParent},Mode=TwoWay}"><ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><Border Background="Transparent" CornerRadius="10"><TextBlock Text="⌄" Foreground="#AFB9CA" HorizontalAlignment="Right" Margin="0,0,14,0" VerticalAlignment="Center" FontSize="16"/></Border></ControlTemplate></ToggleButton.Template></ToggleButton><Popup x:Name="PART_Popup" Placement="Bottom" AllowsTransparency="True" IsOpen="{TemplateBinding IsDropDownOpen}" Focusable="False" PopupAnimation="Fade"><Border Background="#202733" BorderBrush="#505B6E" BorderThickness="1" CornerRadius="10" Padding="5" MinWidth="{Binding ActualWidth,RelativeSource={RelativeSource TemplatedParent}}" MaxHeight="360"><ScrollViewer><ItemsPresenter KeyboardNavigation.DirectionalNavigation="Contained"/></ScrollViewer></Border></Popup></Grid></ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType="ComboBoxItem"><Setter Property="Foreground" Value="#F1F2F6"/><Setter Property="Padding" Value="10,8"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBoxItem"><Border x:Name="row" CornerRadius="7" Padding="{TemplateBinding Padding}" Background="Transparent"><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property="IsHighlighted" Value="True"><Setter TargetName="row" Property="Background" Value="#55313C"/></Trigger><Trigger Property="IsSelected" Value="True"><Setter TargetName="row" Property="Background" Value="#3B3543"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
 <Style TargetType="CheckBox"><Setter Property="Foreground" Value="#E9EDF4"/><Setter Property="Margin" Value="0,8"/><Setter Property="VerticalContentAlignment" Value="Center"/>
 <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox"><DockPanel Background="Transparent"><Grid Width="34" Height="20" Margin="0,0,10,0" DockPanel.Dock="Left"><Border x:Name="track" CornerRadius="10" Background="#313C4D" BorderBrush="#637187" BorderThickness="1"/><Ellipse x:Name="thumb" Width="14" Height="14" Fill="#BCC6D6" HorizontalAlignment="Left" Margin="3,0"/></Grid><ContentPresenter VerticalAlignment="Center"/></DockPanel><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="track" Property="Background" Value="#A84153"/><Setter TargetName="track" Property="BorderBrush" Value="#E47D8C"/><Setter TargetName="thumb" Property="HorizontalAlignment" Value="Right"/><Setter TargetName="thumb" Property="Fill" Value="#FFF4F6"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value=".4"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
 <Style TargetType="RadioButton"><Setter Property="Foreground" Value="#E9EDF4"/><Setter Property="Margin" Value="0,8,18,8"/><Setter Property="VerticalContentAlignment" Value="Center"/></Style>
 <Style TargetType="ListBox"><Setter Property="Background" Value="Transparent"/><Setter Property="BorderThickness" Value="0"/><Setter Property="Foreground" Value="#F1F2F6"/><Setter Property="ScrollViewer.HorizontalScrollBarVisibility" Value="Disabled"/></Style>
 <Style TargetType="ListBoxItem"><Setter Property="HorizontalContentAlignment" Value="Stretch"/><Setter Property="Padding" Value="12,12"/><Setter Property="Margin" Value="0,3"/><Setter Property="Foreground" Value="#ABB5C6"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ListBoxItem"><Border x:Name="card" Background="Transparent" CornerRadius="14" Padding="{TemplateBinding Padding}" BorderThickness="1" BorderBrush="Transparent"><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="card" Property="Background" Value="#252C39"/></Trigger><Trigger Property="IsSelected" Value="True"><Setter TargetName="card" Property="Background" Value="#48252F"/><Setter TargetName="card" Property="BorderBrush" Value="#A84F5D"/><Setter Property="Foreground" Value="#FFF1F2"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
 <Style TargetType="Expander"><Setter Property="Foreground" Value="#E9EDF4"/><Setter Property="Margin" Value="0,6"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Expander"><Border Background="#151C26" BorderBrush="#303A49" BorderThickness="1" CornerRadius="14" Padding="15,10"><StackPanel><ToggleButton IsChecked="{Binding IsExpanded,RelativeSource={RelativeSource TemplatedParent}}" HorizontalContentAlignment="Stretch" Background="Transparent" BorderThickness="0" Foreground="#E9EDF4"><ToggleButton.Template><ControlTemplate TargetType="ToggleButton"><DockPanel Background="Transparent"><TextBlock Text="⌄" DockPanel.Dock="Right" Margin="12,0,0,0" Foreground="#B48289"/><ContentPresenter/></DockPanel></ControlTemplate></ToggleButton.Template><ContentPresenter ContentSource="Header"/></ToggleButton><ContentPresenter x:Name="body" Visibility="Collapsed" Margin="0,14,0,4"/></StackPanel></Border><ControlTemplate.Triggers><Trigger Property="IsExpanded" Value="True"><Setter TargetName="body" Property="Visibility" Value="Visible"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
 <Style TargetType="ScrollBar"><Setter Property="Width" Value="8"/><Setter Property="Background" Value="Transparent"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ScrollBar"><Grid Background="Transparent"><Track x:Name="PART_Track" IsDirectionReversed="True"><Track.DecreaseRepeatButton><RepeatButton Command="ScrollBar.PageUpCommand" Opacity="0" Focusable="False"/></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType="Thumb"><Border CornerRadius="4" Margin="2,0" Background="#536074"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command="ScrollBar.PageDownCommand" Opacity="0" Focusable="False"/></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>
 <Style TargetType="ToolTip"><Setter Property="Background" Value="#273140"/><Setter Property="Foreground" Value="#EFF2F7"/><Setter Property="BorderBrush" Value="#7C5360"/><Setter Property="Padding" Value="14"/><Setter Property="MaxWidth" Value="360"/></Style>
</ResourceDictionary>
"""));
    }
    public static TextBlock Label(string text, double size = 13, Brush? color = null) => new() { Text = text, FontSize = size, Foreground = color ?? Text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4), LineHeight = size * 1.6 };
    public static StackPanel Stack() => new() { Margin = new Thickness(20) };
    public static Button Button(string caption, Action action) { var b = new Button { Content = caption }; b.Click += (_, _) => action(); return b; }
    public static WrapPanel Buttons(params Button[] buttons) { var p = new WrapPanel { Margin = new Thickness(0, 10, 0, 10) }; foreach (var b in buttons) { b.Margin = new Thickness(0,0,8,6); p.Children.Add(b); } return p; }
    public static TextBox Input(string binding, object source, bool multiline = false)
    {
        var t = new TextBox { MinWidth = 120, AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multiline ? 72 : 38 };
        t.SetBinding(TextBox.TextProperty, new Binding(binding) { Source = source, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged, ValidatesOnExceptions = true }); return t;
    }
    public static CheckBox Check(string caption, string path, object source)
    { var c = new CheckBox { Content = caption }; c.SetBinding(CheckBox.IsCheckedProperty, new Binding(path) { Source = source, Mode = BindingMode.TwoWay }); return c; }
    public static void Field(Panel panel, string caption, FrameworkElement input, string? help = null)
    {
        var row = new Grid { Margin = new Thickness(0, 6, 0, 6) }; row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var labels = new DockPanel { VerticalAlignment = VerticalAlignment.Center, Margin=new Thickness(0,0,12,0) };
        if (help != null) { var b = Help(help); DockPanel.SetDock(b,Dock.Right); labels.Children.Add(b); }
        labels.Children.Add(Label(caption)); row.Children.Add(labels); Grid.SetColumn(input, 1); row.Children.Add(input); panel.Children.Add(row);
    }
    public static Button Help(string text)
    {
        // ToolTipService owns hover dismissal; StaysOpen=false throws during normal hover.
        var tip = new ToolTip { Content = Label(text,12) }; var b = new Button { Content="?", Width=23,Height=23,Padding=new Thickness(0),Margin=new Thickness(5,0,0,0),Foreground=Muted,ToolTip=tip,VerticalAlignment=VerticalAlignment.Center };
        System.Windows.Automation.AutomationProperties.SetName(b, "帮助：" + text); ToolTipService.SetInitialShowDelay(b,200); b.Click+=(_,_)=>{tip.PlacementTarget=b;tip.IsOpen=true;}; return b;
    }
    public sealed record Choice(string Label, object Value) { public override string ToString() => Label; }
    public static ComboBox Select(string path, object source, IEnumerable<Choice> choices)
    { var c = new ComboBox { ItemsSource = choices.ToArray(), DisplayMemberPath = "Label", SelectedValuePath = "Value" }; c.SetBinding(ComboBox.SelectedValueProperty, new Binding(path) { Source = source, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }); return c; }
    public static ScrollViewer Scroll(UIElement content) => new() { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, PanningMode=PanningMode.VerticalOnly };
    public static Border Card(UIElement child, int padding=18) => new() { Background=new LinearGradientBrush(Color.FromArgb(218,30,36,47),Color.FromArgb(238,17,22,30),60),BorderBrush=Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(20),Padding=new Thickness(padding),Child=child };
    public static void Section(Panel p, string text) { p.Children.Add(new Border { BorderBrush = Line, BorderThickness = new Thickness(0, 0, 0, 1), Margin = new Thickness(0, 6, 0, 12), Padding = new Thickness(0,0,0,8), Child = Label(text, 17) }); }
    public static Expander Fold(Panel p,string title,UIElement content,bool expanded=false)
    { var expander=new Expander{Header=title,Content=content,IsExpanded=expanded};expander.Expanded+=(_,_)=>{foreach(var other in p.Children.OfType<Expander>())if(other!=expander)other.IsExpanded=false;};p.Children.Add(expander);return expander; }
    public static FrameworkElement Volume(string path, object source)
    {
        var row=new DockPanel(); var value=Label("",12,Muted);value.Width=38;value.TextAlignment=TextAlignment.Right;value.SetBinding(TextBlock.TextProperty,new Binding(path){Source=source,StringFormat="{0:0}"});DockPanel.SetDock(value,Dock.Right);row.Children.Add(value);
        var s = new Slider { Minimum = 0, Maximum = 100, TickFrequency = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6, 0, 6) };
        s.SetBinding(Slider.ValueProperty, new Binding(path) { Source = source, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });s.ValueChanged+=(_,_)=>value.GetBindingExpression(TextBlock.TextProperty)?.UpdateTarget();row.Children.Add(s);return row;
    }
}
