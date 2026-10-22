using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Xaml.Behaviors;
using TanukiTarkovMap.Core.Utils;
using TanukiTarkovMap.Messages;
using TanukiTarkovMap.Models.Services;
using TanukiTarkovMap.ViewModels;

namespace TanukiTarkovMap.Behaviors;

public sealed class MainWindowInitializationBehavior : Behavior<Window>, IRecipient<HotkeySettingsChangedMessage>
{
    private MainWindowViewModel? _viewModel;
    private HotkeyService? _hotkeyService;
    private TrayWindowBehavior? _trayBehavior;

    public static readonly DependencyProperty LoadingPanelNameProperty =
        DependencyProperty.Register(nameof(LoadingPanelName), typeof(string), typeof(MainWindowInitializationBehavior),
            new PropertyMetadata("LoadingPanel"));

    public string LoadingPanelName
    {
        get => (string)GetValue(LoadingPanelNameProperty);
        set => SetValue(LoadingPanelNameProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        _hotkeyService = ServiceLocator.HotkeyService;
        AssociatedObject.Initialized += OnWindowInitialized;
        AssociatedObject.Loaded += OnWindowLoaded;
        AssociatedObject.Closed += OnWindowClosed;
        ServiceLocator.GameWindowTracker.GameExited += OnGameExited;
        if (AssociatedObject.IsInitialized)
        {
            InitializeWindow();
        }
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Initialized -= OnWindowInitialized;
        AssociatedObject.Loaded -= OnWindowLoaded;
        AssociatedObject.Closed -= OnWindowClosed;
        ServiceLocator.GameWindowTracker.GameExited -= OnGameExited;
        WeakReferenceMessenger.Default.Unregister<HotkeySettingsChangedMessage>(this);
        _viewModel = null;
        _trayBehavior = null;
        _hotkeyService = null;
        base.OnDetaching();
    }

    private void OnWindowInitialized(object? sender, EventArgs e) => InitializeWindow();

    private void InitializeWindow()
    {
        _viewModel = (MainWindowViewModel)AssociatedObject.DataContext;
        var window = AssociatedObject;
        window.Width = _viewModel.CurrentWindowWidth;
        window.Height = _viewModel.CurrentWindowHeight;
        window.Left = _viewModel.CurrentWindowLeft;
        window.Top = _viewModel.CurrentWindowTop;
        Logger.SimpleLog($"[Constructor] Set window size explicitly: {window.Width}x{window.Height} at ({window.Left}, {window.Top})");
        WeakReferenceMessenger.Default.Register(this);
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        var window = AssociatedObject;
        if (window.FindName(LoadingPanelName) is UIElement loadingPanel)
        {
            loadingPanel.Visibility = Visibility.Collapsed;
        }

        window.Width = _viewModel!.CurrentWindowWidth;
        window.Height = _viewModel.CurrentWindowHeight;
        Logger.SimpleLog($"[MainWindow_Loaded] Restored window size: {window.Width}x{window.Height}");

        _trayBehavior = Interaction.GetBehaviors(window).OfType<TrayWindowBehavior>().FirstOrDefault();
        try
        {
            ServiceLocator.GameWindowTracker.Start(window.Dispatcher);
            _hotkeyService!.Initialize(window, () => _trayBehavior?.ToggleVisibility());
            _hotkeyService.RegisterHotkey(_viewModel.HotkeyEnabled, _viewModel.HotkeyKey);
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to initialize HotkeyService", ex);
        }
    }

    private void OnGameExited(object? sender, EventArgs e)
    {
        if (App.GetSettings().GameFocusHotkeyEnabled && (!AssociatedObject.IsVisible || AssociatedObject.WindowState == WindowState.Minimized))
            _trayBehavior?.ShowFromTray();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _viewModel?.FlushPendingBoundsSave();
        _viewModel?.StopRaidFocusTracking();
        ServiceLocator.GameWindowTracker.GameExited -= OnGameExited;
        ServiceLocator.GameWindowTracker.Dispose();
    }

    public void Receive(HotkeySettingsChangedMessage message)
    {
        try
        {
            _viewModel!.LoadSettings();
            _hotkeyService!.UpdateHotkey(_viewModel.HotkeyEnabled, _viewModel.HotkeyKey);
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to update hotkey settings", ex);
        }
    }
}
