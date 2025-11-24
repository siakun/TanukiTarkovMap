/**
 * 연결 상태 감지 스크립트
 *
 * 목적: "Status Connected" 상태를 감지하여 C#에 알림
 */

(function() {
    'use strict';

    let connectionDetected = false;  // 중복 알림 방지 플래그

    /**
     * 연결 상태 확인 함수
     *
     * @returns {boolean} 연결 감지 여부
     */
    function checkConnectionStatus() {
        try {
            // 여러 선택자로 상태 요소 찾기
            const statusElements = document.querySelectorAll('.pilot-status, [class*="status"], [class*="pilot"]');

            for (const element of statusElements) {
                const text = element.textContent || '';

                // "Status"와 "Connected" 텍스트가 모두 포함되거나
                // 'connected' 클래스가 있는지 확인
                if ((text.includes('Status') && text.includes('Connected')) ||
                    (element.classList.contains('connected'))) {

                    if (!connectionDetected) {
                        connectionDetected = true;

                        // C#에 메시지 전송
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

    // 1초 후 초기 체크
    setTimeout(function() {
        checkConnectionStatus();
    }, 1000);

    // DOM 변경 감시
    const observer = new MutationObserver(function(mutations) {
        if (!connectionDetected) {
            checkConnectionStatus();
        }
    });

    // 감시 시작
    observer.observe(document.body, {
        childList: true,       // 자식 추가/제거
        subtree: true,         // 모든 하위 요소
        characterData: true    // 텍스트 내용 변경
    });

    console.log('[Connection Detection] Monitoring started');
})();
