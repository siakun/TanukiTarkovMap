/**
 * SVG 투명화 스크립트
 *
 * 목적:
 * - SVG 요소에서 #0f0f0f 색상을 투명하게 변경
 * - 지도 배경을 투명하게 만들어 오버레이 효과 개선
 */

(function () {
    'use strict';

    /**
     * 지정된 색상들을 투명하게 변경
     * - #0f0f0f (rgb(15, 15, 15))
     * - #3a3e48 (rgb(58, 62, 72))
     */
    function makeColorTransparent() {
        // 투명화할 색상 목록
        const colorsToRemove = [
            'rgb(15, 15, 15)',   // #0f0f0f
            'rgb(58, 62, 72)'    // #3a3e48
        ];

        // 모든 SVG 요소 찾기
        const svgElements = document.querySelectorAll('svg, svg *');

        svgElements.forEach(element => {
            const computedStyle = window.getComputedStyle(element);

            // fill 속성 확인
            const fill = computedStyle.fill;
            if (colorsToRemove.includes(fill)) {
                element.style.fill = 'transparent';
            }

            // stroke 속성 확인
            const stroke = computedStyle.stroke;
            if (colorsToRemove.includes(stroke)) {
                element.style.stroke = 'transparent';
            }

            // background-color 확인 (일반 요소용)
            const bgColor = computedStyle.backgroundColor;
            if (colorsToRemove.includes(bgColor)) {
                element.style.backgroundColor = 'transparent';
            }
        });

        // SVG가 아닌 일반 요소도 확인
        const allElements = document.querySelectorAll('*');
        allElements.forEach(element => {
            const computedStyle = window.getComputedStyle(element);
            const bgColor = computedStyle.backgroundColor;

            if (colorsToRemove.includes(bgColor)) {
                element.style.backgroundColor = 'transparent';
            }
        });
    }

    // 페이지 로드 직후 실행
    makeColorTransparent();

    // MutationObserver로 동적으로 추가되는 요소 감시
    const observer = new MutationObserver(mutations => {
        const colorsToRemove = [
            'rgb(15, 15, 15)',   // #0f0f0f
            'rgb(58, 62, 72)'    // #3a3e48
        ];

        for (const mutation of mutations) {
            if (mutation.type === 'childList') {
                mutation.addedNodes.forEach(node => {
                    if (node instanceof HTMLElement || node instanceof SVGElement) {
                        // 추가된 요소와 그 자식들에 대해 투명화 적용
                        const computedStyle = window.getComputedStyle(node);

                        if (colorsToRemove.includes(computedStyle.fill)) {
                            node.style.fill = 'transparent';
                        }
                        if (colorsToRemove.includes(computedStyle.stroke)) {
                            node.style.stroke = 'transparent';
                        }
                        if (colorsToRemove.includes(computedStyle.backgroundColor)) {
                            node.style.backgroundColor = 'transparent';
                        }

                        // 자식 요소들도 확인
                        const children = node.querySelectorAll('*');
                        children.forEach(child => {
                            const childStyle = window.getComputedStyle(child);
                            if (colorsToRemove.includes(childStyle.fill)) {
                                child.style.fill = 'transparent';
                            }
                            if (colorsToRemove.includes(childStyle.stroke)) {
                                child.style.stroke = 'transparent';
                            }
                            if (colorsToRemove.includes(childStyle.backgroundColor)) {
                                child.style.backgroundColor = 'transparent';
                            }
                        });
                    }
                });
            }
        }
    });

    // 전체 document 감시
    observer.observe(document.body, {
        childList: true,
        subtree: true
    });

    // 약간의 딜레이 후 재실행 (동적 로딩 대응)
    setTimeout(makeColorTransparent, 1000);
    setTimeout(makeColorTransparent, 2000);
})();
