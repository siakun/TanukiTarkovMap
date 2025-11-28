/**
 * 페이지 레이아웃 조정 스크립트
 *
 * 목적: 웹페이지의 불필요한 마진/패딩 제거
 */

(function() {
    try {
        // querySelectorAll로 모든 .wrap 클래스 요소 찾기
        const wrapElements = document.querySelectorAll('.wrap');
        // forEach로 각 요소의 스타일 변경
        wrapElements.forEach(element => {
            element.style.margin = '0';
            element.style.padding = '0';
        });

        const contentWideElements = document.querySelectorAll('.content.wide');
        contentWideElements.forEach(element => {
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
