namespace RevitAdapterCommon;

public class PipeCommand
{
    public string Command { get; set; } = string.Empty;
    public string TestAssembly { get; set; } = string.Empty;
    public string[] TestMethods { get; set; } = [];
    public string CancelPipe { get; set; } = string.Empty;
}
