using System.IO;

namespace TanukiTarkovMap.Models.Utils
{
    public class LogFileWatcher
    {
        readonly string folder;
        readonly string searchPattern;
        readonly int checkInterval;

        volatile bool isStopping = false;
        long lastFileSize = 0;
        FileSystemWatcher fileCreateWatcher;

        public event EventHandler<FileChangedEventArgs> Created;
        public event EventHandler<FileChangedEventArgs> Changed;

        public LogFileWatcher(string folder, string searchPattern, int checkInterval = 5000)
        {
            this.folder = folder;
            this.searchPattern = searchPattern;
            this.checkInterval = checkInterval;
        }

        string TryGetFilePath()
        {
            // 로그가 롤오버되면 application_001.log 처럼 접미사가 커지므로 이름 역순 첫 파일이 최신이다
            return Directory.GetFiles(folder, searchPattern)
                .OrderByDescending(filePath => filePath, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        public void Start()
        {
            Reset();

            var filePath = TryGetFilePath();

            // if file exists - start monitoring changes
            if (!String.IsNullOrEmpty(filePath))
            {
                StartFileChangeMonitoring(filePath);
            }
            else
            {
                // waiting for file creation
                fileCreateWatcher = new FileSystemWatcher(folder, searchPattern);
                fileCreateWatcher.Created += OnLogFileCreated;
                fileCreateWatcher.Renamed += OnLogFileCreated;
                fileCreateWatcher.EnableRaisingEvents = true;
            }
        }

        void StartFileChangeMonitoring(string filePath)
        {
            Task.Run(() => CheckFile(filePath));
        }

        void OnLogFileCreated(object sender, FileSystemEventArgs e)
        {
            // monitoring changes
            StartFileChangeMonitoring(e.FullPath);

            // file create monitoring stop
            StopFileCreationMonitoring();

            // trigger created
            Created?.Invoke(this, new FileChangedEventArgs(e.FullPath));
        }

        void CheckFile(string filePath)
        {
            while (!isStopping)
            {
                try
                {
                    // check file size
                    FileInfo fileInfo = new FileInfo(filePath);
                    long currentFileSize = fileInfo.Length;

                    if (currentFileSize > lastFileSize)
                    {
                        lastFileSize = currentFileSize;
                        // trigger change
                        Changed?.Invoke(this, new FileChangedEventArgs(filePath));
                    }
                }
                catch (Exception)
                {
                    // Error occurred - stop check loop
                    return;
                }

                // wait
                Thread.Sleep(checkInterval);
            }
        }

        public void Stop()
        {
            // stop changes monitoring
            isStopping = true;

            StopFileCreationMonitoring();
        }

        void StopFileCreationMonitoring()
        {
            if (fileCreateWatcher != null)
            {
                fileCreateWatcher.Created -= OnLogFileCreated;
                fileCreateWatcher.Dispose();
                fileCreateWatcher = null;
            }
        }

        void Reset()
        {
            isStopping = false;
            lastFileSize = 0;
        }
    }
}
