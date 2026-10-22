using System.Diagnostics;
using System.Net;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.Web;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection(LoginLoaderConfigurationCollection.Name)]
public sealed class DataToolLocalLiveStackSmokeTests : IDisposable
{
    private readonly LoginLoaderPayloadFixture _loaderPayloads = new();
    private readonly string _usersRoot = Path.Combine(Path.GetTempPath(), "wolf-local-live-smoke-" + Guid.NewGuid());

    [Fact]
    public async Task LocalLiveStackSmokeProvesLoaderGatewayAccountAndWorldRoute()
    {
        using var beastMetadata = new NativeLoginBeastMetadataScope(1000);
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1_400_123);

        await using var loginWeb = LoginLoaderHttpServer.Start(IPAddress.Loopback, port: 0);
        await using var gateway = CreateLegacyGuardShieldLoopbackServer();
        await using var world = CreateLegacyWorldLoopbackServer(
            new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot)).HandleFrameAsync);
        world.Start();
        await using var account = CreateLegacyAccountLoopbackServer(
            new AccountServerFrameHandler(
                new AccountLoginService(new FakeAccountRepository(
                    new AccountRecord(
                        1_400_123,
                        "fixture-user",
                        "test-pass-01",
                        EntityId: 0,
                        BannedId: AccountLoginService.AllowedBannedId,
                        IsOnline: false))),
                new AccountHandoffService(new InMemoryAccountHandoffTicketStore()),
                worldEndpoint: new ConquerEndpoint(
                    "WorldServer",
                    "127.0.0.1",
                    (ushort)world.BoundEndPoint.Port)).HandleFrameAsync);
        gateway.Start();
        account.Start();

        var result = await RunDataToolAsync(
            "local-live-stack-smoke",
            "127.0.0.1",
            gateway.BoundEndPoint.Port.ToString(),
            account.BoundEndPoint.Port.ToString(),
            "fixture-user",
            "test-pass-01",
            "FixtureRealm",
            "--login-web-base-url",
            $"http://127.0.0.1:{loginWeb.BoundEndPoint.Port}/",
            "--expect-world-ip",
            "127.0.0.1",
            "--expect-world-port",
            world.BoundEndPoint.Port.ToString(),
            "--timeout-ms",
            "10000");

        Assert.True(
            result.ExitCode == 0,
            $"DataTool exit code: {result.ExitCode}{Environment.NewLine}stdout: {result.StandardOutput}{Environment.NewLine}stderr: {result.StandardError}");
        Assert.Contains("Local live stack smoke:", result.StandardOutput);
        Assert.Contains("loginweb=http://127.0.0.1:", result.StandardOutput);
        Assert.Contains("ready=True", result.StandardOutput);
        Assert.Contains($"gateway=127.0.0.1:{gateway.BoundEndPoint.Port} response=", result.StandardOutput);
        Assert.Contains(
            $"account=127.0.0.1:{account.BoundEndPoint.Port} forward identifier=1400123 type=Ready ip=127.0.0.1 port={world.BoundEndPoint.Port}",
            result.StandardOutput);
        Assert.Contains(
            $"world=127.0.0.1:{world.BoundEndPoint.Port} accountKey=1400123 answerOk=True status=answer-ok",
            result.StandardOutput);
        Assert.True(result.StandardOutput.Contains(
            "observed-opcodes=2179" + Environment.NewLine,
            StringComparison.Ordinal), result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task HelpMentionsLocalLiveStackSmokeCommand()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool local-live-stack-smoke <address> <gatewayPort> <accountPort> <username> <password> <serverName> [--login-web-base-url url] [--expect-world-ip ip] [--expect-world-port port] [--timeout-ms ms]",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    public void Dispose()
    {
        _loaderPayloads.Dispose();
        if (Directory.Exists(_usersRoot))
        {
            Directory.Delete(_usersRoot, recursive: true);
        }
    }

    private static ConquerTcpServer CreateLegacyGuardShieldLoopbackServer()
    {
        return new ConquerTcpServer(
            new ConquerTcpServerOptions(
                ConquerRuntimeDefaults.Gateway,
                IPAddress.Loopback,
                ListenPort: 0,
                WireMode: ConquerTcpWireMode.LegacyGuardShield,
                GuardShieldTextProtocol: LegacyGuardShieldTextProtocolOptions.FromEnvironment()),
            (_, _, _) => throw new InvalidOperationException("GuardShield text mode must not dispatch packet frames."));
    }

    private static ConquerTcpServer CreateLegacyAccountLoopbackServer(
        Func<ConquerTcpConnection, PacketFrame, CancellationToken, ValueTask> handleFrame)
    {
        return new ConquerTcpServer(
            new ConquerTcpServerOptions(
                ConquerRuntimeDefaults.AccountServer,
                IPAddress.Loopback,
                ListenPort: 0,
                ServerFirstPayloadFactory: () => ConquerServerFirstPayloads.Create(ConquerRuntimeDefaults.AccountServer),
                WireMode: ConquerTcpWireMode.LegacyAccountServer),
            handleFrame);
    }

    private static ConquerTcpServer CreateLegacyWorldLoopbackServer(
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
            UID=1400123
            Body=1003
            Face=1
            Haire=0
            Name=FixturePlayer
            Class=10
            FirstClass=0
            SecoundeClass=0
            Reborn=0
            Map=1000
            X=50
            Y=50
            Level=10
            Experience=0
            PMap=1000
            PMapX=50
            PMapY=50
            Agility=10
            Strength=10
            Vitaliti=10
            Spirit=10
            Atributes=0
            MinHitPoints=100
            MinMana=100
            Money=0
            ConquerPoints=0
            BoundConquerPoints=0
            BankCps=0
            PkPoints=0
            Online=0

            """);
    }

    private async Task<ConsoleRunResult> RunDataToolAsync(params string[] arguments)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var process = TestProjectProcess.Create(repositoryRoot, "WolfConquer.DataTool");
        _loaderPayloads.ConfigureReadinessProcess(process);
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

    private sealed class FakeAccountRepository(params AccountRecord[] accounts) : IAccountRepository
    {
        public ValueTask<IReadOnlyList<AccountRecord>> FindByUsernameAsync(
            string username,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<AccountRecord> matches = accounts
                .Where(account => string.Equals(account.Username, username, StringComparison.Ordinal))
                .ToArray();
            return ValueTask.FromResult(matches);
        }
    }

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}

