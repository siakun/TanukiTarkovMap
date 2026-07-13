/**
 * 페이지 레이아웃 조정 스크립트
 *
 * 목적: 웹페이지의 불필요한 마진/패딩 제거
 */

(function() {
    try {
        // 스크롤바 숨기기 - 웹 앱이므로 스크롤바 불필요
        const hideScrollbarStyle = document.createElement('style');
        hideScrollbarStyle.textContent = `
            html, body {
                scrollbar-width: none !important;
                -ms-overflow-style: none !important;
            }
            html::-webkit-scrollbar,
            body::-webkit-scrollbar {
                display: none !important;
            }
        `;
        document.head.appendChild(hideScrollbarStyle);
        console.log('[Page Layout] Scrollbar hidden');

        // querySelectorAll로 모든 .wrap 클래스 요소 찾기
        const wrapElements = document.querySelectorAll('.wrap');
        // forEach로 각 요소의 스타일 변경
        wrapElements.forEach(element => {
            element.style.margin = '0';
            element.style.padding = '0';
        });

        // 맵 페이지 컨테이너(.content.maps)는 좌우 padding 15px가 있어
        // 맵을 아무리 확대해도 화면 양옆에 검은 띠가 남으므로 함께 제거한다
        const contentElements = document.querySelectorAll('.content.wide, .content.maps');
        contentElements.forEach(element => {
            element.style.margin = '0';
            element.style.padding = '0';
        });

        const pRelativeElements = document.querySelectorAll('.p-relative');
        pRelativeElements.forEach(element => {
            element.style.margin = '0';
            element.style.padding = '0';
        });

        const alertBoxElements = document.querySelectorAll('.alert-box');
        alertBoxElements.forEach(element => {
            element.style.margin = '0';
            element.style.padding = '0';
        });

        // body 마진/패딩 제거
        document.body.style.margin = '0';
        document.body.style.padding = '0';

        // 컨테이너 크기 변경을 맵 뷰어와 마커 캔버스에 반영 (미반영 시 캔버스가 이전 폭으로 남음)
        window.dispatchEvent(new Event('resize'));

        console.log('[Remove Margins] Page margins and paddings removed');

        // C#에 완료 메시지 전송 (Browser 크기 조정 트리거용)
        setTimeout(() => {
            try {
                // CefSharp.PostMessage: C#으로 메시지 전송
                CefSharp.PostMessage(JSON.stringify({
                    type: 'margins-removed'
                }));
                console.log('[Remove Margins] Sent margins-removed message to C#');
            } catch (e) {
                console.error('[Remove Margins] Failed to send message:', e);
            }
        }, 100);

    } catch (e) {
        console.error('[Remove Margins] Error:', e);
    }
})();
