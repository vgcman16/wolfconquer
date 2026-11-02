using System.Runtime.CompilerServices;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937XuanbaoAdditionResolveInspectionCommandTests
{
    // Actual built DataTool process. No reflection or invocation of Program's private methods.
    private static async Task<Result> RunProcess(string workingDirectory, params string[] args)
    {
        using var process = TestProjectProcess.Create(Repository(), "WolfConquer.DataTool");
        process.StartInfo.WorkingDirectory = workingDirectory;
        foreach (var argument in args) process.StartInfo.ArgumentList.Add(argument);
        Assert.True(process.Start(), "Could not start the actual DataTool command.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException primary)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (Exception cleanup) { throw new AggregateException("CLI timed out and process cleanup failed.", primary, cleanup); }
            throw new TimeoutException("Owned Addition/Resolve command timed out.", primary);
        }
        return new(process.ExitCode, await output, await error);
    }

    private static string Repository([CallerFilePath] string source = "")
    {
        for (DirectoryInfo? directory = new(Path.GetDirectoryName(source)!); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln"))) return directory.FullName;
        throw new InvalidOperationException("Repository root missing.");
    }
}
