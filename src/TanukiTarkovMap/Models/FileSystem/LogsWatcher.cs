using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using TanukiTarkovMap.Models.Data;
using TanukiTarkovMap.Models.Services;
using TanukiTarkovMap.Models.Utils;

/**
LogsWatcher - 타르코프 로그를 따라 읽어 맵 진입, 퀘스트 완료, 레이드 종료를 알린다

Purpose: 게임은 자기 상태를 로그로만 알려 준다. 이 클래스는 지금 열려 있는 세션 폴더의
application 로그와 notifications 로그를 이어 읽어, 앱이 화면을 맞추는 데 쓸 신호를 꺼낸다.

Architecture: 세 층이다. FileSystemWatcher가 새 세션 폴더 생성을 잡고, LogFileWatcher 둘이
각 로그 파일의 증가분을 알리며, ProcessLogFile이 늘어난 구간만 해석한다.

Core Functionality:
- Start()/Stop()/Restart(): 감시 시작과 종료. 경로 설정이 바뀌면 Restart로 갈아탄다
- 맵 진입 감지: "scene preset" 줄의 번들 이름을 MapConfiguration으로 맵에 대응시킨다
- 퀘스트 완료 감지: 알림 로그의 JSON에서 완료(type 12)만 골라 넘긴다
- 레이드 종료 감지: BEClient 초기화 줄을 신호로 스크린샷을 정리한다

State Management:
- curLogFolder: 지금 따라가는 세션 폴더. 다른 폴더에서 온 알림은 무시하는 기준이 된다
- filePositions: 파일별 마지막 읽기 위치. 키가 없으면 그 파일은 아직 따라잡기 전이다
- LastDetectedMap: 마지막으로 읽어 낸 맵. 폴더가 바뀌면 비운다
- reportedReadFailures: 읽기 실패를 파일마다 한 번만 남기기 위한 기록

Method Flow:
  Start -> GetLatestLogFolder -> MonitorLogFolder -> LogFileWatcher 둘 기동
  LogFileWatcher.Created/Changed -> OnLogFileChanged -> (폴더 확인) -> ProcessLogFile
  ProcessLogFile -> 첫 읽기면 맵만 기억, 아니면 OnMapChanged/OnQuestCompleted 발행
  Logs 폴더에 새 폴더 생성 -> OnNewFolderCreated -> MonitorLogFolder (지난 맵 폐기)

Key Methods:
- ProcessLogFile(path): 잠금 안에서 늘어난 구간만 읽는다. 파일별 첫 호출은 따라잡기다
- OnNewFolderCreated(...): 게임 재시작으로 생긴 새 세션 폴더로 대상을 옮긴다
- ParseLoc(line, re): "path:maps/{이름}.bundle"에서 번들 이름만 뽑는다

Dependencies:
- LogFileWatcher: 파일 증가분 알림. 롤오버되면 새 파일로 스스로 옮겨 간다
- MapConfiguration: 번들 이름 -> MapInfo
- MapEventService: 꺼낸 신호를 ViewModel로 발행하는 통로
- GameSessionCleaner: 레이드 종료 시 스크린샷 정리

Design Rationale: "따라잡기인가"를 파일별로 판정한다. 지난 세션의 로그를 그대로 재생하면
화면이 옛 맵으로 튀므로 첫 읽기에서는 기억만 하고, 그 뒤부터 화면을 바꾼다.

Historical Context: 2026-08-21 이전에는 두 파일의 첫 읽기를 전역 카운터 하나로 셌다.
카운터가 2에 닿아야 실시간으로 넘어가는 구조라, 한쪽 로그가 늦게 생기면 다른 쪽의
실시간 신호까지 함께 삼켰다. 판정을 파일별로 옮겨 두 파일이 서로를 막지 않게 했다.

Critical Warnings:
- LastDetectedMap을 폴더 전환에서 비우는 것을 빼지 말 것. 남겨 두면 게임을 다시 켠 뒤
  스크린샷을 찍을 때마다 지난 판의 맵으로 화면이 끌려간다
- ProcessLogFile을 잠금 밖에서 부르지 말 것. 로그 파일 둘의 폴링이 같은 주기로 나란히
  돌아 거의 같은 순간에 들어오며, filePositions가 함께 망가진다

Last Updated: 2026-08-21 | .NET 8 | 지난 맵이 스크린샷마다 되살아나던 문제 수정
*/
namespace TanukiTarkovMap.Models.FileSystem
{
    public static class LogsWatcher
    {
        // Map change - application.log
        // 씬 로드 줄은 PVP/PVE 모두에서 남고 매치 생성 줄(NetworkGameCreate)보다 먼저, 더 자주 나온다
        static readonly string SCENE_PRESET_SUBSTRING = "application|scene preset";
        static readonly string ScenePresetRe = @"path:maps\/(?<loc>\w+)\.bundle";

