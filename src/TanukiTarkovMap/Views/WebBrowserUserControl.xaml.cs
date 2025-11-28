using System.Windows.Controls;
using System.Windows.Input;
using CefSharp;
using CefSharp.Wpf;
using TanukiTarkovMap.ViewModels;

namespace TanukiTarkovMap.Views
{
    /// <summary>
    /// WebBrowserUserControl.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class WebBrowserUserControl : UserControl
    {
        private ChromiumWebBrowser? _browser;

        public WebBrowserUserControl()
        {
            InitializeComponent();

            // 브라우저 생성 및 설정
            CreateBrowser();

            // ViewModel에 브라우저 인스턴스 전달 및 URL 로드
            // 중요: 이벤트 핸들러가 등록된 후에 URL을 로드해야 JavascriptMessageReceived가 정상 작동
            Loaded += (s, e) =>
            {
                if (DataContext is WebBrowserViewModel viewModel && _browser != null)
                {
                    // 1. 먼저 ViewModel에 브라우저 전달 (이벤트 핸들러 등록)
                    viewModel.SetBrowser(_browser);

                    // 2. 이벤트 핸들러 등록 후 실제 URL 로드
                    _browser.Load(viewModel.Address);
                }
            };

            // F12 키로 개발자 도구 열기
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.F12)
                {
                    _browser?.ShowDevTools();
                }
            };
        }

        /// <summary>
        /// ChromiumWebBrowser 생성
        /// </summary>
        private void CreateBrowser()
        {
            // about:blank로 초기화 (실제 URL은 이벤트 핸들러 등록 후 로드)
            _browser = new ChromiumWebBrowser("about:blank");

            // 컨테이너에 브라우저 추가
            BrowserContainer.Children.Add(_browser);
        }

        /// <summary>
        /// ViewModel 접근용 프로퍼티
        /// </summary>
        public WebBrowserViewModel? ViewModel => DataContext as WebBrowserViewModel;

        /// <summary>
        /// 브라우저 인스턴스 접근용 프로퍼티
        /// </summary>
        public ChromiumWebBrowser? Browser => _browser;
    }
}
