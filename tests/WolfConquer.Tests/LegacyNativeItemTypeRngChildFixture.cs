using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WolfConquer.Core.World;
using Xunit.Abstractions;

namespace WolfConquer.Tests;

// No CRT call occurs here: the only invocation is the fresh child's synchronous C# entry.
internal static class LegacyNativeItemTypeRngChildFixture
{
    private const string Marker = "__LEGACY_NATIVE_ITEMTYPE_RNG_CHILD_RESULT__";

    internal static JsonElement Run(string scenario, ITestOutputHelper output)
    {
        Assert.True(OperatingSystem.IsWindows(), "Mandatory physical CRT control requires Windows.");
        var executable = Environment.GetEnvironmentVariable(LegacyNativeConsoleChildFixture.PwshPathVariable);
        var expectedExecutableHash = Environment.GetEnvironmentVariable(LegacyNativeConsoleChildFixture.PwshHashVariable);
        Assert.False(string.IsNullOrEmpty(executable), "Root must bind a measured PS7 executable.");
        Assert.False(string.IsNullOrEmpty(expectedExecutableHash), "Root must bind its SHA256.");
        Assert.True(Path.IsPathFullyQualified(executable!));
        Assert.Equal(7, FileVersionInfo.GetVersionInfo(executable!).ProductMajorPart);

        var core = typeof(LegacyNativeBaseFunc).Assembly.Location;
        var tests = typeof(LegacyNativeItemTypeRngChildScenarios).Assembly.Location;
        using var executableLease = new FileStream(executable!, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var coreLease = new FileStream(core, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var testsLease = new FileStream(tests, FileMode.Open, FileAccess.Read, FileShare.Read);
        Assert.Equal(expectedExecutableHash, Hash(executableLease));
        var coreHash = Hash(coreLease);
        var testsHash = Hash(testsLease);
        var guard = Guid.NewGuid().ToString("N");
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(ChildScript));
        var info = new ProcessStartInfo(executable!)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand", encoded })
            info.ArgumentList.Add(argument);
        var tokens = new[] { executable! }.Concat(info.ArgumentList).ToArray();
        Assert.All(tokens, token => Assert.False(token.Contains('"') || token.EndsWith('\\')));
        var wholeQuotedBound = string.Join(" ", tokens.Select(token => "\"" + token + "\""));
        Assert.True(wholeQuotedBound.Length + 1 < 32767);
        info.Environment["WOLFCONQUER_ITEMTYPE_RNG_CORE"] = core;
        info.Environment["WOLFCONQUER_ITEMTYPE_RNG_CORE_SHA256"] = coreHash;
        info.Environment["WOLFCONQUER_ITEMTYPE_RNG_TESTS"] = tests;
        info.Environment["WOLFCONQUER_ITEMTYPE_RNG_TESTS_SHA256"] = testsHash;
        info.Environment["WOLFCONQUER_ITEMTYPE_RNG_SCENARIO"] = scenario;
        info.Environment["WOLFCONQUER_ITEMTYPE_RNG_GUARD"] = guard;

        using var process = new Process { StartInfo = info };
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();
        Task? stdoutCopy = null;
        Task? stderrCopy = null;
        var started = false;
        var pid = 0;
        long ticks = 0;
        try
        {
            Assert.True(process.Start());
            started = true;
            _ = process.SafeHandle;
            stdoutCopy = process.StandardOutput.BaseStream.CopyToAsync(stdout);
            stderrCopy = process.StandardError.BaseStream.CopyToAsync(stderr);
            process.StandardInput.Close();
            pid = process.Id;
            ticks = process.StartTime.ToUniversalTime().Ticks;
            process.WaitForExit();
            Task.WhenAll(stdoutCopy, stderrCopy).GetAwaiter().GetResult();
        }
        finally
        {
            if (started)
            {
                stdoutCopy ??= process.StandardOutput.BaseStream.CopyToAsync(stdout);
                stderrCopy ??= process.StandardError.BaseStream.CopyToAsync(stderr);
                process.WaitForExit();
                Task.WhenAll(stdoutCopy, stderrCopy).GetAwaiter().GetResult();
            }
        }

        var outBytes = stdout.ToArray();
        var errBytes = stderr.ToArray();
        output.WriteLine(JsonSerializer.Serialize(new
        {
            Scenario = scenario, NativePid = pid,
            CreatedTicksDecimal = ticks.ToString(CultureInfo.InvariantCulture),
            ActualOsExit = process.ExitCode,
            CreatorOwnedHandleRetainedThroughActualExit = true,
            AuthoredArgumentList = info.ArgumentList.ToArray(),
            WholeQuotedCliCharactersPlusNul = wholeQuotedBound.Length + 1,
            CoreSha256 = coreHash, TestsSha256 = testsHash,
            StdoutUtf8Base64 = Convert.ToBase64String(outBytes),
            StdoutSha256 = Convert.ToHexString(SHA256.HashData(outBytes)), StdoutBytes = outBytes.Length,
            StderrUtf8Base64 = Convert.ToBase64String(errBytes),
            StderrSha256 = Convert.ToHexString(SHA256.HashData(errBytes)), StderrBytes = errBytes.Length,
            ActualCimCommandLineClaimed = false,
        }));
        Assert.Equal(0, process.ExitCode);
        Assert.Empty(errBytes);
        var text = new UTF8Encoding(false, true).GetString(outBytes);
        Assert.StartsWith(Marker, text, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(text[Marker.Length..].TrimEnd('\r', '\n'));
        var result = document.RootElement.Clone();
        Assert.Equal(guard, result.GetProperty("Guard").GetString());
        Assert.Equal(scenario, result.GetProperty("Scenario").GetString());
        Assert.Equal(coreHash, result.GetProperty("CoreSha256").GetString());
        Assert.Equal(testsHash, result.GetProperty("TestsSha256").GetString());
        Assert.True(result.GetProperty("Completed").GetBoolean());
        Assert.True(result.GetProperty("SameSynchronousThread").GetBoolean());
        Assert.False(string.IsNullOrEmpty(result.GetProperty("ProcessArchitecture").GetString()));
        Assert.False(string.IsNullOrEmpty(result.GetProperty("FrameworkDescription").GetString()));
        return result.GetProperty("Data").Clone();
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
        $guard = [Environment]::GetEnvironmentVariable('WOLFCONQUER_ITEMTYPE_RNG_GUARD')
        if ($guard -notmatch '^[0-9a-f]{32}$') { throw 'Missing fresh child guard' }
        $core = [Environment]::GetEnvironmentVariable('WOLFCONQUER_ITEMTYPE_RNG_CORE')
        $coreHash = [Environment]::GetEnvironmentVariable('WOLFCONQUER_ITEMTYPE_RNG_CORE_SHA256')
        $tests = [Environment]::GetEnvironmentVariable('WOLFCONQUER_ITEMTYPE_RNG_TESTS')
        $testsHash = [Environment]::GetEnvironmentVariable('WOLFCONQUER_ITEMTYPE_RNG_TESTS_SHA256')
        $scenario = [Environment]::GetEnvironmentVariable('WOLFCONQUER_ITEMTYPE_RNG_SCENARIO')
        foreach ($pair in @(@($core, $coreHash), @($tests, $testsHash))) {
            $actual = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($pair[0])))
            if ($actual -cne $pair[1]) { throw 'Held assembly file identity mismatch' }
        }
        $null = [Reflection.Assembly]::LoadFrom($core)
        $assembly = [Reflection.Assembly]::LoadFrom($tests)
        $type = $assembly.GetType('WolfConquer.Tests.LegacyNativeItemTypeRngChildScenarios', $true)
        $method = $type.GetMethod('Execute', [Reflection.BindingFlags]'Public,Static')
        # One synchronous compiled call contains every seed/draw. No per-draw PowerShell dispatch.
        $json = [string]$method.Invoke($null, [object[]]@($scenario, $guard, $coreHash, $testsHash))
        [Console]::Out.WriteLine('__LEGACY_NATIVE_ITEMTYPE_RNG_CHILD_RESULT__' + $json)
        """;
}
