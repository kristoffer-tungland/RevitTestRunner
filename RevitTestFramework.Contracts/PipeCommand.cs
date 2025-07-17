namespace RevitTestFramework.Contracts;

public record PipeCommand
{
    public required string Command { get; init; }
    public required string TestAssembly { get; init; }
    public required string[] TestMethods { get; init; }
    public required string CancelPipe { get; init; }
}
