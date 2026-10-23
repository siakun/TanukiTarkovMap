using System.Windows;
using System.Windows.Controls;
using TanukiTarkovMap.Localization;

namespace TanukiTarkovMap.Views;

public sealed class DefaultValueNotice : TextBlock
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(bool), typeof(DefaultValueNotice), new PropertyMetadata(false, OnStateChanged));

    public static readonly DependencyProperty DefaultValueProperty = DependencyProperty.Register(
        nameof(DefaultValue), typeof(bool), typeof(DefaultValueNotice), new PropertyMetadata(false, OnStateChanged));

    public DefaultValueNotice() => Refresh();

    public bool Value
    {
        get => (bool)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public bool DefaultValue
    {
        get => (bool)GetValue(DefaultValueProperty);
        set => SetValue(DefaultValueProperty, value);
    }

    private static void OnStateChanged(DependencyObject notice, DependencyPropertyChangedEventArgs e)
        => ((DefaultValueNotice)notice).Refresh();

    private void Refresh()
    {
        Visibility = Value == DefaultValue ? Visibility.Collapsed : Visibility.Visible;
        SetResourceReference(TextProperty, DefaultValue ? nameof(Strings.Settings_Default_On) : nameof(Strings.Settings_Default_Off));
    }
}
