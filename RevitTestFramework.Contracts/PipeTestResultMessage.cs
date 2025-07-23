namespace RevitTestFramework.Contracts;

public record PipeTestResultMessage
{
    public required string Name { get; init; }
    public required string Outcome { get; init; }
    public required double Duration { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorStackTrace { get; init; }
}

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