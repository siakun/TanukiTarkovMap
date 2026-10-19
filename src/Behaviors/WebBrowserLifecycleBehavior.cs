using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CefSharp;
using CefSharp.Wpf;
using Microsoft.Xaml.Behaviors;
using TanukiTarkovMap.Core.Data;
using TanukiTarkovMap.Core.Offline;
using TanukiTarkovMap.Models.Offline;
using TanukiTarkovMap.ViewModels;

namespace TanukiTarkovMap.Behaviors
{

    public class WebBrowserLifecycleBehavior : Behavior<ContentControl>
    {
        private WebBrowserViewModel? _viewModel;
        private ChromiumWebBrowser? _browser;
        private IRequestContext? _localContext;

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.Loaded += OnBrowserLoaded;
            AssociatedObject.DataContextChanged += OnDataContextChanged;
            AssociatedObject.KeyDown += OnBrowserKeyDown;

            ConnectViewModel();
        }

        protected override void OnDetaching()
        {
            AssociatedObject.Loaded -= OnBrowserLoaded;
            AssociatedObject.DataContextChanged -= OnDataContextChanged;
            AssociatedObject.KeyDown -= OnBrowserKeyDown;
            DisconnectViewModel();

            base.OnDetaching();
        }

        private void OnBrowserLoaded(object sender, RoutedEventArgs e)
        {
            ConnectViewModel();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ConnectViewModel();
        }

        private void OnBrowserKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F12 && _browser?.IsDisposed == false)
            {
                _browser.ShowDevTools();
            }
        }

        private void ConnectViewModel()
        {
            var viewModel = AssociatedObject.DataContext as WebBrowserViewModel;
            if (ReferenceEquals(_viewModel, viewModel))
            {
                return;
            }

            DisconnectViewModel();
            _viewModel = viewModel;
            if (_viewModel == null) return;

            _viewModel.BrowserModeChanged += OnBrowserModeChanged;
            ReplaceBrowser();
        }

        private void OnBrowserModeChanged(object? sender, EventArgs e) => ReplaceBrowser();

        private void ReplaceBrowser()
        {
            ReleaseBrowser();
            if (_viewModel == null) return;

            if (_viewModel.Mode == MapViewMode.Local)
            {
                _localContext = LocalViewerHost.CreateRequestContext();
                Cef.AddDisposable(_localContext);
            }

            _browser = new ChromiumWebBrowser
            {
                RequestContext = _localContext,
                Address = "about:blank",
            };
            _browser.BrowserSettings.WindowlessFrameRate = 60;
            Interaction.GetBehaviors(_browser).Add(new DuplicateMouseMoveFilterBehavior());

            _viewModel.SetBrowser(_browser);
            AssociatedObject.Content = _browser;
        }

        private void ReleaseBrowser()
        {
            _viewModel?.SetBrowser(null);
            AssociatedObject.Content = null;
            if (_browser != null)
            {
                Interaction.GetBehaviors(_browser).Clear();
                _browser.Dispose();
                _browser = null;
            }
            if (_localContext != null)
            {
                Cef.RemoveDisposable(_localContext);
                if (!_localContext.IsDisposed) _localContext.Dispose();
                _localContext = null;
            }
        }

        private void DisconnectViewModel()
        {
            if (_viewModel != null) _viewModel.BrowserModeChanged -= OnBrowserModeChanged;
            ReleaseBrowser();
            _viewModel = null;
        }
    }
}
