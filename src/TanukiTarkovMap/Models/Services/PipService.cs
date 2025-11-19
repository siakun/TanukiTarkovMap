using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;
using TanukiTarkovMap.Models.Constants;
using TanukiTarkovMap.Models.Utils;

namespace TanukiTarkovMap.Models.Services
{
    public class PipService : IPipService
    {
        public async Task ApplyPipModeJavaScriptAsync(object webView, string mapName)
        {
            Logger.SimpleLog($"[PipService] ApplyPipModeJavaScriptAsync called for map: {mapName}");

            if (webView is not WebView2 webView2 || webView2.CoreWebView2 == null)
            {
                Logger.SimpleLog("[PipService] WebView2 is null or not initialized, aborting");
                return;
            }

            try
            {
                Logger.SimpleLog("[PipService] Step 1: Removing existing PIP overlay");
                // 1. Remove existing PIP overlay
                await webView2.CoreWebView2.ExecuteScriptAsync(
                    JavaScriptConstants.REMOVE_PIP_OVERLAY_SCRIPT
                );

                Logger.SimpleLog("[PipService] Step 2: Applying map scaling");
                // 2. Apply map scaling
                var transformMatrix = GetMapTransform(mapName);
                Logger.SimpleLog($"[PipService] Transform matrix: {transformMatrix}");
                await webView2.CoreWebView2.ExecuteScriptAsync(
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

                Logger.SimpleLog("[PipService] Step 3: Removing UI elements");
                // 3. Remove UI elements for PIP mode
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

                Logger.SimpleLog($"[PipService] Successfully applied PIP mode JavaScript for map: {mapName}");
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

        public string GetMapTransform(string mapName)
        {
            // Null or empty 체크
            if (string.IsNullOrEmpty(mapName))
            {
                Logger.SimpleLog("[PipService] GetMapTransform: mapName is null or empty, using default transform");
                return "matrix(0.15, 0, 0, 0.15, -150, -150)";
            }

            var settings = Env.GetSettings();

            if (settings.MapSettings != null &&
                settings.MapSettings.ContainsKey(mapName) &&
                !string.IsNullOrEmpty(settings.MapSettings[mapName].Transform))
            {
                return settings.MapSettings[mapName].Transform;
            }

            // Default transform based on map name
            return mapName switch
            {
                "factory_day_preset" => "matrix(0.166113, 0, 0, 0.166113, -165.258, -154.371)",
                "woods_preset" => "matrix(0.111237, 0, 0, 0.111237, -101.331, -113.302)",
                "customs_preset" => "matrix(0.177979, 0, 0, 0.177979, -215.026, -185.151)",
                "rezerv_base_preset" => "matrix(0.222473, 0, 0, 0.222473, -227.365, -224.862)",
                "sandbox_high_preset" => "matrix(0.347614, 0, 0, 0.347614, -346.781, -365.505)",
                "city_preset" => "matrix(0.21875, 0, 0, 0.21875, -193.814, -223.336)",
                "lighthouse_preset" => "matrix(0.241013, 0, 0, 0.241013, -258.081, -256.536)",
                "shopping_mall" => "matrix(0.125141, 0, 0, 0.125141, -124.377, -127.995)",
                "shoreline_preset" => "matrix(0.222473, 0, 0, 0.222473, -231.212, -228.746)",
                "laboratory_preset" => "matrix(0.124512, 0, 0, 0.124512, -191.645, -129.873)",
                _ => "matrix(0.15, 0, 0, 0.15, -150, -150)"
            };
        }
    }
}