using System.Diagnostics;
using System.Net;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed class DataToolLegacyAccountSmokeTests
{
    [Fact]
    public async Task LegacyAccountSmokePrintsForwardPacket()
    {
        var store = new InMemoryAccountHandoffTicketStore();
        var worldEndpoint = new ConquerEndpoint("WorldServer", "127.0.0.1", 5818);
        var account = new AccountRecord(
            1296473,
            "user02",
            "fixture-pw",
            EntityId: 0x0013BEED,
            BannedId: AccountLoginService.AllowedBannedId,
            IsOnline: false);
        var handler = new AccountServerFrameHandler(
            new AccountLoginService(new FakeAccountRepository(account)),
            new AccountHandoffService(store),
            worldEndpoint: worldEndpoint);
        await using var server = CreateLegacyLoopbackServer(handler.HandleFrameAsync);

        server.Start();
        var result = await RunDataToolAsync(
            "legacy-account-smoke",
            "127.0.0.1",
            server.BoundEndPoint.Port.ToString(),
            "user02",
            "fixture-pw",
            "TestSrv",
            "--expect-ip",
            "127.0.0.1",
            "--expect-port",
            "5818");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "forward identifier=1296473 type=Ready ip=127.0.0.1 port=5818",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);

        var ticket = Assert.Single(store.Tickets);
        Assert.Equal(account.Id, ticket.AccountId);
        Assert.Equal("user02", ticket.Username);
        Assert.Equal(worldEndpoint, ticket.WorldEndpoint);
    }

    [Fact]
    public async Task LegacyAccountSmokeReturnsNonzeroForExpectationMismatch()
    {
        var handler = new AccountServerFrameHandler(
            new AccountLoginService(new FakeAccountRepository(
                new AccountRecord(
                    1296473,
                    "user02",
                    "fixture-pw",
                    EntityId: 0x0013BEED,
                    BannedId: AccountLoginService.AllowedBannedId,
                    IsOnline: false))),
            new AccountHandoffService(new InMemoryAccountHandoffTicketStore()),
            worldEndpoint: new ConquerEndpoint("WorldServer", "127.0.0.1", 5818));
        await using var server = CreateLegacyLoopbackServer(handler.HandleFrameAsync);

        server.Start();
        var result = await RunDataToolAsync(
            "legacy-account-smoke",
            "127.0.0.1",
            server.BoundEndPoint.Port.ToString(),
            "user02",
            "fixture-pw",
            "TestSrv",
            "--expect-ip",
            "127.0.0.2");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains(
            "forward identifier=1296473 type=Ready ip=127.0.0.1 port=5818",
            result.StandardOutput);
        Assert.Contains(
            "Expected forward ip 127.0.0.2 but received 127.0.0.1.",
            result.StandardError);
    }

    [Fact]
    public async Task HelpMentionsLegacyAccountSmokeCommand()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool legacy-account-smoke <address> <port> <username> <password> <serverName> [--expect-ip ip] [--expect-port port] [--timeout-ms ms]",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    private static ConquerTcpServer CreateLegacyLoopbackServer(
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
