using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TarkovClient
{
    public static class GameSessionCleaner
    {

        /// <summary>
        /// 프로그램 시작 시 구 로그 폴더들을 정리 (최신 폴더 제외)
        /// </summary>
        public static void CleanOldLogFolders()
        {
            try
            {
                // 설정 확인 - 자동 삭제가 비활성화되어 있으면 종료
                if (!Env.GetSettings().autoDeleteLogs)
                {
                    return;
                }

                if (!Directory.Exists(Env.LogsFolder))
                {
                    return;
                }

                // 모든 로그 폴더 가져오기
                var logDirectories = Directory
                    .GetDirectories(Env.LogsFolder)
                    .OrderByDescending(dir => Directory.GetCreationTime(dir))
                    .ToArray();

                // 최소 2개 이상 폴더가 있을 때만 정리 (최신 1개는 보존)
                if (logDirectories.Length <= 1)
                {
                    return;
                }

                // 최신 폴더를 제외한 나머지 삭제
                for (int i = 1; i < logDirectories.Length; i++)
                {
                    try
                    {
                        var oldLogDir = logDirectories[i];

                        // 폴더 내 모든 파일 삭제 시도
                        var files = Directory.GetFiles(
                            oldLogDir,
                            "*.*",
                            SearchOption.AllDirectories
                        );
                        foreach (var file in files)
                        {
                            try
                            {
                                File.SetAttributes(file, FileAttributes.Normal);
                                File.Delete(file);
                            }
                            catch (Exception)
                            {
                                // 개별 파일 삭제 실패 무시
                            }
                        }

                        // 폴더 삭제 시도
                        Directory.Delete(oldLogDir, true);
                    }
                    catch (Exception)
                    {
                        // 폴더 삭제 실패 무시
                    }
                }
            }
            catch (Exception)
            {
                // 전체 프로세스 실패 무시
            }
        }

        /// <summary>
        /// 스크린샷 파일들을 정리 (독립 메서드로 유지)
        /// </summary>

        public static void CleanScreenshotFiles()
        {
            try
            {
                // 설정 확인 - 자동 삭제가 비활성화되어 있으면 종료
                if (!Env.GetSettings().autoDeleteScreenshots)
                {
                    return;
                }

                if (!Directory.Exists(Env.ScreenshotsFolder))
                {
                    return;
                }

                var screenshotFiles = Directory
                    .GetFiles(Env.ScreenshotsFolder, "*.*")
                    .Where(file =>
                    {
                        var ext = Path.GetExtension(file).ToLower();
                        return (ext == ".png" || ext == ".jpg" || ext == ".jpeg");
                    })
                    .ToArray();

                // 병렬 처리로 성능 최적화
                Parallel.ForEach(screenshotFiles, screenshotFile =>
                {
                    try
                    {
                        // 파일 속성 변경 시도 (읽기 전용 해제)
                        File.SetAttributes(screenshotFile, FileAttributes.Normal);

                        // 강제 삭제 시도
                        File.Delete(screenshotFile);
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                    catch (Exception) { }
                });
            }
            catch (Exception)
            {
                // 에러 무시
            }
        }

    }
}
