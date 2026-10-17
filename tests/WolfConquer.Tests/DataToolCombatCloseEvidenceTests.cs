using System.Diagnostics;
using System.Runtime.CompilerServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class DataToolCombatCloseEvidenceTests
{
    [Fact]
    public async Task ReportsExactInboundAttackAndOutboundFramesWhenWireTraceIsPresent()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-combat-close-evidence-" + Guid.NewGuid().ToString("N"));
        var worldLog = Path.Combine(tempRoot, "WorldServer.out.log");
        var clientLog = Path.Combine(tempRoot, "debug.log");
        var attackFrameHex = "1800570808A0FA4E10F7CB1818D004200228021802";
        var attackResponseHex = "1800570808F7CB1810A0FA4E18D004200228021801";

        try
        {
            await WriteFixtureFileAsync(
                worldLog,
                $"""
                WorldServer accepted client 127.0.0.1:51165 on 127.0.0.1:5818
                WorldServer legacy MsgLoginGame Success key=1293600 uid=1293600 map=1002.
                LegacyGameWireTrace inbound-frame frameLength=8 opcode=2163 payloadLength=4 expectedSeal=Client frameHex=0800730801000000
                WorldServer sent proactive monster movement monsterCount=2.
                LegacyGameWireTrace inbound-frame frameLength=24 opcode={MsgAttackPacket.Opcode} payloadLength=20 expectedSeal=Client frameHex={attackFrameHex}
                WorldServer replaced legacy monster physical attack with CombatGear spell session=accountId=1293600 monsterUid=403191 spellId=22090 sort=21 targets=3 totalDamage=99.
                LegacyGameWireTrace outbound-frame frameLength=24 opcode={MsgAttackPacket.Opcode} payloadLength=20 plainHex={attackResponseHex} encryptedHex=AABBCC
                WorldServer source-silent legacy packet session=accountId=1293600 opcode=2383 payloadLength=5. PARITY-GAP source-silent payload opcode=2383 payloadLength=5 payloadSampleHex=0800108F02
                WorldServer closed client 127.0.0.1:51165 on 127.0.0.1:5818
                """);
            await WriteFixtureFileAsync(
                clientLog,
                """
                Got a player with hero id! 1293600 -- Wed Jul  8 16:51:35 2026
                Error: CreateNewAni(ani/common.ani, BlastTrace) failed. -- Wed Jul 08 16:51:43 2026
                """);

            var result = await RunDataToolAsync(
                "combat-close-evidence",
                "--world-log",
                worldLog,
                "--client-log",
                clientLog);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Combat close evidence:", result.StandardOutput);
            Assert.Contains($"World log: {Path.GetFullPath(worldLog)}", result.StandardOutput);
            Assert.Contains($"Client log: {Path.GetFullPath(clientLog)}", result.StandardOutput);
            Assert.Contains("World latest close: True line=9", result.StandardOutput);
            Assert.Contains("World latest login success before close: True line=2", result.StandardOutput);
            Assert.Contains(
                $"World latest inbound MsgAttack before close: line=5 opcode={MsgAttackPacket.Opcode} frameLength=24 payloadLength=20 hex={attackFrameHex}",
                result.StandardOutput);
            Assert.Contains($"World last outbound frames before close: count=1 frames=7:{MsgAttackPacket.Opcode}:20", result.StandardOutput);
            Assert.Contains("World semantic combat before close: count=2", result.StandardOutput);
            Assert.Contains("World source-silent before close: count=1", result.StandardOutput);
            Assert.Contains("Client close/error symptoms: count=2", result.StandardOutput);
            Assert.Contains("Exact wire evidence ready: True", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ReportsSemanticCombatButNotExactWhenWireTraceIsMissing()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-combat-close-evidence-missing-wire-" + Guid.NewGuid().ToString("N"));
        var worldLog = Path.Combine(tempRoot, "WorldServer.out.log");

        try
        {
            await WriteFixtureFileAsync(
                worldLog,
                """
                WorldServer accepted client 127.0.0.1:51165 on 127.0.0.1:5818
                WorldServer legacy MsgLoginGame Success key=1293600 uid=1293600 map=1002.
                WorldServer sent legacy CombatGear UpdateTarget passive status session=accountId=1293600 spellId=21550 statusFlag=410 duration=5.
                WorldServer source-silent legacy packet session=accountId=1293600 opcode=2383 payloadLength=4. PARITY-GAP source-silent payload opcode=2383 payloadLength=4 payloadSampleHex=08001000
                WorldServer closed client 127.0.0.1:51165 on 127.0.0.1:5818
                """);

            var result = await RunDataToolAsync(
                "combat-close-evidence",
                "--world-log",
                worldLog);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("World latest close: True line=5", result.StandardOutput);
            Assert.Contains("World latest inbound MsgAttack before close: line=<none>", result.StandardOutput);
            Assert.Contains("World last outbound frames before close: count=0 frames=<none>", result.StandardOutput);
            Assert.Contains("World semantic combat before close: count=1", result.StandardOutput);
            Assert.Contains("World source-silent before close: count=1", result.StandardOutput);
            Assert.Contains("Exact wire evidence ready: False", result.StandardOutput);
            Assert.Contains("Next capture: enable WOLFCONQUER_TRACE_LEGACY_GAME_WIRE=1", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ReportsBoundedCombatCompatibilityLifecycleAndMissingAnimationEvidence()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-combat-close-evidence-7899-" + Guid.NewGuid().ToString("N"));
        var worldLog = Path.Combine(tempRoot, "WorldServer.out.log");
        var clientLog = Path.Combine(tempRoot, "debug.log");

        try
        {
            var worldLines = new List<string>
            {
                "Legacy CombatGear monster-hit passive spell blocklist enabled for client compatibility: 21680,22090,22100.",
                "Legacy proactive monster attacks disabled for client compatibility.",
                "Legacy CombatGear monster-hit passives disabled for client compatibility.",
                "Legacy weapon-proc monster attacks disabled for client compatibility.",
                "WorldServer accepted client 127.0.0.1:51165 on 127.0.0.1:5818",
                "WorldServer legacy MsgLoginGame Success key=1293600 uid=1293600 map=1002.",
                "WorldServer skipped legacy CombatGear monster-hit passive replacement session=accountId=1293600 monsterUid=403191; source=MyCombatGear.CheckCombatGear compatibility fence.",
                "WorldServer replaced legacy monster physical attack with CombatGear spell session=accountId=1293600 monsterUid=403191 spellId=22090 sort=21 targets=3 totalDamage=99.",
                "WorldServer skipped legacy weapon proc monster attack session=accountId=1293600 monsterUid=403191; source=MsgAttackPacket.Physical GetWeaponSpell/RandomSpell compatibility fence.",
                "WorldServer handled legacy weapon proc monster attack session=accountId=1293600 spellId=1045 weaponProcRandomSpell=1045.",
            };
            for (var count = 1; count <= 13; count++)
            {
                worldLines.Add($"WorldServer sent proactive monster attacks monsterCount={count}.");
            }
            worldLines.Add("WorldServer sent legacy guard monster-protection attacks monsterCount=1.");
            worldLines.Add("WorldServer skipped legacy guard monster-protection attack guardUid=400001 targetUid=400002; spellId=0 is not a valid legacy UInt16.");
            worldLines.Add("WorldServer sent legacy monster death drop session=accountId=1293600 monsterUid=403191 itemId=1088000 kind=Item amount=1 count=1.");
            worldLines.Add("WorldServer sent monster death remove monsterCount=1 viewerCount=1.");
            worldLines.Add("WorldServer sent monster respawn monsterCount=1 viewerCount=1.");
            worldLines.Add("WorldServer closed client 127.0.0.1:51165 on 127.0.0.1:5818");

            await WriteFixtureFileAsync(worldLog, string.Join(Environment.NewLine, worldLines));
            await WriteFixtureFileAsync(
                clientLog,
                """
                Got a player with hero id! 1293600 -- Wed Jul  8 16:51:35 2026
                Error: CreateNewAni(ani/common.ani, BlastTrace) failed. -- Wed Jul 08 16:51:43 2026
                [Ani.cpp] Ani Index [Cartridge_Case] Not Found In [ani/common.ani]
                Read Error in ItemType.dat
                """);

            var result = await RunDataToolAsync(
                "combat-close-evidence",
                "--world-log",
                worldLog,
                "--client-log",
                clientLog);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("World combat compatibility fences before close: count=6", result.StandardOutput);
            Assert.Contains(
                "line=1 preview=Legacy CombatGear monster-hit passive spell blocklist enabled for client compatibility: 21680,22090,22100.",
                result.StandardOutput);
            Assert.DoesNotContain(
                "Newer-client CombatGear floor-passive compatibility enabled.",
                result.StandardOutput);
            Assert.Contains("World CombatGear monster-hit passive outcomes before close: count=2", result.StandardOutput);
            Assert.Contains("World legacy weapon-proc monster attack outcomes before close: count=2", result.StandardOutput);
            Assert.Contains("WorldServer skipped legacy weapon proc monster attack", result.StandardOutput);
            Assert.Contains("WorldServer handled legacy weapon proc monster attack", result.StandardOutput);
            Assert.Contains("World proactive monster attacks before close: count=12", result.StandardOutput);
            Assert.Contains("line=23 preview=WorldServer sent proactive monster attacks monsterCount=13.", result.StandardOutput);
            Assert.DoesNotContain("line=11 preview=WorldServer sent proactive monster attacks monsterCount=1.", result.StandardOutput);
            Assert.Contains("World guard monster-protection attacks before close: count=2", result.StandardOutput);
            Assert.Contains("World monster death/drop/remove/respawn before close: count=3", result.StandardOutput);
            Assert.Contains("Client missing-animation errors: count=2", result.StandardOutput);
            Assert.Contains("World semantic combat before close:", result.StandardOutput);
            Assert.Contains("Client close/error symptoms:", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ReportsHistoricalCombatGearFloorPassiveCompatibilityEvidence()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "wolfconquer-combat-close-evidence-historical-floor-passive-" + Guid.NewGuid().ToString("N"));
        var worldLog = Path.Combine(tempRoot, "WorldServer.out.log");

        try
        {
            await WriteFixtureFileAsync(
                worldLog,
                """
                Newer-client CombatGear floor-passive compatibility enabled.
                WorldServer accepted client 127.0.0.1:51165 on 127.0.0.1:5818
                WorldServer legacy MsgLoginGame Success key=1293600 uid=1293600 map=1002.
                WorldServer closed client 127.0.0.1:51165 on 127.0.0.1:5818
                """);

            var result = await RunDataToolAsync(
                "combat-close-evidence",
                "--world-log",
                worldLog);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("World combat compatibility fences before close: count=1", result.StandardOutput);
            Assert.Contains(
                "line=1 preview=Newer-client CombatGear floor-passive compatibility enabled.",
                result.StandardOutput);
            Assert.DoesNotContain(
                "Legacy CombatGear monster-hit passive spell blocklist enabled for client compatibility",
                result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HelpIncludesCombatCloseEvidenceUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool combat-close-evidence --world-log path [--client-log path] [--world-since-line N] [--client-since-line N]",
            result.StandardOutput);
    }

    private static async Task<ConsoleRunResult> RunDataToolAsync(params string[] arguments)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var process = CreateDataToolProcess(repositoryRoot);
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail($"DataTool did not exit. stdout: {await outputTask} stderr: {await errorTask}");
        }

        return new ConsoleRunResult(process.ExitCode, await outputTask, await errorTask);
    }

    private static Process CreateDataToolProcess(string repositoryRoot)
    {
        var copiedDataTool = Path.Combine(AppContext.BaseDirectory, "WolfConquer.DataTool.dll");
        if (!File.Exists(copiedDataTool))
        {
            return TestProjectProcess.Create(repositoryRoot, "WolfConquer.DataTool");
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = repositoryRoot,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.ArgumentList.Add(copiedDataTool);
        process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        process.StartInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        return process;
    }

    private static async Task WriteFixtureFileAsync(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, contents);
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourcePath = "")
    {
        var fromOutputDirectory = TryFindRepositoryRoot(AppContext.BaseDirectory);
        if (fromOutputDirectory is not null)
        {
            return fromOutputDirectory;
        }

        var fromWorkingDirectory = TryFindRepositoryRoot(Environment.CurrentDirectory);
        if (fromWorkingDirectory is not null)
        {
            return fromWorkingDirectory;
        }

        var sourceDirectory = Path.GetDirectoryName(sourcePath);
        if (sourceDirectory is not null)
        {
            var fromSourceDirectory = TryFindRepositoryRoot(sourceDirectory);
            if (fromSourceDirectory is not null)
            {
                return fromSourceDirectory;
            }
        }

        throw new InvalidOperationException("Could not locate WolfConquer.sln from the test output or working directory.");
    }

    private static string? TryFindRepositoryRoot(string startDirectory)
    {
        DirectoryInfo? directory = new(startDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}
