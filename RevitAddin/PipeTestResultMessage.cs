namespace RevitAddin;

public class PipeTestResultMessage
{
    public string Name { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public double Duration { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorStackTrace { get; set; }
}
