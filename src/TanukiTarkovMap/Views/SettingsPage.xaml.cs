using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using TanukiTarkovMap.Models.Data;
using TanukiTarkovMap.Models.Services;

namespace TanukiTarkovMap.Views
{
    /// <summary>
    /// SettingsPage.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SettingsPage : UserControl
    {
        // 핫키 입력 모드 플래그
        private bool _isHotkeyInputMode = false;

        public SettingsPage()
        {
            InitializeComponent();
            LoadSettings();
            UpdateHotkeySettingsState(); // 핫키 설정 상태 초기화
        }

        /// <summary>
        /// 현재 설정을 UI에 로드
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                var settings = App.GetSettings();

                // 타르코프 경로 설정
                GameFolderTextBox.Text = App.GameFolder ?? string.Empty;
                ScreenshotsFolderTextBox.Text = App.ScreenshotsFolder ?? string.Empty;

                // 단축키 설정
                HotkeyEnabledCheckBox.IsChecked = settings.HotkeyEnabled;
                HotkeyButton.Content = settings.HotkeyKey;

                // 파일 자동 정리 설정
                AutoDeleteLogsCheckBox.IsChecked = settings.autoDeleteLogs;
                AutoDeleteScreenshotsCheckBox.IsChecked = settings.autoDeleteScreenshots;
            }
            catch (Exception) { }
        }


        /// <summary>
        /// 설정 저장 (자동 저장)
        /// </summary>
        private void SaveSettings()
        {
            try
            {
                var settings = App.GetSettings();

                // 타르코프 경로 설정 저장
                string gameFolderPath = GameFolderTextBox.Text?.Trim();
                string screenshotsFolderPath = ScreenshotsFolderTextBox.Text?.Trim();

                if (!string.IsNullOrEmpty(gameFolderPath))
                {
                    App.GameFolder = gameFolderPath;
                    settings.GameFolder = gameFolderPath;
                }

                if (!string.IsNullOrEmpty(screenshotsFolderPath))
                {
                    App.ScreenshotsFolder = screenshotsFolderPath;
                    settings.ScreenshotsFolder = screenshotsFolderPath;
                }

                // 단축키 설정 저장
                settings.HotkeyEnabled = HotkeyEnabledCheckBox.IsChecked ?? true;
                settings.HotkeyKey = HotkeyButton.Content?.ToString()?.Trim() ?? "F11";

                // 파일 자동 정리 설정 저장
                settings.autoDeleteLogs = AutoDeleteLogsCheckBox.IsChecked ?? false;
                settings.autoDeleteScreenshots = AutoDeleteScreenshotsCheckBox.IsChecked ?? false;

                // 설정 저장
                App.SetSettings(settings);
                Settings.Save();

                // 핫키 설정이 변경된 경우 MainWindow에서 핫키 재등록
                if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.UpdateHotkeySettings();
                }
            }
            catch (Exception) { }
        }

        /// <summary>
        /// 게임 폴더 경로 변경 시
        /// </summary>
        private void GameFolder_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        /// <summary>
        /// 스크린샷 폴더 경로 변경 시
        /// </summary>
        private void ScreenshotsFolder_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        /// <summary>
        /// 로그 자동 정리 설정 변경 시
        /// </summary>
        private void AutoDeleteLogs_Changed(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        /// <summary>
        /// 스크린샷 자동 정리 설정 변경 시
        /// </summary>
        private void AutoDeleteScreenshots_Changed(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        /// <summary>
        /// 단축키 활성화 설정 변경
        /// </summary>
        private void HotkeyEnabled_Changed(object sender, RoutedEventArgs e)
        {
            UpdateHotkeySettingsState();
            SaveSettings();
        }

        /// <summary>
        /// 핫키 설정 활성화/비활성화 상태 업데이트
        /// </summary>
        private void UpdateHotkeySettingsState()
        {
            try
            {
                bool isHotkeyEnabled = HotkeyEnabledCheckBox.IsChecked ?? false;

                // 핫키 버튼 활성화 상태 설정
                HotkeyButton.IsEnabled = isHotkeyEnabled;
                HotkeyButton.Opacity = isHotkeyEnabled ? 1.0 : 0.5;

                // 안내 텍스트 투명도 조정
                foreach (var child in HotkeyInputPanel.Children)
                {
                    if (child is TextBlock textBlock)
                    {
                        textBlock.Opacity = isHotkeyEnabled ? 1.0 : 0.5;
                    }
                    else if (child is StackPanel stackPanel)
                    {
                        foreach (var innerChild in stackPanel.Children.OfType<TextBlock>())
                        {
                            innerChild.Opacity = isHotkeyEnabled ? 1.0 : 0.5;
                        }
                    }
                }
            }
            catch (Exception) { }
        }

        /// <summary>
        /// 핫키 버튼 클릭 시 (입력 모드 시작)
        /// </summary>
        private void HotkeyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                HotkeyButton.Content = "키를 눌러주세요...";
                HotkeyButton.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x6A, 0x6A, 0x2A)
                ); // 노란빛

                // 포커스 설정
                HotkeyButton.Focus();
                _isHotkeyInputMode = true;
            }
            catch (Exception) { }
        }

        /// <summary>
        /// 핫키 버튼 포커스 해제 시 (입력 모드 종료)
        /// </summary>
        private void HotkeyButton_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                // 입력 모드 종료
                _isHotkeyInputMode = false;

                // 원래 상태로 복원
                HotkeyButton.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x3A, 0x3A, 0x3A)
                );

                // 빈 값이거나 안내 텍스트인 경우 기본값으로 설정
                if (
                    HotkeyButton.Content?.ToString() == "키를 눌러주세요..."
                    || string.IsNullOrWhiteSpace(HotkeyButton.Content?.ToString())
                )
                {
                    HotkeyButton.Content = "F11";
                }

                // 설정 저장
                SaveSettings();
            }
            catch (Exception) { }
        }

        /// <summary>
        /// 핫키 버튼 PreviewKeyDown 이벤트 (모든 키 캐치)
        /// </summary>
        private void HotkeyButton_PreviewKeyDown(
            object sender,
            System.Windows.Input.KeyEventArgs e
        )
        {
            try
            {
                if (!_isHotkeyInputMode)
                {
                    return;
                }

                // Tab 키는 포커스 이동을 위해 허용
                if (e.Key == System.Windows.Input.Key.Tab)
                {
                    return;
                }

                // 단일 키 분석 및 즉시 설정
                string keyString = e.Key.ToString();

                if (!string.IsNullOrEmpty(keyString))
                {
                    HotkeyButton.Content = keyString;

                    // 입력 모드 종료 및 포커스 해제
                    _isHotkeyInputMode = false;
                    HotkeyButton.Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x3A, 0x3A, 0x3A)
                    );
                    HotkeyButton.MoveFocus(
                        new System.Windows.Input.TraversalRequest(
                            System.Windows.Input.FocusNavigationDirection.Next
                        )
                    );
                }

                // 키 입력 차단
                e.Handled = true;
            }
            catch (Exception) { }
        }

        /// <summary>
        /// 핫키 버튼 KeyDown 이벤트
        /// </summary>
        private void HotkeyButton_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                if (!_isHotkeyInputMode)
                {
                    return;
                }

                // Tab 키는 포커스 이동을 위해 무시
                if (e.Key == System.Windows.Input.Key.Tab)
                {
                    return;
                }

                e.Handled = true;
            }
            catch (Exception) { }
        }

        /// <summary>
        /// 게임 폴더 찾아보기 버튼 클릭
        /// </summary>
        private void BrowseGameFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFolderDialog
                {
                    Title = "Escape From Tarkov 게임 폴더 선택",
                    InitialDirectory = !string.IsNullOrEmpty(GameFolderTextBox.Text)
                        ? GameFolderTextBox.Text
                        : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Multiselect = false
                };

                if (dialog.ShowDialog() == true)
                {
                    GameFolderTextBox.Text = dialog.FolderName;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"폴더 선택 중 오류가 발생했습니다: {ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        /// <summary>
        /// 스크린샷 폴더 찾아보기 버튼 클릭
        /// </summary>
        private void BrowseScreenshotsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFolderDialog
                {
                    Title = "스크린샷 폴더 선택",
                    InitialDirectory = !string.IsNullOrEmpty(ScreenshotsFolderTextBox.Text)
                        ? ScreenshotsFolderTextBox.Text
                        : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Multiselect = false
                };

                if (dialog.ShowDialog() == true)
                {
                    ScreenshotsFolderTextBox.Text = dialog.FolderName;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"폴더 선택 중 오류가 발생했습니다: {ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }
    }
}
