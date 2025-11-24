namespace TanukiTarkovMap.Models.JavaScript
{
    /// <summary>
    /// 연결 상태 감지 관련 JavaScript 스크립트
    /// - "Status Connected" 상태 감지
    /// - C#에 연결 이벤트 알림
    /// </summary>
    public static class ConnectionDetector
    {
        /// <summary>
        /// "Status Connected" 상태를 감지하는 스크립트
        ///
        /// JavaScript 파일 위치: Models/JavaScript/Scripts/connection-detector.js
        ///
        /// 실행 절차:
        /// 1. .pilot-status, [class*="status"], [class*="pilot"] 선택자로 상태 요소들 검색
        /// 2. 각 요소의 텍스트가 'Status'와 'Connected'를 포함하거나 .connected 클래스를 가지는지 확인
        /// 3. 연결 감지 시 C#에 'pilot-connected' 메시지 전송 (한 번만)
        /// 4. MutationObserver로 DOM 변경 감시하여 지속적으로 확인
        /// 5. 1초 후 초기 체크 실행
        /// </summary>
        public static string DETECT_CONNECTION_STATUS =>
            JavaScriptLoader.Load("connection-detector.js");
    }
}
