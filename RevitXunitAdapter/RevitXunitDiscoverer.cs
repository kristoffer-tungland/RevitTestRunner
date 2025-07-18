using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using RevitTestFramework.Xunit;
using System.Reflection;
using System.Linq;

namespace RevitXunitAdapter
{
    [FileExtension(".dll")]
    [DefaultExecutorUri("executor://RevitXunitExecutor")]
    public class RevitXunitDiscoverer : ITestDiscoverer
    {
        private static readonly string[] SystemMethodNames = { "GetType", "ToString", "Equals", "GetHashCode", "Finalize", "MemberwiseClone" };

        public void DiscoverTests(IEnumerable<string> sources, IDiscoveryContext discoveryContext, IMessageLogger logger, ITestCaseDiscoverySink discoverySink)
        {
            logger?.SendMessage(TestMessageLevel.Informational, "RevitXunitDiscoverer: Starting test discovery");
            
            try
            {
                int totalTestsFound = 0;
                int assembliesProcessed = 0;
                
                foreach (var source in sources)
                {
                    logger?.SendMessage(TestMessageLevel.Informational, $"RevitXunitDiscoverer: Processing assembly: {Path.GetFileName(source)}");
                    
                    if (!File.Exists(source))
                    {
                        logger?.SendMessage(TestMessageLevel.Warning, $"RevitXunitDiscoverer: Assembly not found: {source}");
                        continue;
                    }

                    try
                    {
                        var assembly = Assembly.LoadFrom(source);
                        int testsInAssembly = DiscoverTestsInAssembly(assembly, source, logger, discoverySink);
                        totalTestsFound += testsInAssembly;
                        assembliesProcessed++;
                        
                        if (testsInAssembly > 0)
                        {
                            logger?.SendMessage(TestMessageLevel.Informational, $"RevitXunitDiscoverer: Found {testsInAssembly} test(s) in {assembly.GetName().Name}");
                        }
                        else
                        {
                            logger?.SendMessage(TestMessageLevel.Informational, $"RevitXunitDiscoverer: No tests found in {assembly.GetName().Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.SendMessage(TestMessageLevel.Error, $"RevitXunitDiscoverer: Failed to load assembly {Path.GetFileName(source)}: {ex.Message}");
                    }
                }
                
                logger?.SendMessage(TestMessageLevel.Informational, $"RevitXunitDiscoverer: Discovery completed - {totalTestsFound} test(s) found across {assembliesProcessed} assemblies");
            }
            catch (Exception ex)
            {
                logger?.SendMessage(TestMessageLevel.Error, $"RevitXunitDiscoverer: Unexpected error during discovery: {ex.Message}");
            }
        }

        private int DiscoverTestsInAssembly(Assembly assembly, string source, IMessageLogger? logger, ITestCaseDiscoverySink discoverySink)
        {
            int testCount = 0;
            var relevantTypes = assembly.GetTypes()
                .Where(t => !t.IsAbstract && t.IsClass && t.IsPublic)
                .Where(t => HasTestMethods(t))
                .ToList();
            
            if (relevantTypes.Count == 0)
            {
                return 0;
            }

            logger?.SendMessage(TestMessageLevel.Informational, $"RevitXunitDiscoverer: Scanning {relevantTypes.Count} test class(es) for tests");
            
            foreach (var type in relevantTypes)
            {
                var testMethodsInType = 0;
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    .Where(m => !SystemMethodNames.Contains(m.Name) && !m.IsSpecialName)
                    .ToList();
                
                foreach (var method in methods)
                {
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
                                logger?.SendMessage(TestMessageLevel.Informational, $"RevitXunitDiscoverer: Test '{method.Name}' uses model: {Path.GetFileName(revitTestAttr.LocalPath)}");
                            }
                            else if (!string.IsNullOrEmpty(revitTestAttr.ProjectGuid) || !string.IsNullOrEmpty(revitTestAttr.ModelGuid))
                            {
                                logger?.SendMessage(TestMessageLevel.Informational, $"RevitXunitDiscoverer: Test '{method.Name}' uses project/model GUIDs");
                            }
                        }
                        
                        discoverySink.SendTestCase(tc);
                        testCount++;
                        testMethodsInType++;
                    }
                }
                
                if (testMethodsInType > 0)
                {
                    logger?.SendMessage(TestMessageLevel.Informational, $"RevitXunitDiscoverer: Found {testMethodsInType} test method(s) in {type.Name}");
                }
            }
            
            return testCount;
        }

        private static bool HasTestMethods(Type type)
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Any(m => IsTestMethod(m));
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
