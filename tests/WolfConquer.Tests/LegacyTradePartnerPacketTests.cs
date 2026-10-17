using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTradePartnerPacketTests
{
    [Fact]
    public void StartupTradePartnerWritesSyntheticLegacyAddPartnerLayout()
    {
        Assert.True(LegacyTradePartnerStartupPackets.TryCreateTradePartner(CreateRecord(), targetOnline: false, out var packet));
        var bytes = packet.ToFrameBytes();
        Assert.Equal(CreateSyntheticFrame(), bytes);
        Assert.Equal(128, bytes.Length);
        Assert.Equal(124, LegacyTradePartnerPacket.PayloadLength);
        Assert.Equal((ushort)2004, LegacyTradePartnerPacket.Opcode);
        Assert.True(LegacyTradePartnerPacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void TradePartnerActionValuesMatchLegacyEnum()
    {
        Assert.Equal((byte)0, (byte)LegacyTradePartnerAction.RequestPartnership);
        Assert.Equal((byte)1, (byte)LegacyTradePartnerAction.RejectRequest);
        Assert.Equal((byte)2, (byte)LegacyTradePartnerAction.AddOnline);
        Assert.Equal((byte)3, (byte)LegacyTradePartnerAction.AddOffline);
        Assert.Equal((byte)4, (byte)LegacyTradePartnerAction.BreakPartnership);
        Assert.Equal((byte)5, (byte)LegacyTradePartnerAction.AddPartner);
    }

    [Fact]
    public void StartupTradePartnerFiltersUnsupportedRelationsAndOwners()
    {
        var records = new[]
        {
            CreateRecord(relationType: 1), CreateRecord(relationType: 5),
            CreateRecord(), CreateRecord(ownerId: 12_003),
        };
        Assert.False(LegacyTradePartnerStartupPackets.TryCreateTradePartner(records[0], targetOnline: false, out _));
        Assert.False(LegacyTradePartnerStartupPackets.TryCreateTradePartner(records[1], targetOnline: false, out _));
        var packets = LegacyTradePartnerStartupPackets.CreateTradePartnerStartup(
            records, ownerId: 12_001, onlineTargetIds: new HashSet<uint> { 12_002 });
        var packet = Assert.Single(packets);
        Assert.Equal(LegacyTradePartnerAction.AddPartner, packet.Action);
        Assert.Equal(12_002U, packet.Uid);
        Assert.True(packet.Online);
        Assert.Equal("FixtureTrader", packet.Name);
    }

    [Fact]
    public void StartupTradePartnerCarriesOnlineFlagWithoutClearingStoredMemberFields()
    {
        var record = CreateRecord();
        Assert.True(LegacyTradePartnerStartupPackets.TryCreateTradePartner(record, targetOnline: false, out var offline));
        Assert.True(LegacyTradePartnerStartupPackets.TryCreateTradePartner(record, targetOnline: true, out var online));
        Assert.False(offline.Online);
        Assert.True(online.Online);
        Assert.Equal(0, offline.HoursLeft);
        Assert.Equal(0, online.HoursLeft);
        Assert.Equal(103_004U, offline.LookFace);
        Assert.Equal(103_004U, online.LookFace);
        Assert.Equal(42U, offline.Level);
        Assert.Equal(42U, online.Level);
        Assert.Equal(2U, offline.NobilityRank);
        Assert.Equal(2U, online.NobilityRank);
        Assert.Equal("fixture-note", offline.PersonalSignature);
        Assert.Equal("fixture-note", online.PersonalSignature);
    }

    [Fact]
    public void TradePartnerParserRejectsWrongOpcodeAndInvalidFixedFields()
    {
        var wrongOpcode = new PacketWriter((ushort)(LegacyTradePartnerPacket.Opcode + 1), LegacyTradePartnerPacket.PayloadLength);
        Assert.False(LegacyTradePartnerPacket.TryParse(ReadFrame(wrongOpcode.ToArray()), out _));
        var invalid = CreateSyntheticFrame();
        invalid[PacketHeader.Size + 88] = 0;
        Assert.False(LegacyTradePartnerPacket.TryParse(ReadFrame(invalid), out _));
    }

    private static SocialAssociationRecord CreateRecord(int ownerId = 12_001, int relationType = 3)
    {
        return SocialAssociationParser.ParseRow("synthetic-associations.txt", 2,
            $"{ownerId}/{relationType}/0/0/0/12002/37/0/0/0/FixtureTrader/0/0/0/0/103004/42/2/fixture-note/11/");
    }

    private static byte[] CreateSyntheticFrame()
    {
        // Independent fixed-layout fixture with invented IDs, name, and note.
        var bytes = new byte[128];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0), 128);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2004);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 12_002);
        bytes[8] = 5;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 103_004);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 42);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20), 2);
        Encoding.UTF8.GetBytes("fixture-note").CopyTo(bytes, 28);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(92), 1);
        Encoding.UTF8.GetBytes("FixtureTrader").CopyTo(bytes, 96);
        return bytes;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
