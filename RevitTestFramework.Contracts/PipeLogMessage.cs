namespace RevitTestFramework.Contracts;

/// <summary>
/// Message sent from Revit to report log entries back to the test framework
/// </summary>
public record PipeLogMessage
{
    public required string Type { get; init; } // "LOG"
    public required string Level { get; init; } // "DEBUG", "INFO", "WARN", "ERROR", "FATAL"
    public required string Message { get; init; }
    public required string Timestamp { get; init; }
    public string? Source { get; init; } // Optional source/context information
}