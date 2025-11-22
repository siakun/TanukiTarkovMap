namespace TanukiTarkovMap.Models.JavaScript
{
    /// <summary>
    /// 페이지 레이아웃 조정 관련 JavaScript 스크립트
    /// - 웹 페이지의 마진과 패딩 제거
    /// - C# WebView2 크기 조정 트리거
    /// </summary>
    public static class PageLayout
    {
        /// <summary>
        /// 웹 페이지의 마진과 패딩을 제거하는 스크립트
        ///
        /// 실행 절차:
        /// 1. .wrap, .content.wide, .p-relative, .alert-box 클래스 요소들의 마진/패딩을 0으로 설정
        /// 2. body 요소의 마진/패딩도 0으로 설정
        /// 3. 콘솔에 로그 출력
        /// 4. 100ms 후 C#에 'margins-removed' 메시지 전송 (WebView 크기 조정 트리거용)
        /// </summary>
        public const string REMOVE_PAGE_MARGINS_SCRIPT =
            @"
                (function() {
                    try {
                        // wrap 클래스 마진/패딩 제거
                        const wrapElements = document.querySelectorAll('.wrap');
                        wrapElements.forEach(element => {
                            element.style.margin = '0';
                            element.style.padding = '0';
                        });

                        // content wide 클래스 마진/패딩 제거
                        const contentWideElements = document.querySelectorAll('.content.wide');
                        contentWideElements.forEach(element => {
                            element.style.margin = '0';
                            element.style.padding = '0';
                        });

                        // p-relative 클래스 마진/패딩 제거
                        const pRelativeElements = document.querySelectorAll('.p-relative');
                        pRelativeElements.forEach(element => {
                            element.style.margin = '0';
                            element.style.padding = '0';
                        });

                        // alert-box 클래스 마진/패딩 제거
                        const alertBoxElements = document.querySelectorAll('.alert-box');
                        alertBoxElements.forEach(element => {
                            element.style.margin = '0';
                            element.style.padding = '0';
                        });

                        // body 마진/패딩도 제거
                        document.body.style.margin = '0';
                        document.body.style.padding = '0';

                        console.log('[Remove Margins] Page margins and paddings removed (wrap, content, p-relative, alert-box)');

                        // C#에 마진 제거 완료 알림 (WebView 크기 조정 트리거용)
                        setTimeout(() => {
                            try {
                                window.chrome.webview.postMessage(JSON.stringify({
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
            ";
    }
}
