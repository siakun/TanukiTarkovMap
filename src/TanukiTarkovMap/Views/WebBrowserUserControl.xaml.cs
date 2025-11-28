using System.Windows.Controls;
using TanukiTarkovMap.ViewModels;

namespace TanukiTarkovMap.Views
{
    /// <summary>
    /// WebBrowserUserControl.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class WebBrowserUserControl : UserControl
    {
        public WebBrowserUserControl()
        {
            InitializeComponent();

            // ViewModel에 브라우저 인스턴스 전달
            Loaded += (s, e) =>
            {
                if (DataContext is WebBrowserViewModel viewModel)
                {
                    viewModel.SetBrowser(Browser);
                }
            };
        }

        /// <summary>
        /// ViewModel 접근용 프로퍼티
        /// </summary>
        public WebBrowserViewModel? ViewModel => DataContext as WebBrowserViewModel;
    }
}
