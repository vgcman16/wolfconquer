using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyClanPacketTests
{
    [Fact]
    public void LegacyClanStartupWritesSyntheticInfoLayout()
    {
        var dbRoot = Path.Combine(Path.GetTempPath(), "wolf-legacy-clan-packet-" + Guid.NewGuid());
        try
        {
            var usersRoot = Path.Combine(dbRoot, "Users");
            var clansRoot = Path.Combine(dbRoot, "Clans");
            Directory.CreateDirectory(usersRoot);
            Directory.CreateDirectory(clansRoot);
            File.WriteAllText(Path.Combine(clansRoot, "900.txt"), "Count=1\n900/TestClan/FixtureLeader/2/1200/None/4/0/0/0/\n0/\n0/\n");
            File.WriteAllText(Path.Combine(usersRoot, "9000001.ini"), SnapshotText());
            var snapshot = CharacterSnapshotParser.ParseText(SnapshotText());

            Assert.True(LegacyClanPacket.TryCreateStartup(snapshot, usersRoot, out var packet));

            var bytes = packet.ToFrameBytes();
            var frame = ReadFrame(bytes);

            string[] expectedValues =
            [
                "900 1 0 1200 2 100 0 4 0 0 0 1500",
                "TestClan",
                "FixtureLeader",
                "0 0 0 0 0 0 0",
                string.Empty,
                string.Empty,
            ];
            Assert.Equal(17 + expectedValues.Sum(value => 2 + Encoding.UTF8.GetByteCount(value)), bytes.Length);
            Assert.Equal(1U, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4)));
            Assert.Equal(900U, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8, 4)));
            Assert.Equal(0U, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12, 4)));
            Assert.Equal((byte)6, bytes[16]);
            var offset = 17;
            foreach (var value in expectedValues)
            {
                var encoded = Encoding.UTF8.GetBytes(value);
                Assert.Equal((ushort)encoded.Length, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2)));
                Assert.Equal(encoded, bytes.AsSpan(offset + 2, encoded.Length).ToArray());
                offset += 2 + encoded.Length;
            }
            Assert.Equal(bytes.Length, offset);
            Assert.Equal(WorldPacketOpcodes.LegacyClan, frame.Header.Opcode);
            Assert.True(LegacyClanPacket.TryParse(frame, out var parsed));
            Assert.Equal(1U, parsed.Mode);
            Assert.Equal(900U, parsed.ClanId);
            Assert.Equal(expectedValues, parsed.Values);
        }
        finally
        {
            if (Directory.Exists(dbRoot))
            {
                Directory.Delete(dbRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void LegacyClanMyClanWarStatusMatchesSourceAppendSingleClientShape()
    {
        var packet = LegacyClanPacket.CreateMyClanWarStatus(900);
        var bytes = packet.ToFrameBytes();

        Assert.Equal(32, bytes.Length);
        Assert.True(LegacyClanPacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(LegacyClanPacket.MyClanMode, parsed.Mode);
        Assert.Equal(900U, parsed.ClanId);
        Assert.Equal(0U, parsed.Parameter);
        Assert.Equal(["0 0 0 0 0 0 0"], parsed.Values);
    }

    [Fact]
    public void LegacyClanMembersMatchesSourceFixedListShape()
    {
        var dbRoot = Path.Combine(Path.GetTempPath(), "wolf-legacy-clan-members-" + Guid.NewGuid());
        try
        {
            var usersRoot = Path.Combine(dbRoot, "Users");
            var clansRoot = Path.Combine(dbRoot, "Clans");
            Directory.CreateDirectory(usersRoot);
            Directory.CreateDirectory(clansRoot);
            File.WriteAllText(
                Path.Combine(clansRoot, "900.txt"),
                "Count=1\n900/TestClan/FixtureLeader/2/1200/None/4/0/0/0/\n0/\n0/\n");
            File.WriteAllText(Path.Combine(usersRoot, "9000001.ini"), SnapshotText());
            File.WriteAllText(
                Path.Combine(usersRoot, "9000002.ini"),
                MemberSnapshotText(9000002, "FixtureMemberA", level: 110, classCode: 45, clanRank: 10, clanDonation: 200));
            File.WriteAllText(
                Path.Combine(usersRoot, "9000003.ini"),
                MemberSnapshotText(9000003, "FixtureMemberB", level: 120, classCode: 55, clanRank: 11, clanDonation: 300));
            var snapshot = CharacterSnapshotParser.ParseText(SnapshotText());

            Assert.True(LegacyClanPacket.TryCreateMembers(snapshot, usersRoot, out var packet));

            Assert.Equal(["FixtureHero", "FixtureMemberA", "FixtureMemberB"], packet.Members.Select(member => member.Name));
            Assert.Equal([true, false, false], packet.Members.Select(member => member.Online));

            var bytes = packet.ToFrameBytes();
            Assert.Equal(176, bytes.Length);
            Assert.True(LegacyClanMemberListPacket.TryParse(ReadFrame(bytes), out var parsed));
            Assert.Equal(3, parsed.Members.Count);
            Assert.Equal("FixtureHero", parsed.Members[0].Name);
            Assert.Equal(40, parsed.Members[0].Level);
            Assert.Equal(100, parsed.Members[0].Rank);
            Assert.True(parsed.Members[0].Online);
            Assert.Equal(10U, parsed.Members[0].Class);
            Assert.Equal(1500U, parsed.Members[0].Donation);
            Assert.Equal("FixtureMemberA", parsed.Members[1].Name);
            Assert.Equal(10, parsed.Members[1].Rank);
            Assert.False(parsed.Members[1].Online);
            Assert.Equal(45U, parsed.Members[1].Class);
            Assert.Equal(200U, parsed.Members[1].Donation);
        }
        finally
        {
            if (Directory.Exists(dbRoot))
            {
                Directory.Delete(dbRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void LegacyClanBulletinMatchesSourceSetAnnouncementShape()
    {
        var dbRoot = Path.Combine(Path.GetTempPath(), "wolf-legacy-clan-bulletin-" + Guid.NewGuid());
        try
        {
            var usersRoot = Path.Combine(dbRoot, "Users");
            var clansRoot = Path.Combine(dbRoot, "Clans");
            Directory.CreateDirectory(usersRoot);
            Directory.CreateDirectory(clansRoot);
            File.WriteAllText(
                Path.Combine(clansRoot, "900.txt"),
                "Count=1\n900/TestClan/FixtureLeader/2/1200/Test bulletin/4/0/0/0/\n0/\n0/\n");
            var snapshot = CharacterSnapshotParser.ParseText(SnapshotText());

            Assert.True(LegacyClanPacket.TryCreateBulletin(snapshot, usersRoot, out var packet));

            Assert.Equal(LegacyClanPacket.SetAnnouncementMode, packet.Mode);
            Assert.Equal(900U, packet.ClanId);
            Assert.Equal(0U, packet.Parameter);
            Assert.Equal(["Test bulletin"], packet.Values);
        }
        finally
        {
            if (Directory.Exists(dbRoot))
            {
                Directory.Delete(dbRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void LegacyClanQuitAndDedicateCallbacksRoundTripLikeSourceEchoes()
    {
        var quit = LegacyClanPacket.CreateQuitCallback(900, 12, ["leave", "now"]);
        var dedicate = LegacyClanPacket.CreateDedicateCallback(900, 1500, ["donation", "1500"]);

        AssertLegacyCallbackPacket(quit, LegacyClanPacket.QuitMode, 900U, 12U, ["leave", "now"]);
        AssertLegacyCallbackPacket(dedicate, LegacyClanPacket.DedicateMode, 900U, 1500U, ["donation", "1500"]);
    }

    [Fact]
    public void LegacyClanStartupSkipsWithoutClanFile()
    {
        var dbRoot = Path.Combine(Path.GetTempPath(), "wolf-legacy-clan-packet-missing-" + Guid.NewGuid());
        try
        {
            var usersRoot = Path.Combine(dbRoot, "Users");
            Directory.CreateDirectory(usersRoot);
            var snapshot = CharacterSnapshotParser.ParseText(SnapshotText());

            Assert.False(LegacyClanPacket.TryCreateStartup(snapshot, usersRoot, out _));
        }
        finally
        {
            if (Directory.Exists(dbRoot))
            {
                Directory.Delete(dbRoot, recursive: true);
            }
        }
    }

    private static string SnapshotText()
    {
        return """
            [Character]
            UID=9000001
            Body=1008
            Face=1
            Name=FixtureHero
            Class=10
            Map=1002
            X=20
            Y=30
            FirstClass=0
            SecoundeClass=0
            Reborn=0
            Level=40
            Haire=1
            Experience=0
            PMap=1002
            PMapX=20
            PMapY=30
            Agility=10
            Strength=10
            Vitaliti=10
            Spirit=10
            Atributes=0
            MinHitPoints=100
            MinMana=50
            ConquerPoints=0
            BankCps=0
            BoundConquerPoints=0
            Money=1000
            PkPoints=0
            Online=0
            ClanID=900
            ClanRank=100
            ClanDonation=1500
            """;
    }

    private static string MemberSnapshotText(
        long uid,
        string name,
        long level,
        long classCode,
        long clanRank,
        long clanDonation)
    {
        return SnapshotText()
            .Replace("UID=9000001", "UID=" + uid)
            .Replace("Name=FixtureHero", "Name=" + name)
            .Replace("Class=10", "Class=" + classCode)
            .Replace("Level=40", "Level=" + level)
            .Replace("ClanRank=100", "ClanRank=" + clanRank)
            .Replace("ClanDonation=1500", "ClanDonation=" + clanDonation);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static void AssertLegacyCallbackPacket(
        LegacyClanPacket packet,
        uint expectedMode,
        uint expectedClanId,
        uint expectedParameter,
        IReadOnlyList<string> expectedValues)
    {
        var bytes = packet.ToFrameBytes();
        Assert.True(LegacyClanPacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(expectedMode, parsed.Mode);
        Assert.Equal(expectedClanId, parsed.ClanId);
        Assert.Equal(expectedParameter, parsed.Parameter);
        Assert.Equal(expectedValues, parsed.Values);
    }
}
