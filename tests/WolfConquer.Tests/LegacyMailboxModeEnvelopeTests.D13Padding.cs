using System.Buffers.Binary;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMailboxModeEnvelopeTests
{
    [Theory]
    [InlineData(LegacyMailboxD13CapturedFrames.Page7, LegacyMailboxModeType.ClaimAll, 1, 7, 7, 77, 73)]
    [InlineData(LegacyMailboxD13CapturedFrames.Content1, LegacyMailboxModeType.Content, 0, 0, 8, 22, 18)]
    [InlineData(LegacyMailboxD13CapturedFrames.Page2, LegacyMailboxModeType.ClaimAll, 8, 2, 7, 37, 33)]
    public void ActualCapturedD13ZeroTrailersParseOnlyInD13AndLeaveCanonicalAckUnchanged(
        string hex, LegacyMailboxModeType mode, int first, int count, int padding, int frameLength, int payloadLength)
    {
        var bytes = Convert.FromHexString(hex); var frame = Frame(bytes);
        Assert.Equal(frameLength, bytes.Length); Assert.Equal(frameLength, frame.Header.Length);
        Assert.Equal(frameLength, frame.Frame.Length); Assert.Equal(payloadLength, frame.Payload.Length);
        Assert.True(LegacyMailboxModePacket.TryParse(frame, ConquerProtocolProfile.VisibleDx8D13, out var parsed));
        Assert.Equal(mode, parsed.Type); Assert.Equal(1U, parsed.Id); Assert.Equal((uint)count, parsed.Count);
        Assert.Equal(Enumerable.Range(first, count).Select(id => (ulong)id), parsed.Items);
        Assert.False(LegacyMailboxModePacket.TryParse(frame, out _));
        Assert.False(LegacyMailboxModePacket.TryParse(frame, ConquerProtocolProfile.RootD11_7899, out _));
        Assert.False(LegacyMailboxModePacket.TryParse(frame, (ConquerProtocolProfile)12345, out _));
        var canonical = parsed.ToFrameBytes();
        Assert.Equal(bytes.Length - padding, canonical.Length);
        Assert.Equal(bytes.AsSpan(4, canonical.Length - 4).ToArray(), canonical[4..]);
        foreach (var profile in new[] { ConquerProtocolProfile.RootD11_7899, ConquerProtocolProfile.VisibleDx8D13 })
        {
            Assert.True(LegacyMailboxModePacket.TryParse(Frame(canonical), profile, out var oldShape));
            Assert.Equal(parsed.Type, oldShape.Type); Assert.Equal(parsed.Id, oldShape.Id); Assert.Equal(parsed.Items, oldShape.Items);
        }
        Assert.True(LegacyMailboxModePacket.TryParse(Frame(canonical), out _));
    }

    [Fact]
    public void D13PaddingNeverForgivesNonzeroBytesWrongLengthsModesOrCountOverflow()
    {
        foreach (var (hex, padding) in LegacyMailboxD13CapturedFrames.All)
        {
            var original = Convert.FromHexString(hex);
            for (var index = original.Length - padding; index < original.Length; index++)
            {
                var changed = original.ToArray(); changed[index] = 1;
                AssertD13Rejected(Frame(changed));
            }
            var canonical = original[..^padding]; BinaryPrimitives.WriteUInt16LittleEndian(canonical, (ushort)canonical.Length);
            foreach (var wrong in new[] { padding - 1, padding + 1 })
                AssertD13Rejected(Frame(LegacyMailboxD13CapturedFrames.Pad(canonical, wrong)));
            foreach (var count in new[] { 268_435_455U, 536_870_912U, uint.MaxValue })
            {
                var changed = original.ToArray(); BinaryPrimitives.WriteUInt32LittleEndian(changed.AsSpan(10), count);
                AssertD13Rejected(Frame(changed));
            }
            foreach (ushort mode in new ushort[] { 0, 2, 3, 4, 5, 6, 8, ushort.MaxValue })
            {
                var changed = original.ToArray(); BinaryPrimitives.WriteUInt16LittleEndian(changed.AsSpan(4), mode);
                AssertD13Rejected(Frame(changed));
            }
            for (var length = 0; length < original.Length; length++)
            {
                var physical = original[..length];
                if (length < 4) continue;
                BinaryPrimitives.WriteUInt16LittleEndian(physical, (ushort)physical.Length);
                // Removing the entire known trailer yields an intentionally valid canonical frame.
                if (length == canonical.Length) continue;
                AssertD13Rejected(Frame(physical));
            }
        }
        foreach (var hex in new[] { LegacyMailboxD13CapturedFrames.Page7, LegacyMailboxD13CapturedFrames.Page2 })
        {
            var original = Convert.FromHexString(hex); var count = BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(10));
            foreach (var wrong in new[] { count - 1, count + 1 })
            {
                var changed = original.ToArray(); BinaryPrimitives.WriteUInt32LittleEndian(changed.AsSpan(10), wrong);
                AssertD13Rejected(Frame(changed));
            }
            var content = original.ToArray(); BinaryPrimitives.WriteUInt16LittleEndian(content.AsSpan(4), 1);
            AssertD13Rejected(Frame(content)); // Content padding exception requires count0 and eight zeros.
        }
    }

    [Fact]
    public void ContentWithOneZeroItemRemainsOldCanonicalSemanticsRatherThanAPaddingException()
    {
        var bytes = Convert.FromHexString(LegacyMailboxD13CapturedFrames.Content1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(10), 1);
        Assert.True(LegacyMailboxModePacket.TryParse(Frame(bytes), out var parsed));
        Assert.Equal(new[] { 0UL }, parsed.Items);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(10), 2);
        AssertD13Rejected(Frame(bytes));
    }

    private static void AssertD13Rejected(WolfConquer.Core.Packets.PacketFrame frame)
    {
        Assert.False(LegacyMailboxModePacket.TryParse(frame, ConquerProtocolProfile.VisibleDx8D13, out var packet));
        Assert.Null(packet);
    }
}

internal static class LegacyMailboxD13CapturedFrames
{
    // Root's actual inbound trace export observed-2264.json, lines417/446/474.
    internal const string Page7 = "4D00D80807000100000007000000010000000000000002000000000000000300000000000000040000000000000005000000000000000600000000000000070000000000000000000000000000";
    internal const string Content1 = "1600D808010001000000000000000000000000000000";
    internal const string Page2 = "2500D808070001000000020000000800000000000000090000000000000000000000000000";
    internal static readonly (string Hex, int Padding)[] All = [(Page7, 7), (Content1, 8), (Page2, 7)];

    internal static byte[] Pad(byte[] canonical, int zeroBytes = 7)
    {
        var bytes = new byte[checked(canonical.Length + zeroBytes)]; canonical.CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)bytes.Length));
        return bytes;
    }
}
