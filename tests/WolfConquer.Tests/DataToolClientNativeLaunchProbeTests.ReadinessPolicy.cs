using System.Diagnostics;
using System.Text;

namespace WolfConquer.Tests;

public sealed partial class DataToolClientNativeLaunchProbeTests
{
    [Theory]
    [InlineData(0L, 1500, false)]
    [InlineData(3499L, 1500, false)]
    [InlineData(3500L, 1500, false)]
    [InlineData(3501L, 0, true)]
    [InlineData(5000L, 0, true)]
    [InlineData(5001L, 0, true)]
    [InlineData(long.MaxValue, 0, true)]
    public void ReadinessWindowPreservesFull1500InsideOriginal5000(long elapsed, int wait, bool timedOut)
    {
        var options = new global::ClientNativeLaunchProbeOptions("fixture", ".", [], 5000, 1500, true, [], 8, "ready");
        var result = global::Program.ResolveClientNativeLaunchReadyWindow(options, elapsed);
        Assert.True(result.Requested);
        Assert.True(result.Observed);
        Assert.Equal(timedOut, result.TimedOut);
        Assert.Equal(wait, result.WaitMilliseconds);
        Assert.Equal(timedOut, result.Error.Length != 0);
    }

    [Theory]
    [InlineData(4999L, 1, false)]
    [InlineData(5000L, 0, true)]
    public void ReadinessWithoutKillAfterUsesOnlyRemainingOverallBudget(long elapsed, int wait, bool timedOut)
    {
        var options = new global::ClientNativeLaunchProbeOptions("fixture", ".", [], 5000, null, true, [], 8, "ready");
        var result = global::Program.ResolveClientNativeLaunchReadyWindow(options, elapsed);
        Assert.Equal(wait, result.WaitMilliseconds);
        Assert.Equal(timedOut, result.TimedOut);
    }

    [Fact]
    public void ReadinessPolicyRejectsNegativeElapsedInsteadOfInventingMoreBudget()
    {
        var options = new global::ClientNativeLaunchProbeOptions("fixture", ".", [], 5000, 1500, true, [], 8, "ready");
        Assert.Throws<ArgumentOutOfRangeException>(() => global::Program.ResolveClientNativeLaunchReadyWindow(options, -1));
    }

    [Theory]
    [InlineData(1500, 1500)]
    [InlineData(1500, 2000)]
    public void ReadinessParserRequiresStartupBudgetInAdditionToKillWindow(int timeout, int killAfter)
    {
        Assert.Throws<ArgumentException>(() => global::Program.ResolveClientNativeLaunchProbeOptions(
            ["client-native-launch-probe", "fixture.exe", "--timeout-ms", timeout.ToString(),
                "--kill-after-ms", killAfter.ToString(), "--ready-file", "ready"]));
    }

    [Fact]
    public void ReadinessParserPreservesArgsBoundaryAndOldNoReadyDefaults()
    {
        var options = global::Program.ResolveClientNativeLaunchProbeOptions(
            ["client-native-launch-probe", "fixture.exe", "--ready-file", "marker", "--args", "--ready-file", "literal"]);
        Assert.Equal(Path.GetFullPath("marker"), options.ReadyFilePath);
        Assert.Equal(["--ready-file", "literal"], options.Arguments);
        var defaults = global::Program.ResolveClientNativeLaunchProbeOptions(["client-native-launch-probe", "fixture.exe"]);
        Assert.Null(defaults.ReadyFilePath);
        Assert.Equal(5000, defaults.TimeoutMs);
        Assert.Null(defaults.KillAfterMs);
        Assert.True(defaults.KillOnTimeout);
    }

    [Theory]
    [InlineData("missing", false)]
    [InlineData("valid", true)]
    [InlineData("wrong-pid", false)]
    [InlineData("incomplete", false)]
    [InlineData("oversized", false)]
    [InlineData("predates-process", false)]
    public async Task ReadinessMarkerRequiresCompleteBoundedFreshTargetPid(string mode, bool expected)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var tempRoot = CreateTempRoot();
        try
        {
            using var self = Process.GetCurrentProcess();
            var path = Path.Combine(tempRoot, "marker");
            var marker = "pid=" + self.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\nready\n";
            if (mode != "missing")
            {
                var contents = mode switch
                {
                    "wrong-pid" => "pid=0\nready\n",
                    "incomplete" => marker[..^1],
                    "oversized" => marker + new string('x', 65),
                    _ => marker,
                };
                await File.WriteAllBytesAsync(path, Encoding.UTF8.GetBytes(contents));
                if (mode == "predates-process")
                {
                    File.SetCreationTimeUtc(path, self.StartTime.ToUniversalTime().AddMinutes(-1));
                }
            }

            var observed = global::Program.TryReadClientNativeLaunchReadyMarker(
                path, self.Id, self.StartTime.ToUniversalTime(), out var error);
            Assert.Equal(expected, observed);
            if (!expected)
            {
                Assert.NotEmpty(error);
            }
            else
            {
                Assert.Empty(error);
            }
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }
}
