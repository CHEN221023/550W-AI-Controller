using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using Controller550W.Controller.Services;

namespace Controller550W.Controller.Views;

internal sealed class SelectionWindow : Window
{
    public object[] Selected { get; private set; } = [];
    public SelectionWindow(string title, IEnumerable<object> items, (string Caption, string Path)[] columns)
    {
        Title = title; Width = 1040; Height = 640; MinWidth = 750; MinHeight = 400; Ui.Style(this);
        var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Extended, EnableRowVirtualization = true, Background = Ui.Panel, Foreground = Ui.Text, RowBackground = Ui.Panel, AlternatingRowBackground = Ui.Bg, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, HorizontalGridLinesBrush = Ui.Line, BorderBrush = Ui.Line };
        foreach (var (caption, path) in columns)
        {
            if (path == "Icon")
            {
                var image = new FrameworkElementFactory(typeof(Image)); image.SetBinding(Image.SourceProperty, new Binding("Icon")); image.SetValue(Image.WidthProperty, 24d); image.SetValue(Image.HeightProperty, 24d); image.SetValue(Image.MarginProperty, new Thickness(4));
                grid.Columns.Add(new DataGridTemplateColumn { Header = caption, Width = 42, CellTemplate = new DataTemplate { VisualTree = image } });
            }
            else grid.Columns.Add(new DataGridTextColumn { Header = caption, Binding = new Binding(path), Width = path == "Path" ? new DataGridLength(1, DataGridLengthUnitType.Star) : DataGridLength.Auto });
        }
        var all = items.ToArray(); grid.ItemsSource = all;
        var filter = new TextBox { Margin = new Thickness(0, 8, 0, 10) }; filter.TextChanged += (_, _) => grid.ItemsSource = all.Where(x => x.ToString()?.Contains(filter.Text, StringComparison.OrdinalIgnoreCase) == true).ToArray();
        var root = new DockPanel { Margin = new Thickness(18) }; var top = new StackPanel(); top.Children.Add(Ui.Label(title, 21, Ui.Amber)); top.Children.Add(Ui.Label("选择一项或按 Ctrl / Shift 选择多项。可按名称、窗口标题或路径筛选。", 12, Ui.Muted)); top.Children.Add(filter); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        var bottom = Ui.Buttons(Ui.Button("添加选中的应用", () => { Selected = grid.SelectedItems.Cast<object>().ToArray(); if (Selected.Length > 0) DialogResult = true; }), Ui.Button("取消", () => DialogResult = false)); DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom); root.Children.Add(grid); Content = root;
    }
}
