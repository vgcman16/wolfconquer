using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;

namespace WolfConquer.Tests;

// Test-only process ownership. No production callback or console substitute.
internal static class LegacyNativeItemTypeParseChildFixture
{
    internal const string PwshPathVariable = "WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH";
    internal const string PwshHashVariable = "WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH_SHA256";
    private const string Marker = "__LEGACY_NATIVE_ITEM_TYPE_PARSE_RESULT__";

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
        start.Environment["WOLFCONQUER_PARSE_CHILD_ASSEMBLY"] = assembly;
        start.Environment["WOLFCONQUER_PARSE_CHILD_ASSEMBLY_SHA256"] = assemblyHash;
        start.Environment["WOLFCONQUER_PARSE_CHILD_SCENARIO"] = scenario;
        start.Environment["WOLFCONQUER_PARSE_CHILD_GUARD"] = guard;
        start.Environment["WOLFCONQUER_PARSE_CHILD_SCRIPT"] = heldScript.Path;
        start.Environment["WOLFCONQUER_PARSE_CHILD_SCRIPT_SHA256"] = heldScript.Sha256;

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
            var directory = System.IO.Path.Combine(tempRoot, "WolfConquer.NativeItemTypeParse." + Guid.NewGuid().ToString("N"));
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
        $p = [Environment]::GetEnvironmentVariable('WOLFCONQUER_PARSE_CHILD_SCRIPT')
        $b = [IO.File]::ReadAllBytes($p)
        $h = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($b))
        if ($h -cne [Environment]::GetEnvironmentVariable('WOLFCONQUER_PARSE_CHILD_SCRIPT_SHA256')) { throw 'Held script mismatch' }
        & ([ScriptBlock]::Create([Text.UTF8Encoding]::new($false, $true).GetString($b)))
        """;

    private const string ChildScript = """
        $ErrorActionPreference = 'Stop'
        Set-StrictMode -Version Latest
        $guard = [Environment]::GetEnvironmentVariable('WOLFCONQUER_PARSE_CHILD_GUARD')
        if ($guard -notmatch '^[0-9a-f]{32}$') { throw 'Fresh parse child guard missing' }
        $path = [Environment]::GetEnvironmentVariable('WOLFCONQUER_PARSE_CHILD_ASSEMBLY')
        $bytes = [IO.File]::ReadAllBytes($path)
        $actual = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
        if ($actual -cne [Environment]::GetEnvironmentVariable('WOLFCONQUER_PARSE_CHILD_ASSEMBLY_SHA256')) { throw 'Parse child assembly mismatch' }
        $assembly = [Reflection.Assembly]::Load($bytes)
        $rowType = $assembly.GetType('WolfConquer.Core.World.LegacyNativeItemRuneMetadata', $true)
        $tableType = $assembly.GetType('WolfConquer.Core.World.LegacyNativeItemTypeRuneMetadata', $true)
        $plusType = $assembly.GetType('WolfConquer.Core.World.LegacyNativeItemTypePlus', $true)
        $null = $assembly.GetType('WolfConquer.Core.Runtime.LegacyNativeConsole', $true)
        $parse = $rowType.GetMethod('Parse', [Reflection.BindingFlags]'Public,Instance')
        if ($null -eq $parse) { throw 'Actual Parse method missing' }
        $scenario = [Environment]::GetEnvironmentVariable('WOLFCONQUER_PARSE_CHILD_SCENARIO')
        $data = [ordered]@{ Guard=$guard; Scenario=$scenario; AssemblySha256=$actual; Completed=$false }
        function P535Row { return [Activator]::CreateInstance($rowType, $true) }
        function P535Get($row, [string]$name) { return ,($rowType.GetField($name).GetValue($row)) }
        function P535Put($row, [string]$name, $value) { $rowType.GetField($name).SetValue($row, $value) }
        function P535Cache([string]$name) { return ,($tableType.GetField($name).GetValue($null)) }
        function P535SetCache([string]$name, $value) { $tableType.GetField($name).SetValue($null, $value) }
        function P535Fields([int]$count = 64) {
            [string[]]$fields = @(for ($i=0; $i -lt $count; $i++) { '0' })
            $values = @{0='100001';1=' Blade ';2='31';3='32';4='33';5='1';6='34';7='35';10='0';11='36';13='37';14='38';15='39';16='40';17='41';18='42';19='43';21='44';25='4455';29='45';30='46';31='47';36='48';38='49';39='50';40='51';41='52';42='53';43='54';44='55';45='56';46='57';47='58';48='59';49='60';50='61';51='62';52='NinjaKatana';54='7';55='0';56='0';58='67';60='68';61='69';62='70';63='71'}
            foreach ($key in $values.Keys) { if ($key -lt $count) { $fields[$key] = $values[$key] } }
            return ,$fields
        }
        function P535Parse($row, [string[]]$fields) { $null = $parse.Invoke($row, [object[]]@(($fields -join '@@'))) }
        function P535Map($row, [string[]]$names) {
            $map = [ordered]@{}
            foreach ($name in $names) { $value=P535Get $row $name; if ($null -ne $value -and $value.GetType().IsEnum) { $value=[uint32]$value }; $map[$name]=$value }
            return ,$map
        }
        function P535Fault($row, $line) {
            try { $null=$parse.Invoke($row, [object[]]@($line)); throw 'Expected pretry escape' }
            catch { return $_.Exception.GetBaseException().GetType().FullName }
        }
        switch ($scenario) {
            'NullLine' {
                $row=P535Row; $old=[Array]::CreateInstance($plusType, 2); P535Put $row 'Plus' $old
                $data.ExceptionType=P535Fault $row $null
                $data.NewPlusLength=(P535Get $row 'Plus').Length
                $data.PlusReplaced=![object]::ReferenceEquals($old, (P535Get $row 'Plus'))
                $data.Fields=P535Map $row @('ID','Name','Class')
            }
            'ShortRecord' {
                $row=P535Row; P535Put $row 'ID' ([uint32]77)
                $data.ExceptionType=P535Fault $row '1@@short'
                $data.PlusLength=(P535Get $row 'Plus').Length
                $data.Id=P535Get $row 'ID'
            }
            'FiftyTwoTokens' {
                $row=P535Row; P535Put $row 'ID' ([uint32]77); P535Put $row 'Name' 'prior'; P535Put $row 'Class' ([uint32]88)
                P535Parse $row (P535Fields 52)
                $data.Fields=P535Map $row @('ID','Name','Class'); $data.PlusLength=(P535Get $row 'Plus').Length
            }
            'NulIdentifier' {
                $row=P535Row; P535Put $row 'ID' ([uint32]77); $f=P535Fields; $f[0]=[string][char]0
                P535Parse $row $f
                $data.Id=P535Get $row 'ID'; $data.Name=P535Get $row 'Name'; $data.PlusLength=(P535Get $row 'Plus').Length
            }
            'EmptyAndZeroName' {
                $results=[Collections.Generic.List[object]]::new()
                foreach ($name in @('   ','0')) {
                    $row=P535Row; P535Put $row 'Class' ([uint32]99); $f=P535Fields; $f[1]=$name
                    P535Parse $row $f
                    $results.Add((P535Map $row @('ID','Name','Class')))
                }
                $data.Results=$results.ToArray()
            }
            'CompleteRecord' {
                $row=P535Row; P535Put $row 'AllowUpgradePlus' $true; P535Put $row 'GradeKey' ([int]-99); P535Put $row 'Detoxication' ([uint32]88)
                $f=P535Fields; $f[8]='not used'; $f[9]='also not used'; P535Parse $row $f
                $data.Fields=P535Map $row @('ID','Name','Class','Proficiency','Level','Gender','Strength','Agility','Type','GoldWorth','MaxAttack','MinAttack','PhysicalDefence','Frequency','Dodge','ItemHP','ItemMP','Durability','MagicAttack','MagicDefence','AttackRange','ConquerPointsWorth','Time','Crytical','SCrytical','Imunity','Penetration','Block','BreackTrough','ConterAction','MetalResistance','WoodResistance','WaterResistance','FireResistance','EarthResistance','StackSize','Description','RuneSpellID','BonusAttribute','Sort','PhysicalDamageIncrease','MagicDamageIncrease','PhysicalDamageDecrease','MagicDamageDecrease','AllowUpgradePlus','GradeKey','Detoxication')
                $data.PlusLength=(P535Get $row 'Plus').Length
                $plus=P535Get $row 'Plus'; $data.AllPlusElementsNull=$true
                for ($i=0; $i -lt $plus.Length; $i++) { if ($null -ne $plus.GetValue($i)) { $data.AllPlusElementsNull=$false } }
            }
            'CurrentCulture' {
                $culture=[Globalization.CultureInfo]::GetCultureInfo('tr-TR').Clone()
                $culture.NumberFormat.PositiveSign='p'
                [Globalization.CultureInfo]::CurrentCulture=$culture
                $row=P535Row; $f=P535Fields; $f[2]='p31'; $f[52]='I'; P535Parse $row $f
                $data.Class=P535Get $row 'Class'; $data.Description=P535Get $row 'Description'; $data.Sort=P535Get $row 'Sort'
            }
            'DescriptionAndPlusNames' {
                $descriptions=[Collections.Generic.List[object]]::new()
                foreach ($description in @('Ninja`sKatana','Earrings','Bow','Backsword')) {
                    $row=P535Row; $f=P535Fields; $f[52]=$description; P535Parse $row $f
                    $descriptions.Add((P535Get $row 'Description'))
                }
                $names=[Collections.Generic.List[object]]::new()
                foreach ($id in @('730000','730001','730009','730010')) {
                    $row=P535Row; $f=P535Fields; $f[0]=$id; P535Parse $row $f
                    $names.Add((P535Get $row 'Name'))
                }
                $data.Descriptions=$descriptions.ToArray(); $data.Names=$names.ToArray()
            }
            'TypeAndStack' {
                $results=[Collections.Generic.List[object]]::new()
                foreach ($type in @('0','4294967295')) {
                    $row=P535Row; P535Put $row 'StackSize' ([uint16]999); $f=P535Fields; $f[10]=$type; $f[46]='invalid'
                    P535Parse $row $f; $results.Add((P535Map $row @('Type','StackSize','Sort')))
                }
                $data.Results=$results.ToArray()
            }
            'Exactly56' {
                $row=P535Row; P535Put $row 'RuneSpellID' ([uint32]91); P535Put $row 'Sort' ([uint32]92); $f=P535Fields 56; $f[55]='6'
                P535Parse $row $f
                $data.Fields=P535Map $row @('PurificationLevel','PurificationMeteorNeed','RuneSpellID','Sort','Description')
                $data.PurificationCount=(P535Cache 'PurificationItems').Count
            }
            'PurificationSameAndExcluded' {
                $row=P535Row; $f=P535Fields; $f[55]='6'; $f[56]='9'; P535Parse $row $f
                $cache=P535Cache 'PurificationItems'; $bucket=$cache[[uint16]6]
                $data.SameReference=[object]::ReferenceEquals($row,$bucket[[uint32]100001])
                $data.Meteor=P535Get $row 'PurificationMeteorNeed'
                foreach ($id in @('729305','727465')) { $other=P535Row; $f[0]=$id; P535Parse $other $f }
                $data.BucketCount=$bucket.Count; $data.ContainsExcludedA=$bucket.ContainsKey([uint32]729305); $data.ContainsExcludedB=$bucket.ContainsKey([uint32]727465)
            }
            'PurificationDuplicate' {
                $first=P535Row; $f=P535Fields; $f[55]='6'; P535Parse $first $f
                $second=P535Row; $f[25]='991'; P535Parse $second $f
                $bucket=(P535Cache 'PurificationItems')[[uint16]6]
                $data.KeepsFirst=[object]::ReferenceEquals($first,$bucket[[uint32]100001])
                $data.DistinctRows=![object]::ReferenceEquals($first,$second)
                $data.FirstRune=P535Get $first 'RuneSpellID'; $data.SecondRune=P535Get $second 'RuneSpellID'
            }
            'BothAccessoryPositions' {
                $results=[Collections.Generic.List[object]]::new()
                foreach ($id in @('350001','370001')) {
                    $row=P535Row; $f=P535Fields; $f[0]=$id; $f[46]='99'; P535Parse $row $f
                    $results.Add([ordered]@{Id=[uint32]$id;Stack=(P535Get $row 'StackSize');SameReference=[object]::ReferenceEquals($row,(P535Cache 'Accessorys')[[uint32]$id])})
                }
                $data.Results=$results.ToArray()
            }
            'SteedAndGarment' {
                $results=[Collections.Generic.List[object]]::new()
                foreach ($pair in @(@('200000','SteedMounts'),@('181305','Garments'))) {
                    $row=P535Row; $f=P535Fields; $f[0]=$pair[0]; P535Parse $row $f
                    $results.Add([ordered]@{Id=[uint32]$pair[0];Stack=(P535Get $row 'StackSize');SameReference=[object]::ReferenceEquals($row,(P535Cache $pair[1])[[uint32]$pair[0]])})
                }
                $data.Results=$results.ToArray()
            }
            'CategoryDuplicates' {
                $results=[Collections.Generic.List[object]]::new()
                foreach ($pair in @(@('350001','Accessorys'),@('200000','SteedMounts'),@('181305','Garments'))) {
                    $first=P535Row; $f=P535Fields; $f[0]=$pair[0]; P535Parse $first $f
                    $second=P535Row; $f[25]='991'; P535Parse $second $f
                    $results.Add([ordered]@{KeepsFirst=[object]::ReferenceEquals($first,(P535Cache $pair[1])[[uint32]$pair[0]]);SecondRune=(P535Get $second 'RuneSpellID')})
                }
                $data.Results=$results.ToArray()
            }
            'CpOverrides' {
                $results=[Collections.Generic.List[object]]::new()
                foreach ($id in @('754099','754999','753999','751999','619028','723723','3005945')) {
                    $row=P535Row; $f=P535Fields; $f[0]=$id; $f[36]='123'; P535Parse $row $f
                    $results.Add([ordered]@{Id=[uint32]$id;Cp=(P535Get $row 'ConquerPointsWorth')})
                }
                $data.Results=$results.ToArray()
            }
            'PublishedBeforeSortFault' {
                $row=P535Row; P535Put $row 'Sort' ([uint32]999); P535Put $row 'PhysicalDamageIncrease' ([uint16]888)
                $f=P535Fields; $f[55]='6'; $f[58]='bad sort'; P535Parse $row $f
                $same=(P535Cache 'PurificationItems')[[uint16]6][[uint32]100001]
                $data.SameReference=[object]::ReferenceEquals($row,$same)
                $data.Fields=P535Map $same @('RuneSpellID','BonusAttribute','Sort','PhysicalDamageIncrease')
            }
            'PurificationBeforeNullAccessory' {
                $row=P535Row; P535Put $row 'RuneSpellID' ([uint32]900); $f=P535Fields; $f[0]='350001'; $f[55]='6'; $f[46]='99'
                P535SetCache 'Accessorys' $null
                P535Parse $row $f
                $data.SamePurification=[object]::ReferenceEquals($row,(P535Cache 'PurificationItems')[[uint16]6][[uint32]350001])
                $data.AccessoryStillNull=$null -eq (P535Cache 'Accessorys')
                $data.Fields=P535Map $row @('StackSize','RuneSpellID','Sort')
            }
            'ByteOverflow' {
                $row=P535Row; P535Put $row 'Proficiency' ([byte]7); P535Put $row 'Level' ([byte]8); $f=P535Fields; $f[3]='256'
                P535Parse $row $f
                $data.Fields=P535Map $row @('ID','Name','Class','Proficiency','Level','RuneSpellID')
            }
            'ResistanceOverflow' {
                $row=P535Row; P535Put $row 'MetalResistance' ([uint32]77); P535Put $row 'StackSize' ([uint16]88); P535Put $row 'Description' 'prior'
                $f=P535Fields; $f[47]='65536'; P535Parse $row $f
                $data.Fields=P535Map $row @('ConterAction','MetalResistance','WoodResistance','StackSize','Description','RuneSpellID')
            }
            'ShortOptionalTail' {
                $row=P535Row
                foreach ($name in @('PhysicalDamageIncrease','MagicDamageIncrease','PhysicalDamageDecrease','MagicDamageDecrease')) { P535Put $row $name ([uint16]999) }
                P535Parse $row (P535Fields 59)
                $data.Fields=P535Map $row @('Sort','PhysicalDamageIncrease','MagicDamageIncrease','PhysicalDamageDecrease','MagicDamageDecrease')
            }
            'FullOptionalTail' {
                $row=P535Row; $f=P535Fields; $f[60]='65535'; $f[61]='65534'; $f[62]='65533'; $f[63]='65532'; P535Parse $row $f
                $data.Fields=P535Map $row @('PhysicalDamageIncrease','MagicDamageIncrease','PhysicalDamageDecrease','MagicDamageDecrease')
            }
            'RemovedEmptyTokens' {
                $row=P535Row; $f=P535Fields; $f[2]=''; $f[3]='23'; P535Parse $row $f
                $data.Fields=P535Map $row @('ID','Class','Proficiency','Level','Gender')
            }
            'NullPurificationBucket' {
                $cache=P535Cache 'PurificationItems'; $cache.Add([uint16]6,$null)
                $row=P535Row; P535Put $row 'RuneSpellID' ([uint32]900); $f=P535Fields; $f[0]='350001'; $f[55]='6'; $f[46]='99'; P535Parse $row $f
                $data.BucketStillNull=$null -eq $cache[[uint16]6]
                $data.AccessoryCount=(P535Cache 'Accessorys').Count
                $data.Fields=P535Map $row @('PurificationLevel','PurificationMeteorNeed','StackSize','RuneSpellID')
            }
            'TrailingTokens' {
                $row=P535Row; [string[]]$f=P535Fields; $f += @('unused invalid 64','unused invalid 65'); P535Parse $row $f
                $data.Fields=P535Map $row @('ID','Sort','PhysicalDamageIncrease','MagicDamageDecrease')
            }
            'ReparseSameObject' {
                $row=P535Row; $f=P535Fields; $f[55]='6'; P535Parse $row $f
                $old=P535Get $row 'Plus'; $f[0]='100002'; $f[25]='991'; P535Parse $row $f
                $bucket=(P535Cache 'PurificationItems')[[uint16]6]
                $data.OldKeySame=[object]::ReferenceEquals($row,$bucket[[uint32]100001])
                $data.NewKeySame=[object]::ReferenceEquals($row,$bucket[[uint32]100002])
                $data.PlusChanged=![object]::ReferenceEquals($old,(P535Get $row 'Plus'))
                $data.Fields=P535Map $bucket[[uint32]100001] @('ID','RuneSpellID')
            }
            default { throw 'Unknown actual Parse scenario' }
        }
        $data.Completed=$true
        [Console]::Out.WriteLine('__LEGACY_NATIVE_ITEM_TYPE_PARSE_RESULT__'+($data | ConvertTo-Json -Depth 10 -Compress -EscapeHandling EscapeNonAscii))
        """;
}
