using System.IO.Pipes;
using System.Text.Json;

namespace RevitAddin
{
    public class PipeCommand
    {
        public string Command { get; set; } = string.Empty;
        public string TestAssembly { get; set; } = string.Empty;
        public string[] TestMethods { get; set; } = System.Array.Empty<string>();
    }

    public static class PipeClient
    {
        public static string RunTests(string pipeName, string assemblyPath, string[]? methods)
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
            client.Connect();
            var command = new PipeCommand
            {
                Command = "RunTests",
                TestAssembly = assemblyPath,
                TestMethods = methods ?? System.Array.Empty<string>()
            };
            var json = JsonSerializer.Serialize(command);
            using var writer = new StreamWriter(client, leaveOpen: true);
            writer.WriteLine(json);
            writer.Flush();
            using var reader = new StreamReader(client);
            var resultPath = reader.ReadLine() ?? string.Empty;
            return resultPath;
        }
    }
}
