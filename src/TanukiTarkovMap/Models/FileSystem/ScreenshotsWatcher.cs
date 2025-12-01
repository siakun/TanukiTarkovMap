using System.IO;
using TanukiTarkovMap.Models.Services;
using TanukiTarkovMap.Models.Utils;

namespace TanukiTarkovMap.Models.FileSystem
{
    /**
    ScreenshotsWatcher - 타르코프 스크린샷 폴더 감시

    Purpose: Screenshots 폴더의 새 파일 생성을 감지하여 tarkov-market에 전송

    Core Functionality:
    - Start(): 스크린샷 폴더 감시 시작, 폴더 미존재 시 부모 폴더 감시
    - Stop(): 모든 감시자 정리
    - Restart(): 감시자 재시작 (경로 변경 시 호출)

    첫 실행 문제 해결:
    - Screenshots 폴더가 없을 경우 부모 폴더(Escape from Tarkov) 감시
    - 부모 폴더에 Screenshots 생성 시 자동으로 감시 시작
    */
    public static class ScreenshotsWatcher
    {
        private static FileSystemWatcher? _screenshotsWatcher;
        private static FileSystemWatcher? _parentWatcher;

        public static void Start()
        {
            var screenshotsPath = App.ScreenshotsFolder;

            if (string.IsNullOrEmpty(screenshotsPath))
            {
                Logger.SimpleLog("[ScreenshotsWatcher] Screenshots path is empty, skipping");
                return;
            }

            if (Directory.Exists(screenshotsPath))
            {
                StartScreenshotsWatcher(screenshotsPath);
            }
            else
            {
                Logger.SimpleLog($"[ScreenshotsWatcher] Folder not found: {screenshotsPath}");
                StartParentFolderWatcher(screenshotsPath);
            }
        }

        private static void StartScreenshotsWatcher(string path)
        {
            StopParentWatcher();

            _screenshotsWatcher = new FileSystemWatcher(path);
            _screenshotsWatcher.Created += OnScreenshot;
            _screenshotsWatcher.EnableRaisingEvents = true;

            Logger.SimpleLog($"[ScreenshotsWatcher] Started watching: {path}");
        }

        private static void StartParentFolderWatcher(string screenshotsPath)
        {
            // 부모 폴더 = "Escape from Tarkov" 폴더
            var parentPath = Path.GetDirectoryName(screenshotsPath);

            if (string.IsNullOrEmpty(parentPath))
            {
                Logger.SimpleLog("[ScreenshotsWatcher] Cannot determine parent folder");
                return;
            }

            // 부모 폴더도 없으면 조부모 폴더 감시 (Documents)
            if (!Directory.Exists(parentPath))
            {
                var grandParentPath = Path.GetDirectoryName(parentPath);
                if (!string.IsNullOrEmpty(grandParentPath) && Directory.Exists(grandParentPath))
                {
                    Logger.SimpleLog($"[ScreenshotsWatcher] Watching grandparent for folder creation: {grandParentPath}");
                    _parentWatcher = new FileSystemWatcher(grandParentPath);
                    _parentWatcher.Created += (s, e) => OnParentFolderCreated(e, screenshotsPath);
                    _parentWatcher.EnableRaisingEvents = true;
                }
                return;
            }

            Logger.SimpleLog($"[ScreenshotsWatcher] Watching parent for Screenshots creation: {parentPath}");
            _parentWatcher = new FileSystemWatcher(parentPath);
            _parentWatcher.Created += (s, e) => OnParentFolderCreated(e, screenshotsPath);
            _parentWatcher.EnableRaisingEvents = true;
        }

        private static void OnParentFolderCreated(FileSystemEventArgs e, string targetScreenshotsPath)
        {
            // Screenshots 폴더가 생성되었는지 확인
            if (e.FullPath.Equals(targetScreenshotsPath, StringComparison.OrdinalIgnoreCase) ||
                e.FullPath.Equals(Path.GetDirectoryName(targetScreenshotsPath), StringComparison.OrdinalIgnoreCase))
            {
                // 약간의 지연 후 재시도 (폴더 생성 완료 대기)
                System.Threading.Tasks.Task.Delay(500).ContinueWith(_ =>
                {
                    if (Directory.Exists(targetScreenshotsPath))
                    {
                        Logger.SimpleLog($"[ScreenshotsWatcher] Screenshots folder created, starting watcher");
                        StartScreenshotsWatcher(targetScreenshotsPath);
                    }
                });
            }
        }

        private static void StopParentWatcher()
        {
            if (_parentWatcher != null)
            {
                _parentWatcher.EnableRaisingEvents = false;
                _parentWatcher.Dispose();
                _parentWatcher = null;
            }
        }

        public static void Stop()
        {
            if (_screenshotsWatcher != null)
            {
                _screenshotsWatcher.Created -= OnScreenshot;
                _screenshotsWatcher.Dispose();
                _screenshotsWatcher = null;
            }

            StopParentWatcher();
        }

        public static void Restart()
        {
            Stop();
            Start();
        }

        static void OnScreenshot(object sender, FileSystemEventArgs e)
        {
            try
            {
                string filename = e.Name ?? "";
                string fullPath = e.FullPath ?? "";

                if (!string.IsNullOrEmpty(filename))
                {
                    // 파일 정보 수집
                    string fileInfo = "";
                    if (File.Exists(fullPath))
                    {
                        try
                        {
                            System.Threading.Thread.Sleep(100);
                            var info = new FileInfo(fullPath);
                            fileInfo = $", Size: {info.Length / 1024.0:F2} KB, Created: {info.CreationTime:yyyy-MM-dd HH:mm:ss}";
                        }
                        catch { }
                    }

                    Logger.SimpleLog($"[Screenshot] {filename} | Path: {fullPath}{fileInfo}");

                    Server.SendFilename(filename);

                    // 2차 트리거: 스크린샷 생성 이벤트 발생
                    ServiceLocator.MapEventService.OnScreenshotTaken();
                }
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"[Screenshot Error] {ex.Message}");
            }
        }
    }
}
