using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using TanukiTarkovMap.Models.Data;
using TanukiTarkovMap.Models.Services;
using TanukiTarkovMap.Models.Utils;

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

        static string curLogFolder;
        static Dictionary<string, long> filePositions = new();

        /// <summary>
        /// 로그에서 마지막으로 확인한 맵. 초기 읽기 구간에서 감지한 것도 담긴다.
        /// 레이드 도중 앱을 켜 실시간 감지를 놓쳤을 때 스크린샷 시점의 보정에 쓰인다
        /// </summary>
        public static MapInfo? LastDetectedMap { get; private set; }

        static FileSystemWatcher logsFoldersWatcher;
        static LogFileWatcher appLogFileWatcher;
        static LogFileWatcher notifLogFileWatcher;

        static int _initialLogsReadCount = 0;
        static bool IsAllInitialLogsRead
        {
            get { return _initialLogsReadCount == 2; }
        }

        static void SetInitialLogsReadDone()
        {
            if (!IsAllInitialLogsRead)
            {
                _initialLogsReadCount++;
            }
        }

        static void ResetInitialLogsReadDone()
        {
            _initialLogsReadCount = 0;
            filePositions.Clear();

            // 경로가 바뀌어 재시작하는 경우가 있어, 낡은 맵이 남지 않도록 비운다
            LastDetectedMap = null;
        }

        public static void Start()
        {
            ResetInitialLogsReadDone();

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
            curLogFolder = GetLatestLogFolder();
            if (curLogFolder != null)
            {
                MonitorLogFolder(curLogFolder);
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

            filePositions.Clear();
        }

        static void MonitorLogFolder(string logsFolder)
        {
            // clear prev
            ClearLogsWatcher();

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

        static void OnNewFolderCreated(object sender, FileSystemEventArgs e)
        {
            // check new folder - newest
            var newDirectory = e.FullPath;
            if (Directory.GetCreationTime(newDirectory) > Directory.GetCreationTime(curLogFolder))
            {
                curLogFolder = newDirectory;
                // monitor new folder
                MonitorLogFolder(curLogFolder);
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
            ProcessLogFile(e.FullPath);
        }

        static void ProcessLogFile(string filePath)
        {
            try
            {
                // last read position
                long lastPosition = 0;
                if (filePositions.ContainsKey(filePath))
                {
                    lastPosition = filePositions[filePath];
                }

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
                            // 맵 감지만 초기 읽기 구간에서도 수행한다.
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

                                    // 지난 판의 맵으로 화면이 바뀌지 않도록 전환은 초기 읽기 이후에만 한다
                                    if (IsAllInitialLogsRead && mapInfo != null)
                                    {
                                        ServiceLocator.MapEventService.OnMapChanged(mapInfo, MapChangeSource.RaidEntry);
                                    }
                                }
                                continue;
                            }

                            // 나머지 신호는 지난 로그를 다시 처리하면 안 되므로 초기 읽기 구간에서 건너뛴다
                            if (!IsAllInitialLogsRead)
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
            catch (Exception)
            {
                // 로그 파일 처리 에러 무시
            }

            // initial read completed
            SetInitialLogsReadDone();
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
