using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;

namespace WolfConquer.Tests;

// Test-only process ownership. No production callback or console substitute.
internal static class LegacyNativeDbReadChildFixture
{
    internal const string PwshPathVariable = "WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH";
    internal const string PwshHashVariable = "WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH_SHA256";
    private const string Marker = "__LEGACY_NATIVE_DBREAD_RESULT__";

    internal static JsonElement Run(string scenario, ITestOutputHelper output)
    {
        Assert.True(OperatingSystem.IsWindows(), "This control requires actual Windows standard-handle APIs.");
        var executable = Environment.GetEnvironmentVariable(PwshPathVariable);
        var executableHash = Environment.GetEnvironmentVariable(PwshHashVariable);
        Assert.False(string.IsNullOrEmpty(executable), "Root must supply the measured PS7 executable path.");
        Assert.False(string.IsNullOrEmpty(executableHash), "Root must supply the measured PS7 executable SHA256.");
        Assert.True(Path.IsPathFullyQualified(executable!));
        Assert.True(File.Exists(executable));
        Assert.Equal(executableHash, Hash(File.ReadAllBytes(executable!)));
        LegacyNativeDbReadOwnedFiles.OrdinaryChain(executable!);
        var version = FileVersionInfo.GetVersionInfo(executable!);
        Assert.Equal(7, version.ProductMajorPart);
        Assert.Matches(@"\APowerShell(?: Core)?\z", version.ProductName!);

        var assembly = typeof(WolfConquer.Core.Data.LegacyNativeDbRead).Assembly.Location;
        var assemblyHash = Hash(File.ReadAllBytes(assembly));
        var guard = Guid.NewGuid().ToString("N");
        using var heldScript = HeldScript.Create(ChildScript);
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(BootstrapScript));
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
        start.Environment["WOLFCONQUER_DBREAD_CHILD_WORKSPACE"] = System.IO.Path.GetDirectoryName(LegacyNativeDbReadOwnedFiles.ArtifactsRoot())!;
        start.Environment["WOLFCONQUER_DBREAD_CHILD_ASSEMBLY"] = assembly;
        start.Environment["WOLFCONQUER_DBREAD_CHILD_ASSEMBLY_SHA256"] = assemblyHash;
        start.Environment["WOLFCONQUER_DBREAD_CHILD_SCENARIO"] = scenario;
        start.Environment["WOLFCONQUER_DBREAD_CHILD_GUARD"] = guard;
        start.Environment["WOLFCONQUER_DBREAD_CHILD_SCRIPT"] = heldScript.Path;
        start.Environment["WOLFCONQUER_DBREAD_CHILD_SCRIPT_SHA256"] = heldScript.Sha256;

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
            ChildJson = stdout,
            RuntimeProductVersion = version.ProductVersion,
            ScriptSha256 = heldScript.Sha256,
            ScriptHeldReadOnlyThroughActualExit = true,
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
        Assert.Equal(executableHash, Hash(File.ReadAllBytes(executable!)));
        Assert.Equal(assemblyHash, Hash(File.ReadAllBytes(assembly)));
        Assert.Equal(0, process.ExitCode);
        Assert.Equal("", stderr);
        Assert.StartsWith(Marker, stdout, StringComparison.Ordinal);
        var json = stdout[Marker.Length..].TrimEnd('\r', '\n');
        using var document = JsonDocument.Parse(json);
        var result = document.RootElement.Clone();
        Assert.Equal(guard, result.GetProperty("Guard").GetString());
        Assert.Equal(scenario, result.GetProperty("Scenario").GetString());
        Assert.Equal(assemblyHash, result.GetProperty("AssemblySha256").GetString());
        Assert.True(result.GetProperty("Completed").GetBoolean());
        return result;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private sealed class HeldScript : IDisposable
    {
        private readonly string directory;
        private readonly byte[] expected;
        private readonly FileStream lease;
        internal string Path { get; }
        internal string Sha256 => Hash(expected);

        private HeldScript(string directory, string path, byte[] expected, FileStream lease)
        {
            this.directory = directory;
            Path = path;
            this.expected = expected;
            this.lease = lease;
        }

        internal static HeldScript Create(string script)
        {
            var tempRoot = LegacyNativeDbReadOwnedFiles.ArtifactsRoot();
            var directory = System.IO.Path.Combine(tempRoot, "goal541-db-read-held-script-" + Guid.NewGuid().ToString("N"));
            Assert.False(Directory.Exists(directory));
            Directory.CreateDirectory(directory);
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(directory);
            var path = System.IO.Path.Combine(directory, "probe.ps1");
            var bytes = new UTF8Encoding(false, true).GetBytes(script);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(path);
            var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                Assert.Equal(bytes.Length, held.Length);
                var readback = new byte[bytes.Length];
                held.ReadExactly(readback);
                Assert.Equal(bytes, readback);
                held.Position = 0;
                return new HeldScript(directory, path, bytes, held);
            }
            catch
            {
                held.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            // Run's finally has retained/waited/drained the original child before this lease ends.
            lease.Dispose();
            var tempRoot = System.IO.Path.TrimEndingDirectorySeparator(
                LegacyNativeDbReadOwnedFiles.ArtifactsRoot()) + System.IO.Path.DirectorySeparatorChar;
            var resolvedDirectory = System.IO.Path.GetFullPath(directory);
            Assert.StartsWith(tempRoot, resolvedDirectory, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(System.IO.Path.Combine(resolvedDirectory, "probe.ps1"), System.IO.Path.GetFullPath(Path));
            Assert.False((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0);
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(Path);
            Assert.Single(Directory.GetFileSystemEntries(directory));
            Assert.Equal(expected, File.ReadAllBytes(Path));
            File.Delete(Path);
            Directory.Delete(directory, recursive: false);
        }
    }

    private const string BootstrapScript = """
        $ErrorActionPreference = 'Stop'
        $p = [Environment]::GetEnvironmentVariable('WOLFCONQUER_DBREAD_CHILD_SCRIPT')
        $b = [IO.File]::ReadAllBytes($p)
        $h = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($b))
        if ($h -cne [Environment]::GetEnvironmentVariable('WOLFCONQUER_DBREAD_CHILD_SCRIPT_SHA256')) { throw 'Held script mismatch' }
        & ([ScriptBlock]::Create([Text.UTF8Encoding]::new($false, $true).GetString($b)))
        """;

    private const string ChildScript = """
        $ErrorActionPreference = 'Stop'
        Set-StrictMode -Version Latest
        if ($PSVersionTable.PSVersion.Major -ne 7) { throw 'Actual PowerShell7 required' }
        $guard=[Environment]::GetEnvironmentVariable('WOLFCONQUER_DBREAD_CHILD_GUARD')
        if ($guard -cnotmatch '\A[0-9a-f]{32}\z') { throw 'Fresh child guard missing' }
        $path=[Environment]::GetEnvironmentVariable('WOLFCONQUER_DBREAD_CHILD_ASSEMBLY')
        $expected=[Environment]::GetEnvironmentVariable('WOLFCONQUER_DBREAD_CHILD_ASSEMBLY_SHA256')
        $scenario=[Environment]::GetEnvironmentVariable('WOLFCONQUER_DBREAD_CHILD_SCENARIO')
        $workspace=[Environment]::GetEnvironmentVariable('WOLFCONQUER_DBREAD_CHILD_WORKSPACE')
        if (![IO.Path]::IsPathFullyQualified($workspace) -or
            $workspace -cne [IO.Path]::GetFullPath($workspace) -or
            ![IO.File]::Exists([IO.Path]::Combine($workspace,'WolfConquer.sln'))) { throw 'Owned workspace required' }
        $bytes=[IO.File]::ReadAllBytes($path)
        $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
        if ($hash -cne $expected) { throw 'Actual Core identity mismatch' }
        $assembly=[Reflection.Assembly]::Load($bytes)
        $type=$assembly.GetType('WolfConquer.Core.Data.LegacyNativeDbRead',$true)
        $flags=[Reflection.BindingFlags]::Instance -bor [Reflection.BindingFlags]::NonPublic
        $items=$type.GetField('items',$flags);$cursor=$type.GetField('curent_count',$flags)
        $sr=$type.GetField('SR',$flags);$count=$type.GetField('Count');$acount=$type.GetField('aCount')
        $read=$type.GetMethod('Reader');$out=$type.GetMethod('OutBase')
        $artifacts=[IO.Path]::GetFullPath([IO.Path]::Combine($workspace,'.artifacts'))
        $root=[IO.Path]::GetFullPath([IO.Path]::Combine($artifacts,'goal541-db-read-child-data-'+$guard))
        $file=[IO.Path]::GetFullPath([IO.Path]::Combine($root,'rows.txt'))
        function P541Ordinary([string]$p) {
            $walk=[IO.Path]::GetFullPath($p)
            while ($null -ne $walk) {
                try { if (([IO.File]::GetAttributes($walk) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Reparse path' } }
                catch [IO.FileNotFoundException] { }
                catch [IO.DirectoryNotFoundException] { }
                $walk=[IO.Path]::GetDirectoryName($walk)
            }
        }
        P541Ordinary $artifacts
        if ([IO.File]::Exists($root) -or [IO.Directory]::Exists($root)) { throw 'Fresh data root required' }
        $null=[IO.Directory]::CreateDirectory($root);P541Ordinary $root
        if ([IO.Path]::GetDirectoryName($file) -cne $root) { throw 'Owned leaf containment' }
        $seed=[ordered]@{Hash=$null}
        function P541Hash {
            [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($file)))
        }
        function P541Write([string]$text) {
            P541Ordinary $file
            $mode=[IO.FileMode]::CreateNew
            if ($null -ne $seed.Hash) {
                if ((P541Hash) -cne $seed.Hash) { throw 'Seed changed before rewrite' }
                $mode=[IO.FileMode]::Truncate
            }
            $b=[Text.UTF8Encoding]::new($false,$true).GetBytes($text)
            $stream=[IO.File]::Open($file,$mode,[IO.FileAccess]::Write,[IO.FileShare]::None)
            try { $stream.Write($b);$stream.Flush($true) } finally { $stream.Dispose() }
            $seed.Hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($b))
            if ((P541Hash) -cne $seed.Hash) { throw 'Seed readback mismatch' }
        }
        function P541DeleteSeed {
            P541Ordinary $root;P541Ordinary $file
            $all=@([IO.Directory]::GetFileSystemEntries($root))
            if ($all.Count -ne 1 -or $all[0] -cne $file -or
                $null -eq $seed.Hash -or (P541Hash) -cne $seed.Hash) { throw 'Owned seed deletion guard' }
            [IO.File]::Delete($file);$seed.Hash=$null
        }
        $data=[ordered]@{Guard=$guard;Scenario=$scenario;AssemblySha256=$hash;Completed=$false;Primed=$false;Returned=$false;ReturnValue=$null;ExceptionType=$null}
        $reader=$null;$locked=$null
        try {
            if ($scenario -notin @('MissingDefaultZero','HeaderNoEquals','HeaderNull','HeaderInvalid',
                'HeaderNegative','HeaderOverflow','ShareZero','SharePositive','MissingDefaultPositive')) { throw 'Unknown DbRead control' }
            if ($scenario -ne 'MissingDefaultZero') { P541Write ("Count=2"+[char]10+"old"+[char]10+"two"+[char]10) }
            $ctor=$type.GetConstructor([type[]]@([string],[bool]))
            $reader=$ctor.Invoke([object[]]@($file,$true))
            if ($scenario -notin @('MissingDefaultZero','ShareZero')) {
                if (!$read.Invoke($reader,[object[]]@($false))) { throw 'Actual priming Reader failed' }
                if ($type.GetMethod('ReadString').Invoke($reader,[object[]]@('fallback')) -cne 'old') { throw 'Actual priming cursor' }
                if (!$type.GetMethod('UseRead').Invoke($reader,[object[]]@())) { throw 'Actual priming aCount' }
                $data.Primed=$true
            }
            $before=[Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
            $before.Add('Items',$items.GetValue($reader));$before.Add('Sr',$sr.GetValue($reader))
            $data.ItemsNullBefore=$null -eq $before['Items']
            switch ($scenario) {
                'HeaderNoEquals' { P541Write 'missing-equals' }
                'HeaderNull' { P541Write '' }
                'HeaderInvalid' { P541Write 'Count=invalid' }
                'HeaderNegative' { P541Write 'Count=-1' }
                'HeaderOverflow' { P541Write 'Count=2147483648' }
                'ShareZero' { $locked=[IO.File]::Open($file,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None) }
                'SharePositive' { $locked=[IO.File]::Open($file,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None) }
                'MissingDefaultPositive' { P541DeleteSeed }
            }
            try {
                $data.ReturnValue=$read.Invoke($reader,[object[]]@($true))
                $data.Returned=$true
            } catch {
                $data.ExceptionType=$_.Exception.GetBaseException().GetType().FullName
            }
            $data.Count=$count.GetValue($reader)
            $data.Cursor=$cursor.GetValue($reader)
            $data.ACount=$acount.GetValue($reader)
            $data.ItemsNull=$null -eq $items.GetValue($reader)
            $data.SameItems=[object]::ReferenceEquals($before['Items'],$items.GetValue($reader))
            $data.SameSr=[object]::ReferenceEquals($before['Sr'],$sr.GetValue($reader))
            $data.ItemsLength=$null;$data.First=$null;$data.Second=$null
            if (!$data.ItemsNull) {
                $data.ItemsLength=$items.GetValue($reader).Length
                if ($data.ItemsLength -gt 0) { $data.First=$items.GetValue($reader)[0] }
                if ($data.ItemsLength -gt 1) { $data.Second=$items.GetValue($reader)[1] }
            }
            $data.SrClosed=$false
            if ($null -ne $sr.GetValue($reader)) {
                try { $null=$sr.GetValue($reader).Peek() }
                catch { if ($_.Exception.GetBaseException() -is [ObjectDisposedException]) { $data.SrClosed=$true } else { throw } }
            }
        } finally {
            if ($null -ne $locked) { $locked.Dispose() }
            if ($null -ne $reader) { $type.GetMethod('Dispose').Invoke($reader,[object[]]@()) }
            P541Ordinary $root;P541Ordinary $file
            if ([IO.Path]::GetDirectoryName($root) -cne $artifacts -or
                [IO.Path]::GetFileName($root) -cne ('goal541-db-read-child-data-'+$guard)) { throw 'Cleanup root changed' }
            $all=@([IO.Directory]::GetFileSystemEntries($root))
            if ($null -eq $seed.Hash) {
                if ($all.Count -ne 0 -or [IO.File]::Exists($file) -or [IO.Directory]::Exists($file)) { throw 'Missing-control contents changed' }
            } else {
                if ($all.Count -ne 1 -or $all[0] -cne $file -or
                    [IO.Directory]::Exists($file) -or (P541Hash) -cne $seed.Hash) { throw 'Changed/unowned seed preserved' }
                [IO.File]::Delete($file)
            }
            [IO.Directory]::Delete($root,$false)
        }
        $data.Completed=$true
        [Console]::Out.WriteLine('__LEGACY_NATIVE_DBREAD_RESULT__'+($data|ConvertTo-Json -Depth 10 -Compress -EscapeHandling EscapeNonAscii))
        """;
}
