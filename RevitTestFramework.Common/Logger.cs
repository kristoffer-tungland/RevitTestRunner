using System.Diagnostics;
using System.Text.Json;

namespace RevitTestFramework.Common;

/// <summary>
/// Simple logging interface to avoid external dependencies
/// </summary>
public interface ILogger
{
    void LogDebug(string message);
    void LogInformation(string message);
    void LogWarning(string message);
    void LogError(string message);
    void LogError(Exception exception, string message);
    void LogFatal(string message);
    void LogFatal(Exception exception, string message);
}

/// <summary>
/// File-based logger implementation without external dependencies
/// </summary>
public class FileLogger : ILogger
{
    private readonly string _logFilePath;
    private readonly object _lockObject = new object();
    private static FileLogger? _instance;

    private FileLogger(string logFilePath)
    {
        _logFilePath = logFilePath;
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Log initialization
        WriteLog("INFO", $"Logger initialized. Log file: {_logFilePath}");
        
        // Clean up old log files (keep only last 30 days)
        CleanupOldLogFiles(directory);
    }

    public static FileLogger Instance
    {
        get
        {
            if (_instance == null)
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var logDirectory = Path.Combine(appDataPath, "RevitTestRunner", "Logs");
                var logFileName = $"RevitTestFramework.Common-{DateTime.Now:yyyyMMdd}.log";
                var logFilePath = Path.Combine(logDirectory, logFileName);
                
                _instance = new FileLogger(logFilePath);
            }
            return _instance;
        }
    }

    public void LogDebug(string message) => WriteLog("DEBUG", message);
    public void LogInformation(string message) => WriteLog("INFO", message);
    public void LogWarning(string message) => WriteLog("WARN", message);
    public void LogError(string message) => WriteLog("ERROR", message);
    public void LogError(Exception exception, string message) => WriteLog("ERROR", $"{message}{Environment.NewLine}{exception}");
    public void LogFatal(string message) => WriteLog("FATAL", message);
    public void LogFatal(Exception exception, string message) => WriteLog("FATAL", $"{message}{Environment.NewLine}{exception}");

    private void WriteLog(string level, string message)
    {
        try
        {
            lock (_lockObject)
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
                var processId = Environment.ProcessId;
                var threadId = Environment.CurrentManagedThreadId;
                var logEntry = $"{timestamp} [{level}] [PID:{processId}] [TID:{threadId}] {message}{Environment.NewLine}";
                
                File.AppendAllText(_logFilePath, logEntry);
                
                // Also write to Debug output as fallback
                Debug.WriteLine($"[{level}] {message}");
            }
        }
        catch (Exception ex)
        {
            // Fallback to Debug/Trace if file writing fails
            Debug.WriteLine($"Logging failed: {ex.Message}");
            Debug.WriteLine($"Original message: [{level}] {message}");
        }
    }

    private void CleanupOldLogFiles(string? logDirectory)
    {
        try
        {
            if (string.IsNullOrEmpty(logDirectory) || !Directory.Exists(logDirectory))
                return;

            var cutoffDate = DateTime.Now.AddDays(-30);
            var logFiles = Directory.GetFiles(logDirectory, "RevitTestFramework.Common-*.log");
            
            foreach (var logFile in logFiles)
            {
                var fileInfo = new FileInfo(logFile);
                if (fileInfo.CreationTime < cutoffDate)
                {
                    try
                    {
                        File.Delete(logFile);
                        Debug.WriteLine($"Deleted old log file: {logFile}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to delete old log file {logFile}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during log file cleanup: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a logger with context (for compatibility with existing code)
    /// </summary>
    public static ILogger ForContext<T>() => Instance;
    
    /// <summary>
    /// Creates a logger with context (for compatibility with existing code)
    /// </summary>
    public static ILogger ForContext(Type type) => Instance;
}

/// <summary>
/// A pipe-aware logger that forwards log messages through a StreamWriter in addition to file logging
/// </summary>
public class PipeAwareLogger : ILogger
{
    private readonly ILogger _fileLogger;
    private readonly StreamWriter? _pipeWriter;
    private readonly string? _source;
    private readonly object _lockObject = new object();

    public PipeAwareLogger(ILogger fileLogger, StreamWriter? pipeWriter, string? source = null)
    {
        _fileLogger = fileLogger ?? throw new ArgumentNullException(nameof(fileLogger));
        _pipeWriter = pipeWriter;
        _source = source;
    }

    public void LogDebug(string message) => LogWithLevel("DEBUG", message);
    public void LogInformation(string message) => LogWithLevel("INFO", message);
    public void LogWarning(string message) => LogWithLevel("WARN", message);
    public void LogError(string message) => LogWithLevel("ERROR", message);
    public void LogError(Exception exception, string message) => LogWithLevel("ERROR", $"{message}{Environment.NewLine}{exception}");
    public void LogFatal(string message) => LogWithLevel("FATAL", message);
    public void LogFatal(Exception exception, string message) => LogWithLevel("FATAL", $"{message}{Environment.NewLine}{exception}");

    private void LogWithLevel(string level, string message)
    {
        // Always log to file first
        switch (level)
        {
            case "DEBUG":
                _fileLogger.LogDebug(message);
                break;
            case "INFO":
                _fileLogger.LogInformation(message);
                break;
            case "WARN":
                _fileLogger.LogWarning(message);
                break;
            case "ERROR":
                _fileLogger.LogError(message);
                break;
            case "FATAL":
                _fileLogger.LogFatal(message);
                break;
        }

        // Forward to pipe if available
        ForwardToPipe(level, message);
    }

    private void ForwardToPipe(string level, string message)
    {
        if (_pipeWriter == null) return;

        // Don't forward DEBUG level messages through the pipe - keep them file-only
        if (level.Equals("DEBUG", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            lock (_lockObject)
            {
                var logMessage = new PipeLogMessage
                {
                    Type = "LOG",
                    Level = level,
                    Message = message,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"),
                    Source = _source
                };

                var json = JsonSerializer.Serialize(logMessage);
                _pipeWriter.WriteLine(json);
                _pipeWriter.Flush();
            }
        }
        catch (Exception ex)
        {
            // If pipe writing fails, fall back to Debug output
            Debug.WriteLine($"Failed to forward log to pipe: {ex.Message}");
            Debug.WriteLine($"Original log message: [{level}] {message}");
        }
    }

    /// <summary>
    /// Creates a pipe-aware logger with context
    /// </summary>
    public static PipeAwareLogger ForContext<T>(StreamWriter? pipeWriter) => 
        new PipeAwareLogger(FileLogger.Instance, pipeWriter, typeof(T).Name);
    
    /// <summary>
    /// Creates a pipe-aware logger with context
    /// </summary>
    public static PipeAwareLogger ForContext(Type type, StreamWriter? pipeWriter) => 
        new PipeAwareLogger(FileLogger.Instance, pipeWriter, type.Name);
}

/// <summary>
/// Represents a log message sent through the pipe
/// </summary>
public class PipeLogMessage
{
    public string Type { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string? Source { get; set; }
}