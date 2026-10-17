using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyAssociationStartupPacketTests
{
    [Fact]
    public void KnownPersonPacketWritesLegacyFriendLayout()
    {
        var packet = new LegacyKnownPersonPacket(
            LegacyKnownPersonAction.AddFriend,
            Uid: 1_293_600,
            Online: true,
            NobilityRank: 5,
            UserInfo: 0,
            LookFace: 14_048,
            Level: 140,
            PersonalSignature: "hey",
            Name: "FriendTwo");

        var bytes = packet.ToFrameBytes();

        Assert.True(LegacyKnownPersonPacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal((ushort)128, ParsedFrameLength(bytes));
        Assert.Equal((ushort)2139, WorldPacketOpcodes.LegacyKnownPerson);
        Assert.Equal(WorldPacketOpcodes.LegacyKnownPerson, LegacyKnownPersonPacket.Opcode);
        Assert.Equal(packet, parsed);
        Assert.Equal(
            "80005b0820bd13000f0100000500000000000000e03600008c000000686579",
            Convert.ToHexString(bytes[..31]).ToLowerInvariant());
        Assert.Equal(0, bytes[31]);
        Assert.Equal("01000000467269656e6454776f", Convert.ToHexString(bytes[92..105]).ToLowerInvariant());
    }

    [Fact]
    public void StartupKnownPersonMapsFriendAndEnemyRelations()
    {
        var friend = ParseRow("1293116/1/600/0/0/1293600/0/0/0/0/FriendTwo/0/0/0/0/1671008/140/5/hello/14048/");
        var enemy = ParseRow("1293116/2/0/0/0/1293601/0/0/0/0/EnemyOne/0/0/0/0/1671009/137/3//11025/");

        Assert.True(LegacyAssociationStartupPackets.TryCreateKnownPerson(friend, targetOnline: false, out var offlineFriend));
        Assert.True(LegacyAssociationStartupPackets.TryCreateKnownPerson(friend, targetOnline: true, out var onlineFriend));
        Assert.True(LegacyAssociationStartupPackets.TryCreateKnownPerson(enemy, targetOnline: false, out var offlineEnemy));

        Assert.Equal(LegacyKnownPersonAction.AddFriend, offlineFriend.Action);
        Assert.False(offlineFriend.Online);
        Assert.Equal(1_293_600U, offlineFriend.Uid);
        Assert.Equal("FriendTwo", offlineFriend.Name);
        Assert.Equal(0U, offlineFriend.NobilityRank);
        Assert.Equal(0U, offlineFriend.LookFace);
        Assert.Equal(0U, offlineFriend.Level);
        Assert.Empty(offlineFriend.PersonalSignature);

        Assert.True(onlineFriend.Online);
        Assert.Equal(5U, onlineFriend.NobilityRank);
        Assert.Equal(1_671_008U, onlineFriend.LookFace);
        Assert.Equal(140U, onlineFriend.Level);
        Assert.Equal("hello", onlineFriend.PersonalSignature);

        Assert.Equal(LegacyKnownPersonAction.AddEnemy, offlineEnemy.Action);
        Assert.Equal(1_293_601U, offlineEnemy.Uid);
        Assert.Equal("EnemyOne", offlineEnemy.Name);
    }

    [Fact]
    public void StartupListFiltersOwnerAndUnsupportedRelations()
    {
        var records = new[]
        {
            ParseRow("1293116/1/0/0/0/1293600/0/0/0/0/FriendTwo/0/0/0/0/1671008/140/5//14048/"),
            ParseRow("1293116/2/0/0/0/1293601/0/0/0/0/EnemyOne/0/0/0/0/1671009/137/3//11025/"),
            ParseRow("1293116/3/0/0/0/1293602/0/0/0/0/Partner/0/0/0/0/1671010/130/1//1050/"),
            ParseRow("1293117/1/0/0/0/1293603/0/0/0/0/OtherOwner/0/0/0/0/1671011/120/0//1040/"),
        };

        var packets = LegacyAssociationStartupPackets.CreateKnownPersonStartup(
            records,
            ownerId: 1_293_116,
            onlineTargetIds: new HashSet<uint> { 1_293_601 });

        Assert.Equal([LegacyKnownPersonAction.AddFriend, LegacyKnownPersonAction.AddEnemy], packets.Select(packet => packet.Action));
        Assert.Equal([false, true], packets.Select(packet => packet.Online));
        Assert.Equal([1_293_600U, 1_293_601U], packets.Select(packet => packet.Uid));
    }

    [Fact]
    public void KnownPersonPacketWritesUtf8FixedStringsByByteLength()
    {
        var packet = new LegacyKnownPersonPacket(
            LegacyKnownPersonAction.AddFriend,
            Uid: 1,
            Online: true,
            NobilityRank: 0,
            UserInfo: 0,
            LookFace: 0,
            Level: 1,
            PersonalSignature: "\u00E9\u00E9\u00E9",
            Name: "\u00E9\u00E9");

        Assert.True(LegacyKnownPersonPacket.TryParse(ReadFrame(packet.ToFrameBytes()), out var parsed));
        Assert.Equal(packet.PersonalSignature, parsed.PersonalSignature);
        Assert.Equal(packet.Name, parsed.Name);
    }

    [Fact]
    public void KnownPersonParserRejectsWrongOpcodeAndInvalidFixedFields()
    {
        var wrongOpcode = new PacketWriter((ushort)(WorldPacketOpcodes.LegacyKnownPerson + 1), LegacyKnownPersonPacket.PayloadLength);
        Assert.False(LegacyKnownPersonPacket.TryParse(ReadFrame(wrongOpcode.ToArray()), out _));

        var invalid = new LegacyKnownPersonPacket(
            LegacyKnownPersonAction.AddEnemy,
            Uid: 1,
            Online: false,
            NobilityRank: 0,
            UserInfo: 0,
            LookFace: 0,
            Level: 0,
            PersonalSignature: "",
            Name: "Enemy").ToFrameBytes();
        invalid[PacketHeader.Size + 88] = 0;

        Assert.False(LegacyKnownPersonPacket.TryParse(ReadFrame(invalid), out _));
    }

    private static SocialAssociationRecord ParseRow(string row)
    {
        return SocialAssociationParser.ParseRow("Associate.txt", 2, row);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static ushort ParsedFrameLength(byte[] bytes)
    {
        Assert.True(PacketReader.TryRead(bytes, out var reader));
        return reader.Header.Length;
    }
}
