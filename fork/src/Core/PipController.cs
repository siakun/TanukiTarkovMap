using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using TarkovClient.Constants;
using TarkovClient.Utils;

namespace TarkovClient
{
    public class PipController
    {
        private static PipController _instance;
        public static PipController Instance => _instance;

        private MainWindow _mainWindow;
        private bool _isActive = false;
        private string _currentMap = null;

        // 최상단 유지를 위한 타이머
        private DispatcherTimer _topmostTimer;
        private readonly object _timerLock = new object();

        // 자동 복원 기능을 위한 상태 추적
        private bool _elementsHidden = false;
        private double _lastKnownWidth = 0;
        private double _lastKnownHeight = 0;

        // PiP 상태 추적을 위한 public 속성
        public bool IsActive => _isActive;

        // 현재 맵 정보를 외부에서 접근할 수 있도록 하는 메서드
        public string GetCurrentMap() => _currentMap;

        /// <summary>
        /// PiP 모드를 토글합니다 (활성화되어 있으면 비활성화, 비활성화되어 있으면 활성화)
        /// </summary>
        public void TogglePip()
        {
            if (_isActive)
            {
                HidePip();
            }
            else
            {
                ShowPip();
            }
        }

        /// <summary>
        /// PiP 창의 위치를 관리합니다 (최상단 ↔ 최소화/백그라운드 토글)
        /// PiP 모드가 활성화된 상태에서만 동작
        /// </summary>
        public void TogglePipWindowPosition()
        {
            if (!_isActive)
            {
                return;
            }

            try
            {
                if (_mainWindow.WindowState == System.Windows.WindowState.Minimized)
                {
                    // 최소화 상태 → 복원 + 최상단 (포커스 변경 없이)
                    {
                        Utils.WindowTopmost.SetTopmost(_mainWindow);
                    }
                }
                else
                {
                    // 최상단 상태 → 최소화
                    Utils.WindowTopmost.RemoveTopmost(_mainWindow);
                    _mainWindow.WindowState = System.Windows.WindowState.Minimized;
                }
            }
            catch { }
        }

        public PipController(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            _instance = this;
        }

        // 맵 변경 시 호출 (1차 트리거)
        public void OnMapChanged(string mapName)
        {
            _currentMap = mapName;

            // PiP 기능이 활성화되어 있을 때만 ShowPip 호출
            var settings = Env.GetSettings();
            if (settings.pipEnabled)
            {
                var mapSetting = GetMapSetting(settings, _currentMap);
                if (mapSetting.enabled)
                {
                    ShowPip();
                }
                else { }
            }
            else { }
        }

        // 스크린샷 생성 시 호출 (2차 트리거)
        public void OnScreenshotTaken()
        {
            if (_isActive)
            {
                return;
            }

            ShowPip();
        }

        // PiP 모드 표시 (메인윈도우 크기 조절 방식)
        public async void ShowPip()
        {
            // 설정 정보 가져오기
            var settings = Env.GetSettings();

            try
            {
                // 처음 활성화하는 경우에만 일반 모드 설정 저장 및 초기화
                if (!_isActive)
                {
                    SaveNormalModeSettings();

                    _isActive = true;

                    // 창 크기 변경 이벤트 핸들러 등록
                    RegisterSizeChangedHandler();
                }

                // 첫 번째 탭으로 자동 전환
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        var tabContainer =
                            _mainWindow.FindName("TabContainer")
                            as System.Windows.Controls.TabControl;
                        if (tabContainer != null && tabContainer.Items.Count > 0)
                        {
                            tabContainer.SelectedIndex = 0;
                        }
                        else { }
                    }
                    catch { }
                });

                await Task.Delay(500);

