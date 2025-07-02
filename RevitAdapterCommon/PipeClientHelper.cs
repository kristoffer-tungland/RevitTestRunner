using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;

namespace RevitAdapterCommon;

public static class PipeClientHelper
{
    public const string PipeNamePrefix = "RevitTestPipe_";

    public static NamedPipeClientStream ConnectToRevit()
    {
        foreach (var proc in Process.GetProcessesByName("Revit"))
        {
            var pipeName = PipeNamePrefix + proc.Id;
            var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
            try
            {
                client.Connect(100);
                return client;
            }
            catch
            {
                client.Dispose();
            }
        }
        throw new InvalidOperationException("No Revit process with test pipe found.");
    }

    public static string SendCommand(object command)
    {
        using var client = ConnectToRevit();
        var json = JsonSerializer.Serialize(command);
        using var sw = new StreamWriter(client, leaveOpen: true);
        sw.WriteLine(json);
        sw.Flush();
        using var sr = new StreamReader(client);
        var result = sr.ReadLine() ?? string.Empty;
        return result;
    }

    public static void SendCommandStreaming(object command, Action<string> handleLine)
    {
        using var client = ConnectToRevit();
        var json = JsonSerializer.Serialize(command);
        using var sw = new StreamWriter(client, leaveOpen: true);
        sw.WriteLine(json);
        sw.Flush();
        using var sr = new StreamReader(client);
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            handleLine(line);
            if (line == "END")
                break;
        }
    }
}
