namespace RevitAddin;

public class PipeCommand
{
    public string Command { get; set; } = string.Empty;
    public string TestAssembly { get; set; } = string.Empty;
    public string[] TestMethods { get; set; } = System.Array.Empty<string>();
}