                // 웹 요소 제거 및 지도 스케일링 (전체 화면에서) - UI 스레드에서 실행
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await ExecuteElementRemovalAndMapScaling();
                });

                await Task.Delay(500);

                // 초기 상태 설정 (요소들이 숨김 상태로 시작)
                _elementsHidden = true;

                await ApplyPipModeSettings();

                ApplyPipWindowSettings();
            }
            catch (Exception)
            {
                _isActive = false;
            }
        }

        // PiP 모드 해제 (메인윈도우 일반 모드 복원)
        public void HidePip()
        {
            if (!_isActive)
                return;

            try
            {
                _isActive = false;

                // 창 크기 변경 이벤트 핸들러 제거
                UnregisterSizeChangedHandler();

                // 상태 초기화
                _elementsHidden = false;
                _lastKnownWidth = 0;
                _lastKnownHeight = 0;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // 1. 일반 모드 크기 및 위치 복원
                    var settings = Env.GetSettings();
                    _mainWindow.Width = settings.normalWidth;
                    _mainWindow.Height = settings.normalHeight;

                    if (settings.normalLeft >= 0 && settings.normalTop >= 0)
                    {
                        _mainWindow.Left = settings.normalLeft;
                        _mainWindow.Top = settings.normalTop;
                    }
                    else
                    {
                        _mainWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    }

                    // 2. 최소 크기 제한 복원
                    _mainWindow.MinWidth = 1000;
                    _mainWindow.MinHeight = 700;

                    // 3. 타이틀바 및 리사이즈 모드 복원
                    _mainWindow.WindowStyle = WindowStyle.SingleBorderWindow;
                    _mainWindow.ResizeMode = ResizeMode.CanResize;

                    // 4. 최상위 해제 (WPF + Win32 API)
                    _mainWindow.Topmost = false;
                    WindowTopmost.RemoveTopmost(_mainWindow);
                });

                // JavaScript로 제거된 요소들 복원
                RestorePipJavaScriptActions();

                // PiP 모드 UI 해제
                RestoreNormalModeSettings();
            }
            catch (Exception) { }
        }

        // 맵 컨텐츠 업데이트
        public void UpdateMapContent(string mapName)
        {
            _currentMap = mapName;
            // WebView2에서 맵이 자동으로 동기화됨 (WebSocket 통신)
        }

        /// <summary>
        /// 창 크기 변경 이벤트 핸들러 등록
        /// </summary>
        private void RegisterSizeChangedHandler()
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _mainWindow.SizeChanged += OnWindowSizeChanged;
                });
            }
            catch (Exception) { }
        }

        // 웹 요소 제거 및 지도 스케일링
        private async Task ExecuteElementRemovalAndMapScaling()
        {
            try
            {
                // 현재 활성 탭의 WebView2 가져오기 (UI 스레드에서 실행)
                Microsoft.Web.WebView2.Wpf.WebView2 activeWebView = null;
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    activeWebView = GetActiveWebView();
                });

                if (activeWebView?.CoreWebView2 == null)
                {
                    return;
                }

                // 0. 기존 PiP 오버레이 제거 (맵 변경 시 중복 방지)
                await activeWebView.CoreWebView2.ExecuteScriptAsync(
                    JavaScriptConstants.REMOVE_PIP_OVERLAY_SCRIPT
                );

                // 1. 지도 스케일링 (#map) - 맵별 설정 적용
                var settings = Env.GetSettings();
                string transformMatrix = GetMapTransform(settings, _currentMap);

                await activeWebView.CoreWebView2.ExecuteScriptAsync(
                    $@"
                    try {{
                        var mapElement = document.querySelector('#map');
                        if (mapElement) {{
                            mapElement.style.transformOrigin = '0px 0px 0px';
                            mapElement.style.transform = '{transformMatrix}';
                        }}
                    }} catch {{
                    }}
                "
                );

                // 우측 판넬 제거
                await activeWebView.CoreWebView2.ExecuteScriptAsync(
                    JavaScriptConstants.REMOVE_TARKOV_MARGET_ELEMENT_PANNEL_RIGHT
                );

                // 좌측 판넬 제거
                await activeWebView.CoreWebView2.ExecuteScriptAsync(
                    JavaScriptConstants.REMOVE_TARKOV_MARGET_ELEMENT_PANNEL_LEFT
                );

                // 3. 특정 요소 숨김 (panel_top)
                await activeWebView.CoreWebView2.ExecuteScriptAsync(
                    JavaScriptConstants.REMOVE_TARKOV_MARGET_ELEMENT_PANNEL_TOP
                );

                // 4. header 요소 숨김 처리 (display: none)
                await activeWebView.CoreWebView2.ExecuteScriptAsync(
                    JavaScriptConstants.REMOVE_TARKOV_MARGET_ELEMENT_HEADER
                );

                // 5. footer-wrap 요소 숨김
                await activeWebView.CoreWebView2.ExecuteScriptAsync(
                    JavaScriptConstants.REMOVE_TARKOV_MARGET_ELEMENT_FOOTER
                );
            }
            catch (Exception) { }
        }

        // PiP 모드 UI 설정 적용
        private async Task ApplyPipModeSettings()
        {
            try
            {
                var settings = Env.GetSettings();

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    // 현재 활성 WebView2 가져오기
                    var activeWebView = GetActiveWebView();

                    // 탭 사이드바 숨김 처리 및 TabControl 확장
                    var tabSidebar =
                        _mainWindow.FindName("TabSidebar") as System.Windows.Controls.Border;
                    var tabContainer =
                        _mainWindow.FindName("TabContainer") as System.Windows.Controls.TabControl;

                    if (tabSidebar != null)
                    {
                        tabSidebar.Visibility = Visibility.Collapsed;
                    }

                    // TabContainer를 전체 너비로 확장 및 헤더 영역 숨김
                    if (tabContainer != null)
                    {
                        System.Windows.Controls.Grid.SetColumn(tabContainer, 0);
                        System.Windows.Controls.Grid.SetColumnSpan(tabContainer, 2);

                        // 헤더 영역을 화면 위로 밀어서 숨김 (헤더 높이 약 30px)
                        tabContainer.Margin = new System.Windows.Thickness(0, -30, 0, 0);

                        // PiP 모드에서 Z-Index를 더 낮게 설정 (호버 영역이 위에 오도록)
                        System.Windows.Controls.Panel.SetZIndex(tabContainer, 10);
                    }

                    // TabContainer의 Z-Index를 낮춤
                    if (tabContainer != null)
                    {
                        System.Windows.Controls.Panel.SetZIndex(tabContainer, 50);
                    }

                    // JavaScript 기반 PiP 오버레이 생성 (검증 및 재시도 포함)
                    if (activeWebView != null && activeWebView.CoreWebView2 != null)
                    {
                        // 오버레이 생성
                        await activeWebView.CoreWebView2.ExecuteScriptAsync(
                            JavaScriptConstants.CREATE_PIP_OVERLAY_SCRIPT
                        );

                        await Task.Delay(500);

                        var verificationResult =
                            await activeWebView.CoreWebView2.ExecuteScriptAsync(
                                @"
                            (function() {
                                const overlay = document.getElementById('tarkov-client-pip-overlay');
                                const controlBar = document.getElementById('pip-control-bar');
                                const webArea = document.getElementById('pip-web-area');
                                
                                return JSON.stringify({
                                    overlayExists: !!overlay,
                                    controlBarExists: !!controlBar,
                                    webAreaExists: !!webArea,
                                    overlayVisible: overlay ? overlay.style.display !== 'none' : false
                                });
                            })()
                            "
                            );

                        // 검증 실패 시 재시도
                        var verification = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(
                            verificationResult
                        );
                        if (
                            !verification.overlayExists
                            || !verification.controlBarExists
                            || !verification.webAreaExists
                        )
                        {
                            // 기존 오버레이 완전 제거
                            await activeWebView.CoreWebView2.ExecuteScriptAsync(
                                JavaScriptConstants.REMOVE_PIP_OVERLAY_SCRIPT
                            );

                            await Task.Delay(200);

                            // 재생성
                            await activeWebView.CoreWebView2.ExecuteScriptAsync(
                                JavaScriptConstants.CREATE_PIP_OVERLAY_SCRIPT
                            );
                        }
                    }
                });
            }
            catch (Exception) { }
        }

        // PiP 모드 창 크기/위치 설정 적용
        private void ApplyPipWindowSettings()
        {
            try
            {
                var settings = Env.GetSettings();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // 1. 최소 크기 제한 임시 해제
                    _mainWindow.MinWidth = 200;
                    _mainWindow.MinHeight = 150;

                    // 2. 타이틀바 제거 및 리사이즈 모드 설정
                    _mainWindow.WindowStyle = WindowStyle.None;
                    _mainWindow.ResizeMode = ResizeMode.CanResize;

                    // 3. PiP 크기 및 위치 설정 - 맵별 설정 적용
                    var mapSetting = GetMapSetting(settings, _currentMap);
                    _mainWindow.Width = mapSetting.width;
                    _mainWindow.Height = mapSetting.height;

                    // 4. 위치 설정 - 맵별 설정 적용
                    if (mapSetting.left >= 0 && mapSetting.top >= 0)
                    {
                        _mainWindow.Left = mapSetting.left;
                        _mainWindow.Top = mapSetting.top;
                    }
                    else
                    {
                        // 기본 위치: 화면 우하단
                        _mainWindow.Left =
                            SystemParameters.PrimaryScreenWidth - mapSetting.width - 0;
                        _mainWindow.Top =
                            SystemParameters.PrimaryScreenHeight - mapSetting.height - 80;
                    }

                    // 5. 최상단 설정 적용 (핫키와 동일한 방식으로 통일)
                    bool topmostResult = WindowTopmost.SetTopmost(_mainWindow);
                });
            }
            catch (Exception) { }
        }

        // PiP 모드 해제 시 JavaScript로 제거된 요소들 복원
        private async void RestorePipJavaScriptActions()
        {
            try
            {
                // 현재 활성 탭의 WebView2 가져오기 (UI 스레드에서 실행)
                Microsoft.Web.WebView2.Wpf.WebView2 activeWebView = null;
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    activeWebView = GetActiveWebView();
                });

                if (activeWebView?.CoreWebView2 == null)
                {
                    return;
                }

                // Tarkov Market(Tarkov Pilot 제거 요소 복원)
                await activeWebView.CoreWebView2.ExecuteScriptAsync(
                    JavaScriptConstants.TARKOV_MARGET_ELEMENT_RESTORE
                );
            }
            catch (Exception) { }
        }

        // 현재 활성 탭의 WebView2 가져오기
        private Microsoft.Web.WebView2.Wpf.WebView2 GetActiveWebView()
        {
            try
            {
                var mainWindow = _mainWindow as MainWindow;
                if (
                    mainWindow?.TabContainer?.SelectedItem
                    is System.Windows.Controls.TabItem selectedTab
                )
                {
                    // MainWindow의 _tabWebViews 딕셔너리에서 WebView2 가져오기
                    var webViewField = typeof(MainWindow).GetField(
                        "_tabWebViews",
                        System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.Instance
                    );

                    if (
                        webViewField?.GetValue(mainWindow)
                        is System.Collections.Generic.Dictionary<
                            System.Windows.Controls.TabItem,
                            Microsoft.Web.WebView2.Wpf.WebView2
                        > tabWebViews
                    )
                    {
                        if (tabWebViews.TryGetValue(selectedTab, out var webView))
                        {
                            return webView;
                        }
                    }
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // 일반 모드 설정 저장
        private void SaveNormalModeSettings()
        {
            try
            {
                var settings = Env.GetSettings();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    settings.normalLeft = _mainWindow.Left;
                    settings.normalTop = _mainWindow.Top;
                    settings.normalWidth = _mainWindow.Width;
                    settings.normalHeight = _mainWindow.Height;

                    Env.SetSettings(settings);
                    Settings.Save();
                });
            }
            catch (Exception) { }
        }

        // 일반 모드 UI 설정 복원
        private async void RestoreNormalModeSettings()
        {
            try
            {
                // JavaScript PiP 오버레이 제거 (UI 스레드 외부에서 실행)
                var activeWebView = GetActiveWebView();
                if (activeWebView != null && activeWebView.CoreWebView2 != null)
                {
                    await activeWebView.CoreWebView2.ExecuteScriptAsync(
                        JavaScriptConstants.REMOVE_PIP_OVERLAY_SCRIPT
                    );
                }

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // 탭 사이드바 복원 및 TabControl 원래 위치로 복원
                    var tabSidebar =
                        _mainWindow.FindName("TabSidebar") as System.Windows.Controls.Border;
                    var tabContainer =
                        _mainWindow.FindName("TabContainer") as System.Windows.Controls.TabControl;

                    if (tabSidebar != null)
                    {
                        tabSidebar.Visibility = Visibility.Visible;
                    }

                    // TabContainer를 원래 위치로 복원 및 헤더 영역 복원
                    if (tabContainer != null)
                    {
                        System.Windows.Controls.Grid.SetColumn(tabContainer, 1);
                        System.Windows.Controls.Grid.SetColumnSpan(tabContainer, 1);

                        // 헤더 영역 복원 (Margin 초기화)
                        tabContainer.Margin = new System.Windows.Thickness(0, 0, 0, 0);

                        // Z-Index를 원래대로 복원
                        System.Windows.Controls.Panel.SetZIndex(tabContainer, 100);
                    }
                });
            }
            catch (Exception) { }
        }

        // 맵별 Transform 값 가져오기
        private string GetMapTransform(AppSettings settings, string mapName)
        {
            if (string.IsNullOrEmpty(mapName))
            {
                return MapTransformCalculator.CalculateFactoryMapTransform(300, 250);
            }

            // 맵별 설정에서 transform 값 확인
            if (settings.mapSettings != null && settings.mapSettings.ContainsKey(mapName))
            {
                var mapSetting = settings.mapSettings[mapName];
                if (!string.IsNullOrEmpty(mapSetting.transform))
                {
                    return mapSetting.transform;
                }
            }

            // 저장된 transform이 없으면 기본값 사용 (현재는 Factory 계산식)
            return MapTransformCalculator.CalculateFactoryMapTransform(300, 250);
        }

        // 맵별 설정 가져오기
        private MapSetting GetMapSetting(AppSettings settings, string mapName)
        {
            if (string.IsNullOrEmpty(mapName))
            {
                return new MapSetting();
            }

            // 맵별 설정 확인
            if (settings.mapSettings != null && settings.mapSettings.ContainsKey(mapName))
            {
                return settings.mapSettings[mapName];
            }

            // 저장된 설정이 없으면 기본값으로 새로 생성하고 저장
            var defaultSetting = new MapSetting();
            if (settings.mapSettings == null)
            {
                settings.mapSettings = new System.Collections.Generic.Dictionary<
                    string,
                    MapSetting
                >();
            }
            settings.mapSettings[mapName] = defaultSetting;
            Env.SetSettings(settings);
            Settings.Save();

            return defaultSetting;
        }

        /// <summary>
        /// 창 크기 변경 이벤트 핸들러 제거
        /// </summary>
        private void UnregisterSizeChangedHandler()
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _mainWindow.SizeChanged -= OnWindowSizeChanged;
                });
            }
            catch (Exception) { }
        }

        /// <summary>
        /// 창 크기 변경 이벤트 핸들러
        /// </summary>
        private async void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                // PiP 모드가 아니면 처리하지 않음
                if (!_isActive)
                    return;

                var settings = Env.GetSettings();

                // 자동 복원 기능이 비활성화되어 있으면 처리하지 않음
                if (!settings.enableAutoRestore)
                    return;

                double currentWidth = e.NewSize.Width;
                double currentHeight = e.NewSize.Height;

                // 이전과 같은 크기면 처리하지 않음 (불필요한 처리 방지)
                if (
                    Math.Abs(currentWidth - _lastKnownWidth) < 1
                    && Math.Abs(currentHeight - _lastKnownHeight) < 1
                )
                    return;

                _lastKnownWidth = currentWidth;
                _lastKnownHeight = currentHeight;

                // 현재 크기가 임계값 이상인지 확인
                bool isLargeSize = IsLargeSize(currentWidth, currentHeight, settings);

                // 상태 변화가 있을 때만 JavaScript 실행
                if (isLargeSize && _elementsHidden)
                {
                    await RestoreElementsForLargeSize();
                    _elementsHidden = false;
                }
                else if (!isLargeSize && !_elementsHidden)
                {
                    await HideElementsForSmallSize();
                    _elementsHidden = true;
                }
                else { }
            }
            catch (Exception) { }
        }

        /// <summary>
        /// 현재 창 크기가 임계값 이상인지 확인
        /// </summary>
        private bool IsLargeSize(double width, double height, AppSettings settings)
        {
            return width >= settings.restoreThresholdWidth
                && height >= settings.restoreThresholdHeight;
        }

        /// <summary>
        /// 큰 크기일 때 요소들을 복원
        /// </summary>
        private async Task RestoreElementsForLargeSize()
        {
            try
            {
                // 현재 활성 탭의 WebView2 가져오기
                Microsoft.Web.WebView2.Wpf.WebView2 activeWebView = null;
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    activeWebView = GetActiveWebView();
                });

                if (activeWebView?.CoreWebView2 == null)
                {
                    return;
                }

                // JavaScript로 요소 복원
                await activeWebView.CoreWebView2.ExecuteScriptAsync(
                    JavaScriptConstants.RESTORE_ELEMENTS_FOR_LARGE_SIZE
                );
            }
            catch (Exception) { }
        }

        /// <summary>
        /// 작은 크기일 때 요소들을 숨김
        /// </summary>
        private async Task HideElementsForSmallSize()
        {
            try
            {
                // 현재 활성 탭의 WebView2 가져오기
                Microsoft.Web.WebView2.Wpf.WebView2 activeWebView = null;
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    activeWebView = GetActiveWebView();
                });

                if (activeWebView?.CoreWebView2 == null)
                {
                    return;
                }

                // JavaScript로 요소 숨김
                await activeWebView.CoreWebView2.ExecuteScriptAsync(
                    JavaScriptConstants.HIDE_ELEMENTS_FOR_SMALL_SIZE
                );
            }
            catch (Exception) { }
        }
    }
}
