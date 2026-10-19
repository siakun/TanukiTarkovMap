using TanukiTarkovMap.Core.Services;
using System.Reflection;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using Siakun.AutoUpdate;
using TanukiTarkovMap.Messages;
using TanukiTarkovMap.Models.Utils;
using TanukiTarkovMap.Core.Utils;

namespace TanukiTarkovMap.Models.Services
{
    internal static class UpdateServiceFactory
    {
        internal static readonly string RepositoryUrl =
            typeof(UpdateServiceFactory).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute => attribute.Key == "UpdateRepositoryUrl")?.Value
            ?? "https://github.com/siakun/TanukiTarkovMap";

        internal static UpdateService Create()
        {
            var catalog = new GitHubReleaseCatalog(RepositoryUrl, log: Logger.SimpleLog);
            var updateService = new UpdateService(catalog, new UpdateSettingsAdapter(), Logger.SimpleLog);

            updateService.UpdateReady += (_, e) =>
                WeakReferenceMessenger.Default.Send(new UpdateReadyMessage(e.Version.ToString()));

            return updateService;
        }

        private sealed class UpdateSettingsAdapter : IUpdateSettings
        {
            public bool AutoUpdateEnabled
            {
                get => App.GetSettings().AutoUpdateEnabled;
                set => RunOnUiThread(() =>
                {
                    App.GetSettings().AutoUpdateEnabled = value;
                    Settings.Save();
                });
            }

            public bool PrereleaseEnabled => App.GetSettings().PrereleaseEnabled;

            private static void RunOnUiThread(Action action)
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null || dispatcher.CheckAccess())
                {
                    action();
                    return;
                }

                dispatcher.Invoke(action);
            }
        }
    }
}