        static readonly string NOTIFICATION_SUBSTRING = "push-notifications|Got notification | ChatMessageReceived";
        static readonly string LINE_START_WITH_DATE = "^\\d{4}-\\d{2}-\\d{2} \\d{1,2}:\\d{1,2}:\\d{1,2}.\\d{3}";

        // BattlEye client initialization - application.log
        static readonly string BECLIENT_INIT_SUBSTRING = "BEClient inited successfully";

        // 알림 로그의 종류 값. 예전에는 값을 그대로 웹에 넘겨 사이트가 걸렀지만,
        // window.pilot.questComplete에는 완료만 넘길 수 있어 여기서 가린다 (10 시작, 11 실패, 12 완료)
        static readonly string QUEST_COMPLETE_NOTIFICATION_TYPE = "12";

        // 폴링 스레드와 FileSystemWatcher 스레드가 함께 읽으므로 volatile로 둔다
        static volatile string curLogFolder;

        // 파일별 마지막 읽기 위치. 키가 있다는 것은 그 파일의 첫 읽기(따라잡기)가 끝났다는 뜻이다.
        // 두 로그 파일의 폴링이 5초 간격으로 나란히 돌아 같은 순간에 들어오므로 잠금으로 감싼다
        static readonly Dictionary<string, long> filePositions = new();
        static readonly HashSet<string> reportedReadFailures = new();
        static readonly object processLock = new();

        /// <summary>
        /// 로그에서 마지막으로 확인한 맵. 첫 읽기(따라잡기) 구간에서 감지한 것도 담긴다.
        /// 레이드 도중 앱을 켜 실시간 감지를 놓쳤을 때 스크린샷 시점의 보정에 쓰인다.
        /// 감시 대상 로그 폴더가 바뀌면(게임 재시작) 지난 판의 맵이 남지 않도록 비운다
        /// </summary>
        public static MapInfo? LastDetectedMap { get; private set; }

        static FileSystemWatcher logsFoldersWatcher;
        static LogFileWatcher appLogFileWatcher;
        static LogFileWatcher notifLogFileWatcher;

        public static void Start()
        {
            // Check if LogsFolder is null or empty first
            if (string.IsNullOrEmpty(App.LogsFolder))
            {
                // 게임 설치 폴더 미탐지: 자동 맵 전환/스크린샷 정리/퀘스트 전송이 모두 비활성화된다
                Logger.SimpleLog("[LogsWatcher] Game folder not found, log watching disabled");
                return;
            }

            if (!Directory.Exists(App.LogsFolder))
            {
                Logger.SimpleLog($"[LogsWatcher] Logs folder not found: {App.LogsFolder}");
                return;
            }

            // newest log folder
            var latestFolder = GetLatestLogFolder();
            if (latestFolder != null)
            {
                MonitorLogFolder(latestFolder);
            }

            // lookig for new folders creation
            logsFoldersWatcher = new FileSystemWatcher(App.LogsFolder);
            logsFoldersWatcher.Created += OnNewFolderCreated;
            logsFoldersWatcher.EnableRaisingEvents = true;
        }

        public static void Stop()
        {
            ClearLogsFoldersWatcher();
            ClearLogsWatcher();
        }

        public static void Restart()
        {
            Stop();
            Start();
        }

        static void ClearLogsFoldersWatcher()
        {
            if (logsFoldersWatcher != null)
            {
                logsFoldersWatcher.Created -= OnNewFolderCreated;
                logsFoldersWatcher.Dispose();
                logsFoldersWatcher = null;
            }

            curLogFolder = null;

            lock (processLock)
            {
                filePositions.Clear();
                reportedReadFailures.Clear();
                LastDetectedMap = null;
            }
        }

