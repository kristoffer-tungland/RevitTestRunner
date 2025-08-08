using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RevitTestFramework.Common;
using Xunit;

namespace RevitTestFramework.Common.Tests
{
    public class AddinManifestToolTests
    {
        [Fact]
        public void GetOptionOrDefault_ReturnsValue_WhenKeyExists()
        {
            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "output", "C:/test/output" }
            };
            string result = GetOptionOrDefault(options, "output", "default");
            Assert.Equal("C:/test/output", result);
        }

        [Fact]
        public void GetOptionOrDefault_ReturnsDefault_WhenKeyMissing()
        {
            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string result = GetOptionOrDefault(options, "missing", "default");
            Assert.Equal("default", result);
        }

        [Fact]
        public void GetDefaultOutputDir_ReturnsExpectedPath()
        {
            string revitVersion = "2025";
            string expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Autodesk", "Revit", "Addins", revitVersion);
            string actual = GetDefaultOutputDir(revitVersion);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ExtractRevitVersionFromAssemblyVersion_StandardVersion()
        {
            string version = "2025.1.0";
            string result = ExtractRevitVersionFromAssemblyVersion(version);
            Assert.Equal("2025", result);
        }

        [Fact]
        public void ExtractRevitVersionFromAssemblyVersion_PreReleaseVersion()
        {
            string version = "2025.1.0-pullrequest0018.103";
            string result = ExtractRevitVersionFromAssemblyVersion(version);
            Assert.Equal("2025", result);
        }

        [Theory]
        [InlineData("2025.1.0-pullrequest0018.103", "2025.1.0.18103")]
        [InlineData("2025.1.0-pullrequest0018.109", "2025.1.0.18109")]
        [InlineData("2025.0.0-alpha.1", "2025.0.0.1")]
        [InlineData("2026.2.5-beta0042.999", "2026.2.5.42999")]
        [InlineData("2025.1.0", "2025.1.0.0")]
        [InlineData("2025.0.0", "2025.0.0.0")]
        [InlineData("", "2025.0.0.0")]
        public void NormalizeVersionForAssembly_ProducesExpectedResults(string input, string expected)
        {
            var actual = AddinManifestTool.NormalizeVersionForAssembly(input);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("2025.1.0-dev")] // "dev" has no numbers, uses hash
        [InlineData("2025.1.0-rc1.123.456")] // "1123456" > 65535, uses hash
        [InlineData("2025.1.0-feature123.456.789")] // Too large, will be hashed
        [InlineData("2025.1.0-build12345.67890")] // Too large, will be hashed
        public void NormalizeVersionForAssembly_HandlesSpecialCases(string input)
        {
            var actual = AddinManifestTool.NormalizeVersionForAssembly(input);
            
            // Verify it's a valid version format and the revision is within valid range
            var parts = actual.Split('.');
            Assert.Equal(4, parts.Length);
            Assert.Equal("2025", parts[0]);
            Assert.Equal("1", parts[1]);
            Assert.Equal("0", parts[2]);
            Assert.True(int.TryParse(parts[3], out int revision));
            Assert.True(revision >= 1 && revision <= 65535);
        }

        [Theory]
        [InlineData("2025.1.0-pullrequest0018.103", "2025.1.0.18103")]
        [InlineData("2025.1.0-pullrequest0018.109", "2025.1.0.18109")]
        [InlineData("2025.0.0-alpha.1", "2025.0.0.1")]
        [InlineData("2026.2.5-beta0042.999", "2026.2.5.42999")]
        [InlineData("2025.1.0", "2025.1.0")]
        [InlineData("2025.0.0", "2025.0.0")]
        public void NormalizeVersionForPipe_ProducesExpectedResults(string input, string expected)
        {
            var actual = RevitTestFramework.Contracts.PipeNaming.NormalizeVersionForPipe(input);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("2025.1.0-dev")] // "dev" has no numbers, uses hash
        [InlineData("2025.1.0-rc1.123.456")] // "1123456" > 65535, uses hash
        public void NormalizeVersionForPipe_HandlesSpecialCases(string input)
        {
            var actual = RevitTestFramework.Contracts.PipeNaming.NormalizeVersionForPipe(input);
            
            // Verify it's a valid version format and the revision is within valid range
            var parts = actual.Split('.');
            Assert.Equal(4, parts.Length);
            Assert.Equal("2025", parts[0]);
            Assert.Equal("1", parts[1]);
            Assert.Equal("0", parts[2]);
            Assert.True(int.TryParse(parts[3], out int revision));
            Assert.True(revision >= 1 && revision <= 65535);
        }

        // --- Copied logic from Program.cs for testability ---
        private static string GetOptionOrDefault(Dictionary<string, string> options, string key, string defaultValue)
        {
            return options.TryGetValue(key, out string? value) ? value : defaultValue;
        }

        private static string GetDefaultOutputDir(string revitVersion)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Autodesk", "Revit", "Addins", revitVersion);
        }

        private static string ExtractRevitVersionFromAssemblyVersion(string assemblyVersion)
        {
            string baseVersion = assemblyVersion.Split('-')[0];
            var parts = baseVersion.Split('.');
            return parts.Length > 0 ? parts[0] : "2025";
        }
    }
}