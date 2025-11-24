/**
 * 스크린샷 경로 자동 입력 스크립트
 *
 * 목적: 웹페이지의 "Screenshots folder" 입력란에 경로 자동 입력
 *
 * 주의: 이 스크립트는 C#에서 string.Format으로 {0}에 경로를 넣어서 실행됩니다.
 * 예: string.Format(script, "C:\\Users\\...\\Screenshots")
 */

(function() {
    // 디버그 정보를 저장할 배열
    const debugInfo = [];

    try {
        debugInfo.push('Starting search for Screenshots folder input...');

        // ========================================
        // Step 1: 라벨 찾기
        // ========================================
        // Array.from: NodeList를 배열로 변환
        // find: 조건에 맞는 첫 번째 요소 찾기
        const screenshotLabelText = Array.from(document.querySelectorAll('div.mb-5')).find(div =>
            div.textContent.trim() === 'Screenshots folder (case sensitive)'
        );

        if (!screenshotLabelText) {
            debugInfo.push('Label div not found');
            return JSON.stringify({ status: 'label_not_found', debug: debugInfo });
        }

        debugInfo.push('Found label div');

        // ========================================
        // Step 2: 부모 컨테이너 찾기
        // ========================================
        // ?. (옵셔널 체이닝): 객체가 null이면 undefined 반환 (에러 안남)
        const container = screenshotLabelText?.parentElement;
        if (!container) {
            debugInfo.push('Parent container not found');
            return JSON.stringify({ status: 'container_not_found', debug: debugInfo });
        }

        debugInfo.push('Found parent container');

        // ========================================
        // Step 3: 입력 컨테이너 찾기 (두 번째 자식)
        // ========================================
        const children = Array.from(container?.children || []);
        const inputContainer = children[1];  // 인덱스 1 = 두 번째 요소

        if (!inputContainer) {
            debugInfo.push('Input container (2nd child) not found');
            return JSON.stringify({ status: 'input_container_not_found', debug: debugInfo });
        }

        debugInfo.push('Found input container');

        // ========================================
        // Step 4: input 요소 찾기
        // ========================================
        const input = inputContainer?.querySelector('input');

        if (!input) {
            debugInfo.push('Input element not found');
            return JSON.stringify({ status: 'input_not_found', debug: debugInfo });
        }

        // 백틱(`) 사용: ${}로 변수 값 삽입
        debugInfo.push(`Input found - placeholder: ${input.placeholder}`);
        debugInfo.push(`Current value: '${input.value}'`);

        // ========================================
        // Step 5: 값 설정 및 이벤트 발생
        // ========================================
        // C#에서 {0}에 실제 경로가 들어갑니다
        input.value = '{0}';

        // Vue/Nuxt 프레임워크를 위한 이벤트 트리거
        // bubbles: true = 이벤트가 부모로 전파됨
        input.dispatchEvent(new Event('input', { bubbles: true }));
        input.dispatchEvent(new Event('change', { bubbles: true }));
        input.dispatchEvent(new Event('blur', { bubbles: true }));

        debugInfo.push('Value set successfully');
        return JSON.stringify({ status: 'success', debug: debugInfo });

    } catch (e) {
        debugInfo.push(`Error: ${e.message}`);
        return JSON.stringify({ status: 'error', message: e.message, debug: debugInfo });
    }
})();
