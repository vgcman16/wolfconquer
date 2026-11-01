using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ProtoBuf.Meta;
using Xunit.Abstractions;

namespace WolfConquer.Tests;

// Every scenario owns a real new process; Default is never reset or replaced.
internal static class LegacyNativeCombatGearLoadChildFixture
{
    private const string Marker = "__NATIVE_GEAR_LOAD__";
    private const string ProtobufSha256 = "66653D787AB796A55C55AFDFA35D2FD9AD78C89BBD51E0D01965214399FA56B6";

    internal static JsonElement Run(string scenario, ITestOutputHelper output, bool objectPacket = false, string? knownEntry = null)
        => Run(scenario, output, objectPacket, knownEntry, null);

    internal static JsonElement Run(string scenario, ITestOutputHelper output, bool objectPacket, string? knownEntry,
        string? ordinaryOwnedRoot)
    {
        Assert.True(OperatingSystem.IsWindows());
        var executable = Environment.GetEnvironmentVariable(LegacyNativeConsoleChildFixture.PwshPathVariable);
        var executableHash = Environment.GetEnvironmentVariable(LegacyNativeConsoleChildFixture.PwshHashVariable);
        Assert.False(string.IsNullOrEmpty(executable), "Root must bind the measured PS7 executable path.");
        Assert.False(string.IsNullOrEmpty(executableHash), "Root must bind its measured SHA256.");
        Assert.True(Path.IsPathFullyQualified(executable!));
        Assert.Equal(7, FileVersionInfo.GetVersionInfo(executable!).ProductMajorPart);
        var core = typeof(WolfConquer.Core.Data.LegacyNativeCombatGearTable).Assembly.Location;
        var tests = typeof(LegacyNativeCombatGearLoadChildScenarios).Assembly.Location;
        var protobuf = typeof(RuntimeTypeModel).Assembly.Location;
        var paths = new[] { executable!, core, tests, protobuf };
        foreach (var path in paths) LegacyNativeDbReadOwnedFiles.OrdinaryChain(path);
        using var executableLease = File.Open(executable!, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var coreLease = File.Open(core, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var testsLease = File.Open(tests, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var protobufLease = File.Open(protobuf, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hashes = new[] { Hash(executableLease), Hash(coreLease), Hash(testsLease), Hash(protobufLease) };
        Assert.Equal(executableHash, hashes[0]);
        Assert.Equal(ProtobufSha256, hashes[3]);
        var guard = Guid.NewGuid().ToString("N");
        var start = new ProcessStartInfo(executable!)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand",
            Convert.ToBase64String(Encoding.Unicode.GetBytes(Script)) }) start.ArgumentList.Add(arg);
        Assert.True(string.Join(" ", new[] { executable! }.Concat(start.ArgumentList).Select(arg => "\"" + arg + "\"")).Length + 1 < 32767);
        for (int i = 1; i < paths.Length; i++)
        {
            start.Environment["WOLF_GEAR_LOAD_FILE_" + i] = paths[i];
            start.Environment["WOLF_GEAR_LOAD_HASH_" + i] = hashes[i];
        }
        start.Environment["WOLF_GEAR_LOAD_GUARD"] = guard;
        start.Environment["WOLF_GEAR_LOAD_SCENARIO"] = scenario;
        start.Environment["WOLF_GEAR_LOAD_ENTRY_METHOD"] = knownEntry ?? (objectPacket ? "ExecuteObjectPacket" : "Execute");
        if (ordinaryOwnedRoot is not null)
        {
            Assert.Equal("ExecuteOrdinaryAdmission", knownEntry);
            LegacyNativeHairfaceStorageLoginTests.RequireOrdinaryRestartOwnedRoot(ordinaryOwnedRoot);
            start.Environment["WOLF_ORDINARY_RESTART_ROOT"] = ordinaryOwnedRoot;
        }
        using var process = new Process { StartInfo = start };
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();
        Task? outCopy = null, errCopy = null;
        bool started = false;
        try
        {
            Assert.True(process.Start());
            started = true;
            _ = process.SafeHandle;
            outCopy = process.StandardOutput.BaseStream.CopyToAsync(stdout);
            errCopy = process.StandardError.BaseStream.CopyToAsync(stderr);
            process.StandardInput.Close();
            process.WaitForExit();
        }
        finally
        {
            if (started)
            {
                outCopy ??= process.StandardOutput.BaseStream.CopyToAsync(stdout);
                errCopy ??= process.StandardError.BaseStream.CopyToAsync(stderr);
                process.WaitForExit();
                Task.WhenAll(outCopy, errCopy).GetAwaiter().GetResult();
            }
        }
        output.WriteLine($"Scenario={scenario}; native PID={process.Id}; actual exit={process.ExitCode}; creator-owned handle retained through exit.");
        string Decode(byte[] bytes, string stream)
        {
            try { return new UTF8Encoding(false, true).GetString(bytes); }
            catch (DecoderFallbackException failure)
            {
                int index = Math.Clamp(failure.Index, 0, bytes.Length);
                int start = Math.Max(0, index - 32);
                int length = Math.Min(bytes.Length - start, 96);
                output.WriteLine($"Invalid UTF-8 in {stream}: length={bytes.Length}; index={failure.Index}; bytes[{start}..{start + length})=\\x" +
                    string.Join("\\x", bytes.AsSpan(start, length).ToArray().Select(value => value.ToString("X2"))));
                throw;
            }
        }
        string text = Decode(stdout.ToArray(), "stdout");
        string error = Decode(stderr.ToArray(), "stderr");
        output.WriteLine(text);
        if (error.Length != 0) output.WriteLine(error);
        Assert.Equal(0, process.ExitCode);
        Assert.Equal("", error);
        Assert.StartsWith(Marker, text, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(text[Marker.Length..].TrimEnd('\r', '\n'));
        var result = document.RootElement.Clone();
        Assert.Equal(guard, result.GetProperty("Guard").GetString());
        Assert.Equal(scenario, result.GetProperty("Scenario").GetString());
        Assert.True(result.GetProperty("Completed").GetBoolean());
        Assert.False(string.IsNullOrEmpty(result.GetProperty("FrameworkDescription").GetString()));
        return result.GetProperty("Data").Clone();
    }

    private static string Hash(Stream stream)
    {
        string result = Convert.ToHexString(SHA256.HashData(stream));
        stream.Position = 0;
        return result;
    }

    private const string Script = """
        $utf8=[Text.UTF8Encoding]::new($false,$true)
        [Console]::OutputEncoding=$utf8
        $OutputEncoding=$utf8
        $ErrorActionPreference='Stop'
        Set-StrictMode -Version Latest
        if ($PSVersionTable.PSVersion.Major -ne 7) { throw 'Measured PS7 required' }
        $guard=[Environment]::GetEnvironmentVariable('WOLF_GEAR_LOAD_GUARD')
        if ($guard -cnotmatch '\A[0-9a-f]{32}\z') { throw 'Fresh child guard required' }
        $paths=@{}
        foreach ($i in @(1,2,3)) {
            $path=[Environment]::GetEnvironmentVariable('WOLF_GEAR_LOAD_FILE_'+$i)
            $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($path)))
            if ($hash -cne [Environment]::GetEnvironmentVariable('WOLF_GEAR_LOAD_HASH_'+$i)) { throw 'Held assembly identity mismatch' }
            $paths[$i]=$path
        }
        $null=[Reflection.Assembly]::LoadFrom($paths[3])
        $null=[Reflection.Assembly]::LoadFrom($paths[1])
        $assembly=[Reflection.Assembly]::LoadFrom($paths[2])
        $type=$assembly.GetType('WolfConquer.Tests.LegacyNativeCombatGearLoadChildScenarios',$true)
        $entry=[Environment]::GetEnvironmentVariable('WOLF_GEAR_LOAD_ENTRY_METHOD')
        if ($entry -cnotin @('Execute','ExecuteObjectPacket','ExecuteMailboxPackage','ExecutePirateRecovery','ExecuteOrdinaryAdmission')) { throw 'Known child entry required' }
        $method=$type.GetMethod($entry,[Reflection.BindingFlags]'Public,Static')
        $scenario=[Environment]::GetEnvironmentVariable('WOLF_GEAR_LOAD_SCENARIO')
        $json=[string]$method.Invoke($null,[object[]]@($scenario,$guard))
        [Console]::Out.WriteLine('__NATIVE_GEAR_LOAD__'+$json)
        """;
}
