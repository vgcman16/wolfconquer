using System.Buffers;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed partial class AccountServerFrameHandlerTests
{
    [Fact]
    public void InternalLoginPacketRoundTripsNullTerminatedUtf8Fields()
    {
        var request = new InternalAccountLoginPacket("user02", "fixture-pw");
        var frame = ReadFrame(request.ToFrameBytes());

        Assert.True(InternalAccountLoginPacket.TryParse(frame, out var parsed));
        Assert.Equal("user02", parsed.Username);
        Assert.Equal("fixture-pw", parsed.Password);
    }

    [Fact]
    public void InternalLoginPacketRejectsMissingPasswordTerminator()
    {
        var writer = new PacketWriter(AccountPacketOpcodes.InternalLoginRequest, payloadLength: 7);
        "user02"u8.CopyTo(writer.Payload);
        writer.Payload[^1] = (byte)'x';

        Assert.False(InternalAccountLoginPacket.TryParse(ReadFrame(writer.ToArray()), out _));
    }

    [Fact]
    public async Task HandlerSendsSuccessResponseForValidatedAccount()
    {
        var account = Account(1296473, "user02", "fixture-pw", entityId: 1293600);
        var handler = new AccountServerFrameHandler(
            new AccountLoginService(new FakeAccountRepository(account)));
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(connection, ReadFrame(new InternalAccountLoginPacket("user02", "fixture-pw").ToFrameBytes()));

        AssertResponse(sent.Single(), AccountLoginWireStatus.Success, account.Id);
    }

    [Fact]
    public async Task HandlerIssuesHandoffTicketForValidatedAccount()
    {
        var store = new InMemoryAccountHandoffTicketStore();
        var handoff = new AccountHandoffService(store);
        var account = Account(1296473, "user02", "fixture-pw", entityId: 1293600);
        var handler = new AccountServerFrameHandler(
            new AccountLoginService(new FakeAccountRepository(account)),
            handoff);
        var sent = new List<byte[]>();

        await handler.HandleFrameAsync(
            CreateSendingConnection(sent),
            ReadFrame(new InternalAccountLoginPacket("user02", "fixture-pw").ToFrameBytes()));

        AssertResponse(sent.Single(), AccountLoginWireStatus.Success, account.Id);
        Assert.True(PacketReader.TryRead(sent.Single(), out var reader));
        Assert.True(AccountLoginResponsePacket.TryReadHandoff(reader, out var token, out var endpoint));
        Assert.True(store.TryGet(token, out var ticket));
        Assert.Equal(account.Id, ticket.AccountId);
        Assert.Equal("user02", ticket.Username);
        Assert.Equal(ConquerRuntimeDefaults.WorldServer, ticket.WorldEndpoint);
        Assert.Equal("127.0.0.1:5818", endpoint);
    }

    [Fact]
    public async Task HandlerSendsLegacyForwardForValidatedAuthentication()
    {
        var store = new InMemoryAccountHandoffTicketStore();
        var handoff = new AccountHandoffService(store);
        var worldEndpoint = new ConquerEndpoint("WorldServer", "127.0.0.1", 5818);
        var account = Account(0x0013BEED, "user02", "fixture-pw", entityId: 0x0013C89A);
        var handler = new AccountServerFrameHandler(
            new AccountLoginService(new FakeAccountRepository(account)),
            handoff,
            worldEndpoint: worldEndpoint);
        var sent = new List<byte[]>();

        await handler.HandleFrameAsync(
            CreateSendingConnection(sent),
            ReadFrame(new LegacyAccountAuthenticationPacket("user02", "fixture-pw", "TestSrv").ToFrameBytes()));

        AssertLegacyForward(
            sent.Single(),
            LegacyAccountForwardType.Ready,
            identifier: 0x0013BEED,
            port: 5818,
            ip: "127.0.0.1");
        var ticket = Assert.Single(store.Tickets);
        Assert.Equal(account.Id, ticket.AccountId);
        Assert.Equal("user02", ticket.Username);
        Assert.Equal(worldEndpoint, ticket.WorldEndpoint);
    }

    [Fact]
    public async Task HandlerUsesAccountIdForLegacyIdentifierWhenEntityIdDiffers()
    {
        var store = new InMemoryAccountHandoffTicketStore();
        var handoff = new AccountHandoffService(store);
        var worldEndpoint = new ConquerEndpoint("WorldServer", "127.0.0.1", 5818);
        // This authored 12-byte fixture encrypts to a NUL at byte 10, exercising raw-password fallback.
        var account = Account(1296474, "user03", "fixture01-1w", entityId: 1293600);
        var handler = new AccountServerFrameHandler(
            new AccountLoginService(new FakeAccountRepository(account)),
            handoff,
            worldEndpoint: worldEndpoint);
        var sent = new List<byte[]>();

        await handler.HandleFrameAsync(
            CreateSendingConnection(sent),
            ReadFrame(new LegacyAccountAuthenticationPacket("user03", "fixture01-1w", "TestSrv").ToFrameBytes()));

        AssertLegacyForward(
            sent.Single(),
            LegacyAccountForwardType.Ready,
            identifier: 1296474,
            port: 5818,
            ip: "127.0.0.1");
        Assert.Equal(1296474, Assert.Single(store.Tickets).AccountId);
    }

    [Fact]
    public async Task HandlerSendsLegacyCaptureOverrideForwardWithoutRepository()
    {
        var worldEndpoint = new ConquerEndpoint("WorldServer", "127.0.0.1", 15819);
        var handler = new AccountServerFrameHandler(
            loginService: null,
            handoffService: null,
            worldEndpoint: worldEndpoint,
            legacyCaptureOverrideIdentifier: 0x0013BEED);
        var sent = new List<byte[]>();

        await handler.HandleFrameAsync(
            CreateSendingConnection(sent),
            ReadFrame(new LegacyAccountAuthenticationPacket(string.Empty, string.Empty, "TestSrv").ToFrameBytes()));

        AssertLegacyForward(
            sent.Single(),
            LegacyAccountForwardType.Ready,
            identifier: 0x0013BEED,
            port: 15819,
            ip: "127.0.0.1");
    }

    [Fact]
    public async Task HandlerSendsWrongPasswordResponseWhenPasswordDoesNotMatch()
    {
        var handler = new AccountServerFrameHandler(
            new AccountLoginService(new FakeAccountRepository(Account(1296473, "user02", "fixture-pw"))));
        var sent = new List<byte[]>();

        await handler.HandleFrameAsync(
            CreateSendingConnection(sent),
            ReadFrame(new InternalAccountLoginPacket("user02", "wrong").ToFrameBytes()));

        AssertResponse(sent.Single(), AccountLoginWireStatus.WrongPassword, accountId: 0);
    }

    [Fact]
    public async Task HandlerSendsLegacyInvalidInfoForwardWhenPasswordDoesNotMatch()
    {
        var store = new InMemoryAccountHandoffTicketStore();
        using var log = new StringWriter();
        var handler = new AccountServerFrameHandler(
            new AccountLoginService(new FakeAccountRepository(Account(1296473, "user02", "fixture-pw"))),
            new AccountHandoffService(store),
            log);
        var sent = new List<byte[]>();

        await handler.HandleFrameAsync(
            CreateSendingConnection(sent),
            ReadFrame(new LegacyAccountAuthenticationPacket("user02", "wrong", "TestSrv").ToFrameBytes()));

        AssertLegacyForward(
            sent.Single(),
            LegacyAccountForwardType.InvalidInfo,
            identifier: 0,
            port: 0,
            ip: string.Empty);
        Assert.Empty(store.Tickets);
        var output = log.ToString();
        Assert.Contains("AccountServer legacy login password diagnostic", output);
        Assert.Contains("requestLength=5", output);
        Assert.Contains("candidateCount=1", output);
        Assert.Contains("bestStoredLength=10", output);
        Assert.Contains("bestCommonPrefixLength=0", output);
        Assert.Contains("bestLengthDelta=-5", output);
        Assert.Contains("tailShape=none", output);
        Assert.Contains("AccountServer legacy login InvalidInfo reason=WrongPassword for user02.", output);
        Assert.DoesNotContain("wrong", output);
        Assert.DoesNotContain("fixture-pw", output);
    }

    [Fact]
    public async Task HandlerLogsExactPasswordOwnerDiagnosticForLegacyWrongPassword()
    {
        var store = new InMemoryAccountHandoffTicketStore();
        using var log = new StringWriter();
        var handler = new AccountServerFrameHandler(
            new AccountLoginService(new FakeAccountRepository(
                Account(1296474, "user03", "fixture01-1w"),
                Account(1296473, "user02", "fixture-pw"))),
            new AccountHandoffService(store),
            log);
        var sent = new List<byte[]>();

        await handler.HandleFrameAsync(
            CreateSendingConnection(sent),
            ReadFrame(new LegacyAccountAuthenticationPacket("user03", "fixture-pw", "TestSrv").ToFrameBytes()));

        AssertLegacyForward(
            sent.Single(),
            LegacyAccountForwardType.InvalidInfo,
            identifier: 0,
            port: 0,
            ip: string.Empty);
        Assert.Empty(store.Tickets);
        var output = log.ToString();
        Assert.Contains("exactPasswordOwnerCount=1", output);
        Assert.Contains("firstExactPasswordOwnerId=1296473", output);
        Assert.Contains("firstExactPasswordOwnerUsername=user02", output);
        Assert.DoesNotContain("fixture01-1w", output);
        Assert.DoesNotContain("fixture-pw", output);
    }

    [Fact]
    public async Task HandlerCanUseConfiguredLegacySavedPasswordOwnerRebind()
    {
        var store = new InMemoryAccountHandoffTicketStore();
        using var log = new StringWriter();
        var worldEndpoint = new ConquerEndpoint("WorldServer", "127.0.0.1", 5818);
        var target = Account(1296474, "user03", "fixture01-1w", entityId: 0x0013C89A);
        var handler = new AccountServerFrameHandler(
            new AccountLoginService(new FakeAccountRepository(
                target,
                Account(1296471, "guest", "fixture2-pw"))),
            new AccountHandoffService(store),
            log,
            worldEndpoint,
            legacySavedPasswordRebinds: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["user03"] = "guest",
            });
        var sent = new List<byte[]>();

        await handler.HandleFrameAsync(
            CreateSendingConnection(sent),
            ReadFrame(new LegacyAccountAuthenticationPacket("user03", "fixture2-pw", "TestSrv").ToFrameBytes()));

        AssertLegacyForward(
            sent.Single(),
            LegacyAccountForwardType.Ready,
            identifier: 1296474,
            port: 5818,
            ip: "127.0.0.1");
        Assert.Equal(target.Id, Assert.Single(store.Tickets).AccountId);
        var output = log.ToString();
        Assert.Contains("AccountServer legacy login password rebound", output);
        Assert.Contains("match=LegacySavedPasswordOwnerRebind", output);
        Assert.Contains("submittedPasswordOwnerUsername=guest", output);
        Assert.Contains("targetUsername=user03", output);
        Assert.Contains("AccountServer legacy login Ready reason=Success for user03.", output);
        Assert.DoesNotContain("fixture2-pw", output);
        Assert.DoesNotContain("fixture01-1w", output);
    }

    [Fact]
    public async Task HandlerSendsLegacyForwardForTrailingNoisePassword()
    {
        var store = new InMemoryAccountHandoffTicketStore();
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(store);
        var worldEndpoint = new ConquerEndpoint("WorldServer", "127.0.0.1", 5818);
        var account = Account(1296474, "user03", "fixture01-1w", entityId: 0x0013C89A);
        var handler = new AccountServerFrameHandler(
            new AccountLoginService(new FakeAccountRepository(account)),
            handoff,
            log,
            worldEndpoint);
        var sent = new List<byte[]>();

        await handler.HandleFrameAsync(
            CreateSendingConnection(sent),
            ReadFrame(new LegacyAccountAuthenticationPacket("user03", "fixture01-1wzz", "TestSrv").ToFrameBytes()));

        AssertLegacyForward(
            sent.Single(),
            LegacyAccountForwardType.Ready,
            identifier: 1296474,
            port: 5818,
            ip: "127.0.0.1");
        var ticket = Assert.Single(store.Tickets);
        Assert.Equal(account.Id, ticket.AccountId);
        var output = log.ToString();
        Assert.Contains("AccountServer legacy login password normalized match=LegacyTrailingNoise", output);
        Assert.Contains("requestLength=14", output);
        Assert.Contains("storedLength=12", output);
        Assert.Contains("trailingLength=2", output);
        Assert.Contains("AccountServer legacy login Ready reason=Success for user03.", output);
        Assert.DoesNotContain("fixture01-1w", output);
        Assert.DoesNotContain("zz", output);
    }

    [Fact]
    public async Task HandlerUsesRawPasswordFallbackWhenLegacyNullStripCorruptsEncryptedPassword()
    {
        var store = new InMemoryAccountHandoffTicketStore();
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(store);
        var worldEndpoint = new ConquerEndpoint("WorldServer", "127.0.0.1", 5818);
        var account = Account(1296474, "user03", "fixture01-1w", entityId: 0x0013C89A);
        var handler = new AccountServerFrameHandler(
            new AccountLoginService(new FakeAccountRepository(account)),
            handoff,
            log,
            worldEndpoint);
        var sent = new List<byte[]>();

        await handler.HandleFrameAsync(
            CreateSendingConnection(sent),
            ReadFrame(new LegacyAccountAuthenticationPacket("user03", "fixture01-1w", "TestSrv").ToFrameBytes()));

        AssertLegacyForward(
            sent.Single(),
            LegacyAccountForwardType.Ready,
            identifier: 1296474,
            port: 5818,
            ip: "127.0.0.1");
        Assert.Equal(account.Id, Assert.Single(store.Tickets).AccountId);
        var output = log.ToString();
        Assert.Contains("AccountServer legacy login password decoded fallback=RawEncryptedNull", output);
        Assert.Contains("AccountServer legacy login Ready reason=Success for user03.", output);
        Assert.DoesNotContain("fixture01-1w", output);
    }

    [Fact]
    public async Task HandlerDoesNotIssueHandoffTicketForWrongPassword()
    {
        var store = new InMemoryAccountHandoffTicketStore();
        var handler = new AccountServerFrameHandler(
            new AccountLoginService(new FakeAccountRepository(Account(1296473, "user02", "fixture-pw"))),
            new AccountHandoffService(store));
        var sent = new List<byte[]>();

        await handler.HandleFrameAsync(
            CreateSendingConnection(sent),
            ReadFrame(new InternalAccountLoginPacket("user02", "wrong").ToFrameBytes()));

        AssertResponse(sent.Single(), AccountLoginWireStatus.WrongPassword, accountId: 0);
        Assert.True(PacketReader.TryRead(sent.Single(), out var reader));
        Assert.False(AccountLoginResponsePacket.TryReadHandoff(reader, out _, out _));
        Assert.Empty(store.Tickets);
    }

    [Fact]
    public async Task HandlerSendsInvalidPacketResponseForMalformedInternalLogin()
    {
        var handler = new AccountServerFrameHandler(
            new AccountLoginService(new FakeAccountRepository(Account(1296473, "user02", "fixture-pw"))));
        var sent = new List<byte[]>();
        var writer = new PacketWriter(AccountPacketOpcodes.InternalLoginRequest, payloadLength: 3);
        writer.Payload[0] = (byte)'x';
        writer.Payload[1] = (byte)'y';
        writer.Payload[2] = (byte)'z';

        await handler.HandleFrameAsync(CreateSendingConnection(sent), ReadFrame(writer.ToArray()));

        AssertResponse(sent.Single(), AccountLoginWireStatus.InvalidPacket, accountId: 0);
    }

    [Fact]
    public async Task HandlerDoesNotIssueHandoffTicketForMalformedPacket()
    {
        var store = new InMemoryAccountHandoffTicketStore();
        var handler = new AccountServerFrameHandler(
            new AccountLoginService(new FakeAccountRepository(Account(1296473, "user02", "fixture-pw"))),
            new AccountHandoffService(store));
        var sent = new List<byte[]>();
        var writer = new PacketWriter(AccountPacketOpcodes.InternalLoginRequest, payloadLength: 3);
        writer.Payload[0] = (byte)'x';
        writer.Payload[1] = (byte)'y';
        writer.Payload[2] = (byte)'z';

        await handler.HandleFrameAsync(CreateSendingConnection(sent), ReadFrame(writer.ToArray()));

        AssertResponse(sent.Single(), AccountLoginWireStatus.InvalidPacket, accountId: 0);
        Assert.True(PacketReader.TryRead(sent.Single(), out var reader));
        Assert.False(AccountLoginResponsePacket.TryReadHandoff(reader, out _, out _));
        Assert.Empty(store.Tickets);
    }

    [Fact]
    public async Task HandlerSendsLegacyInvalidProtocolForwardForMalformedAuthentication()
    {
        var store = new InMemoryAccountHandoffTicketStore();
        var handler = new AccountServerFrameHandler(
            new AccountLoginService(new FakeAccountRepository(Account(1296473, "user02", "fixture-pw"))),
            new AccountHandoffService(store));
        var sent = new List<byte[]>();
        var writer = new PacketWriter(
            AccountPacketOpcodes.LegacyAuthentication,
            LegacyAccountAuthenticationPacket.PayloadLength - 1);

        await handler.HandleFrameAsync(CreateSendingConnection(sent), ReadFrame(writer.ToArray()));

        AssertLegacyForward(
            sent.Single(),
            LegacyAccountForwardType.InvalidAuthenticationProtocol,
            identifier: 0,
            port: 0,
            ip: string.Empty);
        Assert.Empty(store.Tickets);
    }

    [Fact]
    public async Task HandlerRejectsMalformedLegacyAuthenticationDespiteConfiguredFallback()
    {
        var store = new InMemoryAccountHandoffTicketStore();
        using var log = new StringWriter();
        var worldEndpoint = new ConquerEndpoint("WorldServer", "127.0.0.1", 5818);
        var account = Account(1293600, "2", "2");
        var handler = new AccountServerFrameHandler(
            new AccountLoginService(new FakeAccountRepository(account)),
            new AccountHandoffService(store),
            log,
            worldEndpoint,
            legacyMalformedAuthFallbackRequest: new AccountLoginRequest("2", "2"));
        var sent = new List<byte[]>();
        var writer = new PacketWriter(
            AccountPacketOpcodes.LegacyAuthentication,
            LegacyAccountAuthenticationPacket.PayloadLength);
        writer.Payload[0] = 149;
        writer.Payload[1] = 212;
        writer.Payload[2] = 230;

        await handler.HandleFrameAsync(CreateSendingConnection(sent), ReadFrame(writer.ToArray()));

        AssertLegacyForward(
            sent.Single(),
            LegacyAccountForwardType.InvalidAuthenticationProtocol,
            identifier: 0,
            port: 0,
            ip: string.Empty);
        Assert.Empty(store.Tickets);
        Assert.DoesNotContain("Ready", log.ToString());
    }

    [Fact]
    public async Task HandlerDoesNotSuccessDisconnectRejectedMalformedAuthenticationWhenCompatibilityEnabled()
    {
        var store = new InMemoryAccountHandoffTicketStore();
        var worldEndpoint = new ConquerEndpoint("WorldServer", "127.0.0.1", 5818);
        var handler = new AccountServerFrameHandler(
            new AccountLoginService(new FakeAccountRepository(Account(1293600, "2", "2"))),
            new AccountHandoffService(store),
            worldEndpoint: worldEndpoint,
            legacyMalformedAuthFallbackRequest: new AccountLoginRequest("2", "2"),
            disconnectLegacyAccountAfterReady: true);
        var sent = new List<byte[]>();
        var disconnected = false;
        var connection = new ConquerTcpConnection(
            null,
            null,
            (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            },
            _ =>
            {
                disconnected = true;
                return ValueTask.CompletedTask;
            });
        var writer = new PacketWriter(
            AccountPacketOpcodes.LegacyAuthentication,
            LegacyAccountAuthenticationPacket.PayloadLength);
        writer.Payload[0] = 149;
        writer.Payload[1] = 212;
        writer.Payload[2] = 230;

        await handler.HandleFrameAsync(connection, ReadFrame(writer.ToArray()));

        AssertLegacyForward(
            sent.Single(),
            LegacyAccountForwardType.InvalidAuthenticationProtocol,
            identifier: 0,
            port: 0,
            ip: string.Empty);
        Assert.False(disconnected);
        Assert.Empty(store.Tickets);
    }

    [Fact]
    public async Task HandlerLogsUnknownOpcodeAsStructuredParityGap()
    {
        using var log = new StringWriter();
        var handler = new AccountServerFrameHandler(loginService: null, log);
        var sent = new List<byte[]>();
        var writer = new PacketWriter(777, payloadLength: 2);
        writer.Payload[0] = 0xAA;
        writer.Payload[1] = 0xBB;

        await handler.HandleFrameAsync(CreateSendingConnection(sent), ReadFrame(writer.ToArray()));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains("PARITY-GAP category=unsupported-packet severity=warning gap=unsupported-packet", output);
        Assert.Contains("dedupe=unsupported-packet:unsupported-packet:opcode=777:packet=Account", output);
        Assert.Contains(
            "PARITY-GAP unknown account opcode opcode=777 payloadLength=2 payloadSampleHex=AABB from ",
            output);
    }

    [Fact]
    public async Task HandlerSilentlyIgnoresLegacyResDatProbe()
    {
        using var log = new StringWriter();
        var handler = new AccountServerFrameHandler(loginService: null, log);
        var sent = new List<byte[]>();

        await handler.HandleFrameAsync(
            CreateSendingConnection(sent),
            ReadFrame(CreateLegacyResDatProbeFrame()));

        Assert.Empty(sent);
        Assert.Equal(string.Empty, log.ToString());
    }

    [Fact]
    public async Task HandlerSilentlyIgnoresLiveLegacyResDatProbe()
    {
        using var log = new StringWriter();
        var handler = new AccountServerFrameHandler(loginService: null, log);
        var sent = new List<byte[]>();

        await handler.HandleFrameAsync(
            CreateSendingConnection(sent),
            ReadFrame(CreateLiveLegacyResDatProbeFrame()));

        Assert.Empty(sent);
        Assert.Equal(string.Empty, log.ToString());
    }

    [Fact]
    public async Task HandlerLogsMalformedLegacyResourceProbeAsUnknownOpcode()
    {
        using var log = new StringWriter();
        var handler = new AccountServerFrameHandler(loginService: null, log);
        var sent = new List<byte[]>();
        var writer = new PacketWriter(AccountPacketOpcodes.LegacyResourceProbe, payloadLength: 8);
        "foo.dat"u8.CopyTo(writer.Payload);

        await handler.HandleFrameAsync(CreateSendingConnection(sent), ReadFrame(writer.ToArray()));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains("PARITY-GAP category=unsupported-packet severity=warning gap=unsupported-packet", output);
        Assert.Contains("dedupe=unsupported-packet:unsupported-packet:opcode=1052:packet=Account", output);
        Assert.Contains("PARITY-GAP unknown account opcode opcode=1052", output);
    }

    [Fact]
    public async Task HandlerSendsServiceUnavailableWhenRepositoryIsNotConfigured()
    {
        var handler = new AccountServerFrameHandler(loginService: null);
        var sent = new List<byte[]>();

        await handler.HandleFrameAsync(
            CreateSendingConnection(sent),
            ReadFrame(new InternalAccountLoginPacket("user02", "fixture-pw").ToFrameBytes()));

        AssertResponse(sent.Single(), AccountLoginWireStatus.ServiceUnavailable, accountId: 0);
    }

    [Fact]
    public void InternalLoginResponseRoundTripsTicketAndWorldEndpoint()
    {
        var bytes = AccountLoginResponsePacket.Build(
            AccountLoginWireStatus.Success,
            1296473,
            "ticket-123",
            "127.0.0.1:5818");

        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.Equal(AccountLoginWireStatus.Success, AccountLoginResponsePacket.ReadStatus(reader));
        Assert.Equal(1296473, AccountLoginResponsePacket.ReadAccountId(reader));
        Assert.True(AccountLoginResponsePacket.TryReadHandoff(reader, out var ticket, out var endpoint));
        Assert.Equal("ticket-123", ticket);
        Assert.Equal("127.0.0.1:5818", endpoint);
    }

    private static AccountRecord Account(
        long id,
        string username,
        string password,
        long entityId = 0,
        long bannedId = AccountLoginService.AllowedBannedId,
        bool isOnline = false)
    {
        return new AccountRecord(id, username, password, entityId, bannedId, isOnline);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, null, (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static byte[] CreateLegacyResDatProbeFrame()
    {
        var writer = new PacketWriter(AccountPacketOpcodes.LegacyResourceProbe, payloadLength: 16);
        "res.dat"u8.CopyTo(writer.Payload[4..]);
        return writer.ToArray();
    }

    private static byte[] CreateLiveLegacyResDatProbeFrame()
    {
        var writer = new PacketWriter(AccountPacketOpcodes.LegacyResourceProbe, payloadLength: 44);
        byte[] payload =
        [
            0x5A, 0xC8, 0x13, 0x00,
            0xC3, 0x3A, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x72, 0x65, 0x73, 0x2E,
            0x64, 0x61, 0x74, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        ];
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static void AssertResponse(byte[] bytes, AccountLoginWireStatus status, long accountId)
    {
        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.Equal(AccountPacketOpcodes.InternalLoginResponse, reader.Header.Opcode);
        Assert.True(reader.Payload.Length >= AccountLoginResponsePacket.PayloadLength);
        Assert.Equal(status, AccountLoginResponsePacket.ReadStatus(reader));
        Assert.Equal(accountId, AccountLoginResponsePacket.ReadAccountId(reader));
    }

    private static void AssertLegacyForward(
        byte[] bytes,
        LegacyAccountForwardType type,
        uint identifier,
        ushort port,
        string ip)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        Assert.True(LegacyAccountForwardPacket.TryParse(frame, out var packet));
        Assert.Equal(identifier, packet.Identifier);
        Assert.Equal(type, packet.Type);
        Assert.Equal(port, packet.Port);
        Assert.Equal(ip, packet.IP);
    }

    private sealed class FakeAccountRepository(params AccountRecord[] accounts) :
        IAccountRepository,
        IAccountPasswordDiagnosticRepository
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

        public ValueTask<IReadOnlyList<AccountPasswordOwnerDiagnostic>> FindByPasswordAsync(
            string password,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<AccountPasswordOwnerDiagnostic> matches = accounts
                .Where(account => string.Equals(account.Password, password, StringComparison.Ordinal))
                .Select(account => new AccountPasswordOwnerDiagnostic(account.Id, account.Username, account.Password.Length))
                .ToArray();
            return ValueTask.FromResult(matches);
        }
    }
}
