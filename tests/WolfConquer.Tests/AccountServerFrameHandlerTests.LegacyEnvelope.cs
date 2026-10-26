using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed partial class AccountServerFrameHandlerTests
{
    public enum LegacyEnvelopeDefect
    {
        WrongDeclaredLength,
        WrongEmbeddedOpcode,
        ShortEmbeddedLength,
        MissingPhysicalFrame,
        TruncatedPhysicalFrame,
        TrailingPhysicalFrame,
        MissingPayload,
        DifferentPayload,
        SegmentedDifferentPayload,
    }

    public static IEnumerable<object[]> LegacyEnvelopeRejections()
    {
        foreach (var defect in Enum.GetValues<LegacyEnvelopeDefect>())
        {
            foreach (var captureOverride in new[] { false, true })
            {
                foreach (var fallback in new[] { false, true })
                {
                    yield return [defect, captureOverride, fallback];
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(LegacyEnvelopeRejections))]
    public async Task IncoherentAuthenticationEnvelopeNeverUsesOverrideFallbackRepositoryOrTicket(
        LegacyEnvelopeDefect defect,
        bool captureOverride,
        bool fallback)
    {
        var repository = new EnvelopeCountingAccountRepository(Account(1293600, "2", "2"));
        var tickets = new InMemoryAccountHandoffTicketStore();
        using var log = new StringWriter();
        var handler = CreateEnvelopeHandler(repository, tickets, log, captureOverride, fallback);
        var sent = new List<byte[]>();

        await handler.HandleFrameAsync(CreateSendingConnection(sent), CreateIncoherentAuthenticationFrame(defect));

        AssertLegacyForward(sent.Single(), LegacyAccountForwardType.InvalidAuthenticationProtocol, 0, 0, string.Empty);
        Assert.Equal(0, repository.ReadCount);
        Assert.Empty(tickets.Tickets);
        Assert.DoesNotContain("Ready", log.ToString());
        Assert.DoesNotContain("PARITY-BUG", log.ToString());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task IncoherentAuthenticationEnvelopeRejectsBeforeMissingRepositoryShortcut(
        bool captureOverride,
        bool fallback)
    {
        var tickets = new InMemoryAccountHandoffTicketStore();
        using var log = new StringWriter();
        var handler = CreateEnvelopeHandler(null, tickets, log, captureOverride, fallback);
        var sent = new List<byte[]>();

        await handler.HandleFrameAsync(
            CreateSendingConnection(sent),
            CreateIncoherentAuthenticationFrame(LegacyEnvelopeDefect.MissingPhysicalFrame));

        AssertLegacyForward(sent.Single(), LegacyAccountForwardType.InvalidAuthenticationProtocol, 0, 0, string.Empty);
        Assert.Empty(tickets.Tickets);
        Assert.DoesNotContain("Ready", log.ToString());
        Assert.DoesNotContain("PARITY-BUG", log.ToString());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CoherentMalformedFieldsRejectBeforeMissingRepositoryShortcut(
        bool captureOverride,
        bool fallback)
    {
        var tickets = new InMemoryAccountHandoffTicketStore();
        using var log = new StringWriter();
        var handler = CreateEnvelopeHandler(null, tickets, log, captureOverride, fallback);
        var sent = new List<byte[]>();

        await handler.HandleFrameAsync(CreateSendingConnection(sent), CreateCoherentMalformedAuthenticationFrame());

        AssertLegacyForward(sent.Single(), LegacyAccountForwardType.InvalidAuthenticationProtocol, 0, 0, string.Empty);
        Assert.Empty(tickets.Tickets);
        Assert.DoesNotContain("Ready", log.ToString());
        Assert.DoesNotContain("PARITY-BUG", log.ToString());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CoherentValidAuthenticationRetainsExistingNormalAndCaptureBehavior(
        bool captureOverride,
        bool fallback)
    {
        var repository = new EnvelopeCountingAccountRepository(Account(1293600, "2", "2"));
        var tickets = new InMemoryAccountHandoffTicketStore();
        using var log = new StringWriter();
        var handler = CreateEnvelopeHandler(repository, tickets, log, captureOverride, fallback);
        var sent = new List<byte[]>();

        await handler.HandleFrameAsync(
            CreateSendingConnection(sent),
            ReadFrame(new LegacyAccountAuthenticationPacket("2", "2", "Wolf").ToFrameBytes()));

        AssertLegacyForward(sent.Single(), LegacyAccountForwardType.Ready, captureOverride ? 1444400u : 1293600u, 5818, "127.0.0.1");
        Assert.Equal(captureOverride ? 0 : 1, repository.ReadCount);
        Assert.Equal(captureOverride ? 0 : 1, tickets.Tickets.Count);
        Assert.DoesNotContain("PARITY-BUG", log.ToString());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CoherentMalformedFieldsRejectEvenWithConfiguredFallbackOrCaptureOverride(
        bool captureOverride,
        bool fallback)
    {
        var repository = new EnvelopeCountingAccountRepository(Account(1293600, "2", "2"));
        var tickets = new InMemoryAccountHandoffTicketStore();
        using var log = new StringWriter();
        var handler = CreateEnvelopeHandler(repository, tickets, log, captureOverride, fallback);
        var sent = new List<byte[]>();
        await handler.HandleFrameAsync(CreateSendingConnection(sent), CreateCoherentMalformedAuthenticationFrame());

        AssertLegacyForward(
            sent.Single(),
            LegacyAccountForwardType.InvalidAuthenticationProtocol,
            0,
            0,
            string.Empty);
        Assert.Equal(0, repository.ReadCount);
        Assert.Empty(tickets.Tickets);
        Assert.DoesNotContain("Ready", log.ToString());
        Assert.DoesNotContain("PARITY-BUG", log.ToString());
    }

    private static AccountServerFrameHandler CreateEnvelopeHandler(
        EnvelopeCountingAccountRepository? repository,
        InMemoryAccountHandoffTicketStore tickets,
        TextWriter log,
        bool captureOverride,
        bool fallback)
    {
        return new AccountServerFrameHandler(
            repository is null ? null : new AccountLoginService(repository),
            new AccountHandoffService(tickets),
            log,
            new ConquerEndpoint("WorldServer", "127.0.0.1", 5818),
            legacyCaptureOverrideIdentifier: captureOverride ? 1444400u : null,
            legacyMalformedAuthFallbackRequest: fallback ? new AccountLoginRequest("2", "2") : null);
    }

    private static PacketFrame CreateCoherentMalformedAuthenticationFrame()
    {
        var writer = new PacketWriter(AccountPacketOpcodes.LegacyAuthentication, LegacyAccountAuthenticationPacket.PayloadLength);
        writer.Payload[0] = 149;
        writer.Payload[1] = 212;
        writer.Payload[2] = 230;
        return ReadFrame(writer.ToArray());
    }

    private static PacketFrame CreateIncoherentAuthenticationFrame(LegacyEnvelopeDefect defect)
    {
        var bytes = new LegacyAccountAuthenticationPacket("2", "2", "Wolf").ToFrameBytes();
        Assert.True(PacketHeader.TryRead(bytes, out var header));
        var physical = new ReadOnlySequence<byte>(bytes);
        var payloadBytes = bytes[PacketHeader.Size..];
        var payload = new ReadOnlySequence<byte>(payloadBytes);
        switch (defect)
        {
            case LegacyEnvelopeDefect.WrongDeclaredLength:
                header = header with { Length = (ushort)(bytes.Length - 1) };
                break;
            case LegacyEnvelopeDefect.WrongEmbeddedOpcode:
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), ushort.MaxValue);
                break;
            case LegacyEnvelopeDefect.ShortEmbeddedLength:
                BinaryPrimitives.WriteUInt16LittleEndian(bytes, PacketHeader.Size);
                break;
            case LegacyEnvelopeDefect.MissingPhysicalFrame:
                physical = default;
                break;
            case LegacyEnvelopeDefect.TruncatedPhysicalFrame:
                physical = new ReadOnlySequence<byte>(bytes[..^1]);
                break;
            case LegacyEnvelopeDefect.TrailingPhysicalFrame:
                physical = new ReadOnlySequence<byte>(bytes.Concat(new byte[] { 0xa5 }).ToArray());
                break;
            case LegacyEnvelopeDefect.MissingPayload:
                payload = default;
                break;
            case LegacyEnvelopeDefect.DifferentPayload:
                payloadBytes[^1] ^= 0xff;
                break;
            case LegacyEnvelopeDefect.SegmentedDifferentPayload:
                payloadBytes[^1] ^= 0xff;
                var first = new EnvelopeSegment(payloadBytes.AsMemory(0, 2));
                var last = first.Append(payloadBytes.AsMemory(2));
                payload = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(defect));
        }

        return new PacketFrame(header, physical, payload);
    }

    private sealed class EnvelopeCountingAccountRepository(AccountRecord account) : IAccountRepository
    {
        public int ReadCount { get; private set; }

        public ValueTask<IReadOnlyList<AccountRecord>> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            ReadCount++;
            IReadOnlyList<AccountRecord> result = string.Equals(username, account.Username, StringComparison.Ordinal)
                ? [account]
                : [];
            return ValueTask.FromResult(result);
        }
    }

    private sealed class EnvelopeSegment : ReadOnlySequenceSegment<byte>
    {
        public EnvelopeSegment(ReadOnlyMemory<byte> memory)
        {
            Memory = memory;
        }

        public EnvelopeSegment Append(ReadOnlyMemory<byte> memory)
        {
            var next = new EnvelopeSegment(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }
}
