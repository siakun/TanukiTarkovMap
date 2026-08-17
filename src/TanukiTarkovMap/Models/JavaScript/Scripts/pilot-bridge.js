/**
 * Pilot 브리지 스크립트
 *
 * 목적: 게임에서 읽어 낸 사건을 tarkov-market.com이 열어 둔 window.pilot으로 넘긴다.
 *
 * 배경: 2026-08-17 Pilot v2부터 사이트는 로컬 앱의 WebSocket(포트 5123)에 접속하지 않고
 * 자기 서버(wss://tarkov-market.com/ws/pilot)로만 붙는다. 대신 페이지를 띄울 때마다
 * window.pilot에 positionFromScreenshot, mapChange, questComplete를 열어 두므로,
 * 앱이 이 함수를 직접 불러 예전 WebSocket 메시지와 같은 일을 시킨다.
 *
 * 좌표 파싱은 사이트가 파일명에서 직접 하므로 앱은 파일명을 그대로 넘긴다.
 * 맵 전환은 앱이 주소를 직접 바꾸므로 여기서 다루지 않는다.
 */

(function () {
    'use strict';

    /**
     * window.pilot의 함수를 안전하게 호출한다
     *
     * @param {string} methodName - 호출할 window.pilot의 함수 이름
     * @param {string} argument - 그 함수에 넘길 값
     * @returns {boolean} 호출 성공 여부
     */
    function callPilot(methodName, argument) {
        // 페이지가 아직 마운트되지 않았으면 window.pilot이 없다.
        // 이때는 실패를 알리기만 하고, 다음 사건에서 다시 시도한다
        var pilot = window.pilot;

        if (!pilot || typeof pilot[methodName] !== 'function') {
            console.warn('[Pilot Bridge] window.pilot.' + methodName + ' not available');
            return false;
        }

        try {
            pilot[methodName](argument);
            return true;
        } catch (e) {
            console.error('[Pilot Bridge] ' + methodName + ' failed:', e);
            return false;
        }
    }

    window.tanukiPilot = {
        // 스크린샷 파일명 전달 (사이트가 파일명에서 좌표와 시선 방향을 읽어 마커를 옮긴다)
        sendScreenshot: function (filename) {
            return callPilot('positionFromScreenshot', filename);
        },

        // 퀘스트 완료 전달 (사이트에서 pro 계정으로 로그인한 경우에만 반영된다)
        completeQuest: function (questId) {
            return callPilot('questComplete', questId);
        }
    };

    console.log('[Pilot Bridge] Ready');
})();