        /// <summary>
        /// 감시 대상을 이 폴더 하나로 바꾼다.
        /// 폴더가 바뀌었다는 것은 게임이 새로 켜졌다는 뜻이므로 지난 판에서 읽어 둔 맵을 버린다.
        /// 남겨 두면 새 세션에서 스크린샷을 찍을 때마다 지난 판의 맵으로 화면이 끌려간다
        /// </summary>
        static void MonitorLogFolder(string logsFolder)
        {
            // clear prev
            ClearLogsWatcher();

            curLogFolder = logsFolder;

            lock (processLock)
            {
                filePositions.Clear();
                reportedReadFailures.Clear();
                LastDetectedMap = null;
            }

            // log file watcher
            appLogFileWatcher = new LogFileWatcher(logsFolder, "*application*.log");
            appLogFileWatcher.Created += OnLogFileChanged;
            appLogFileWatcher.Changed += OnLogFileChanged;
            appLogFileWatcher.Start();

            // log file watcher
            notifLogFileWatcher = new LogFileWatcher(logsFolder, "*notifications*.log");
            notifLogFileWatcher.Created += OnLogFileChanged;
            notifLogFileWatcher.Changed += OnLogFileChanged;
            notifLogFileWatcher.Start();

            // 모니터링 시작
        }

        static void ClearLogsWatcher()
        {
            if (appLogFileWatcher != null)
            {
                appLogFileWatcher.Created -= OnLogFileChanged;
                appLogFileWatcher.Changed -= OnLogFileChanged;
                appLogFileWatcher.Stop();
                appLogFileWatcher = null;
            }

            if (notifLogFileWatcher != null)
            {
                notifLogFileWatcher.Created -= OnLogFileChanged;
                notifLogFileWatcher.Changed -= OnLogFileChanged;
                notifLogFileWatcher.Stop();
                notifLogFileWatcher = null;
            }
        }

        /// <summary>
        /// 게임이 새 세션 폴더를 만들면 감시 대상을 그쪽으로 옮긴다.
        /// Logs 폴더에는 파일도 생길 수 있고 감시를 시작할 때 폴더가 하나도 없었을 수도 있어,
        /// 폴더인지와 기존 대상이 있는지를 먼저 가린다. 여기서 던진 예외는 FileSystemWatcher가
        /// 삼키지 않고 그대로 올라가 앱을 내리므로 밖으로 흘리지 않는다
        /// </summary>
        static void OnNewFolderCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                var newDirectory = e.FullPath;
                if (!Directory.Exists(newDirectory))
                {
                    return;
                }

