using Microsoft.Extensions.DependencyInjection;
using TanukiTarkovMap.ViewModels;

namespace TanukiTarkovMap.Models.Services
{
    /// <summary>
    /// DI 컨테이너를 통해 Service와 ViewModel을 제공하는 Locator
    ///
    /// 사용법:
    /// - XAML: DataContext="{Binding MainWindowViewModel, Source={StaticResource Locator}}"
    /// - Code: ServiceLocator.BrowserUIService (static 접근)
    /// </summary>
    public class ServiceLocator
    {
        private static IServiceProvider? _serviceProvider;

        /// <summary>
        /// DI 컨테이너 초기화
        /// </summary>
        public static void Initialize()
        {
            var services = new ServiceCollection();

            // Services 등록 (Singleton) - Factory 패턴으로 internal 생성자 호출
            services.AddSingleton(_ => new BrowserUIService());
            services.AddSingleton(_ => new WindowBoundsService());
            services.AddSingleton(_ => new MapEventService());
            services.AddSingleton(_ => new WindowStateManager());
            services.AddSingleton(_ => new HotkeyService());

            // ViewModels 등록
            services.AddTransient<MainWindowViewModel>();

            _serviceProvider = services.BuildServiceProvider();
        }

        #region Service Accessors (Static)
        /// <summary>
        /// BrowserUIService 싱글톤 인스턴스
        /// </summary>
        public static BrowserUIService BrowserUIService
            => _serviceProvider?.GetRequiredService<BrowserUIService>()
               ?? throw new InvalidOperationException("ServiceLocator가 초기화되지 않았습니다.");

        /// <summary>
        /// WindowBoundsService 싱글톤 인스턴스
        /// </summary>
        public static WindowBoundsService WindowBoundsService
            => _serviceProvider?.GetRequiredService<WindowBoundsService>()
               ?? throw new InvalidOperationException("ServiceLocator가 초기화되지 않았습니다.");

        /// <summary>
        /// MapEventService 싱글톤 인스턴스
        /// </summary>
        public static MapEventService MapEventService
            => _serviceProvider?.GetRequiredService<MapEventService>()
               ?? throw new InvalidOperationException("ServiceLocator가 초기화되지 않았습니다.");

        /// <summary>
        /// WindowStateManager 싱글톤 인스턴스
        /// </summary>
        public static WindowStateManager WindowStateManager
            => _serviceProvider?.GetRequiredService<WindowStateManager>()
               ?? throw new InvalidOperationException("ServiceLocator가 초기화되지 않았습니다.");

        /// <summary>
        /// HotkeyService 싱글톤 인스턴스
        /// </summary>
        public static HotkeyService HotkeyService
            => _serviceProvider?.GetRequiredService<HotkeyService>()
               ?? throw new InvalidOperationException("ServiceLocator가 초기화되지 않았습니다.");
        #endregion

        #region ViewModel Accessors (Instance)
        /// <summary>
        /// MainWindowViewModel 인스턴스 (XAML 바인딩용)
        /// </summary>
        public MainWindowViewModel MainWindowViewModel
            => _serviceProvider?.GetRequiredService<MainWindowViewModel>()
               ?? throw new InvalidOperationException("ServiceLocator가 초기화되지 않았습니다.");
        #endregion
    }
}
