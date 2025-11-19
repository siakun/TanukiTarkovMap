using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TarkovClient
{
    /// <summary>
    /// SettingsPage.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SettingsPage : System.Windows.Controls.UserControl
    {
        // 핫키 입력 모드 플래그
        private bool _isHotkeyInputMode = false;

        // 한국 명칭(영어) -> 게임 내부 이름 매핑 (실제 테스트 결과 기반)
        private readonly Dictionary<string, string> _mapDisplayToInternal = new Dictionary<
            string,
            string
        >
        {
            ["그라운드 제로(Ground Zero)"] = "sandbox_high_preset",
            ["팩토리/공장(Factory)"] = "factory_day_preset",
            ["커스텀/세관(Customs)"] = "customs_preset",
            ["우드/삼림(Woods)"] = "woods_preset",
            ["쇼어라인/해안선(Shoreline)"] = "shoreline_preset",
            ["인터체인지/교차로/울트라(Interchange)"] = "shopping_mall",
            ["리저브/군사기지/밀베(Reserve)"] = "rezerv_base_preset",
            ["랩/연구소(The Lab)"] = "laboratory_preset",
            ["라이트하우스/등대(Lighthouse)"] = "lighthouse_preset",
            ["스트리트 오브 타르코프(Streets of Tarkov)"] = "city_preset",
        };

        // 게임 내부 이름 -> 한국 명칭(영어) 역방향 매핑
        private readonly Dictionary<string, string> _mapInternalToDisplay;

        private readonly string[] _mapDisplayNames;

        public SettingsPage()
        {
            InitializeComponent();

            // 역방향 매핑 딕셔너리 초기화
            _mapInternalToDisplay = _mapDisplayToInternal.ToDictionary(
                kvp => kvp.Value,
                kvp => kvp.Key
            );

            // 디스플레이 이름 배열 초기화
            _mapDisplayNames = _mapDisplayToInternal.Keys.ToArray();
            LoadSettings();
            CreateMapSettingsUI();
            UpdateMapSettingsState(); // 초기 상태 설정
            UpdateHotkeySettingsState(); // 핫키 설정 상태 설정
        }

        /// <summary>
        /// 현재 설정을 UI에 로드
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                var settings = Env.GetSettings();

                // 전역 PiP 설정
                GlobalPipEnabledCheckBox.IsChecked = settings.pipEnabled;

                // PiP 위치 기억 설정
                PipRememberPositionCheckBox.IsChecked = settings.pipRememberPosition;

                // PiP 핫키 설정
                PipHotkeyEnabledCheckBox.IsChecked = settings.pipHotkeyEnabled;
                PipHotkeyButton.Content = settings.pipHotkeyKey;

                // 파일 자동 정리 설정
                AutoDeleteLogsCheckBox.IsChecked = settings.autoDeleteLogs;
                AutoDeleteScreenshotsCheckBox.IsChecked = settings.autoDeleteScreenshots;
            }
            catch (Exception) { }
        }

        /// <summary>
        /// 맵별 설정 UI 동적 생성
        /// </summary>
        private void CreateMapSettingsUI()
        {
            try
            {
                var settings = Env.GetSettings();
                MapSettingsPanel.Children.Clear();

                foreach (string mapDisplayName in _mapDisplayNames)
                {
                    // 맵별 설정 패널 생성
                    var mapPanel = new StackPanel
                    {
                        Orientation = System.Windows.Controls.Orientation.Horizontal,
                        Margin = new Thickness(0, 5, 0, 5),
                    };

                    // PiP 활성화 체크박스 (체크박스가 먼저)
                    var enabledCheckBox = new System.Windows.Controls.CheckBox
                    {
                        Content = mapDisplayName,
                        Foreground = new SolidColorBrush(Colors.White),
                        FontSize = 14,
                        VerticalAlignment = VerticalAlignment.Center,
                        Tag = mapDisplayName,
                    };

                    // 현재 설정 값 적용 (내부 이름으로 검색)
                    string mapInternalName = _mapDisplayToInternal[mapDisplayName];
                    if (
                        settings.mapSettings != null
                        && settings.mapSettings.ContainsKey(mapInternalName)
                    )
                    {
                        enabledCheckBox.IsChecked = settings.mapSettings[mapInternalName].enabled;
                    }
                    else
                    {
                        enabledCheckBox.IsChecked = true; // 기본값
                    }

                    // 체크박스 이벤트 핸들러
                    enabledCheckBox.Checked += MapEnabled_Changed;
                    enabledCheckBox.Unchecked += MapEnabled_Changed;

                    // 패널에 컨트롤 추가 (체크박스만)
                    mapPanel.Children.Add(enabledCheckBox);

                    MapSettingsPanel.Children.Add(mapPanel);
                }
            }
            catch (Exception) { }
        }

        /// <summary>
        /// 전역 PiP 활성화 설정 변경
        /// </summary>
        private void GlobalPipEnabled_Changed(object sender, RoutedEventArgs e)
        {
            // 맵별 설정의 활성화/비활성화 상태 업데이트
            UpdateMapSettingsState();
        }

        /// <summary>
        /// 맵별 활성화 설정 변경
        /// </summary>
        private void MapEnabled_Changed(object sender, RoutedEventArgs e)
        {
            // 실시간 저장은 하지 않고 Save 버튼으로만 저장
        }

        /// <summary>
        /// PiP 핫키 활성화 설정 변경
        /// </summary>
        private void PipHotkeyEnabled_Changed(object sender, RoutedEventArgs e)
        {
            UpdateHotkeySettingsState();
        }

        /// <summary>
        /// 맵별 설정 및 종속 설정들의 활성화/비활성화 상태 업데이트
        /// </summary>
        private void UpdateMapSettingsState()
        {
            try
            {
                bool isPipEnabled = GlobalPipEnabledCheckBox.IsChecked ?? false;

                // 1. 종속 설정들 활성화/비활성화
                // PiP 위치 기억 설정
                PipRememberPositionCheckBox.IsEnabled = isPipEnabled;
                PipRememberPositionCheckBox.Opacity = isPipEnabled ? 1.0 : 0.5;

                // PiP 핫키 활성화 설정
                PipHotkeyEnabledCheckBox.IsEnabled = isPipEnabled;
                PipHotkeyEnabledCheckBox.Opacity = isPipEnabled ? 1.0 : 0.5;

                // PiP가 비활성화되면 핫키 설정도 함께 비활성화
                if (!isPipEnabled)
                {
                    UpdateHotkeySettingsState(); // 핫키 관련 UI 업데이트
                }
                else
                {
                    // PiP가 활성화되면 핫키 설정 상태에 따라 업데이트
                    UpdateHotkeySettingsState();
                }

                // 2. 맵별 설정 패널의 모든 컨트롤 상태 변경
                foreach (StackPanel mapPanel in MapSettingsPanel.Children.OfType<StackPanel>())
                {
                    // 체크박스 찾기
                    var checkBox = mapPanel
                        .Children.OfType<System.Windows.Controls.CheckBox>()
                        .FirstOrDefault();

                    // 체크박스 활성화/비활성화
                    if (checkBox != null)
                    {
                        checkBox.IsEnabled = isPipEnabled;
                        checkBox.Opacity = isPipEnabled ? 1.0 : 0.5;
                    }
                }
            }
            catch (Exception) { }
        }

        /// <summary>
        /// 저장 버튼 클릭
        /// </summary>
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = Env.GetSettings();

                // 전역 PiP 설정 저장
                settings.pipEnabled = GlobalPipEnabledCheckBox.IsChecked ?? true;
                settings.pipRememberPosition = PipRememberPositionCheckBox.IsChecked ?? true;
                settings.pipHotkeyEnabled = PipHotkeyEnabledCheckBox.IsChecked ?? false;
                settings.pipHotkeyKey = PipHotkeyButton.Content?.ToString()?.Trim() ?? "F11";

                // 맵별 설정 저장
                if (settings.mapSettings == null)
                {
                    settings.mapSettings = new Dictionary<string, MapSetting>();
                }

                // 각 맵별 설정 저장
                foreach (StackPanel mapPanel in MapSettingsPanel.Children.OfType<StackPanel>())
                {
                    var checkBox = mapPanel
                        .Children.OfType<System.Windows.Controls.CheckBox>()
                        .FirstOrDefault();

                    if (checkBox != null)
                    {
                        string mapDisplayName = checkBox.Tag?.ToString();
                        if (
                            !string.IsNullOrEmpty(mapDisplayName)
                            && _mapDisplayToInternal.ContainsKey(mapDisplayName)
                        )
                        {
                            // 디스플레이 이름을 내부 이름으로 변환
                            string mapInternalName = _mapDisplayToInternal[mapDisplayName];

                            if (!settings.mapSettings.ContainsKey(mapInternalName))
                            {
                                settings.mapSettings[mapInternalName] = new MapSetting();
                            }

                            var mapSetting = settings.mapSettings[mapInternalName];
                            mapSetting.enabled = checkBox.IsChecked ?? true;
                        }
                    }
                }

                // 파일 자동 정리 설정 저장
                settings.autoDeleteLogs = AutoDeleteLogsCheckBox.IsChecked ?? false;
                settings.autoDeleteScreenshots = AutoDeleteScreenshotsCheckBox.IsChecked ?? false;

                // 설정 저장
                Env.SetSettings(settings);
                Settings.Save();

                // 핫키 설정이 변경된 경우 MainWindow에서 핫키 재등록
                if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.UpdateHotkeySettings();
                }

                System.Windows.MessageBox.Show(
                    "설정이 저장되었습니다.",
                    "설정 저장",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"설정 저장 중 오류가 발생했습니다: {ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        /// <summary>
        /// 핫키 설정 활성화/비활성화 상태 업데이트
        /// </summary>
        private void UpdateHotkeySettingsState()
        {
            try
            {
                bool isPipEnabled = GlobalPipEnabledCheckBox.IsChecked ?? false;
                bool isHotkeyEnabled =
                    (PipHotkeyEnabledCheckBox.IsChecked ?? false) && isPipEnabled;

                // 핫키 버튼 활성화 상태 설정
                PipHotkeyButton.IsEnabled = isHotkeyEnabled;
                PipHotkeyButton.Opacity = isHotkeyEnabled ? 1.0 : 0.5;

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
        private void PipHotkeyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PipHotkeyButton.Content = "키를 눌러주세요...";
                PipHotkeyButton.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x6A, 0x6A, 0x2A)
                ); // 노란빛

                // 포커스 설정 및 확인
                bool focusResult = PipHotkeyButton.Focus();

                _isHotkeyInputMode = true;
            }
            catch (Exception) { }
        }

        /// <summary>
        /// 핫키 버튼 포커스 해제 시 (입력 모드 종료)
        /// </summary>
        private void PipHotkeyButton_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                // 입력 모드 종료
                _isHotkeyInputMode = false;

                // 원래 상태로 복원
                PipHotkeyButton.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x3A, 0x3A, 0x3A)
                );

                // 빈 값이거나 안내 텍스트인 경우 기본값으로 설정
                if (
                    PipHotkeyButton.Content?.ToString() == "키를 눌러주세요..."
                    || string.IsNullOrWhiteSpace(PipHotkeyButton.Content?.ToString())
                )
                {
                    PipHotkeyButton.Content = "F11";
                }
            }
            catch (Exception) { }
        }

        /// <summary>
        /// 핫키 버튼 PreviewKeyDown 이벤트 (모든 키 캐치)
        /// </summary>
        private void PipHotkeyButton_PreviewKeyDown(
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
                    PipHotkeyButton.Content = keyString;

                    // 입력 모드 종료 및 포커스 해제
                    _isHotkeyInputMode = false;
                    PipHotkeyButton.Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x3A, 0x3A, 0x3A)
                    );
                    PipHotkeyButton.MoveFocus(
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
        /// 핫키 버튼 KeyDown 이벤트 (실제 키 처리)
        /// </summary>
        private void PipHotkeyButton_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
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

                string keyString = GetKeyString(e.Key, e.KeyboardDevice.Modifiers);

                if (!string.IsNullOrEmpty(keyString))
                {
                    PipHotkeyButton.Content = keyString;

                    // 키 입력 후 포커스 해제하여 입력 모드 종료
                    PipHotkeyButton.MoveFocus(
                        new System.Windows.Input.TraversalRequest(
                            System.Windows.Input.FocusNavigationDirection.Next
                        )
                    );
                }

                e.Handled = true;
            }
            catch (Exception) { }
        }

        /// <summary>
        /// 키와 모디파이어를 문자열로 변환
        /// </summary>
        private string GetKeyString(
            System.Windows.Input.Key key,
            System.Windows.Input.ModifierKeys modifiers
        )
        {
            try
            {
                var keyParts = new List<string>();

                // 모디파이어 키 추가
                if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
                    keyParts.Add("Ctrl");
                if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt))
                    keyParts.Add("Alt");
                if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
                    keyParts.Add("Shift");
                if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Windows))
                    keyParts.Add("Win");

                // 메인 키 추가
                string mainKey = GetMainKeyString(key);
                if (!string.IsNullOrEmpty(mainKey))
                {
                    keyParts.Add(mainKey);
                    return string.Join("+", keyParts);
                }

                return string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 메인 키를 문자열로 변환
        /// </summary>
        private string GetMainKeyString(System.Windows.Input.Key key)
        {
            // F키
            if (key >= System.Windows.Input.Key.F1 && key <= System.Windows.Input.Key.F12)
                return key.ToString();

            // 숫자 키
            if (key >= System.Windows.Input.Key.D0 && key <= System.Windows.Input.Key.D9)
                return key.ToString().Replace("D", "");

            // 알파벳 키
            if (key >= System.Windows.Input.Key.A && key <= System.Windows.Input.Key.Z)
                return key.ToString();

            // 기타 특수 키들
            switch (key)
            {
                case System.Windows.Input.Key.Space:
                    return "Space";
                case System.Windows.Input.Key.Enter:
                    return "Enter";
                case System.Windows.Input.Key.Escape:
                    return "Esc";
                case System.Windows.Input.Key.Back:
                    return "Backspace";
                case System.Windows.Input.Key.Delete:
                    return "Delete";
                case System.Windows.Input.Key.Home:
                    return "Home";
                case System.Windows.Input.Key.End:
                    return "End";
                case System.Windows.Input.Key.PageUp:
                    return "PageUp";
                case System.Windows.Input.Key.PageDown:
                    return "PageDown";
                case System.Windows.Input.Key.Up:
                    return "Up";
                case System.Windows.Input.Key.Down:
                    return "Down";
                case System.Windows.Input.Key.Left:
                    return "Left";
                case System.Windows.Input.Key.Right:
                    return "Right";
                case System.Windows.Input.Key.Insert:
                    return "Insert";
                default:
                    return string.Empty;
            }
        }
    }
}
