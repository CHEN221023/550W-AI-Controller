using System.Windows;
using System.Windows.Controls;
namespace Controller550W.Controller.Views;

internal sealed class CloseConfirmationWindow : Window
{
    public event Action? Confirmed;
    public event Action? Cancelled;
    bool answered;
    public void Dismiss(){answered=true;Close();}
    public CloseConfirmationWindow(string name)
    {
        Title="550W · 关闭确认";Width=370;Height=160;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=true;WindowStartupLocation=WindowStartupLocation.CenterScreen;Ui.Style(this);
        var p=Ui.Stack();p.Margin=new Thickness(20);p.Children.Add(Ui.Label("关闭 "+name+"？",20,Ui.Text));
        var cancel=Ui.Button("取消",()=>{if(answered)return;answered=true;Cancelled?.Invoke();Close();});
        var close=Ui.Button("关闭",()=>{if(answered)return;answered=true;Confirmed?.Invoke();Close();});close.IsDefault=true;cancel.IsCancel=true;
        p.Children.Add(Ui.Buttons(cancel,close));Content=p;
        Closed+=(_,_)=>{if(!answered){answered=true;Cancelled?.Invoke();}};
    }
}
