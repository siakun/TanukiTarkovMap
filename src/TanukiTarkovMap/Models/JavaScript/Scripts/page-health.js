/**
 * 페이지 상태 보고 스크립트
 *
 * 목적: 앱 안에서만 나타나는 렌더 실패의 원인을 앱 로그에 남긴다.
 *
 * 배경: 로컬 모드로 앱을 켠 첫 로딩에서 바닥 맵이 그려지지 않는 일이 있었다(2026-08-18).
 * 마커와 이름표는 나오는데 지형만 없는 상태였고, 사본에서 빠진 요청도 없었다. 그때 페이지가
 * 낸 오류는 사람이 여는 DevTools 창에만 남아 앱 로그에는 아무 단서가 없었다. 다시 그 상태가
 * 되면 무엇이 실패했는지 바로 알 수 있게, 페이지의 오류와 맵이 그려졌는지를 앱으로 보낸다.
 *
 * 무엇을 보내는가:
 * - page-error: 스크립트 오류, 자원 로드 실패, 처리되지 않은 거부
 * - page-health: 맵 페이지에서 바닥 맵이 그려졌는지 (로드 뒤 한 번)
 *
 * 언제 넣는가: FrameLoadStart. 페이지가 자원을 받기 전에 들어가야 로딩 중에 난 실패를 잡는다
 */

(function () {
    'use strict';

    // 같은 페이지에 두 번 들어가도 처리기를 겹쳐 달지 않는다
    if (window.__tanukiPageHealth) return;
    window.__tanukiPageHealth = true;

    // 오류가 쏟아지는 페이지에서 로그를 덮지 않도록 상한을 둔다
    var MAX_REPORTS = 12;
    var reported = 0;

    // 맵이 늦게 그려지는 맵이 있어 넉넉히 기다린 뒤 확인한다 (ms)
    var HEALTH_DELAY = 6000;

    function send(payload) {
        try {
            window.CefSharp.PostMessage(JSON.stringify(payload));
        } catch (e) {
            // 앱 밖(개발용 브라우저)에서는 CefSharp이 없다. 이때는 보고를 건너뛴다
        }
    }

    function report(kind, detail) {
        if (reported >= MAX_REPORTS) return;
        reported++;

        send({ type: 'page-error', kind: kind, detail: String(detail).slice(0, 300) });
    }

    // 자원 로드 실패는 버블링하지 않으므로 캡처 단계에서 받는다.
    // 맵 조각(js) 하나가 못 오는 경우가 여기에 잡힌다
    window.addEventListener('error', function (event) {
        var target = event.target;

        if (target && target !== window && (target.src || target.href)) {
            report('resource', target.src || target.href);
            return;
        }

        report('script', (event.message || '') + ' @ ' + (event.filename || '') + ':' + (event.lineno || 0));
    }, true);

    window.addEventListener('unhandledrejection', function (event) {
        var reason = event.reason;
        report('promise', reason && reason.message ? reason.message : reason);
    });

    // 맵 페이지에서만 확인한다. 바닥 맵은 svg.svg-map, 마커와 이름표는 svg.map-layer에 그려진다
    if (location.pathname.indexOf('/maps/') !== -1) {
        setTimeout(function () {
            send({
                type: 'page-health',
                path: location.pathname,
                baseMap: !!document.querySelector('svg.svg-map'),
                markerLayer: !!document.querySelector('svg.map-layer')
            });
        }, HEALTH_DELAY);
    }
})();
