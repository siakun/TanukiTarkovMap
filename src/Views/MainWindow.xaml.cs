using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Xaml.Behaviors;
using TanukiTarkovMap.Behaviors;
using TanukiTarkovMap.Messages;
using TanukiTarkovMap.Models.Services;
using TanukiTarkovMap.Core.Services;
using TanukiTarkovMap.Models.Utils;
using TanukiTarkovMap.Core.Utils;
using TanukiTarkovMap.ViewModels;

namespace TanukiTarkovMap.Views
{
    public class WindowBoundsChangedEventArgs : EventArgs
    {
        public Rect Bounds { get; set; }
    }

    public partial class MainWindow : Window, IRecipient<HotkeySettingsChangedMessage>
    {
        private MainWindowViewModel _viewModel;
        private HotkeyService _hotkeyService;
        private TrayWindowBehavior? _trayBehavior;

        public MainWindow()
        {
            _hotkeyService = ServiceLocator.HotkeyService;

            InitializeComponent();

            _viewModel = (MainWindowViewModel)DataContext;

            this.Width = _viewModel.CurrentWindowWidth;
            this.Height = _viewModel.CurrentWindowHeight;
            this.Left = _viewModel.CurrentWindowLeft;
            this.Top = _viewModel.CurrentWindowTop;
            Logger.SimpleLog($"[Constructor] Set window size explicitly: {this.Width}x{this.Height} at ({this.Left}, {this.Top})");

            Loaded += MainWindow_Loaded;

            WeakReferenceMessenger.Default.Register(this);

        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;

            this.Width = _viewModel.CurrentWindowWidth;
            this.Height = _viewModel.CurrentWindowHeight;
            Logger.SimpleLog($"[MainWindow_Loaded] Restored window size: {this.Width}x{this.Height}");

            var behaviors = Interaction.GetBehaviors(this);
            _trayBehavior = behaviors.OfType<TrayWindowBehavior>().FirstOrDefault();

            InitializeHotkeyService();
        }

        private void InitializeHotkeyService()
        {
            try
            {
                _hotkeyService.Initialize(this, () =>
                {
                    _trayBehavior?.ToggleVisibility();
                });

                _hotkeyService.RegisterHotkey(_viewModel.HotkeyEnabled, _viewModel.HotkeyKey);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize HotkeyService", ex);
            }
        }

        public void Receive(HotkeySettingsChangedMessage message)
        {
            try
            {
                _viewModel.LoadSettings();

                _hotkeyService.UpdateHotkey(_viewModel.HotkeyEnabled, _viewModel.HotkeyKey);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to update hotkey settings", ex);
            }
        }
    }
}
