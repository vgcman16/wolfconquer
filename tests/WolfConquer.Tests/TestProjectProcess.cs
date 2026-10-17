using System.Diagnostics;

namespace WolfConquer.Tests;

internal static class TestProjectProcess
{
    public static Process Create(string repositoryRoot, string projectName, bool enableRaisingEvents = false)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = repositoryRoot,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = enableRaisingEvents,
        };

        process.StartInfo.ArgumentList.Add(GetBuiltAssemblyPath(repositoryRoot, projectName));
        process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        process.StartInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        return process;
    }

    public static Process CreateFromProjectPath(
        string repositoryRoot,
        string projectRelativePath,
        bool enableRaisingEvents = false)
    {
        var projectName = Path.GetFileNameWithoutExtension(projectRelativePath);
        return Create(repositoryRoot, projectName, enableRaisingEvents);
    }

    private static string GetBuiltAssemblyPath(string repositoryRoot, string projectName)
    {
        var outputDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        var coLocatedAssemblyPath = Path.Combine(outputDirectory.FullName, projectName + ".dll");
        if (File.Exists(coLocatedAssemblyPath))
        {
            return coLocatedAssemblyPath;
        }

        var testProjectRoot = Path.Combine(repositoryRoot, "tests", "WolfConquer.Tests");
        var projectOutputRoot = Path.Combine(repositoryRoot, "src", projectName);
        var isolatedOutputPath = TryGetMatchingProjectOutputPath(
            outputDirectory.FullName,
            testProjectRoot,
            projectOutputRoot,
            projectName);
        if (isolatedOutputPath is not null)
        {
            return isolatedOutputPath;
        }

        var targetFramework = outputDirectory.Name;
        var configuration = outputDirectory.Parent?.Name ?? "Debug";
        var assemblyPath = Path.Combine(
            repositoryRoot,
            "src",
            projectName,
            "bin",
            configuration,
            targetFramework,
            projectName + ".dll");

        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException(
                $"{projectName} has not been built for {configuration}/{targetFramework}. Build the test project or solution before running child-process tests.",
                assemblyPath);
        }

        return assemblyPath;
    }

    private static string? TryGetMatchingProjectOutputPath(
        string testOutputDirectory,
        string testProjectRoot,
        string projectOutputRoot,
        string projectName)
    {
        var relativeOutput = Path.GetRelativePath(testProjectRoot, testOutputDirectory);
        if (relativeOutput.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativeOutput))
        {
            return null;
        }

        var assemblyPath = Path.Combine(projectOutputRoot, relativeOutput, projectName + ".dll");
        return File.Exists(assemblyPath) ? assemblyPath : null;
    }
}
