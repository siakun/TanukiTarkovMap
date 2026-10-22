using System.Diagnostics;
using TanukiTarkovMap.Core.Services;
using TanukiTarkovMap.Core.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using Siakun.AutoUpdate;
using TanukiTarkovMap.ViewModels;

namespace TanukiTarkovMap.Models.Services
{
    public class ServiceLocator
    {
        private static IServiceProvider? _serviceProvider;

        public static void Initialize()
        {
            var services = new ServiceCollection();

            services.AddSingleton(_ => new MapEventService());
            services.AddSingleton(_ => new WindowStateManager());
            services.AddSingleton<GameWindowTracker>();
            services.AddSingleton(provider => new HotkeyService(provider.GetRequiredService<GameWindowTracker>()));
            services.AddSingleton(_ => new GoonTrackerService());
            services.AddSingleton(provider =>
            {
                var version = FileVersionInfo.GetVersionInfo(typeof(App).Assembly.Location);
                return new RaidShareService(provider.GetRequiredService<MapEventService>(),
                    version.ProductVersion ?? version.FileVersion ?? string.Empty);
            });
            services.AddSingleton(_ => UpdateServiceFactory.Create());

            services.AddSingleton<LogsWatcher>();
            services.AddSingleton<ScreenshotsWatcher>();

            services.AddSingleton<MainWindowViewModel>();

            _serviceProvider = services.BuildServiceProvider();
        }

        public static LogsWatcher LogsWatcher => _serviceProvider?.GetRequiredService<LogsWatcher>()
            ?? throw new InvalidOperationException("ServiceLocator가 초기화되지 않았습니다.");
        public static ScreenshotsWatcher ScreenshotsWatcher => _serviceProvider?.GetRequiredService<ScreenshotsWatcher>()
            ?? throw new InvalidOperationException("ServiceLocator가 초기화되지 않았습니다.");

        public static GameWindowTracker GameWindowTracker
            => _serviceProvider?.GetRequiredService<GameWindowTracker>()
               ?? throw new InvalidOperationException("ServiceLocator가 초기화되지 않았습니다.");

        public static MapEventService MapEventService
            => _serviceProvider?.GetRequiredService<MapEventService>()
               ?? throw new InvalidOperationException("ServiceLocator가 초기화되지 않았습니다.");

        public static WindowStateManager WindowStateManager
            => _serviceProvider?.GetRequiredService<WindowStateManager>()
               ?? throw new InvalidOperationException("ServiceLocator가 초기화되지 않았습니다.");

        public static HotkeyService HotkeyService
            => _serviceProvider?.GetRequiredService<HotkeyService>()
               ?? throw new InvalidOperationException("ServiceLocator가 초기화되지 않았습니다.");

        public static GoonTrackerService GoonTrackerService
            => _serviceProvider?.GetRequiredService<GoonTrackerService>()
               ?? throw new InvalidOperationException("ServiceLocator가 초기화되지 않았습니다.");

        public static RaidShareService RaidShareService
            => _serviceProvider?.GetRequiredService<RaidShareService>()
               ?? throw new InvalidOperationException("ServiceLocator가 초기화되지 않았습니다.");

        public static UpdateService UpdateService
            => _serviceProvider?.GetRequiredService<UpdateService>()
               ?? throw new InvalidOperationException("ServiceLocator가 초기화되지 않았습니다.");

        public MainWindowViewModel MainWindowViewModel
            => _serviceProvider?.GetRequiredService<MainWindowViewModel>()
               ?? throw new InvalidOperationException("ServiceLocator가 초기화되지 않았습니다.");
    }
}
