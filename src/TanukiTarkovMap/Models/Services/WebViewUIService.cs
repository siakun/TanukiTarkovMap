using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;
using TanukiTarkovMap.Models.JavaScript;
using TanukiTarkovMap.Models.Utils;

namespace TanukiTarkovMap.Models.Services
{
    /// <summary>
    /// WebView2의 UI 요소 가시성을 제어하는 서비스
    ///
    /// 주요 기능:
    /// - tarkov-market.com 웹페이지의 UI 패널(좌측, 우측, 상단, 헤더, 푸터) 숨기기/복원
    /// - Compact 모드에서 맵만 표시하도록 불필요한 UI 요소 제거
    ///
    /// 사용 위치:
    /// - MainWindow.xaml.cs (View)에서 WebView2 직접 제어 시 사용
    /// </summary>
    public class WebViewUIService
    {
        /// <summary>
        /// WebView2의 UI 요소 가시성을 설정합니다.
        /// </summary>
        /// <param name="webView">대상 WebView2 컨트롤</param>
        /// <param name="mapId">현재 맵 ID (로깅용)</param>
        /// <param name="hideElements">true: UI 요소 숨김, false: UI 요소 표시</param>
        public async Task ApplyUIVisibilityAsync(object webView, string mapId, bool hideElements = true)
        {
            Logger.SimpleLog($"[WebViewUIService] ApplyUIVisibilityAsync called for map ID: {mapId}, hideElements: {hideElements}");

            if (webView is not WebView2 webView2 || webView2.CoreWebView2 == null)
            {
                Logger.SimpleLog("[WebViewUIService] WebView2 is null or not initialized, aborting");
                return;
            }

            try
            {
                if (hideElements)
                {
                    await webView2.CoreWebView2.ExecuteScriptAsync(
                        WebElementsControl.HIDE_PANEL_RIGHT
                    );

                    await webView2.CoreWebView2.ExecuteScriptAsync(
                        WebElementsControl.HIDE_PANEL_LEFT
                    );

                    await webView2.CoreWebView2.ExecuteScriptAsync(
                        WebElementsControl.HIDE_PANEL_TOP
                    );

                    await webView2.CoreWebView2.ExecuteScriptAsync(
                        WebElementsControl.HIDE_HEADER
                    );

                    await webView2.CoreWebView2.ExecuteScriptAsync(
                        WebElementsControl.HIDE_FOOTER
                    );
                }
                else
                {
                    await webView2.CoreWebView2.ExecuteScriptAsync(
                        WebElementsControl.RESTORE_UI_ELEMENTS_KEEP_TRANSFORM
                    );
                }

                Logger.SimpleLog($"[WebViewUIService] Successfully applied UI visibility for map ID: {mapId}");
            }
            catch (System.Exception ex)
            {
                Logger.Error("[WebViewUIService] ApplyUIVisibilityAsync error", ex);
            }
        }

        /// <summary>
        /// WebView2의 모든 UI 요소를 복원합니다.
        /// </summary>
        /// <param name="webView">대상 WebView2 컨트롤</param>
        public async Task RestoreUIElementsAsync(object webView)
        {
            Logger.SimpleLog("[WebViewUIService] RestoreUIElementsAsync called");

            if (webView is not WebView2 webView2 || webView2.CoreWebView2 == null)
            {
                Logger.SimpleLog("[WebViewUIService] WebView2 is null or not initialized, aborting");
                return;
            }

            try
            {
                Logger.SimpleLog("[WebViewUIService] Restoring all UI elements");
                await webView2.CoreWebView2.ExecuteScriptAsync(
                    WebElementsControl.RESTORE_ALL_ELEMENTS
                );

                Logger.SimpleLog("[WebViewUIService] Successfully restored all UI elements");
            }
            catch (System.Exception ex)
            {
                Logger.Error("[WebViewUIService] RestoreUIElementsAsync error", ex);
            }
        }
    }
}
