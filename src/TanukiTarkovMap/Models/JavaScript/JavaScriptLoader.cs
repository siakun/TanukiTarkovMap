using System;
using System.IO;
using System.Reflection;

namespace TanukiTarkovMap.Models.JavaScript
{
    /// <summary>
    /// JavaScript 파일을 Embedded Resource에서 로드하는 유틸리티
    ///
    /// 작동 방식:
    /// 1. .js 파일들이 빌드 시 DLL에 Embedded Resource로 포함됩니다
    /// 2. 런타임에 Assembly.GetManifestResourceStream으로 읽어옵니다
    /// 3. 읽은 내용을 문자열로 변환하여 WebView2에서 실행합니다
    ///
    /// 장점:
    /// - Visual Studio에서 JavaScript IntelliSense 완벽 지원
    /// - ESLint, Prettier 등 JavaScript 도구 사용 가능
    /// - 별도 파일 배포 불필요 (DLL에 포함)
    /// - 타입 안전성 유지
    /// </summary>
    public static class JavaScriptLoader
    {
        // Assembly: 현재 실행 중인 DLL 정보
        private static readonly Assembly CurrentAssembly = Assembly.GetExecutingAssembly();

        // 네임스페이스 기본 경로
        private static readonly string ResourcePrefix = "TanukiTarkovMap.Models.JavaScript.Scripts.";

        /// <summary>
        /// JavaScript 파일을 로드합니다
        ///
        /// 사용 예시:
        /// string script = JavaScriptLoader.Load("ui-customization.js");
        /// await webView.CoreWebView2.ExecuteScriptAsync(script);
        /// </summary>
        /// <param name="fileName">JavaScript 파일명 (예: "ui-customization.js")</param>
        /// <returns>JavaScript 코드 문자열</returns>
        /// <exception cref="FileNotFoundException">파일을 찾을 수 없을 때</exception>
        public static string Load(string fileName)
        {
            // 리소스 이름 생성
            // 예: "TanukiTarkovMap.Models.JavaScript.Scripts.ui-customization.js"
            string resourceName = ResourcePrefix + fileName;

            try
            {
                // Embedded Resource에서 스트림 가져오기
                using (Stream stream = CurrentAssembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        // 리소스를 찾을 수 없을 때 자세한 에러 메시지
                        throw new FileNotFoundException(
                            $"JavaScript 파일을 찾을 수 없습니다: {fileName}\n" +
                            $"리소스 이름: {resourceName}\n" +
                            $"확인 사항:\n" +
                            $"1. 파일이 Models/JavaScript/Scripts/ 폴더에 있는지\n" +
                            $"2. 파일의 빌드 작업이 'Embedded Resource'로 설정되어 있는지\n" +
                            $"3. 프로젝트를 다시 빌드했는지"
                        );
                    }

                    // StreamReader: 스트림에서 텍스트 읽기
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        // 모든 내용을 문자열로 읽기
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"JavaScript 파일 로드 중 오류 발생: {fileName}", ex);
            }
        }

        /// <summary>
        /// 템플릿 스크립트를 로드하고 값을 치환합니다
        ///
        /// 사용 예시:
        /// string script = JavaScriptLoader.LoadTemplate("screenshot-path-filler.js", screenshotPath);
        /// await webView.CoreWebView2.ExecuteScriptAsync(script);
        /// </summary>
        /// <param name="fileName">JavaScript 파일명</param>
        /// <param name="args">string.Format에 전달할 인자들 (예: 경로, URL 등)</param>
        /// <returns>치환된 JavaScript 코드 문자열</returns>
        public static string LoadTemplate(string fileName, params object[] args)
        {
            string template = Load(fileName);

            // string.Format: {0}, {1} 등을 실제 값으로 치환
            // 예: input.value = '{0}' → input.value = 'C:\Screenshots'
            return string.Format(template, args);
        }

        /// <summary>
        /// 로드 가능한 모든 JavaScript 리소스 목록을 반환합니다 (디버깅용)
        /// </summary>
        /// <returns>리소스 이름 배열</returns>
        public static string[] GetAvailableScripts()
        {
            // GetManifestResourceNames: DLL에 포함된 모든 리소스 이름 가져오기
            string[] allResources = CurrentAssembly.GetManifestResourceNames();

            // LINQ: 배열을 필터링하고 변환
            return System.Linq.Enumerable.ToArray(
                System.Linq.Enumerable.Select(
                    System.Linq.Enumerable.Where(
                        allResources,
                        r => r.StartsWith(ResourcePrefix) && r.EndsWith(".js")
                    ),
                    r => r.Substring(ResourcePrefix.Length)  // 접두사 제거
                )
            );
        }
    }
}
