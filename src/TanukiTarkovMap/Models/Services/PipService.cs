using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;
using TanukiTarkovMap.Models.JavaScript;
using TanukiTarkovMap.Models.Utils;

namespace TanukiTarkovMap.Models.Services
{
    public class PipService
    {
        public async Task ApplyPipModeJavaScriptAsync(object webView, string mapId, bool hideWebElements = true)
        {
            Logger.SimpleLog($"[PipService] ApplyPipModeJavaScriptAsync called for map ID: {mapId}, hideWebElements: {hideWebElements}");

            if (webView is not WebView2 webView2 || webView2.CoreWebView2 == null)
            {
                Logger.SimpleLog("[PipService] WebView2 is null or not initialized, aborting");
                return;
            }

            try
            {
                // 더 이상 PIP 오버레이를 사용하지 않으므로 제거 스크립트 실행하지 않음

                // Step 2 제거: 맵 스케일링을 적용하지 않음 (창 크기만 조절)
                // PIP 모드에서도 맵은 원본 크기를 유지하고, 창만 작아짐

                // 2. UI 요소 제거 또는 복원 (조건부)
                if (hideWebElements)
                {
                    // Logger.SimpleLog("[PipService] Step 2: Removing UI elements");

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
                    // Logger.SimpleLog("[PipService] Step 2: Restoring UI elements");

                    // UI 요소만 복원 (창 크기는 유지)
                    await webView2.CoreWebView2.ExecuteScriptAsync(
                        WebElementsControl.RESTORE_UI_ELEMENTS_KEEP_TRANSFORM
                    );
                }

                Logger.SimpleLog($"[PipService] Successfully applied PIP mode JavaScript for map ID: {mapId}");
            }
            catch (System.Exception ex)
            {
                Logger.Error("[PipService] ApplyPipModeJavaScriptAsync error", ex);
            }
        }

        public async Task RestoreNormalModeJavaScriptAsync(object webView)
        {
            Logger.SimpleLog("[PipService] RestoreNormalModeJavaScriptAsync called");

            if (webView is not WebView2 webView2 || webView2.CoreWebView2 == null)
            {
                Logger.SimpleLog("[PipService] WebView2 is null or not initialized, aborting");
                return;
            }

            try
            {
                Logger.SimpleLog("[PipService] Restoring removed UI elements");
                // Restore removed elements
                await webView2.CoreWebView2.ExecuteScriptAsync(
                    WebElementsControl.RESTORE_ALL_ELEMENTS
                );

                Logger.SimpleLog("[PipService] Successfully restored normal mode JavaScript");
            }
            catch (System.Exception ex)
            {
                Logger.Error("[PipService] RestoreNormalModeJavaScriptAsync error", ex);
            }
        }
    }
}