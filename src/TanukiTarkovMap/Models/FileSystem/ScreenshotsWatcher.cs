using System.IO;
using System.Text.RegularExpressions;
using TanukiTarkovMap.Models.Services;
using TanukiTarkovMap.Models.Utils;

namespace TanukiTarkovMap.Models.FileSystem
{
    /**
    ScreenshotsWatcher - 타르코프 스크린샷 폴더 감시

    Purpose: Screenshots 폴더의 새 파일 생성을 감지하여 tarkov-market 페이지에 전달

    Core Functionality:
    - Start(): 스크린샷 폴더 감시 시작, 폴더 미존재 시 부모 폴더 감시
    - Stop(): 모든 감시자 정리
    - Restart(): 감시자 재시작 (경로 변경 시 호출)

    첫 실행 문제 해결:
    - Screenshots 폴더가 없을 경우 부모 폴더(Escape from Tarkov) 감시
    - 부모 폴더에 Screenshots 생성 시 자동으로 감시 시작

    맵 보정 대상 판정:
    - 파일명에 좌표가 들어 있는 스크린샷만 맵 보정 신호로 쓴다 (HasRaidCoordinates)
    - 메뉴와 은신처에서 찍은 스크린샷에는 좌표가 없다. 이를 보정에 쓰면 손으로 고른 맵이
      지난 판의 맵으로 되돌아간다
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

                    // 파일명을 웹 페이지의 window.pilot으로 넘겨 위치 마커를 옮긴다
                    ServiceLocator.MapEventService.OnScreenshotTaken(filename);

                    // 레이드 도중 앱을 켜면 진입 로그가 이미 지나가 맵이 전환되지 않는다.
                    // 레이드 안에서 찍은 스크린샷은 그 시점에 레이드 중이라는 증거이므로,
                    // 마지막 감지 맵을 현재 맵으로 보고 보정한다.
                    // 이미 그 맵을 보고 있으면 수신 측에서 걸러지므로 여기서는 비교하지 않는다
                    var lastDetectedMap = LogsWatcher.LastDetectedMap;
                    if (lastDetectedMap != null && HasRaidCoordinates(filename))
                    {
                        ServiceLocator.MapEventService.OnMapChanged(lastDetectedMap, MapChangeSource.Screenshot);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"[Screenshot Error] {ex.Message}");
            }
        }

        /// <summary>
        /// 레이드 안에서 찍은 스크린샷인지 파일명으로 가린다.
        ///
        /// 게임은 레이드 안에서만 좌표와 시선 방향을 파일명에 적는다.
        ///   레이드: 2026-08-20[20-51]_218.68, -51.18, 246.60_0.00000, 0.99967, ... _14.01 (0).png
        ///   메뉴/은신처: 2026-08-20[21-47]_9.43 (0).png
        /// 메뉴에서 찍은 것까지 맵 보정에 쓰면, 다음 레이드를 고르려고 지도를 손으로 바꿔 둔
        /// 사용자를 지난 판의 맵으로 되돌려 버린다.
        ///
        /// 판정은 "숫자, 숫자, 숫자" 묶음의 유무로 한다. 좌표는 음수와 소수를 모두 쓰므로
        /// 값의 범위로는 맵을 가릴 수 없고, 여기서 필요한 것도 값이 아니라 레이드 여부다
        /// </summary>
        static bool HasRaidCoordinates(string filename)
            => RaidCoordinatesRe.IsMatch(filename);

        static readonly Regex RaidCoordinatesRe =
            new(@"-?\d+(\.\d+)?, -?\d+(\.\d+)?, -?\d+(\.\d+)?", RegexOptions.Compiled);
    }
}
