using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed class RuntimeConsoleLogTests
{
    [Fact]
    public void ResolveFromEnvironmentUsesRoleFilesInsideConfiguredLogDirectory()
    {
        var previousDirectory = Environment.GetEnvironmentVariable(RuntimeConsoleLog.LogDirectoryEnvironmentVariable);
        var previousFile = Environment.GetEnvironmentVariable(RuntimeConsoleLog.LogFileEnvironmentVariable);
        var root = Path.Combine(Path.GetTempPath(), "wolf-runtime-log-" + Guid.NewGuid());
        try
        {
            Environment.SetEnvironmentVariable(RuntimeConsoleLog.LogDirectoryEnvironmentVariable, root);
            Environment.SetEnvironmentVariable(RuntimeConsoleLog.LogFileEnvironmentVariable, null);

            var paths = RuntimeConsoleLog.ResolveFromEnvironment("World:Server");

            Assert.NotNull(paths);
            Assert.Equal(Path.Combine(root, "World_Server.out.log"), paths.OutputPath);
            Assert.Equal(Path.Combine(root, "World_Server.err.log"), paths.ErrorPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable(RuntimeConsoleLog.LogDirectoryEnvironmentVariable, previousDirectory);
            Environment.SetEnvironmentVariable(RuntimeConsoleLog.LogFileEnvironmentVariable, previousFile);
        }
    }

    [Fact]
    public void ResolveFromEnvironmentLetsExplicitLogFileOverrideTheDirectory()
    {
        var previousDirectory = Environment.GetEnvironmentVariable(RuntimeConsoleLog.LogDirectoryEnvironmentVariable);
        var previousFile = Environment.GetEnvironmentVariable(RuntimeConsoleLog.LogFileEnvironmentVariable);
        var root = Path.Combine(Path.GetTempPath(), "wolf-runtime-log-" + Guid.NewGuid());
        var outputPath = Path.Combine(root, "combined.log");
        try
        {
            Environment.SetEnvironmentVariable(RuntimeConsoleLog.LogDirectoryEnvironmentVariable, root);
            Environment.SetEnvironmentVariable(RuntimeConsoleLog.LogFileEnvironmentVariable, outputPath);

            var paths = RuntimeConsoleLog.ResolveFromEnvironment("WorldServer");

            Assert.NotNull(paths);
            Assert.Equal(outputPath, paths.OutputPath);
            Assert.Equal(Path.Combine(root, "combined.err.log"), paths.ErrorPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable(RuntimeConsoleLog.LogDirectoryEnvironmentVariable, previousDirectory);
            Environment.SetEnvironmentVariable(RuntimeConsoleLog.LogFileEnvironmentVariable, previousFile);
        }
    }
}
