using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WolfConquer.Core.Runtime;
using Xunit.Abstractions;

namespace WolfConquer.Tests;

// Actual console initialization, gate mutations and all native method calls stay
// in a fresh measured child. No scenario starts a ThreadItem worker.
internal static class LegacyNativeSmartThreadQueueChildFixture
{
    private const string Marker = "__NATIVE_SMART_QUEUE__";

    internal static JsonElement Run(string scenario, ITestOutputHelper output)
    {
        Assert.True(OperatingSystem.IsWindows());
        var executable = Environment.GetEnvironmentVariable(LegacyNativeConsoleChildFixture.PwshPathVariable);
        var expectedExecutableHash = Environment.GetEnvironmentVariable(LegacyNativeConsoleChildFixture.PwshHashVariable);
        Assert.False(string.IsNullOrEmpty(executable), "Root must supply the measured PS7 executable path.");
        Assert.False(string.IsNullOrEmpty(expectedExecutableHash), "Root must supply its measured SHA256.");
        Assert.True(Path.IsPathFullyQualified(executable!));
        Assert.Equal(7, FileVersionInfo.GetVersionInfo(executable!).ProductMajorPart);
        var core = typeof(LegacyNativeThreadItem).Assembly.Location;
        var tests = typeof(LegacyNativeSmartThreadQueueChildScenarios).Assembly.Location;
        using var executableLease = new FileStream(executable!, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var coreLease = new FileStream(core, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var testsLease = new FileStream(tests, FileMode.Open, FileAccess.Read, FileShare.Read);
        Assert.Equal(expectedExecutableHash, Hash(executableLease));
        var coreHash = Hash(coreLease);
        var testsHash = Hash(testsLease);
        var guard = Guid.NewGuid().ToString("N");
        var info = new ProcessStartInfo(executable!)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(ChildScript));
        foreach (var argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand", encoded })
            info.ArgumentList.Add(argument);
        var tokens = new[] { executable! }.Concat(info.ArgumentList).ToArray();
        Assert.All(tokens, token => Assert.False(token.Contains('"') || token.EndsWith('\\')));
        Assert.True(string.Join(" ", tokens.Select(token => "\"" + token + "\"")).Length + 1 < 32767);
        info.Environment["WOLFCONQUER_SMART_QUEUE_CORE"] = core;
        info.Environment["WOLFCONQUER_SMART_QUEUE_CORE_SHA256"] = coreHash;
        info.Environment["WOLFCONQUER_SMART_QUEUE_TESTS"] = tests;
        info.Environment["WOLFCONQUER_SMART_QUEUE_TESTS_SHA256"] = testsHash;
        info.Environment["WOLFCONQUER_SMART_QUEUE_SCENARIO"] = scenario;
        info.Environment["WOLFCONQUER_SMART_QUEUE_GUARD"] = guard;

        using var process = new Process { StartInfo = info };
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();
        Task? stdoutCopy = null;
        Task? stderrCopy = null;
        var started = false;
        var timedOut = false;
        try
        {
            Assert.True(process.Start());
            started = true;
            _ = process.SafeHandle;
            stdoutCopy = process.StandardOutput.BaseStream.CopyToAsync(stdout);
            stderrCopy = process.StandardError.BaseStream.CopyToAsync(stderr);
            process.StandardInput.Close();
            timedOut = !process.WaitForExit(30000);
        }
        finally
        {
            if (started)
            {
                // The exact creator-owned process handle is retained. Kill only
                // this fresh owned child if a regression prevents actual exit.
                stdoutCopy ??= process.StandardOutput.BaseStream.CopyToAsync(stdout);
                stderrCopy ??= process.StandardError.BaseStream.CopyToAsync(stderr);
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
                process.WaitForExit();
                Task.WhenAll(stdoutCopy, stderrCopy).GetAwaiter().GetResult();
            }
        }
        var stdoutBytes = stdout.ToArray();
        var stderrBytes = stderr.ToArray();
        output.WriteLine($"scenario={scenario}; pid={process.Id}; actualExit={process.ExitCode}; timeout={timedOut}; stdoutBytes={stdoutBytes.Length}; stderrBytes={stderrBytes.Length}");
        if (process.ExitCode != 0 || stderrBytes.Length != 0)
            output.WriteLine(new UTF8Encoding(false, true).GetString(stderrBytes));
        Assert.False(timedOut, "Owned child did not finish within 30 seconds and was terminated.");
        Assert.Equal(0, process.ExitCode);
        Assert.Empty(stderrBytes);
        var text = new UTF8Encoding(false, true).GetString(stdoutBytes);
        Assert.StartsWith(Marker, text, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(text[Marker.Length..].TrimEnd('\r', '\n'));
        var result = document.RootElement.Clone();
        Assert.Equal(guard, result.GetProperty("Guard").GetString());
        Assert.Equal(scenario, result.GetProperty("Scenario").GetString());
        Assert.Equal(coreHash, result.GetProperty("CoreSha256").GetString());
        Assert.Equal(testsHash, result.GetProperty("TestsSha256").GetString());
        Assert.False(string.IsNullOrEmpty(result.GetProperty("FrameworkDescription").GetString()));
        return result;
    }

    private static string Hash(Stream stream)
    {
        var result = Convert.ToHexString(SHA256.HashData(stream));
        stream.Position = 0;
        return result;
    }

    private const string ChildScript = """
        $ErrorActionPreference = 'Stop'
        Set-StrictMode -Version Latest
        $guard = [Environment]::GetEnvironmentVariable('WOLFCONQUER_SMART_QUEUE_GUARD')
        if ($guard -notmatch '^[0-9a-f]{32}$') { throw 'Missing fresh child guard' }
        $core = [Environment]::GetEnvironmentVariable('WOLFCONQUER_SMART_QUEUE_CORE')
        $coreHash = [Environment]::GetEnvironmentVariable('WOLFCONQUER_SMART_QUEUE_CORE_SHA256')
        $tests = [Environment]::GetEnvironmentVariable('WOLFCONQUER_SMART_QUEUE_TESTS')
        $testsHash = [Environment]::GetEnvironmentVariable('WOLFCONQUER_SMART_QUEUE_TESTS_SHA256')
        $scenario = [Environment]::GetEnvironmentVariable('WOLFCONQUER_SMART_QUEUE_SCENARIO')
        foreach ($pair in @(@($core, $coreHash), @($tests, $testsHash))) {
            $actual = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($pair[0])))
            if ($actual -cne $pair[1]) { throw 'Held assembly identity mismatch' }
        }
        $null = [Reflection.Assembly]::LoadFrom($core)
        $assembly = [Reflection.Assembly]::LoadFrom($tests)
        $type = $assembly.GetType('WolfConquer.Tests.LegacyNativeSmartThreadQueueChildScenarios', $true)
        $method = $type.GetMethod('Execute', [Reflection.BindingFlags]'Public,Static')
        $json = [string]$method.Invoke($null, [object[]]@($scenario, $guard, $coreHash, $testsHash))
        [Console]::Out.WriteLine('__NATIVE_SMART_QUEUE__' + $json)
        """;
}
