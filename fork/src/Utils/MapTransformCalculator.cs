using System;

namespace TarkovClient.Utils
{
    /// <summary>
    /// 맵별 Transform Matrix 계산을 위한 유틸리티 클래스
    /// PiP 모드에서 창 크기에 따른 동적 지도 스케일링 지원
    /// </summary>
    public static class MapTransformCalculator
    {
        /// <summary>
        /// Factory 맵용 Transform Matrix 계산
        /// </summary>
        /// <param name="width">창 너비</param>
        /// <param name="height">창 높이</param>
        /// <returns>CSS transform matrix 문자열</returns>
        public static string CalculateFactoryMapTransform(double width, double height)
        {
            // 기준값 (300x250, Factory 맵)
            const double baseWidth = 300;
            const double baseHeight = 250;
            const double baseTransX = -93.2495;
            const double baseTransY = -105.55;

            // 실제 측정된 데이터 포인트들
            // 정사각형 근처: 300x250, 450x375, 600x500, 800x640, 1000x800, 1200x960
            // 극단 비율: 1000x500(0.276855, -23.665, -262.629), 500x1000(0.276855, -276.194, -24.2515)

            // 1. 가로세로 비율 분석
            double aspectRatio = width / height;
            double baseAspectRatio = baseWidth / baseHeight; // 1.2
            double aspectDifference = Math.Abs(aspectRatio - baseAspectRatio);

            // 2. Scale 값 계산 (면적 기반)
            double sizeRatio = Math.Sqrt((width * height) / (baseWidth * baseHeight));
            double newScale;
            
            // 극단 비율에서는 Scale 감소 적용
            if (aspectDifference >= 0.5)
            {
                // 극단 비율: Scale을 더 보수적으로 계산
                newScale = 0.12 * (1 + 0.423 * (sizeRatio - 1));
            }
            else
            {
                // 정사각형 근처: 기존 공식 유지
                newScale = 0.12 * (1 + 1.375 * (sizeRatio - 1));
            }

            // 3. 개별 축 비율 계산
            double widthRatio = width / baseWidth;
            double heightRatio = height / baseHeight;

            // 4. Translation 계산 (가로세로 비율 고려)
            if (aspectDifference < 0.5) // 정사각형 근처 (기존 공식 사용)
            {
                double newTransX = baseTransX * (1 + 1.58 * (sizeRatio - 1));
                double newTransY = baseTransY * (1 + 1.68 * (sizeRatio - 1));
                return $"matrix({newScale:F6}, 0, 0, {newScale:F6}, {newTransX:F4}, {newTransY:F4})";
            }
            else // 극단 비율 (새로운 공식)
            {
                // 극단 비율에서는 긴 축은 적게, 짧은 축은 많이 이동
                // 1000x500: X=-23.665, Y=-262.629 패턴 적용
                
                if (width > height) // 가로가 긴 경우
                {
                    // X는 적게 이동 (긴 축), Y는 많이 이동 (짧은 축)
                    double newTransX = baseTransX * (-0.737) * widthRatio;
                    double newTransY = baseTransY * 1.284 * heightRatio;
                    return $"matrix({newScale:F6}, 0, 0, {newScale:F6}, {newTransX:F4}, {newTransY:F4})";
                }
                else // 세로가 긴 경우
                {
                    // X는 많이 이동 (짧은 축), Y는 적게 이동 (긴 축)
                    // 400x600 및 500x1000 데이터 기반 계수 조정
                    double newTransX = baseTransX * 1.52 * widthRatio;
                    double newTransY = baseTransY * 0.315 * heightRatio;
                    return $"matrix({newScale:F6}, 0, 0, {newScale:F6}, {newTransX:F4}, {newTransY:F4})";
                }
            }
        }

        // 향후 다른 맵들 추가 예정
        // public static string CalculateCustomsMapTransform(double width, double height) { ... }
        // public static string CalculateWoodsMapTransform(double width, double height) { ... }
        // public static string CalculateInterchangeMapTransform(double width, double height) { ... }
    }
}