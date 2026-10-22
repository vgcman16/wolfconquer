using System.Diagnostics;
using System.Net;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection(DataToolLegacyWorldSmokeTestCollection.Name)]
public sealed class DataToolLegacyWorldSmokeTests : IDisposable
{
    private readonly string _usersRoot = Path.Combine(Path.GetTempPath(), "wolf-world-smoke-" + Guid.NewGuid());

    [Fact]
    public async Task LegacyWorldSmokePrintsAnswerOk()
    {
        using var beastMetadata = new NativeLoginBeastMetadataScope(1000);
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1_293_600);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot));
        await using var server = CreateLegacyLoopbackServer(handler.HandleFrameAsync);

        server.Start();
        var result = await RunDataToolAsync(
            "legacy-world-smoke",
            "127.0.0.1",
            server.BoundEndPoint.Port.ToString(),
            "1293600",
            "--timeout-ms",
            "10000");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Legacy world smoke:", result.StandardOutput);
        Assert.Contains($"endpoint=127.0.0.1:{server.BoundEndPoint.Port}", result.StandardOutput);
        Assert.Contains("accountKey=1293600", result.StandardOutput);
        Assert.Contains("answerOk=True status=answer-ok", result.StandardOutput);
        Assert.True(result.StandardOutput.Contains(
            "observed-opcodes=2179" + Environment.NewLine,
            StringComparison.Ordinal), result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task LegacyWorldSmokeReturnsNonzeroWhenAnswerOkIsNotSeen()
    {
        Directory.CreateDirectory(_usersRoot);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot));
        await using var server = CreateLegacyLoopbackServer(handler.HandleFrameAsync);

        server.Start();
        var result = await RunDataToolAsync(
            "legacy-world-smoke",
            "127.0.0.1",
            server.BoundEndPoint.Port.ToString(),
            "1293600",
            "--timeout-ms",
            "1000");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("accountKey=1293600", result.StandardOutput);
        Assert.Contains("answerOk=False status=timeout", result.StandardOutput);
        Assert.Contains("observed-opcodes=2179", result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task LegacyWorldCaptureWritesBoundedSnapshotWhenAnswerOkIsSeen()
    {
        using var beastMetadata = new NativeLoginBeastMetadataScope(1000);
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1_293_600);
        var outputPath = Path.Combine(_usersRoot, "legacy-world-capture.json");
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot));
        await using var server = CreateLegacyLoopbackServer(handler.HandleFrameAsync);

        server.Start();
        var result = await RunDataToolAsync(
            "legacy-world-capture",
            "127.0.0.1",
            server.BoundEndPoint.Port.ToString(),
            "1293600",
            outputPath,
            "--timeout-ms",
            "10000",
            "--max-frames",
            "24",
            "--sample-bytes",
            "8");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Legacy world capture:", result.StandardOutput);
        Assert.Contains($"endpoint=127.0.0.1:{server.BoundEndPoint.Port}", result.StandardOutput);
        Assert.Contains("accountKey=1293600", result.StandardOutput);
        Assert.Contains(outputPath, result.StandardOutput);
        Assert.Contains("answerOk=True", result.StandardOutput);
        Assert.True(result.StandardOutput.Contains(
            "observed-opcodes=2179, 2161, 2224, 2047, 22210, 2221, 2165, 2165",
            StringComparison.Ordinal), result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);

        Assert.True(File.Exists(outputPath), "The capture command should write its JSON output.");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
        var root = document.RootElement;
        Assert.Equal("127.0.0.1", root.GetProperty("address").GetString());
        Assert.Equal(server.BoundEndPoint.Port, root.GetProperty("port").GetInt32());
        Assert.Equal(1_293_600, root.GetProperty("accountKey").GetInt64());
        Assert.True(root.GetProperty("answerOkSeen").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("status").GetString()));

        var frames = root.GetProperty("frames");
        Assert.InRange(frames.GetArrayLength(), 21, 24);
        var firstFrame = frames[0];
        Assert.Equal(new[] { 2179, 2161, 2224, 2047, 22210, 2221, 2165, 2165 },
            frames.EnumerateArray().Take(8).Select(frame => frame.GetProperty("opcode").GetInt32()));
        Assert.True(firstFrame.TryGetProperty("length", out var length), "Captured frames should include a length field.");
        Assert.True(length.GetInt32() > 0);
        Assert.True(firstFrame.TryGetProperty("sampleLength", out var sampleLength), "Captured frames should include a sampleLength field.");
        Assert.InRange(sampleLength.GetInt32(), 0, 8);
        Assert.True(firstFrame.TryGetProperty("firstHex", out var firstHex), "Captured frames should include a firstHex field.");
        Assert.False(string.IsNullOrWhiteSpace(firstHex.GetString()));
        Assert.True(firstFrame.TryGetProperty("firstBase64", out var firstBase64), "Captured frames should include a firstBase64 field.");
        Assert.False(string.IsNullOrWhiteSpace(firstBase64.GetString()));
    }

    [Fact]
    public async Task LegacyWorldCaptureWritesSnapshotWhenAnswerOkIsNotSeen()
    {
        Directory.CreateDirectory(_usersRoot);
        var outputPath = Path.Combine(_usersRoot, "legacy-world-capture-timeout.json");
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot));
        await using var server = CreateLegacyLoopbackServer(handler.HandleFrameAsync);

        server.Start();
        var result = await RunDataToolAsync(
            "legacy-world-capture",
            "127.0.0.1",
            server.BoundEndPoint.Port.ToString(),
            "1293600",
            outputPath,
            "--timeout-ms",
            "1000",
            "--max-frames",
            "2",
            "--sample-bytes",
            "8");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Legacy world capture:", result.StandardOutput);
        Assert.Contains("accountKey=1293600", result.StandardOutput);
        Assert.Contains("answerOk=False", result.StandardOutput);
        Assert.Contains("observed-opcodes=2179", result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);

        Assert.True(File.Exists(outputPath), "The capture command should write JSON even when ANSWER_OK is not seen.");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
        var root = document.RootElement;
        Assert.Equal("127.0.0.1", root.GetProperty("address").GetString());
        Assert.Equal(server.BoundEndPoint.Port, root.GetProperty("port").GetInt32());
        Assert.Equal(1_293_600, root.GetProperty("accountKey").GetInt64());
        Assert.False(root.GetProperty("answerOkSeen").GetBoolean());
        Assert.Contains(root.GetProperty("status").GetString(), new[] { "timeout", "closed" });
        var frames = root.GetProperty("frames");
        Assert.Equal(1, frames.GetArrayLength());
        Assert.Equal(2179, frames[0].GetProperty("opcode").GetInt32());
    }

    [Fact]
    public async Task HelpMentionsLegacyWorldSmokeCommand()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool legacy-world-smoke <address> <port> <accountKey> [--timeout-ms ms]",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task HelpMentionsLegacyWorldCaptureCommand()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool legacy-world-capture <address> <port> <accountKey> <outputPath> [--timeout-ms ms] [--max-frames N] [--sample-bytes N]",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    public void Dispose()
    {
        if (Directory.Exists(_usersRoot))
        {
            Directory.Delete(_usersRoot, recursive: true);
        }
    }

    private static ConquerTcpServer CreateLegacyLoopbackServer(
        Func<ConquerTcpConnection, PacketFrame, CancellationToken, ValueTask> handleFrame)
    {
        return new ConquerTcpServer(
            new ConquerTcpServerOptions(
                ConquerRuntimeDefaults.WorldServer,
                IPAddress.Loopback,
                ListenPort: 0,
                WireMode: ConquerTcpWireMode.LegacyGameServer),
            handleFrame);
    }

    private void WriteSnapshot(long accountId)
    {
        File.WriteAllText(
            Path.Combine(_usersRoot, accountId + ".ini"),
            """
            [Character]
            UID=1293600
            Body=1008
            Face=154
            Name=HeroOne
            Class=7051
            Map=1015
            X=830
            Y=584
            FirstClass=0
            SecoundeClass=7050
            Conquer=0
            Reborn=2
            Level=140
            Haire=27
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=36
            Strength=0
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            ConquerPoints=639778904
            BankCps=123
            BoundConquerPoints=456
            Money=98602439956
            GuildDontion=900
            PkPoints=0
            Online=7
            Day=0
            Enilghten=200
            Achivement=0/171966688/262144/523784/939949824/524288/1342381056/3/576/0/0/524288/25167856/6817280/0/
            Activeness=0/80/2/1/10/1/22/0/0/
            Dragon=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            Pheonix=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            Tiger=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            Turtle=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            ChiPoints=153850

            [PaidNobility]
            Rank=King
            Identifier=1293600
            """);
    }

    private static async Task<ConsoleRunResult> RunDataToolAsync(params string[] arguments)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var process = TestProjectProcess.Create(repositoryRoot, "WolfConquer.DataTool");
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start(), "Could not start DataTool.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
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

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate WolfConquer.sln from the test output directory.");
    }

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}

[CollectionDefinition("DataTool legacy world smoke", DisableParallelization = true)]
public sealed class DataToolLegacyWorldSmokeTestCollection
{
    public const string Name = "DataTool legacy world smoke";
}
