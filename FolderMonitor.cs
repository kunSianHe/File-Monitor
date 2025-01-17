using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public class FolderMonitor : IDisposable
{
    private readonly string _sourceFolder;
    private readonly string _destinationFolder;
    private readonly string _logFilePath;
    private readonly FileSystemWatcher _watcher;
    private readonly ConcurrentQueue<string> _fileQueue;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly object _logLock = new object();

    public FolderMonitor(string sourceFolder, string destinationFolder, string logFilePath)
    {
        _sourceFolder = sourceFolder;
        _destinationFolder = destinationFolder;
        _logFilePath = logFilePath;
        _fileQueue = new ConcurrentQueue<string>();
        _cancellationTokenSource = new CancellationTokenSource();

        // 設置 FileSystemWatcher
        _watcher = new FileSystemWatcher
        {
            Path = sourceFolder,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            Filter = "*.*",
            EnableRaisingEvents = true
        };

        _watcher.Created += OnFileCreated;

        // 啟動處理檔案的背景任務
        Task.Run(ProcessFileQueue, _cancellationTokenSource.Token);
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        _fileQueue.Enqueue(e.FullPath);
    }

    private async Task ProcessFileQueue()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            if (_fileQueue.TryDequeue(out string filePath))
            {
                try
                {
                    // 等待檔案寫入完成
                    await WaitForFileAccess(filePath);

                    // 建立目標路徑
                    string fileName = Path.GetFileName(filePath);
                    string destinationPath = Path.Combine(_destinationFolder, fileName);

                    // 移動檔案
                    File.Move(filePath, destinationPath);

                    // 寫入日誌
                    LogFileMovement(fileName, destinationPath);
                }
                catch (Exception ex)
                {
                    LogError($"處理檔案 {filePath} 時發生錯誤: {ex.Message}");
                }
            }
            else
            {
                await Task.Delay(100); // 避免 CPU 使用率過高
            }
        }
    }

    private async Task WaitForFileAccess(string filePath)
    {
        int retryCount = 0;
        while (retryCount < 10)
        {
            try
            {
                using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return;
                }
            }
            catch (IOException)
            {
                await Task.Delay(100);
                retryCount++;
            }
        }
        throw new TimeoutException($"無法存取檔案: {filePath}");
    }

    private void LogFileMovement(string fileName, string destinationPath)
    {
        string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - 檔案 {fileName} 已移動到 {destinationPath}";
        lock (_logLock)
        {
            File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
        }
    }

    private void LogError(string message)
    {
        string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - 錯誤: {message}";
        lock (_logLock)
        {
            File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _watcher.Dispose();
        _cancellationTokenSource.Dispose();
    }
} 