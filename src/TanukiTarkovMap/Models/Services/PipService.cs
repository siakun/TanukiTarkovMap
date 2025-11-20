using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;
using TanukiTarkovMap.Models.Constants;
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
                // Logger.SimpleLog("[PipService] Step 1: Removing existing PIP overlay");
                // 1. Remove existing PIP overlay
                await webView2.CoreWebView2.ExecuteScriptAsync(
                    JavaScriptConstants.REMOVE_PIP_OVERLAY_SCRIPT
                );

                // Step 2 제거: 맵 스케일링을 적용하지 않음 (창 크기만 조절)
                // PIP 모드에서도 맵은 원본 크기를 유지하고, 창만 작아짐

                // 2. UI 요소 제거 또는 복원 (조건부)
                if (hideWebElements)
                {
                    // Logger.SimpleLog("[PipService] Step 2: Removing UI elements");

                    await webView2.CoreWebView2.ExecuteScriptAsync(
                        JavaScriptConstants.REMOVE_TARKOV_MARGET_ELEMENT_PANNEL_RIGHT
                    );

                    await webView2.CoreWebView2.ExecuteScriptAsync(
                        JavaScriptConstants.REMOVE_TARKOV_MARGET_ELEMENT_PANNEL_LEFT
                    );

                    await webView2.CoreWebView2.ExecuteScriptAsync(
                        JavaScriptConstants.REMOVE_TARKOV_MARGET_ELEMENT_PANNEL_TOP
                    );

                    await webView2.CoreWebView2.ExecuteScriptAsync(
                        JavaScriptConstants.REMOVE_TARKOV_MARGET_ELEMENT_HEADER
                    );

                    await webView2.CoreWebView2.ExecuteScriptAsync(
                        JavaScriptConstants.REMOVE_TARKOV_MARGET_ELEMENT_FOOTER
                    );
                }
                else
                {
                    // Logger.SimpleLog("[PipService] Step 2: Restoring UI elements");

                    // UI 요소만 복원 (PIP 모드에서는 맵 transform을 적용하지 않으므로)
                    await webView2.CoreWebView2.ExecuteScriptAsync(
                        JavaScriptConstants.TARKOV_MARGET_ELEMENT_RESTORE_KEEP_TRANSFORM
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
                    JavaScriptConstants.TARKOV_MARGET_ELEMENT_RESTORE
                );

                Logger.SimpleLog("[PipService] Successfully restored normal mode JavaScript");
            }
            catch (System.Exception ex)
            {
                Logger.Error("[PipService] RestoreNormalModeJavaScriptAsync error", ex);
            }
        }

        public string GetMapTransform(string mapId)
        {
            // Null or empty 체크
            if (string.IsNullOrEmpty(mapId))
            {
                // Logger.SimpleLog("[PipService] GetMapTransform: mapId is null or empty, using default transform");
                return "matrix(0.15, 0, 0, 0.15, -150, -150)";
            }

            var settings = App.GetSettings();

            // 설정에서 맵 ID로 조회
            if (settings.MapSettings != null &&
                settings.MapSettings.ContainsKey(mapId) &&
                !string.IsNullOrEmpty(settings.MapSettings[mapId].Transform))
            {
                return settings.MapSettings[mapId].Transform;
            }

            // 맵 ID 기반 기본 transform
            return mapId switch
            {
                "sandbox_high_preset" => "matrix(0.347614, 0, 0, 0.347614, -346.781, -365.505)",
                "factory_day_preset" => "matrix(0.166113, 0, 0, 0.166113, -165.258, -154.371)",
                "customs_preset" => "matrix(0.177979, 0, 0, 0.177979, -215.026, -185.151)",
                "shopping_mall" => "matrix(0.125141, 0, 0, 0.125141, -124.377, -127.995)",
                "woods_preset" => "matrix(0.111237, 0, 0, 0.111237, -101.331, -113.302)",
                "shoreline_preset" => "matrix(0.222473, 0, 0, 0.222473, -231.212, -228.746)",
                "rezerv_base_preset" => "matrix(0.222473, 0, 0, 0.222473, -227.365, -224.862)",
                "lighthouse_preset" => "matrix(0.241013, 0, 0, 0.241013, -258.081, -256.536)",
                "city_preset" => "matrix(0.21875, 0, 0, 0.21875, -193.814, -223.336)",
                "laboratory_preset" => "matrix(0.124512, 0, 0, 0.124512, -191.645, -129.873)",
                _ => "matrix(0.15, 0, 0, 0.15, -150, -150)"
            };
        }
    }
}