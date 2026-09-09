using System.Text.Json;

namespace TanukiTarkovMap.Models.JavaScript
{
    /// <summary>
    /// 페이지 안의 Pilot 서비스로 게임 사건을 넘기는 스크립트
    ///
    /// 왜 이 방식인가:
    /// 2026-08-17 Pilot v2부터 사이트가 로컬 앱의 WebSocket(포트 5123)에 접속하지 않는다.
    /// 앱은 페이지 안의 함수를 직접 호출하며, 전역 객체와 Nuxt 서비스의 차이는 JS 어댑터가 맡는다.
    ///
    /// 동작 원리 (WebElementsControl과 같은 방식):
    /// 1. 상태 확인에 응답하지 않으면 INIT_SCRIPT로 window.tanukiPilot 복구
    /// 2. 스크린샷 호출은 Promise의 결과까지 기다려 지도 반영 여부 확인
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
            "window.tanukiPilot?.version === 3 && typeof window.tanukiPilot.sendScreenshot === 'function' " +
            "&& typeof window.tanukiPilot.isReady === 'function' && typeof window.tanukiPilot.status === 'function' " +
            "&& typeof window.tanukiPilot.getMapHeading === 'function' " +
            "&& typeof window.tanukiPilot.isRendered === 'function';";
        public const string STATUS_SCRIPT = "window.tanukiPilot?.status() ?? 'bridge-unavailable';";

        /// <summary>
        /// 스크린샷 파일명 전달 호출문 생성
        /// </summary>
        public static string SendScreenshot(string filename) =>
            $"return window.tanukiPilot ? window.tanukiPilot.sendScreenshot({ToJsString(filename)}) : false;";

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
