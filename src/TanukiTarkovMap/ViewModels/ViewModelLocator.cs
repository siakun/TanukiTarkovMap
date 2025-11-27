using Microsoft.Extensions.DependencyInjection;
using TanukiTarkovMap.Models.Services;

namespace TanukiTarkovMap.ViewModels
{
    /// <summary>
    /// DI 컨테이너를 통해 ViewModel을 제공하는 Locator
    /// XAML에서 DataContext 바인딩에 사용
    /// </summary>
    public class ViewModelLocator
    {
        private static IServiceProvider? _serviceProvider;

        /// <summary>
        /// DI 컨테이너 초기화
        /// </summary>
        public static void Initialize()
        {
            var services = new ServiceCollection();

            // Services 등록
            services.AddSingleton<WebViewUIService>();
            services.AddSingleton<WindowBoundsService>();

            // ViewModels 등록
            services.AddTransient<MainWindowViewModel>();

            _serviceProvider = services.BuildServiceProvider();
        }

        /// <summary>
        /// MainWindowViewModel 인스턴스
        /// </summary>
        public MainWindowViewModel MainWindowViewModel
            => _serviceProvider?.GetRequiredService<MainWindowViewModel>()
               ?? throw new InvalidOperationException("ViewModelLocator가 초기화되지 않았습니다. Initialize()를 먼저 호출하세요.");
    }
}
