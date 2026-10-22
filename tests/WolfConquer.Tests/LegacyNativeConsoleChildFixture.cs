using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;

namespace WolfConquer.Tests;

// Test-only process ownership. No production callback or console substitute.
internal static class LegacyNativeConsoleChildFixture
{
    internal const string PwshPathVariable = "WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH";
    internal const string PwshHashVariable = "WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH_SHA256";
    private const string Marker = "__LEGACY_NATIVE_CONSOLE_CHILD_RESULT__";

    internal static JsonElement Run(string scenario, ITestOutputHelper output, int blankLines = 0)
    {
        Assert.True(OperatingSystem.IsWindows(), "This control requires actual Windows standard-handle APIs.");
        var executable = Environment.GetEnvironmentVariable(PwshPathVariable);
        var executableHash = Environment.GetEnvironmentVariable(PwshHashVariable);
        Assert.False(string.IsNullOrEmpty(executable), "Root must supply the measured PS7 executable path.");
        Assert.False(string.IsNullOrEmpty(executableHash), "Root must supply the measured PS7 executable SHA256.");
        Assert.True(Path.IsPathFullyQualified(executable!));
        Assert.True(File.Exists(executable));
        Assert.Equal(executableHash, Hash(File.ReadAllBytes(executable!)));

        var assembly = typeof(WolfConquer.Core.Runtime.LegacyNativeConsole).Assembly.Location;
        var assemblyHash = Hash(File.ReadAllBytes(assembly));
        var guard = Guid.NewGuid().ToString("N");
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(ChildScript));
        var start = new ProcessStartInfo(executable!)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand", encoded })
        {
            start.ArgumentList.Add(argument);
        }
        var commandTokens = new[] { executable! }.Concat(start.ArgumentList).ToArray();
        Assert.All(commandTokens, token => Assert.False(token.Contains('"') || token.EndsWith('\\')));
        var wholeQuotedBound = string.Join(" ", commandTokens.Select(token => "\"" + token + "\""));
        Assert.True(wholeQuotedBound.Length + 1 < 32767, "Bound the entire quoted child command including NUL.");
        start.Environment["WOLFCONQUER_CONSOLE_CHILD_ASSEMBLY"] = assembly;
        start.Environment["WOLFCONQUER_CONSOLE_CHILD_ASSEMBLY_SHA256"] = assemblyHash;
        start.Environment["WOLFCONQUER_CONSOLE_CHILD_SCENARIO"] = scenario;
        start.Environment["WOLFCONQUER_CONSOLE_CHILD_GUARD"] = guard;

        using var process = new Process { StartInfo = start };
        using var standardOutput = new MemoryStream();
        using var standardError = new MemoryStream();
        Task? outputCopy = null;
        Task? errorCopy = null;
        var started = false;
        int nativePid = 0;
        long createdTicks = 0;
        try
        {
            Assert.True(process.Start());
            started = true;
            // Acquire the creation-owned OS handle before reading optional metadata.
            _ = process.SafeHandle;
            outputCopy = process.StandardOutput.BaseStream.CopyToAsync(standardOutput);
            errorCopy = process.StandardError.BaseStream.CopyToAsync(standardError);
            process.StandardInput.Close();
            nativePid = process.Id;
            createdTicks = process.StartTime.ToUniversalTime().Ticks;
            process.WaitForExit();
            Task.WhenAll(outputCopy, errorCopy).GetAwaiter().GetResult();
        }
        finally
        {
            if (started)
            {
                // Never replace a pending read with a second read on the same pipe.
                outputCopy ??= process.StandardOutput.BaseStream.CopyToAsync(standardOutput);
                errorCopy ??= process.StandardError.BaseStream.CopyToAsync(standardError);
                process.WaitForExit();
                Task.WhenAll(outputCopy, errorCopy).GetAwaiter().GetResult();
            }
        }

