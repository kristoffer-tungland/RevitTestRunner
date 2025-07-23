namespace RevitTestFramework.Contracts;

public record PipeTestResultMessage
{
    public required string Name { get; init; }
    public required string Outcome { get; init; }
    public required double Duration { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorStackTrace { get; init; }
}
