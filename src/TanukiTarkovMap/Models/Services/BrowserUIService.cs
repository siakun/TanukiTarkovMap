using System.Threading.Tasks;
using CefSharp;
using CefSharp.Wpf;
using TanukiTarkovMap.Models.JavaScript;
using TanukiTarkovMap.Models.Utils;

namespace TanukiTarkovMap.Models.Services
{
    /// <summary>
    /// CefSharp ChromiumWebBrowser의 UI 요소 가시성을 제어하는 서비스
    ///
    /// 주요 기능:
    /// - tarkov-market.com 웹페이지의 UI 패널(좌측, 우측, 상단, 헤더, 푸터) 숨기기/복원
    ///
    /// 사용법: ServiceLocator.BrowserUIService (DI 싱글톤)
    /// </summary>
    public class BrowserUIService
    {
        /// <summary>
        /// DI 컨테이너 전용 생성자 - 외부에서 new 사용 금지
        /// ServiceLocator.CreateInstance()를 통해서만 생성
        /// </summary>
        internal BrowserUIService() { }

        /// <summary>
        /// ChromiumWebBrowser의 UI 요소 가시성을 설정합니다.
        /// </summary>
        /// <param name="browser">대상 ChromiumWebBrowser 컨트롤</param>
        /// <param name="mapId">현재 맵 ID (로깅용)</param>
        /// <param name="hideElements">true: UI 요소 숨김, false: UI 요소 표시</param>
        public async Task ApplyUIVisibilityAsync(ChromiumWebBrowser browser, string mapId, bool hideElements = true)
        {
            Logger.SimpleLog($"[BrowserUIService] ApplyUIVisibilityAsync called for map ID: {mapId}, hideElements: {hideElements}");

            if (browser?.IsBrowserInitialized != true)
            {
                Logger.SimpleLog("[BrowserUIService] Browser is null or not initialized, aborting");
                return;
            }

            try
            {
                // 함수들을 window 객체에 등록 (최초 1회)
                await browser.EvaluateScriptAsync(WebElementsControl.INIT_SCRIPT);

                // 헤더와 푸터는 항상 숨김
                await browser.EvaluateScriptAsync(WebElementsControl.HIDE_HEADER);
                await browser.EvaluateScriptAsync(WebElementsControl.HIDE_FOOTER);

                if (hideElements)
                {
                    // 패널들도 숨김
                    await browser.EvaluateScriptAsync(WebElementsControl.HIDE_PANEL_RIGHT);
                    await browser.EvaluateScriptAsync(WebElementsControl.HIDE_PANEL_LEFT);
                    await browser.EvaluateScriptAsync(WebElementsControl.HIDE_PANEL_TOP);
                }
                else
                {
                    // 패널들만 복원 (헤더/푸터는 숨김 유지)
                    await browser.EvaluateScriptAsync(WebElementsControl.RESTORE_UI_ELEMENTS_KEEP_TRANSFORM);
                }

                Logger.SimpleLog($"[BrowserUIService] Successfully applied UI visibility for map ID: {mapId}");
            }
            catch (System.Exception ex)
            {
                Logger.Error("[BrowserUIService] ApplyUIVisibilityAsync error", ex);
            }
        }

        /// <summary>
        /// ChromiumWebBrowser의 모든 UI 요소를 복원합니다.
        /// </summary>
        /// <param name="browser">대상 ChromiumWebBrowser 컨트롤</param>
        public async Task RestoreUIElementsAsync(ChromiumWebBrowser browser)
        {
            Logger.SimpleLog("[BrowserUIService] RestoreUIElementsAsync called");

            if (browser?.IsBrowserInitialized != true)
            {
                Logger.SimpleLog("[BrowserUIService] Browser is null or not initialized, aborting");
                return;
            }

            try
            {
                // 함수들을 window 객체에 등록 (최초 1회)
                await browser.EvaluateScriptAsync(WebElementsControl.INIT_SCRIPT);

                Logger.SimpleLog("[BrowserUIService] Restoring all UI elements");
                await browser.EvaluateScriptAsync(WebElementsControl.RESTORE_ALL_ELEMENTS);

                Logger.SimpleLog("[BrowserUIService] Successfully restored all UI elements");
            }
            catch (System.Exception ex)
            {
                Logger.Error("[BrowserUIService] RestoreUIElementsAsync error", ex);
            }
        }
    }
}
