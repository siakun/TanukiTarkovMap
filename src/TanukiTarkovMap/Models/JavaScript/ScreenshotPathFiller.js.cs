namespace TanukiTarkovMap.Models.JavaScript
{
    /// <summary>
    /// 스크린샷 경로 자동 입력 관련 JavaScript 스크립트
    /// - 웹 페이지의 "Screenshots folder" 입력란에 경로 자동 입력
    /// - Vue/Nuxt 프레임워크 이벤트 트리거
    /// </summary>
    public static class ScreenshotPathFiller
    {
        /// <summary>
        /// 스크린샷 폴더 경로를 자동으로 입력하는 스크립트
        /// {0} 자리에 스크린샷 폴더 경로가 들어갑니다.
        ///
        /// 실행 절차:
        /// 1. div.mb-5 요소들 중 "Screenshots folder (case sensitive)" 텍스트를 가진 라벨 찾기
        /// 2. 라벨의 부모 요소(mb-15 컨테이너)를 찾기
        /// 3. 부모 요소의 두 번째 자식 요소에서 input 요소 찾기
        /// 4. input.value에 경로 설정
        /// 5. Vue/Nuxt 프레임워크를 위해 input, change, blur 이벤트 발생
        /// 6. 각 단계의 성공/실패 정보를 JSON으로 반환
        /// </summary>
        public const string AUTO_FILL_SCREENSHOTS_PATH =
            @"
                    (function() {{
                        const debugInfo = [];

                        try {{
                            debugInfo.push('Starting search for Screenshots folder input...');

                            // Find the label div with exact text ""Screenshots folder (case sensitive)""
                            const screenshotLabelText = Array.from(document.querySelectorAll('div.mb-5')).find(div =>
                                div.textContent.trim() === 'Screenshots folder (case sensitive)'
                            );

                            if (!screenshotLabelText) {{
                                debugInfo.push('Label div not found');
                                return JSON.stringify({{ status: 'label_not_found', debug: debugInfo }});
                            }}

                            debugInfo.push('Found label div');

                            // Get the parent container (mb-15)
                            const container = screenshotLabelText?.parentElement;
                            if (!container) {{
                                debugInfo.push('Parent container not found');
                                return JSON.stringify({{ status: 'container_not_found', debug: debugInfo }});
                            }}

                            debugInfo.push('Found parent container');

                            // Get the second child (input container)
                            const children = Array.from(container?.children || []);
                            const inputContainer = children[1];

                            if (!inputContainer) {{
                                debugInfo.push('Input container (2nd child) not found');
                                return JSON.stringify({{ status: 'input_container_not_found', debug: debugInfo }});
                            }}

                            debugInfo.push('Found input container');

                            // Get the input element
                            const input = inputContainer?.querySelector('input');

                            if (!input) {{
                                debugInfo.push('Input element not found');
                                return JSON.stringify({{ status: 'input_not_found', debug: debugInfo }});
                            }}

                            debugInfo.push(`Input found - placeholder: ${{input.placeholder}}`);
                            debugInfo.push(`Current value: '${{input.value}}'`);

                            // Set the value
                            input.value = '{0}';

                            // Dispatch events for Vue/Nuxt frameworks
                            input.dispatchEvent(new Event('input', {{ bubbles: true }}));
                            input.dispatchEvent(new Event('change', {{ bubbles: true }}));
                            input.dispatchEvent(new Event('blur', {{ bubbles: true }}));

                            debugInfo.push('Value set successfully');
                            return JSON.stringify({{ status: 'success', debug: debugInfo }});

                        }} catch (e) {{
                            debugInfo.push(`Error: ${{e.message}}`);
                            return JSON.stringify({{ status: 'error', message: e.message, debug: debugInfo }});
                        }}
                    }})();
                ";
    }
}
