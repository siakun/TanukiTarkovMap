using CommunityToolkit.Mvvm.ComponentModel;
using TanukiTarkovMap.Localization;

namespace TanukiTarkovMap.ViewModels;

public partial class SplashWindowViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Status { get; set; } = Strings.Startup_Starting;
}
