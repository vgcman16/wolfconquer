using System.Net.Sockets;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed partial class LegacyAccServerTcpServerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyAccountWireRejectsMalformedAuthenticationDespiteFallbackAndKeepsValidLoginWorking(bool shortFrame)
    {
        var account = new AccountRecord(1293600, "2", "2", 0, AccountLoginService.AllowedBannedId, false);
        var repository = new CountingLegacyWireAccountRepository(account);
        var tickets = new InMemoryAccountHandoffTicketStore();
        var worldEndpoint = new ConquerEndpoint("WorldServer", "127.0.0.1", 5818);
        var handler = new AccountServerFrameHandler(
            new AccountLoginService(repository),
            new AccountHandoffService(tickets),
            worldEndpoint: worldEndpoint,
            legacyMalformedAuthFallbackRequest: new AccountLoginRequest("2", "2"));
        var rejectedRequestCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var validRequestCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatchCount = 0;
        await using var server = CreateLegacyLoopbackServer(async (connection, frame, token) =>
        {
            await handler.HandleFrameAsync(connection, frame, token);
            var completed = Interlocked.Increment(ref dispatchCount);
            if (completed == 1)
            {
                rejectedRequestCompleted.TrySetResult();
            }
            else if (completed == 2)
            {
                validRequestCompleted.TrySetResult();
            }
        });

        server.Start();
        using var client = await ConnectAsync(server);
        var stream = client.GetStream();
        var outboundCipher = new LegacyAccServerCipher();
        var inboundCipher = new LegacyAccServerCipher();
        var encryptedSeed = await ReadExactlyAsync(stream, LegacyAccountPasswordSeedPacket.FrameLength);
        inboundCipher.DecryptFromServer(encryptedSeed);
        Assert.True(LegacyAccountPasswordSeedPacket.TryParse(ReadFrame(encryptedSeed), out _));

        var malformedRequest = CreateMalformedLegacyWireRequest(shortFrame);
        var malformedFrame = ReadFrame(malformedRequest);
        Assert.Equal(malformedRequest.Length, malformedFrame.Frame.Length);
        Assert.Equal(malformedRequest.Length, malformedFrame.Header.Length);
        outboundCipher.EncryptForServer(malformedRequest);
        await stream.WriteAsync(malformedRequest);

        var rejection = await ReadLegacyWireForwardAsync(stream, inboundCipher);
        await rejectedRequestCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(LegacyAccountForwardType.InvalidAuthenticationProtocol, rejection.Type);
        Assert.Equal(0u, rejection.Identifier);
        Assert.Equal((ushort)0, rejection.Port);
        Assert.Equal(string.Empty, rejection.IP);
        Assert.Equal(0, repository.ReadCount);
        Assert.Empty(tickets.Tickets);
        Assert.Equal(1, Volatile.Read(ref dispatchCount));

        // Reuse both real wire counters and the existing socket. Rejection must
        // not grant authority, poison framing, or require a replacement session.
        var validRequest = new LegacyAccountAuthenticationPacket("2", "2", "Wolf").ToFrameBytes();
        outboundCipher.EncryptForServer(validRequest);
        await stream.WriteAsync(validRequest);
        var ready = await ReadLegacyWireForwardAsync(stream, inboundCipher);
        await validRequestCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(LegacyAccountForwardType.Ready, ready.Type);
        Assert.Equal(1293600u, ready.Identifier);
        Assert.Equal((ushort)5818, ready.Port);
        Assert.Equal("127.0.0.1", ready.IP);
        Assert.Equal(1, repository.ReadCount);
        var ticket = Assert.Single(tickets.Tickets);
        Assert.Equal(account.Id, ticket.AccountId);
        Assert.Equal(account.Username, ticket.Username);
        Assert.Equal(worldEndpoint, ticket.WorldEndpoint);
        Assert.Equal(2, Volatile.Read(ref dispatchCount));
    }

    private static byte[] CreateMalformedLegacyWireRequest(bool shortFrame)
    {
        if (shortFrame)
        {
            var bytes = new LegacyAccountAuthenticationPacket("2", "2", "Wolf").ToFrameBytes()[..^1];
            new PacketHeader((ushort)bytes.Length, AccountPacketOpcodes.LegacyAuthentication).WriteTo(bytes);
            return bytes;
        }

        var writer = new PacketWriter(AccountPacketOpcodes.LegacyAuthentication, LegacyAccountAuthenticationPacket.PayloadLength);
        writer.Payload[0] = 149;
        writer.Payload[1] = 212;
        writer.Payload[2] = 230;
        return writer.ToArray();
    }

    private static async Task<LegacyAccountForwardPacket> ReadLegacyWireForwardAsync(
        NetworkStream stream,
        LegacyAccServerCipher cipher)
    {
        var encrypted = await ReadExactlyAsync(stream, LegacyAccountForwardPacket.FrameLength);
        var plain = encrypted.ToArray();
        cipher.DecryptFromServer(plain);
        Assert.False(encrypted.SequenceEqual(plain));
        Assert.True(LegacyAccountForwardPacket.TryParse(ReadFrame(plain), out var packet));
        return packet;
    }

    private sealed class CountingLegacyWireAccountRepository(AccountRecord account) : IAccountRepository
    {
        private int _readCount;

        public int ReadCount => Volatile.Read(ref _readCount);

        public ValueTask<IReadOnlyList<AccountRecord>> FindByUsernameAsync(
            string username,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _readCount);
            IReadOnlyList<AccountRecord> result = string.Equals(username, account.Username, StringComparison.Ordinal)
                ? [account]
                : [];
            return ValueTask.FromResult(result);
        }
    }
}