                // 감시를 시작할 때 폴더가 없었으면 이 폴더가 첫 대상이다
                if (string.IsNullOrEmpty(curLogFolder)
                    || !Directory.Exists(curLogFolder)
                    || Directory.GetCreationTime(newDirectory) > Directory.GetCreationTime(curLogFolder))
                {
                    Logger.SimpleLog($"[LogsWatcher] Switching to new log folder: {Path.GetFileName(newDirectory)}");
                    MonitorLogFolder(newDirectory);
                }
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"[LogsWatcher] New log folder handling failed: {ex.Message}");
            }
        }

        static string GetLatestLogFolder()
        {
            if (string.IsNullOrEmpty(App.LogsFolder))
                return null;

            var directories = Directory.GetDirectories(App.LogsFolder);
            if (directories.Length == 0)
                return null;

            // sort by create date
            var latestDirectory = directories
                .OrderByDescending(d => Directory.GetCreationTime(d))
                .FirstOrDefault();
            return latestDirectory;
        }

        static void OnLogFileChanged(object sender, FileChangedEventArgs e)
        {
            // 폴더를 옮긴 직후, 멈추라고 알린 옛 감시자가 마지막 한 바퀴를 마저 돌며
            // 지난 폴더의 내용을 올려보낼 수 있다. 그 알림으로 지난 판의 맵을 되살리지 않는다
            var owningFolder = Path.GetDirectoryName(e.FullPath);
            if (!string.Equals(owningFolder, curLogFolder, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ProcessLogFile(e.FullPath);
        }

        /// <summary>
        /// 로그 파일에서 지난번에 읽다 만 위치부터 끝까지 읽어 신호를 꺼낸다.
        /// 파일마다 첫 호출은 따라잡기 읽기다. 이때는 맵을 기억만 하고 화면은 바꾸지 않는다.
        /// 파일이 두 개라 폴링이 겹치므로 한 번에 하나씩만 들어가게 잠근다
        /// </summary>
        static void ProcessLogFile(string filePath)
        {
            lock (processLock)
            {
                ProcessLogFileLocked(filePath);
            }
        }

        static void ProcessLogFileLocked(string filePath)
        {
            // 이 파일을 읽은 적이 있으면 그 뒤는 실시간 구간이다
            bool isCatchUpRead = !filePositions.TryGetValue(filePath, out long lastPosition);

            try
            {
                using (
                    var stream = new FileStream(
                        filePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite
                    )
                )
                {
                    stream.Seek(lastPosition, SeekOrigin.Begin);

                    using (var reader = new StreamReader(stream))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            // 맵 감지만 따라잡기 읽기 구간에서도 수행한다.
                            // 레이드 도중 앱을 켜면 진입 로그가 이미 지나가 있어,
                            // 여기서 기억해 두지 않으면 스크린샷 보정에 쓸 맵이 없다
                            if (line.Contains(SCENE_PRESET_SUBSTRING))
                            {
                                var scenePreset = ParseLoc(line, ScenePresetRe);
                                if (!String.IsNullOrEmpty(scenePreset))
                                {
                                    var mapInfo = MapConfiguration.GetByScenePreset(scenePreset);
                                    if (mapInfo != null)
                                    {
                                        LastDetectedMap = mapInfo;
                                    }
                                    else
                                    {
                                        // 새 맵이 추가되면 이 줄이 프리셋 등록의 단서가 된다
                                        Logger.SimpleLog($"[LogsWatcher] Unknown scene preset: {scenePreset}");
                                    }

                                    // 지난 판의 맵으로 화면이 바뀌지 않도록 전환은 따라잡기 읽기 이후에만 한다
                                    if (!isCatchUpRead && mapInfo != null)
                                    {
                                        ServiceLocator.MapEventService.OnMapChanged(mapInfo, MapChangeSource.RaidEntry);
                                    }
                                }
                                continue;
                            }

                            // 나머지 신호는 지난 로그를 다시 처리하면 안 되므로 따라잡기 읽기 구간에서 건너뛴다
                            if (isCatchUpRead)
                            {
                                continue;
                            }

                            if (line.Contains(BECLIENT_INIT_SUBSTRING))
                            {
                                // BattlEye client initialized - game start or raid end
                                GameSessionCleaner.CleanScreenshotFiles();
                            }
                            else if (line.Contains(NOTIFICATION_SUBSTRING))
                            {
                                // reading json
                                StringBuilder jsonBuilder = new StringBuilder();

                                // reading next line (json first line)
                                line = reader.ReadLine();

                                // while not EOF
                                while (line != null)
                                {
                                    // line - starts with date - new log record - exiting json parse
                                    var match = Regex.Match(
                                        line,
                                        LINE_START_WITH_DATE,
                                        RegexOptions.IgnoreCase
                                    );
                                    if (match.Success)
                                    {
                                        break;
                                    }

                                    jsonBuilder.AppendLine(line);
                                    // reading next line
                                    line = reader.ReadLine();
                                }

                                // parse JSON
                                try
                                {
                                    string jsonString = jsonBuilder.ToString();
                                    if (!string.IsNullOrEmpty(jsonString))
                                    {
                                        dynamic questRec = JsonConvert.DeserializeObject(
                                            jsonString
                                        );
                                        if (
                                            questRec != null
                                            && questRec.message != null
                                            && questRec.message.type != null
                                            && questRec.message.templateId != null
                                        )
                                        {
                                            string status = questRec.message.type.ToString();

                                            // "6574e0dedc0d635f633a5805 successMessageText"
                                            string templateId = questRec.message.templateId;
                                            string[] parts = templateId.Split(' ');
                                            if (parts.Length > 0)
                                            {
                                                var questId = parts[0];
                                                if (!string.IsNullOrEmpty(questId)
                                                    && status == QUEST_COMPLETE_NOTIFICATION_TYPE)
                                                {
                                                    ServiceLocator.MapEventService.OnQuestCompleted(questId);
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (System.Text.Json.JsonException)
                                {
                                    // JSON 파싱 에러 무시
                                }
                            }
                        }

                        // save read position
                        filePositions[filePath] = stream.Position;
                    }
                }
            }
            catch (Exception ex)
            {
                // 읽기에 실패하면 위치를 기록하지 않아 다음 주기에 같은 자리부터 다시 읽는다.
                // 조용히 넘기면 로그 추적이 끊긴 것을 아무도 모르므로 파일마다 한 번은 남긴다
                if (reportedReadFailures.Add(filePath))
                {
                    Logger.SimpleLog($"[LogsWatcher] Log read failed ({Path.GetFileName(filePath)}): {ex.Message}");
                }
            }
        }

        public static string ParseLoc(string line, string locationRe)
        {
            // line
            var match = Regex.Match(line, locationRe, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var map = match.Groups["loc"].Value.ToLower();
                return map;
            }

            return null;
        }
    }
}
