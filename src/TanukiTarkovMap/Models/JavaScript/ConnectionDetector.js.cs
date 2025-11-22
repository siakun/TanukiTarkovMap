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
        /// 실행 절차:
        /// 1. .pilot-status, [class*="status"], [class*="pilot"] 선택자로 상태 요소들 검색
        /// 2. 각 요소의 텍스트가 'Status'와 'Connected'를 포함하거나 .connected 클래스를 가지는지 확인
        /// 3. 연결 감지 시 C#에 'pilot-connected' 메시지 전송 (한 번만)
        /// 4. MutationObserver로 DOM 변경 감시하여 지속적으로 확인
        /// 5. 1초 후 초기 체크 실행
        /// </summary>
        public const string DETECT_CONNECTION_STATUS =
            @"
                (function() {
                    'use strict';

                    let connectionDetected = false;

                    function checkConnectionStatus() {
                        try {
                            // Find all elements containing 'Status'
                            const statusElements = document.querySelectorAll('.pilot-status, [class*=""status""], [class*=""pilot""]');

                            for (const element of statusElements) {
                                const text = element.textContent || '';

                                // Check if the element contains 'Connected' and the status circle
                                if ((text.includes('Status') && text.includes('Connected')) ||
                                    (element.classList.contains('connected'))) {

                                    if (!connectionDetected) {
                                        connectionDetected = true;

                                        // Send message to C#
                                        try {
                                            window.chrome.webview.postMessage(JSON.stringify({
                                                type: 'pilot-connected'
                                            }));

                                            console.log('[Connection Detection] Pilot connected!');
                                        } catch (e) {
                                            console.error('[Connection Detection] Failed to send message:', e);
                                        }
                                    }

                                    return true;
                                }
                            }

                            return false;
                        } catch (e) {
                            console.error('[Connection Detection] Error:', e);
                            return false;
                        }
                    }

                    // Initial check
                    setTimeout(function() {
                        checkConnectionStatus();
                    }, 1000);

                    // Watch for DOM changes
                    const observer = new MutationObserver(function(mutations) {
                        if (!connectionDetected) {
                            checkConnectionStatus();
                        }
                    });

                    // Start observing
                    observer.observe(document.body, {
                        childList: true,
                        subtree: true,
                        characterData: true
                    });

                    console.log('[Connection Detection] Monitoring started');
                })();
            ";
    }
}
