namespace TanukiTarkovMap.Models.Data
{
    /// <summary>
    /// WebSocket 메시지 타입 상수
    /// 앱 ↔ 웹 간 통신에서 메시지 종류를 식별하는 데 사용
    /// </summary>
    public static class WsMessageType
    {
        public const string SEND_FILENAME = "SEND_FILENAME";        // 스크린샷 파일명 전송
        public const string POSITION_UPDATE = "POSITION_UPDATE";    // 플레이어 위치 업데이트
        public const string MAP_CHANGE = "MAP_CHANGE";              // 맵 변경
        public const string QUEST_UPDATE = "QUEST_UPDATE";          // 퀘스트 상태 변경
        public const string CONFIGURATION = "CONFIGURATION";        // 앱 설정 정보 전송
        public const string SETTINGS_UPDATE = "SETTINGS_UPDATE";    // 설정 업데이트 요청
        public const string SETTINGS_RESET = "SETTINGS_RESET";      // 설정 초기화 요청
    }
}