        var stdoutBytes = standardOutput.ToArray();
        var stderrBytes = standardError.ToArray();
        var strictUtf8 = new UTF8Encoding(false, true);
        var stdout = strictUtf8.GetString(stdoutBytes);
        var stderr = strictUtf8.GetString(stderrBytes);
        output.WriteLine(JsonSerializer.Serialize(new
        {
            Scenario = scenario,
            NativePid = nativePid,
            CreatedTicksDecimal = createdTicks.ToString(CultureInfo.InvariantCulture),
            ActualOsExit = process.ExitCode,
            CreatorOwnedHandleRetainedThroughActualExit = true,
            AuthoredArgumentList = start.ArgumentList.ToArray(),
            QuotedCommandUpperBoundCharacters = wholeQuotedBound.Length,
            StdoutSha256 = Hash(stdoutBytes),
            StdoutBytes = stdoutBytes.Length,
            StderrSha256 = Hash(stderrBytes),
            StderrBytes = stderrBytes.Length,
            ActualCimCommandLineClaimed = false,
        }));
        Assert.Equal(0, process.ExitCode);
        Assert.Equal("", stderr);
        var prefix = string.Concat(Enumerable.Repeat(Environment.NewLine, blankLines));
        Assert.StartsWith(prefix + Marker, stdout, StringComparison.Ordinal);
        var json = stdout[(prefix.Length + Marker.Length)..].TrimEnd('\r', '\n');
        using var document = JsonDocument.Parse(json);
        var result = document.RootElement.Clone();
        Assert.Equal(guard, result.GetProperty("Guard").GetString());
        Assert.Equal(scenario, result.GetProperty("Scenario").GetString());
        Assert.Equal(assemblyHash, result.GetProperty("AssemblySha256").GetString());
        Assert.True(result.GetProperty("Completed").GetBoolean());
        return result;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private const string ChildScript = """
        $ErrorActionPreference = 'Stop'
        Set-StrictMode -Version Latest
        $guard = [Environment]::GetEnvironmentVariable('WOLFCONQUER_CONSOLE_CHILD_GUARD')
        if ($guard -notmatch '^[0-9a-f]{32}$') { throw 'Fresh child guard missing' }
        $path = [Environment]::GetEnvironmentVariable('WOLFCONQUER_CONSOLE_CHILD_ASSEMBLY')
        $expected = [Environment]::GetEnvironmentVariable('WOLFCONQUER_CONSOLE_CHILD_ASSEMBLY_SHA256')
        $scenario = [Environment]::GetEnvironmentVariable('WOLFCONQUER_CONSOLE_CHILD_SCENARIO')
        $assemblyBytes = [IO.File]::ReadAllBytes($path)
        $actual = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($assemblyBytes))
        if ($actual -cne $expected) { throw 'Child assembly identity mismatch' }
        $assembly = [Reflection.Assembly]::Load($assemblyBytes)
        $console = $assembly.GetType('WolfConquer.Core.Runtime.LegacyNativeConsole', $true)
        $kernel = $assembly.GetType('WolfConquer.Core.Runtime.LegacyNativeConsoleKernel32', $true)
        $public = [Reflection.BindingFlags]'Public,Static'
        $private = [Reflection.BindingFlags]'NonPublic,Static'
        $inputProperty = $console.GetProperty('InputHandle', $public)
        $outputProperty = $console.GetProperty('OutputHandle', $public)
        $getHandle = $kernel.GetMethod('GetStdHandle', $public)
        $writeNative = $kernel.GetMethod('WriteConsole', $public)
        $write = $console.GetMethod('Write', $public)
        $writeLine = $console.GetMethod('WriteLine', $public)
        $allowSource = $console.GetMethod('AllowSource', $public)
        # All actual native-console member invocation below occurs only in this child.
        $allowed = $allowSource.Invoke($null, [object[]]@())
        $input = [IntPtr]$inputProperty.GetValue($null)
        $output = [IntPtr]$outputProperty.GetValue($null)
        if ($input -eq [IntPtr]::Zero -or $input.ToInt64() -eq -1 -or
            $output -eq [IntPtr]::Zero -or $output.ToInt64() -eq -1) { throw 'Redirected pipe handles required' }
        $data = [ordered]@{
            Guard = $guard; Scenario = $scenario; AssemblySha256 = $actual
            Completed = $false; AllowSource = [bool]$allowed
            InputHandleDecimal = $input.ToInt64().ToString([Globalization.CultureInfo]::InvariantCulture)
            OutputHandleDecimal = $output.ToInt64().ToString([Globalization.CultureInfo]::InvariantCulture)
        }
        switch ($scenario) {
            'Initialization' {
                $counter = $console.GetField('ExceptionsCounter', $private).GetValue($null)
                $data.CounterCount = [uint32]$counter.Count
                $data.NativeInputMatches = ([IntPtr]$getHandle.Invoke($null, [object[]]@([int]-10))) -eq $input
                $data.NativeOutputMatches = ([IntPtr]$getHandle.Invoke($null, [object[]]@([int]-11))) -eq $output
            }
            'SameHandleSetters' {
                $inputProperty.SetValue($null, $input)
                $outputProperty.SetValue($null, $output)
                $data.InputMatches = ([IntPtr]$inputProperty.GetValue($null)) -eq $input -and
                    ([IntPtr]$getHandle.Invoke($null, [object[]]@([int]-10))) -eq $input
                $data.OutputMatches = ([IntPtr]$outputProperty.GetValue($null)) -eq $output -and
                    ([IntPtr]$getHandle.Invoke($null, [object[]]@([int]-11))) -eq $output
            }
            'ZeroHandleSetters' {
                try {
                    $inputProperty.SetValue($null, [IntPtr]::Zero)
                    $outputProperty.SetValue($null, [IntPtr]::Zero)
                    $data.StoredInputZero = ([IntPtr]$inputProperty.GetValue($null)) -eq [IntPtr]::Zero
                    $data.StoredOutputZero = ([IntPtr]$outputProperty.GetValue($null)) -eq [IntPtr]::Zero
                    $data.NativeInputZero = ([IntPtr]$getHandle.Invoke($null, [object[]]@([int]-10))) -eq [IntPtr]::Zero
                    $data.NativeOutputZero = ([IntPtr]$getHandle.Invoke($null, [object[]]@([int]-11))) -eq [IntPtr]::Zero
                }
                finally {
                    $inputProperty.SetValue($null, $input)
                    $outputProperty.SetValue($null, $output)
                }
            }
            'IgnoredPipeWriteResult' {
                [object[]]$arguments = @($output, 'pipe reference control', [uint32]22, [uint32]0, [IntPtr]::Zero)
                $data.NativeWriteReturned = [bool]$writeNative.Invoke($null, $arguments)
                $data.NativeWrittenCharacters = [uint32]$arguments[3]
                $null = $write.Invoke($null, [object[]]@('real native-owner pipe write'))
                $data.OwnerWriteReturnedNormally = $true
            }
            'BlankWriteLine' {
                $null = $writeLine.Invoke($null, [object[]]@('', [ConsoleColor]::Red))
                $null = $writeLine.Invoke($null, [object[]]@(' ', [ConsoleColor]::Blue))
                $data.BlankCalls = 2
            }
            'NullWriteLine' {
                try { $null = $writeLine.Invoke($null, [object[]]@($null, [ConsoleColor]::White)); throw 'Expected null fault' }
                catch {
                    $base = $_.Exception.GetBaseException()
                    if ($base -isnot [NullReferenceException]) { throw }
                    $data.ExceptionType = $base.GetType().FullName
                }
            }
            'NullWrite' {
                $null = $write.Invoke($null, [object[]]@($null))
                $data.NullWriteReturnedNormally = $true
            }
            default { throw 'Unknown isolated scenario' }
        }
        $data.Completed = $true
        [Console]::Out.WriteLine('__LEGACY_NATIVE_CONSOLE_CHILD_RESULT__' + ($data | ConvertTo-Json -Depth 5 -Compress))
        """;
}
