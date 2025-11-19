using System.Threading.Tasks;

namespace TanukiTarkovMap.Models.Services
{
    public interface IPipService
    {
        /// <summary>
        /// WebView에 PIP 모드를 위한 JavaScript를 실행합니다.
        /// </summary>
        Task ApplyPipModeJavaScriptAsync(object webView, string mapName);

        /// <summary>
        /// WebView에 일반 모드를 복원하는 JavaScript를 실행합니다.
        /// </summary>
        Task RestoreNormalModeJavaScriptAsync(object webView);

        /// <summary>
        /// 맵별 변환 매트릭스를 가져옵니다.
        /// </summary>
        string GetMapTransform(string mapName);
    }
}