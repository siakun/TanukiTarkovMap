using System.Windows;

namespace TanukiTarkovMap.Views;

public class WindowBoundsChangedEventArgs : EventArgs
{
    public Rect Bounds { get; set; }
}
