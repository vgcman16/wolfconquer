using System.Diagnostics;

namespace WolfConquer.Tests;

public sealed partial class SqlGameplayCheckToolsTests
{
    [SqlGameplayWindowsFact]
    public async Task FullRunnerRefusesAnActuallyHeldHostLeaseBeforeCreatingArtifacts()
    {
        var fullScript = Path.Combine(root, "tools", "dev", "Invoke-LocalCheck.ps1");
        File.Copy(FindTool("Invoke-LocalCheck.ps1"), fullScript);
        File.WriteAllText(Path.Combine(root, "WolfConquer.sln"), "synthetic; never build");
        File.WriteAllText(Path.Combine(root, "Directory.Build.targets"), "<Project />");
        var launcher = Path.Combine(root, "tests", "WolfConquer.Launcher.Tests", "WolfConquer.Launcher.Tests.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(launcher)!);
        File.WriteAllText(launcher, "<Project />");
        File.WriteAllText(Path.Combine(root, "tools", "dev", "CheckLogPump.psm1"), "# inert import only");
        var before = Snapshot();

        // Mutex ownership is thread-affine: acquisition, child wait and release
        // all run synchronously on one dedicated thread. If the enclosing real
        // full gate owns it already, the same actual host lease causes refusal.
        var result = await Task.Run(() =>
        {
            using var gate = new Mutex(false, @"Global\WolfConquer.FocusedChecks");
            bool owns;
            try { owns = gate.WaitOne(0); }
            catch (AbandonedMutexException) { owns = true; }
            try
            {
                var start = new ProcessStartInfo(PowerShellPath())
                { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = OtherDirectory,
                    RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File", fullScript,
                    "-RepositoryRoot", root, "-PowerShellPath", PowerShellPath(),
                    "-DotNetPath", Path.Combine(OtherDirectory, "dotnet.cmd") }) start.ArgumentList.Add(arg);
                using var child = Process.Start(start)!;
                var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
                if (!child.WaitForExit(30_000))
                { child.Kill(entireProcessTree: true); child.WaitForExit(); throw new TimeoutException("Full runner did not refuse the held lease."); }
                return new Result(child.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
            }
            finally { if (owns) gate.ReleaseMutex(); }
        });
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("host lease", result.Error, StringComparison.OrdinalIgnoreCase);
        AssertSnapshot(before);
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class SqlGameplayWindowsFactAttribute : FactAttribute
{
    public SqlGameplayWindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "Requires the actual Windows Global mutex contract; no refusal was exercised.";
    }
}
