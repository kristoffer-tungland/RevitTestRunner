using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace RevitTestAdapter
{
    [FileExtension(".dll")]
    [DefaultExecutorUri("executor://RevitTestExecutor")]
    public class RevitTestDiscoverer : ITestDiscoverer
    {
        public void DiscoverTests(IEnumerable<string> sources, IDiscoveryContext discoveryContext, IMessageLogger logger, ITestCaseDiscoverySink discoverySink)
        {
            foreach (var source in sources)
            {
                var assembly = System.Reflection.Assembly.LoadFrom(source);
                foreach (var type in assembly.GetTypes())
                {
                    foreach (var method in type.GetMethods())
                    {
                        if (method.GetCustomAttributes(typeof(NUnit.Framework.TestAttribute), true).Any())
                        {
                            var tc = new TestCase($"{type.FullName}.{method.Name}", new Uri("executor://RevitTestExecutor"), source);
                            discoverySink.SendTestCase(tc);
                        }
                    }
                }
            }
        }
    }
}
