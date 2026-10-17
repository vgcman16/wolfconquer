using System.Buffers;
using System.Net;
using System.Net.Sockets;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed class LegacyAccServerTcpServerTests
{
    [Fact]
    public async Task LegacyAccountServerWireModeSendsEncryptedPasswordSeed()
    {
        await using var server = CreateLegacyLoopbackServer((_, _, _) => ValueTask.CompletedTask);

        server.Start();
        using var client = await ConnectAsync(server);

        var encryptedSeed = await ReadExactlyAsync(client.GetStream(), LegacyAccountPasswordSeedPacket.FrameLength);
        var plainSeed = encryptedSeed.ToArray();
        new LegacyAccServerCipher().DecryptFromServer(plainSeed);

        Assert.False(encryptedSeed.SequenceEqual(plainSeed));
        Assert.False(PacketReader.TryRead(encryptedSeed, out _));
        Assert.True(LegacyAccountPasswordSeedPacket.TryParse(ReadFrame(plainSeed), out _));
    }

    [Fact]
    public async Task LegacyAccountServerWireModeDispatchesEncryptedAuthenticationAndReturnsEncryptedForward()
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
        using var client = await ConnectAsync(server);
        var stream = client.GetStream();
        var serverDecryptCipher = new LegacyAccServerCipher();
        var encryptedSeed = await ReadExactlyAsync(stream, LegacyAccountPasswordSeedPacket.FrameLength);
        serverDecryptCipher.DecryptFromServer(encryptedSeed);
        Assert.True(LegacyAccountPasswordSeedPacket.TryParse(ReadFrame(encryptedSeed), out _));

        var encryptedRequest = new LegacyAccountAuthenticationPacket("user02", "fixture-pw", "TestSrv").ToFrameBytes();
        new LegacyAccServerCipher().EncryptForServer(encryptedRequest);
        await stream.WriteAsync(encryptedRequest);

        var encryptedForward = await ReadExactlyAsync(stream, LegacyAccountForwardPacket.FrameLength);
        var plainForward = encryptedForward.ToArray();
        serverDecryptCipher.DecryptFromServer(plainForward);

        Assert.False(encryptedForward.SequenceEqual(plainForward));
        Assert.True(LegacyAccountForwardPacket.TryParse(ReadFrame(plainForward), out var forward));
        Assert.Equal(1296473u, forward.Identifier);
        Assert.Equal(LegacyAccountForwardType.Ready, forward.Type);
        Assert.Equal(5818, forward.Port);
        Assert.Equal("127.0.0.1", forward.IP);

        var ticket = Assert.Single(store.Tickets);
        Assert.Equal(account.Id, ticket.AccountId);
        Assert.Equal("user02", ticket.Username);
        Assert.Equal(worldEndpoint, ticket.WorldEndpoint);
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

    private static async Task<TcpClient> ConnectAsync(ConquerTcpServer server)
    {
        var endpoint = server.BoundEndPoint;
        var client = new TcpClient();
        await client.ConnectAsync(endpoint.Address, endpoint.Port);
        return client;
    }

    private static async Task<byte[]> ReadExactlyAsync(NetworkStream stream, int length)
    {
        var buffer = new byte[length];
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await stream.ReadExactlyAsync(buffer, timeout.Token);
        return buffer;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
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
}
