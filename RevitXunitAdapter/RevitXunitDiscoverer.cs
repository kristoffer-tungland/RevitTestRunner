using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using RevitTestFramework.Xunit;
using System.Diagnostics;
using System.Reflection;

namespace RevitXunitAdapter
{
    [FileExtension(".dll")]
    [DefaultExecutorUri("executor://RevitXunitExecutor")]
    public class RevitXunitDiscoverer : ITestDiscoverer
    {
        public void DiscoverTests(IEnumerable<string> sources, IDiscoveryContext discoveryContext, IMessageLogger logger, ITestCaseDiscoverySink discoverySink)
        {
            // Always log that we were called to verify the adapter is loading
            logger?.SendMessage(TestMessageLevel.Informational, "RevitXunitDiscoverer: ENTRY - Discoverer was called!");
            
            try
            {
                logger?.SendMessage(TestMessageLevel.Informational, "RevitXunitDiscoverer: Starting test discovery");
                
                foreach (var source in sources)
                {
                    logger?.SendMessage(TestMessageLevel.Informational, $"RevitXunitDiscoverer: Processing source {source}");
                    
                    if (!File.Exists(source))
                    {
                        logger?.SendMessage(TestMessageLevel.Warning, $"RevitXunitDiscoverer: Source file does not exist: {source}");
                        continue;
                    }

                    try
                    {
                        var assembly = Assembly.LoadFrom(source);
                        logger?.SendMessage(TestMessageLevel.Informational, $"RevitXunitDiscoverer: Loaded assembly {assembly.FullName}");
                        DiscoverTestsInAssembly(assembly, source, logger, discoverySink);
                    }
                    catch (Exception ex)
                    {
                        logger?.SendMessage(TestMessageLevel.Error, $"RevitXunitDiscoverer: Failed to load assembly {source}: {ex.Message}");
                    }
                }
                
                logger?.SendMessage(TestMessageLevel.Informational, "RevitXunitDiscoverer: Test discovery completed");
            }
            catch (Exception ex)
            {
                logger?.SendMessage(TestMessageLevel.Error, $"RevitXunitDiscoverer: Unexpected error during discovery: {ex}");
            }
        }

        private void DiscoverTestsInAssembly(Assembly assembly, string source, IMessageLogger? logger, ITestCaseDiscoverySink discoverySink)
        {
            logger?.SendMessage(TestMessageLevel.Informational, $"RevitXunitDiscoverer: Examining {assembly.GetTypes().Length} types in assembly");
            
            foreach (var type in assembly.GetTypes())
            {
                logger?.SendMessage(TestMessageLevel.Informational, $"RevitXunitDiscoverer: Examining type {type.FullName}");
                
                foreach (var method in type.GetMethods())
                {
                    logger?.SendMessage(TestMessageLevel.Informational, $"RevitXunitDiscoverer: Examining method {method.Name}");
                    
                    if (IsTestMethod(method))
                    {
                        var tc = new TestCase($"{type.FullName}.{method.Name}", new Uri("executor://RevitXunitExecutor"), source);
                        
                        // Add traits/properties if available
                        var revitTestAttr = method.GetCustomAttribute<RevitXunitTestModelAttribute>();
                        if (revitTestAttr != null)
                        {
                            tc.Traits.Add(new Trait("Category", "RevitTest"));
                            if (!string.IsNullOrEmpty(revitTestAttr.LocalPath))
                            {
                                tc.Traits.Add(new Trait("ModelPath", revitTestAttr.LocalPath));
                            }
                        }
                        
                        discoverySink.SendTestCase(tc);
                        logger?.SendMessage(TestMessageLevel.Informational, $"RevitXunitDiscoverer: Discovered test {tc.FullyQualifiedName}");
                    }
                }
            }
        }

        private static bool IsTestMethod(MethodInfo method)
        {
            var hasXunitFact = method.GetCustomAttributes(typeof(Xunit.FactAttribute), true).Any();
            var hasXunitTheory = method.GetCustomAttributes(typeof(Xunit.TheoryAttribute), true).Any();
            var hasRevitAttr = method.GetCustomAttributes(typeof(RevitXunitTestModelAttribute), true).Any();
            
            return hasXunitFact || hasXunitTheory || hasRevitAttr;
        }
    }
}
