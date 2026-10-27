using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyD13PrestigeRankingPacketTests
{
    [Fact]
    public void RawActionSixMatchesIndependentTwoRowPhysicalLayoutGolden()
    {
        Assert.Equal(ConquerProtocolProfile.VisibleDx8D13, LegacyD13PrestigeRankingPacket.Profile);
        Assert.Equal((ushort)2165, LegacyD13PrestigeRankingPacket.Opcode);
        Assert.Equal(113, LegacyD13PrestigeRankingPacket.EntryWireLength);
        Assert.Equal(TwoRowGolden(), TwoRowPacket().ToFrameBytes());
    }

    [Fact]
    public void GoldenParsesBoth113ByteRowsAndPreservesAllOpaqueBytes()
    {
        var bytes = TwoRowGolden();
        Assert.Equal(247, bytes.Length);
        Assert.True(LegacyD13PrestigeRankingPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(0x01020304u, packet.RankType);
        Assert.Equal((ushort)0x0506, packet.RegisteredCount);
        Assert.Equal((byte)7, packet.Page);
        Assert.Equal((byte)8, packet.Finish);
        Assert.Equal(0x090a0b0cu, packet.HeaderValue);
        Assert.Equal(2, packet.Entries.Count);

        var first = packet.Entries[0];
        Assert.Equal((byte)1, first.Rank);
        Assert.Equal(0x0807060504030201L, first.Amount);
        Assert.Equal(80_000_000u, first.CategoryCode);
        Assert.Equal(0x11223344u, first.CharacterUid);
        Assert.Equal(0x01020304u, first.Level);
        Assert.Equal(0x11121314u, first.Class);
        Assert.Equal(0x21222324u, first.Mesh);
        Assert.Equal(0x31323334u, first.OpaqueValue);
        Assert.Equal(Convert.FromHexString("FFD0800000000000000000000000000000000000000000000000000000000000"), first.SecondaryNameBytes.ToArray());
        Assert.Equal(Convert.FromHexString("101112131415161718191A1B1C1D1E1F"), first.OpaqueTailBytes.ToArray());

        var second = packet.Entries[1];
        Assert.Equal((byte)2, second.Rank);
        Assert.Equal(long.MinValue, second.Amount);
        Assert.Equal(80_000_011u, second.CategoryCode);
        Assert.Equal(uint.MaxValue, second.CharacterUid);
        Assert.Equal(uint.MaxValue, second.Level);
        Assert.Equal(0x80000000u, second.Class);
        Assert.Equal(0x10000000u, second.Mesh);
        Assert.Equal(0xa1a2a3a4u, second.OpaqueValue);
        Assert.Equal(Convert.FromHexString("202122232425262728292A2B2C2D2E2F303132333435363738393A3B3C3D3E3F"), second.NameBytes.ToArray());
        Assert.Equal(Convert.FromHexString("606162636465666768696A6B6C6D6E6F"), second.OpaqueTailBytes.ToArray());
        Assert.Equal(bytes, packet.ToFrameBytes());
    }

    [Fact]
    public void RowCopiesBorrowedRawFieldsWithoutTextConversionOrImplicitPadding()
    {
        var name = Enumerable.Repeat((byte)0xff, 32).ToArray();
        var secondary = Enumerable.Repeat((byte)0x80, 32).ToArray();
        var tail = Enumerable.Repeat((byte)0x7f, 16).ToArray();
        var row = new LegacyD13PrestigeRankingEntry(1, 2, 3, 4, name, secondary, 5, 6, 7, tail, 8);
        Array.Clear(name);
        Array.Clear(secondary);
        Array.Clear(tail);

        Assert.All(row.NameBytes.ToArray(), value => Assert.Equal((byte)0xff, value));
        Assert.All(row.SecondaryNameBytes.ToArray(), value => Assert.Equal((byte)0x80, value));
        Assert.All(row.OpaqueTailBytes.ToArray(), value => Assert.Equal((byte)0x7f, value));
    }

    [Fact]
    public void CompleteEmptyRawFrameRoundTripsWithoutDerivingContinuationPolicy()
    {
        var bytes = Convert.FromHexString("15007508060403020106050708000000000C0B0A09");
        Assert.True(LegacyD13PrestigeRankingPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Empty(packet.Entries);
        Assert.Equal((byte)7, packet.Page);
        Assert.Equal((byte)8, packet.Finish);
        Assert.Equal(bytes, packet.ToFrameBytes());
    }

    [Fact]
    public void ExistingSimpleChiFrameRetains109ByteRowsAndCapturedLength()
    {
        var power = LegacyChiPowerState.Parse(LegacyChiPowerType.Dragon,
            "1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/");
        var bytes = LegacyGenericRankingPacket.ChiQueryCount(1_293_600, "Demonic", power).ToFrameBytes();

        Assert.Equal(109, LegacyGenericRankingPacket.SimpleEntryWireLength);
        Assert.Equal(130, bytes.Length);
        Assert.Equal("820075080201879303000000000100000000000000", Convert.ToHexString(bytes.AsSpan(0, 21)));
        Assert.False(LegacyD13PrestigeRankingPacket.TryParse(ReadFrame(bytes), out _));
    }
}
