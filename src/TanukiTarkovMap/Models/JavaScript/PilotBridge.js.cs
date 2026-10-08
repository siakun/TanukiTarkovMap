using System.Text.Json;

namespace TanukiTarkovMap.Models.JavaScript
{
    /// <summary>
    /// 온라인 페이지에 내 위치를 표시하고 퀘스트 사건을 넘기는 스크립트
    ///
    /// 왜 이 방식인가:
    /// 사이트의 위치 입력 경로와 내 위치 그리기는 배포 때마다 바뀌어 위치 표시를 끊어 왔다. 앱은 파일명을
    /// 직접 읽고 내 위치와 방향을 직접 그리며(MapMarkers), 사이트에서는 지도 상태의 좌표 변환만 빌린다.
    /// 층은 사이트가 고르도록 같은 좌표를 사이트 지도 상태의 playerPos에 넘긴다.
    /// 퀘스트 완료는 사이트의 Pilot 서비스로 넘긴다.
    ///
    /// 동작 원리 (WebElementsControl과 같은 방식):
    /// 1. 상태 확인에 응답하지 않으면 INIT_SCRIPT로 window.tanukiPilot 복구
    /// 2. 위치 표시는 마커가 지도에 실제로 보이는지까지 확인해 bool로 돌려준다 (LocalViewer와 같은 모양)
    /// 3. 지도 상태는 로드 시작 때 넣은 MapStateCapture의 기록에서 찾는다
    ///
    /// JavaScript 파일 위치: Models/JavaScript/Scripts/pilot-bridge.js
    /// </summary>
    public static class PilotBridge
    {
        /// <summary>
        /// 초기화 스크립트 - 페이지 로드 시 먼저 실행하여 window.tanukiPilot 등록
        /// </summary>
        public static string INIT_SCRIPT => JavaScriptLoader.Load("pilot-bridge.js");

        public const string IS_INSTALLED_SCRIPT =
            "window.tanukiPilot?.version === 7 && typeof window.tanukiPilot.showScreenshot === 'function' " +
            "&& typeof window.tanukiPilot.isRendered === 'function' && typeof window.tanukiPilot.status === 'function' " +
            "&& typeof window.tanukiPilot.hasPosition === 'function' " +
            "&& typeof window.tanukiPilot.revealPosition === 'function';";
        public const string STATUS_SCRIPT = "window.tanukiPilot?.status() ?? 'bridge-unavailable';";

        /// <summary>
        /// 스크린샷의 위치를 표시하는 호출문. 마커가 지도에 보이면 true
        /// </summary>
        public static string ShowScreenshot(string filename) =>
            $"window.tanukiPilot?.showScreenshot({ToJsString(filename)}) === true;";

        /// <summary>지도 교체나 DOM 초기화 뒤 마지막 위치가 아직 표시되는지 확인</summary>
        public static string IsRendered(string filename) =>
            $"window.tanukiPilot?.isRendered({ToJsString(filename)}) === true;";

        /// <summary>
        /// 퀘스트 완료 전달 호출문 생성
        /// </summary>
        public static string CompleteQuest(string questId) =>
            $"window.tanukiPilot && window.tanukiPilot.completeQuest({ToJsString(questId)});";

        /// <summary>
        /// 값을 JavaScript 문자열 리터럴로 감싼다.
        /// 스크린샷 파일명에는 공백, 쉼표, 괄호가 들어가므로 따옴표로 직접 감싸면 호출문이 깨진다
        /// </summary>
        private static string ToJsString(string value) => JsonSerializer.Serialize(value);
    }
}
