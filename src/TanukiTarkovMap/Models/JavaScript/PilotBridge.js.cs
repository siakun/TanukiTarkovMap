using System.Text.Json;

namespace TanukiTarkovMap.Models.JavaScript
{
    /// <summary>
    /// tarkov-market.com이 열어 둔 window.pilot으로 게임 사건을 넘기는 스크립트
    ///
    /// 왜 이 방식인가:
    /// 2026-08-17 Pilot v2부터 사이트가 로컬 앱의 WebSocket(포트 5123)에 접속하지 않는다.
    /// 대신 페이지마다 window.pilot을 열어 두므로 그 함수를 직접 부른다.
    /// 사이트 서버와의 연결이나 로그인 없이도 좌표 표시는 이 경로로 동작한다.
    ///
    /// 동작 원리 (WebElementsControl과 같은 방식):
    /// 1. 페이지 로드 시 INIT_SCRIPT를 실행해 window.tanukiPilot 등록
    /// 2. 사건이 생길 때마다 SendScreenshot()/CompleteQuest()가 만든 호출문을 실행
    ///
    /// JavaScript 파일 위치: Models/JavaScript/Scripts/pilot-bridge.js
    /// </summary>
    public static class PilotBridge
    {
        /// <summary>
        /// 초기화 스크립트 - 페이지 로드 시 먼저 실행하여 window.tanukiPilot 등록
        /// </summary>
        public static string INIT_SCRIPT => JavaScriptLoader.Load("pilot-bridge.js");

        /// <summary>
        /// 스크린샷 파일명 전달 호출문 생성
        /// </summary>
        public static string SendScreenshot(string filename) =>
            $"window.tanukiPilot && window.tanukiPilot.sendScreenshot({ToJsString(filename)});";

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
