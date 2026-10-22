using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;

namespace WolfConquer.Tests;

// Test-only process ownership. No production callback or console substitute.
internal static class LegacyNativeItemTypeLoadingChildFixture
{
    internal const string PwshPathVariable = "WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH";
    internal const string PwshHashVariable = "WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH_SHA256";
    private const string Marker = "__LEGACY_NATIVE_ITEM_TYPE_LOADING_RESULT__";

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

        var assembly = typeof(WolfConquer.Core.World.LegacyNativeItemRuneMetadata).Assembly.Location;
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
        start.Environment["WOLFCONQUER_LOADING_CHILD_ASSEMBLY"] = assembly;
        start.Environment["WOLFCONQUER_LOADING_CHILD_ASSEMBLY_SHA256"] = assemblyHash;
        start.Environment["WOLFCONQUER_LOADING_CHILD_SCENARIO"] = scenario;
        start.Environment["WOLFCONQUER_LOADING_CHILD_GUARD"] = guard;
        start.Environment["WOLFCONQUER_LOADING_CHILD_SCRIPT"] = heldScript.Path;
        start.Environment["WOLFCONQUER_LOADING_CHILD_SCRIPT_SHA256"] = heldScript.Sha256;

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
            var tempRoot = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath());
            var directory = System.IO.Path.Combine(tempRoot, "WolfConquer.NativeItemTypeLoading." + Guid.NewGuid().ToString("N"));
            Assert.False(Directory.Exists(directory));
            Directory.CreateDirectory(directory);
            Assert.False((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0);
            var path = System.IO.Path.Combine(directory, "probe.ps1");
            var bytes = new UTF8Encoding(false, true).GetBytes(script);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            Assert.False((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0);
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
                System.IO.Path.GetFullPath(System.IO.Path.GetTempPath())) + System.IO.Path.DirectorySeparatorChar;
            var resolvedDirectory = System.IO.Path.GetFullPath(directory);
            Assert.StartsWith(tempRoot, resolvedDirectory, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(System.IO.Path.Combine(resolvedDirectory, "probe.ps1"), System.IO.Path.GetFullPath(Path));
            Assert.False((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0);
            Assert.False((File.GetAttributes(Path) & FileAttributes.ReparsePoint) != 0);
            Assert.Equal(expected, File.ReadAllBytes(Path));
            File.Delete(Path);
            Directory.Delete(directory, recursive: false);
        }
    }

    private const string BootstrapScript = """
        $ErrorActionPreference = 'Stop'
        $p = [Environment]::GetEnvironmentVariable('WOLFCONQUER_LOADING_CHILD_SCRIPT')
        $b = [IO.File]::ReadAllBytes($p)
        $h = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($b))
        if ($h -cne [Environment]::GetEnvironmentVariable('WOLFCONQUER_LOADING_CHILD_SCRIPT_SHA256')) { throw 'Held script mismatch' }
        & ([ScriptBlock]::Create([Text.UTF8Encoding]::new($false, $true).GetString($b)))
        """;

    private const string ChildScript = """
        $ErrorActionPreference = 'Stop'
        Set-StrictMode -Version Latest
        $guard = [Environment]::GetEnvironmentVariable('WOLFCONQUER_LOADING_CHILD_GUARD')
        if ($guard -notmatch '^[0-9a-f]{32}$') { throw 'Fresh child guard missing' }
        $path = [Environment]::GetEnvironmentVariable('WOLFCONQUER_LOADING_CHILD_ASSEMBLY')
        $expected = [Environment]::GetEnvironmentVariable('WOLFCONQUER_LOADING_CHILD_ASSEMBLY_SHA256')
        $scenario = [Environment]::GetEnvironmentVariable('WOLFCONQUER_LOADING_CHILD_SCENARIO')
        $assemblyBytes = [IO.File]::ReadAllBytes($path)
        $actual = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($assemblyBytes))
        if ($actual -cne $expected) { throw 'Child assembly identity mismatch' }
        $assembly = [Reflection.Assembly]::Load($assemblyBytes)
        $tableType = $assembly.GetType('WolfConquer.Core.World.LegacyNativeItemTypeRuneMetadata', $true)
        $rowType = $assembly.GetType('WolfConquer.Core.World.LegacyNativeItemRuneMetadata', $true)
        $configType = $assembly.GetType('WolfConquer.Core.World.LegacyNativeServerConfig', $true)
        $load = $tableType.GetMethod('Loading')
        $location = $configType.GetField('DbLocation')
        $table = [Activator]::CreateInstance($tableType, $true)
        $data = [ordered]@{ Guard=$guard;Scenario=$scenario;AssemblySha256=$actual;Completed=$false }
        function P537Get($row, [string]$name) { return ,($rowType.GetField($name).GetValue($row)) }
        function P537Line([string]$id, [string]$name = 'Item', [string]$purification = '0', [string]$class = '0') {
            [string[]]$f = @(0..63 | ForEach-Object { '0' })
            $f[0]=$id; $f[1]=$name; $f[2]=$class; $f[25]='12345'; $f[52]='Item'
            $f[55]=$purification; $f[58]='67'
            return ($f -join '@@')
        }
        function P537LoadFailure {
            try { $null=$load.Invoke($table, [object[]]@()); throw 'Expected real Loading fault' }
            catch {
                $base=$_.Exception.GetBaseException()
                if ($base.Message -ceq 'Expected real Loading fault') { throw }
                return $base.GetType().FullName
            }
        }
        $tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath())
        $root=[IO.Path]::GetFullPath([IO.Path]::Combine($tempRoot,'WolfConquer.LoadingData.'+$guard))
        if ([IO.Directory]::Exists($root) -or [IO.File]::Exists($root)) { throw 'Fresh data directory required' }
        $null=[IO.Directory]::CreateDirectory($root)
        if (([IO.File]::GetAttributes($root) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Data directory reparse' }
        $ownedFiles=[Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
        $utf8=[Text.UTF8Encoding]::new($false,$true)
        function P537Hash([string]$file) {
            [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($file)))
        }
        function P537Write([string]$name, [string[]]$lines) {
            if ($name -cnotin @('ItemAdd.ini','itemtype.txt','ItemtypeSub.txt')) { throw 'Unowned data leaf' }
            $file=[IO.Path]::GetFullPath([IO.Path]::Combine($root,$name))
            if ([IO.Path]::GetDirectoryName($file) -cne $root) { throw 'Uncontained data leaf' }
            $mode=[IO.FileMode]::CreateNew
            if ($ownedFiles.ContainsKey($file)) {
                if (([IO.File]::GetAttributes($file) -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
                    (P537Hash $file) -cne $ownedFiles[$file]) { throw 'Owned source changed before rewrite' }
                $mode=[IO.FileMode]::Truncate
            }
            $text=([string]::Join([Environment]::NewLine,$lines))
            if ($lines.Length -gt 0) { $text += [Environment]::NewLine }
            $bytes=$utf8.GetBytes($text)
            $stream=[IO.File]::Open($file,$mode,[IO.FileAccess]::Write,[IO.FileShare]::None)
            try { $stream.Write($bytes);$stream.Flush($true) } finally { $stream.Dispose() }
            $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
            if ((P537Hash $file) -cne $hash) { throw 'Owned data readback' }
            $ownedFiles[$file]=$hash
        }
        $oldDirectory=[Environment]::CurrentDirectory
        try {
            if ($scenario -cne 'ConfigDefaults') { $location.SetValue($null,$root+[IO.Path]::DirectorySeparatorChar) }
            switch ($scenario) {
                'ConfigDefaults' {
                    # First static field reads in this fresh process observe actual declared defaults.
                    $values=[ordered]@{}
                    foreach ($field in $configType.GetFields()) { $values[$field.Name]=$field.GetValue($null) }
                    $data.Fields=$values
                }
                'MainAndSub' {
                    P537Write 'ItemAdd.ini' @()
                    P537Write 'itemtype.txt' @((P537Line '111001' 'Main' '0' '1005'))
                    P537Write 'ItemtypeSub.txt' @((P537Line '112001' 'Sub' '0' '4001'))
                    $null=$load.Invoke($table,[object[]]@())
                    $data.Count=$table.Count
                    $data.MainName=P537Get $table[[uint32]111001] 'Name'
                    $data.SubName=P537Get $table[[uint32]112001] 'Name'
                    $data.Class=P537Get $table[[uint32]111001] 'Class'
                    $data.SubClass=P537Get $table[[uint32]112001] 'Class'
                    $data.RuneSpellID=P537Get $table[[uint32]111001] 'RuneSpellID'
                }
                'SharedPlus' {
                    P537Write 'ItemAdd.ini' @('111000 1 10 20 30 40 50 60 70 80')
                    P537Write 'itemtype.txt' @((P537Line '111001'),(P537Line '111009'))
                    P537Write 'ItemtypeSub.txt' @()
                    $null=$load.Invoke($table,[object[]]@())
                    $a=$table[[uint32]111001];$b=$table[[uint32]111009]
                    $pa=P537Get $a 'Plus';$pb=P537Get $b 'Plus'
                    $data.SameArray=[object]::ReferenceEquals($pa,$pb)
                    $data.FirstAllowed=P537Get $a 'AllowUpgradePlus';$data.SecondAllowed=P537Get $b 'AllowUpgradePlus'
                    $data.Length=$pa.Length;$data.PlusID=[uint32]$pa[1].ID;$data.ItemHP=[uint16]$pa[1].ItemHP
                    $data.ZeroSlotNull=$null-eq$pa[0]
                }
                'PlusPriority' {
                    P537Write 'ItemAdd.ini' @('111000 1 10 0 0 0 0 0 0 0','111010 1 20 0 0 0 0 0 0 0','111020 1 30 0 0 0 0 0 0 0','112010 1 40 0 0 0 0 0 0 0','112020 1 50 0 0 0 0 0 0 0','113020 1 60 0 0 0 0 0 0 0')
                    P537Write 'itemtype.txt' @((P537Line '111001'),(P537Line '112001'),(P537Line '113001'))
                    P537Write 'ItemtypeSub.txt' @()
                    $null=$load.Invoke($table,[object[]]@())
                    $ids=[Collections.Generic.List[uint32]]::new()
                    foreach ($id in @([uint32]111001,[uint32]112001,[uint32]113001)) { $plus=P537Get $table[$id] 'Plus';$ids.Add($plus[1].ID) }
                    $data.SelectedPlusIDs=$ids.ToArray()
                }
                'DuplicateCaches' {
                    P537Write 'ItemAdd.ini' @()
                    P537Write 'itemtype.txt' @((P537Line '100001' 'First'),(P537Line '100001' 'MainDuplicate' '6'))
                    P537Write 'ItemtypeSub.txt' @((P537Line '100001' 'SubDuplicate' '7'))
                    $null=$load.Invoke($table,[object[]]@())
                    $cache=$tableType.GetField('PurificationItems').GetValue($null)
                    $first=$table[[uint32]100001];$main=$cache[[uint16]6][[uint32]100001];$sub=$cache[[uint16]7][[uint32]100001]
                    $data.Count=$table.Count;$data.FirstName=P537Get $first 'Name'
                    $data.MainCacheName=P537Get $main 'Name';$data.SubCacheName=P537Get $sub 'Name'
                    $data.MainDistinct=![object]::ReferenceEquals($first,$main)
                    $data.SubDistinct=![object]::ReferenceEquals($first,$sub)
                    $data.CachesDistinct=![object]::ReferenceEquals($main,$sub)
                }
                'MissingItemAdd' {
                    $data.ExceptionType=P537LoadFailure;$data.Count=$table.Count
                }
                'InvalidItemAdd' {
                    P537Write 'ItemAdd.ini' @('111000 13 10 0 0 0 0 0 0 0')
                    $data.ExceptionType=P537LoadFailure;$data.Count=$table.Count
                }
                'MissingMain' {
                    P537Write 'ItemAdd.ini' @('111000 1 10 0 0 0 0 0 0 0')
                    $data.ExceptionType=P537LoadFailure;$data.Count=$table.Count
                }
                'MissingSub' {
                    P537Write 'ItemAdd.ini' @()
                    P537Write 'itemtype.txt' @((P537Line '111001' 'CommittedMain'))
                    $data.ExceptionType=P537LoadFailure;$data.Count=$table.Count
                    $data.Name=P537Get $table[[uint32]111001] 'Name'
                }
                'SkipShortComments' {
                    P537Write 'ItemAdd.ini' @()
                    P537Write 'itemtype.txt' @('','tiny',' // comment longer than eleven','  ',(P537Line '111001' 'Kept'))
                    P537Write 'ItemtypeSub.txt' @('// another sufficiently long comment')
                    $null=$load.Invoke($table,[object[]]@())
                    $data.Count=$table.Count;$data.Name=P537Get $table[[uint32]111001] 'Name'
                }
                'SuccessiveLoads' {
                    P537Write 'ItemAdd.ini' @('111000 1 10 0 0 0 0 0 0 0')
                    P537Write 'itemtype.txt' @((P537Line '111001' 'Original'))
                    P537Write 'ItemtypeSub.txt' @()
                    $null=$load.Invoke($table,[object[]]@());$old=$table[[uint32]111001];$oldPlus=P537Get $old 'Plus'
                    P537Write 'ItemAdd.ini' @('111000 1 20 0 0 0 0 0 0 0')
                    P537Write 'itemtype.txt' @((P537Line '111001' 'IgnoredDuplicate'),(P537Line '111005' 'New'))
                    $null=$load.Invoke($table,[object[]]@());$new=$table[[uint32]111005];$newPlus=P537Get $new 'Plus'
                    $data.Count=$table.Count;$data.OldName=P537Get $old 'Name'
                    $data.SameOldRow=[object]::ReferenceEquals($old,$table[[uint32]111001])
                    $data.SameOldPlus=[object]::ReferenceEquals($oldPlus,(P537Get $old 'Plus'))
                    $data.DifferentGeneration=![object]::ReferenceEquals($oldPlus,$newPlus)
                    $data.OldHP=$oldPlus[1].ItemHP;$data.NewHP=$newPlus[1].ItemHP
                }
                'NullLocation' {
                    P537Write 'ItemAdd.ini' @()
                    P537Write 'itemtype.txt' @((P537Line '111001' 'Relative'))
                    P537Write 'ItemtypeSub.txt' @()
                    [Environment]::CurrentDirectory=$root;$location.SetValue($null,$null)
                    $null=$load.Invoke($table,[object[]]@())
                    $data.Count=$table.Count;$data.Name=P537Get $table[[uint32]111001] 'Name'
                    $data.LocationStillNull=$null-eq$location.GetValue($null)
                }
                'SwallowedParseFault' {
                    P537Write 'ItemAdd.ini' @()
                    $line=P537Line '111001' 'Partial';$fields=$line.Split('@@',[StringSplitOptions]::None);$fields[2]='bad class'
                    P537Write 'itemtype.txt' @(($fields-join'@@'))
                    P537Write 'ItemtypeSub.txt' @()
                    $null=$load.Invoke($table,[object[]]@());$row=$table[[uint32]111001]
                    $data.Count=$table.Count;$data.Name=P537Get $row 'Name';$data.Class=P537Get $row 'Class'
                    $data.RuneSpellID=P537Get $row 'RuneSpellID';$data.Sort=P537Get $row 'Sort';$data.PlusLength=(P537Get $row 'Plus').Length
                }
                default { throw 'Unknown actual Loading scenario' }
            }
        }
        finally {
            [Environment]::CurrentDirectory=$oldDirectory
            $resolved=[IO.Path]::GetFullPath($root)
            $prefix=[IO.Path]::TrimEndingDirectorySeparator($tempRoot)+[IO.Path]::DirectorySeparatorChar
            if (!$resolved.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) -or
                [IO.Path]::GetFileName($resolved) -cne ('WolfConquer.LoadingData.'+$guard) -or
                (([IO.File]::GetAttributes($resolved) -band [IO.FileAttributes]::ReparsePoint) -ne 0)) { throw 'Data cleanup root changed' }
            foreach ($file in $ownedFiles.Keys) {
                if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($file)) -cne $resolved -or
                    ([IO.File]::GetAttributes($file) -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
                    (P537Hash $file) -cne $ownedFiles[$file]) { throw 'Data cleanup ownership changed' }
                [IO.File]::Delete($file)
            }
            [IO.Directory]::Delete($resolved,$false)
        }
        $data.Completed=$true
        [Console]::Out.WriteLine('__LEGACY_NATIVE_ITEM_TYPE_LOADING_RESULT__'+($data|ConvertTo-Json -Depth 10 -Compress -EscapeHandling EscapeNonAscii))
        """;
}
