using System.Windows;

namespace TanukiTarkovMap.Views;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
    }

    public void SetStatus(string status)
    {
        Dispatcher.Invoke(() => StatusText.Text = status);
    }
}
