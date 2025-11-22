namespace TanukiTarkovMap.Models.JavaScript
{
    /// <summary>
    /// 웹 요소 제어 관련 JavaScript 스크립트
    /// - 맵 UI 요소(패널, 헤더, 푸터) 숨기기/복원
    /// - "UI 요소 숨기기" 체크박스 기능 지원
    /// </summary>
    public static class WebElementsControl
    {
        /// <summary>
        /// UI 요소 복원 (맵 transform은 유지)
        ///
        /// 실행 절차:
        /// 1. panel_left, panel_right, panel_top 요소를 찾아서 display 속성을 빈 문자열로 설정 (복원)
        /// 2. header, footer-wrap 요소도 동일하게 복원
        /// 3. 맵의 transform 설정은 건드리지 않음 (현재 창 크기 유지)
        /// </summary>
        public const string RESTORE_UI_ELEMENTS_KEEP_TRANSFORM =
            @"
                    try {
                        // panel_left 복원
                        var panelLeft = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_left');
                        if (panelLeft) {
                            panelLeft.style.display = '';
                        }

                        // panel_right 복원
                        var panelRight = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_right');
                        if (panelRight) {
                            panelRight.style.display = '';
                        }

                        // panel_top 복원
                        var panelTop = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_top');
                        if (panelTop) {
                            panelTop.style.display = '';
                        }

                        // header 복원
                        var header = document.querySelector('#__nuxt > div > div > header');
                        if (header) {
                            header.style.display = '';
                        }

                        // footer-wrap 복원
                        var footerWrap = document.querySelector('#__nuxt > div > div > div.footer-wrap');
                        if (footerWrap) {
                            footerWrap.style.display = '';
                        }

                        // 주의: 맵 transform은 유지 (현재 창 모드 유지)
                    } catch { }
                ";

        /// <summary>
        /// 일반 모드로 완전히 복원
        ///
        /// 실행 절차:
        /// 1. RESTORE_UI_ELEMENTS_KEEP_TRANSFORM와 동일하게 모든 UI 요소 복원
        /// 2. 현재는 맵 transform 초기화를 하지 않음 (창 크기로만 조절하기 때문)
        /// </summary>
        public const string RESTORE_ALL_ELEMENTS =
            @"
                    try {
                        // panel_left 복원
                        var panelLeft = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_left');
                        if (panelLeft) {
                            panelLeft.style.display = '';
                        }

                        // panel_right 복원
                        var panelRight = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_right');
                        if (panelRight) {
                            panelRight.style.display = '';
                        }

                        // panel_top 복원
                        var panelTop = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_top');
                        if (panelTop) {
                            panelTop.style.display = '';
                        }

                        // header 복원
                        var header = document.querySelector('#__nuxt > div > div > header');
                        if (header) {
                            header.style.display = '';
                        }

                        // footer-wrap 복원
                        var footerWrap = document.querySelector('#__nuxt > div > div > div.footer-wrap');
                        if (footerWrap) {
                            footerWrap.style.display = '';
                        }

                        // 맵 transform 초기화 제거: 창 크기로만 조절하므로 초기화 불필요
                    } catch { }
                ";

        /// <summary>
        /// 왼쪽 패널 숨기기
        ///
        /// 실행 절차:
        /// 1. panel_left 요소를 찾아서 display: none 설정
        /// </summary>
        public const string HIDE_PANEL_LEFT =
            @"
                    try {
                        var panelLeft = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_left');
                        if (panelLeft) {
                            panelLeft.style.display = 'none';
                        }
                    } catch { }
                ";

        /// <summary>
        /// 오른쪽 패널 숨기기
        ///
        /// 실행 절차:
        /// 1. panel_right 요소를 찾아서 display: none 설정
        /// </summary>
        public const string HIDE_PANEL_RIGHT =
            @"
                    try {
                        var panelRight = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_right');
                        if (panelRight) {
                            panelRight.style.display = 'none';
                        }
                    } catch { }
                ";

        /// <summary>
        /// 상단 패널 숨기기
        ///
        /// 실행 절차:
        /// 1. panel_top 요소를 찾아서 display: none 설정
        /// </summary>
        public const string HIDE_PANEL_TOP =
            @"
                    try {
                        var panelTop = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_top');
                        if (panelTop) {
                            panelTop.style.display = 'none';
                        }
                    } catch { }
                ";

        /// <summary>
        /// 헤더 숨기기
        ///
        /// 실행 절차:
        /// 1. header 요소를 찾아서 display: none 설정
        /// </summary>
        public const string HIDE_HEADER =
            @"
                    try {
                        var header = document.querySelector('#__nuxt > div > div > header');
                        if (header) {
                            header.style.display = 'none';
                        }
                    } catch { }
                ";

        /// <summary>
        /// 푸터 숨기기 및 C#에 완료 알림
        ///
        /// 실행 절차:
        /// 1. footer-wrap 요소를 찾아서 display: none 설정
        /// 2. 100ms 후 C#에 'ui-elements-removed' 메시지 전송 (맵 리렌더링 트리거용)
        /// </summary>
        public const string HIDE_FOOTER =
            @"
                    try {
                        var footerWrap = document.querySelector('#__nuxt > div > div > div.footer-wrap');
                        if (footerWrap) {
                            footerWrap.style.display = 'none';
                        }

                        // UI 요소 제거 완료 후 C#에 알림 (맵 리렌더링 트리거용)
                        setTimeout(() => {
                            try {
                                window.chrome.webview.postMessage(JSON.stringify({
                                    type: 'ui-elements-removed'
                                }));
                                console.log('[UI Elements] Sent ui-elements-removed message to C#');
                            } catch (e) {
                                console.error('[UI Elements] Failed to send message:', e);
                            }
                        }, 100);
                    } catch { }
                ";

        /// <summary>
        /// 큰 창 크기일 때 요소들을 복원하는 스크립트 (지도 스케일은 유지)
        ///
        /// 실행 절차:
        /// 1. panel_left, panel_right, panel_top 요소를 display 빈 문자열로 복원
        /// 2. header 요소도 복원
        /// 3. 지도 transform은 건드리지 않음
        /// </summary>
        public const string RESTORE_ELEMENTS_FOR_LARGE_SIZE =
            @"
                    try {
                        // panel_left 복원
                        var panelLeft = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_left');
                        if (panelLeft) {
                            panelLeft.style.display = '';
                        }

                        // panel_right 복원
                        var panelRight = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_right');
                        if (panelRight) {
                            panelRight.style.display = '';
                        }

                        // panel_top 복원
                        var panelTop = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_top');
                        if (panelTop) {
                            panelTop.style.display = '';
                        }

                        // header 복원
                        var header = document.querySelector('#__nuxt > div > div > header');
                        if (header) {
                            header.style.display = '';
                        }

                    } catch { }
                ";

        /// <summary>
        /// 작은 창 크기일 때 요소들을 숨기는 스크립트
        ///
        /// 실행 절차:
        /// 1. panel_left, panel_right, panel_top 요소를 display: none으로 숨김
        /// 2. header 요소도 숨김
        /// </summary>
        public const string HIDE_ELEMENTS_FOR_SMALL_SIZE =
            @"
                    try {
                        // panel_left 숨김
                        var panelLeft = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_left');
                        if (panelLeft) {
                            panelLeft.style.display = 'none';
                        }

                        // panel_right 숨김
                        var panelRight = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_right');
                        if (panelRight) {
                            panelRight.style.display = 'none';
                        }

                        // panel_top 숨김
                        var panelTop = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_top');
                        if (panelTop) {
                            panelTop.style.display = 'none';
                        }

                        // header 숨김
                        var header = document.querySelector('#__nuxt > div > div > header');
                        if (header) {
                            header.style.display = 'none';
                        }

                    } catch { }
                ";

        /// <summary>
        /// 현재 요소들의 표시/숨김 상태를 확인하는 스크립트
        ///
        /// 실행 절차:
        /// 1. panel_left, panel_right, header 요소를 찾아서 display 속성 확인
        /// 2. 각 요소의 표시 상태를 JSON으로 반환
        /// 3. 반환 형식: { panelLeftVisible: bool, panelRightVisible: bool, headerVisible: bool }
        /// </summary>
        public const string CHECK_ELEMENTS_VISIBILITY_STATUS =
            @"
                    (function() {
                        try {
                            var panelLeft = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_left');
                            var panelRight = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_right');
                            var header = document.querySelector('#__nuxt > div > div > header');

                            var status = {
                                panelLeftVisible: panelLeft ? (panelLeft.style.display !== 'none') : false,
                                panelRightVisible: panelRight ? (panelRight.style.display !== 'none') : false,
                                headerVisible: header ? (header.style.display !== 'none') : false
                            };

                            return JSON.stringify(status);
                        } catch { }
                    })();
                ";
    }
}
