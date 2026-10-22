using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeUnionClanPersistenceTests
{
    [Fact]
    public void GuildMemberFactoryCopiesNativeFieldsWithNarrowingAndNoInventedOwner()
    {
        var source = new LegacyNativeGuildRuntime.Member
        {
            UID = uint.MaxValue, Name = "Wolf", Level = 65538, Class = 199,
            Mesh = 1002003, NobilityRank = 257,
        };
        var member = LegacyNativeUnionMember.CreateMember(source,
            LegacyNativeUnionMember.MilitaryRanks.Emperor);
        Assert.Equal(uint.MaxValue, member.UID);
        Assert.Same(source.Name, member.Name);
        Assert.Equal((ushort)2, member.Level);
        Assert.Equal(199u, member.Class);
        Assert.Equal(1002003u, member.Mesh);
        Assert.Equal(LegacyNativeNobilityRank.Knight, member.NobilityRank);
        Assert.Equal(LegacyNativeUnionMember.MilitaryRanks.Emperor, member.Rank);
        Assert.Null(member.Owner);
        Assert.False(member.IsOnline);
        source.UnionMem = member;
        Assert.Same(member, source.UnionMem);
        Assert.NotSame(member, LegacyNativeUnionMember.CreateMember(source));
        Assert.Throws<NullReferenceException>(() => LegacyNativeUnionMember.CreateMember(null!));
    }

    [Fact]
    public void UnionMemberKeepsCompleteNativeStorageDefaultsAndPhysicalOwnerOnlineRule()
    {
        var member = new LegacyNativeUnionMember();
        var fields = typeof(LegacyNativeUnionMember).GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "ReceiveKick", "Rank", "Exploits", "MyTreasury", "UID", "Class", "Mesh", "Level", "NobilityRank", "Name", "Owner" }, fields.Select(field => field.Name));
        Assert.Equal(new[] { typeof(byte), typeof(LegacyNativeUnionMember.MilitaryRanks), typeof(uint), typeof(uint), typeof(uint), typeof(uint), typeof(uint), typeof(ushort), typeof(LegacyNativeNobilityRank), typeof(string), typeof(LegacyNativeGameClientRuntime) }, fields.Select(field => field.FieldType));
        Assert.All(fields, field => Assert.False(field.IsInitOnly));
        Assert.Equal((byte)0, member.ReceiveKick);
        Assert.Equal(LegacyNativeUnionMember.MilitaryRanks.Member, member.Rank);
        Assert.Equal(0u, member.Exploits); Assert.Equal(0u, member.MyTreasury); Assert.Equal(0u, member.UID);
        Assert.Equal(0u, member.Class); Assert.Equal(0u, member.Mesh); Assert.Equal((ushort)0, member.Level);
        Assert.Equal(LegacyNativeNobilityRank.Serf, member.NobilityRank);
        Assert.Equal("", member.Name); Assert.Null(member.Owner); Assert.False(member.IsOnline);
        var client = new LegacyNativeGameClientRuntime { Fake = true, FullLoading = false };
        member.Owner = client;
        Assert.Same(client, member.Owner);
        Assert.True(member.IsOnline); // No socket, FullLoading or Player UID requirement exists natively.
        member.Owner = null!;
        Assert.False(member.IsOnline);
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(LegacyNativeNobilityRank)));
        Assert.Equal(new byte[] { 0, 1, 3, 5, 7, 9, 12 }, Enum.GetValues<LegacyNativeNobilityRank>().Select(value => (byte)value));
    }

    [Fact]
    public void ClanMemberKeepsCompleteNativeStorageDefaultsAndAllRankValues()
    {
        var member = new LegacyNativeClanMember();
        var fields = typeof(LegacyNativeClanMember).GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "Rank", "UID", "Name", "Donation", "Level", "Class", "Online" }, fields.Select(field => field.Name));
        Assert.Equal(new[] { typeof(LegacyNativeClanRanks), typeof(uint), typeof(string), typeof(uint), typeof(byte), typeof(uint), typeof(bool) }, fields.Select(field => field.FieldType));
        Assert.All(fields, field => Assert.False(field.IsInitOnly));
        Assert.Equal(LegacyNativeClanRanks.Member, member.Rank);
        Assert.Equal(0u, member.UID); Assert.Equal("", member.Name); Assert.Equal(0u, member.Donation);
        Assert.Equal((byte)0, member.Level); Assert.Equal(0u, member.Class); Assert.False(member.Online);
        Assert.Equal(typeof(ushort), Enum.GetUnderlyingType(typeof(LegacyNativeClanRanks)));
        Assert.Equal((ushort)100, (ushort)LegacyNativeClanRanks.Leader);
        Assert.Equal((ushort)11, (ushort)LegacyNativeClanRanks.Spouse);
        Assert.Equal((ushort)10, (ushort)LegacyNativeClanRanks.Member);
        Assert.Equal(3, Enum.GetValues<LegacyNativeClanRanks>().Length);
    }

    [Fact]
    public void UnionMilitaryRanksAndEveryPureRankMappingRemainLiteral()
    {
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(LegacyNativeUnionMember.MilitaryRanks)));
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(LegacyNativeUnionMember.Ranks)));
        var cases = new (LegacyNativeUnionMember.MilitaryRanks Military, uint MilitaryValue, LegacyNativeUnionMember.Ranks Rank, uint RankValue)[]
        {
            (LegacyNativeUnionMember.MilitaryRanks.Member, 0, LegacyNativeUnionMember.Ranks.Member, 0),
            (LegacyNativeUnionMember.MilitaryRanks.Emperor, 1000, LegacyNativeUnionMember.Ranks.Emperror, 1),
            (LegacyNativeUnionMember.MilitaryRanks.LeftPre, 2000, LegacyNativeUnionMember.Ranks.LeftPremier, 2),
            (LegacyNativeUnionMember.MilitaryRanks.RightPre, 2010, LegacyNativeUnionMember.Ranks.RightPremier, 4),
            (LegacyNativeUnionMember.MilitaryRanks.LeftMar, 2020, LegacyNativeUnionMember.Ranks.LeftMarshal, 8),
            (LegacyNativeUnionMember.MilitaryRanks.RightMar, 2030, LegacyNativeUnionMember.Ranks.RightMarshal, 16),
            (LegacyNativeUnionMember.MilitaryRanks.SEC_Gen, 2040, LegacyNativeUnionMember.Ranks.SecGeneral, 32),
            (LegacyNativeUnionMember.MilitaryRanks.DEF_Gen, 2050, LegacyNativeUnionMember.Ranks.DefGeneral, 64),
            (LegacyNativeUnionMember.MilitaryRanks.PAC_Gen, 2060, LegacyNativeUnionMember.Ranks.ParGeneral, 128),
            (LegacyNativeUnionMember.MilitaryRanks.CO_Gen, 2070, LegacyNativeUnionMember.Ranks.CoGeneral, 256),
            (LegacyNativeUnionMember.MilitaryRanks.Imprl_Guard, 3000, LegacyNativeUnionMember.Ranks.ImprialGuard, 512),
            (LegacyNativeUnionMember.MilitaryRanks.Empress, 4000, LegacyNativeUnionMember.Ranks.Empress, 1024),
            (LegacyNativeUnionMember.MilitaryRanks.SeniorConc, 401, LegacyNativeUnionMember.Ranks.SeniorConc, 2048),
            (LegacyNativeUnionMember.MilitaryRanks.Conc, 402, LegacyNativeUnionMember.Ranks.Conc, 4096),
        };
        Assert.Equal(14, Enum.GetValues<LegacyNativeUnionMember.MilitaryRanks>().Length);
        Assert.Equal(14, Enum.GetValues<LegacyNativeUnionMember.Ranks>().Length);
        foreach (var value in cases)
        {
            Assert.Equal(value.MilitaryValue, (uint)value.Military);
            Assert.Equal(value.RankValue, (uint)value.Rank);
            Assert.Equal(value.Rank, LegacyNativeUnionMember.GetRank(value.Military));
        }
        Assert.Equal(LegacyNativeUnionMember.Ranks.Member,
            LegacyNativeUnionMember.GetRank((LegacyNativeUnionMember.MilitaryRanks)uint.MaxValue));
    }

    [WindowsNativeIniFact]
    public void ActiveUnionWritesItsOwnUidAndFourFieldsButPreservesGoldBrickAndUnrelatedState()
    {
        using var files = new LegacyNativeUnionClanOwnedProfiles();
        var path = files.Seed(uint.MaxValue, "[Character]\r\nUnionUID=88\r\nUnionRank=77\r\nUnionExploits=66\r\nTreasury=55\r\nUnionGoldBrick=44\r\nMarker=keep\r\n[Other]\r\nMarker=elsewhere\r\n", unicode: true);
        var member = new LegacyNativeUnionMember
        {
            UID = uint.MaxValue, Rank = (LegacyNativeUnionMember.MilitaryRanks)uint.MaxValue,
            Exploits = uint.MaxValue - 1, MyTreasury = uint.MaxValue - 2,
            Name = "does not persist", Class = 199, Mesh = 123, Level = 321,
        };
        LegacyNativeServerDatabasePersistence.UpdateUnionMember(member);
        Assert.Equal("4294967295", files.Read(path, "UnionUID"));
        Assert.Equal("4294967295", files.Read(path, "UnionRank"));
        Assert.Equal("4294967294", files.Read(path, "UnionExploits"));
        Assert.Equal("4294967293", files.Read(path, "Treasury"));
        Assert.Equal("44", files.Read(path, "UnionGoldBrick"));
        Assert.Equal("keep", files.Read(path, "Marker"));
        Assert.Equal("elsewhere", files.Read(path, "Marker", "Other"));
        Assert.Equal(uint.MaxValue, member.UID); Assert.Equal(uint.MaxValue - 1, member.Exploits);
        Assert.Equal(uint.MaxValue - 2, member.MyTreasury); Assert.Equal((byte)0, member.ReceiveKick);
        Assert.Equal((LegacyNativeUnionMember.MilitaryRanks)uint.MaxValue, member.Rank);
        Assert.Equal(new byte[] { 255, 254 }, File.ReadAllBytes(path)[..2]);
        files.CaptureExpectedMutation(path);
    }

    [WindowsNativeIniFact]
    public void AnyNonzeroKickClearsGoldBrickButLeavesTheDifferentTreasuryKeyUnchanged()
    {
        foreach (var kick in new byte[] { 1, byte.MaxValue })
        {
            using var files = new LegacyNativeUnionClanOwnedProfiles();
            var path = files.Seed(74, "[Character]\r\nUnionUID=1\r\nUnionRank=2\r\nUnionExploits=3\r\nUnionGoldBrick=4\r\nTreasury=555\r\nClanID=66\r\n");
            var member = new LegacyNativeUnionMember { UID = 74, ReceiveKick = kick, MyTreasury = 999, Exploits = 888, Rank = LegacyNativeUnionMember.MilitaryRanks.Emperor };
            LegacyNativeServerDatabasePersistence.UpdateUnionMember(member);
            foreach (var key in new[] { "UnionUID", "UnionRank", "UnionExploits", "UnionGoldBrick" })
                Assert.Equal("0", files.Read(path, key));
            Assert.Equal("555", files.Read(path, "Treasury"));
            Assert.Equal("66", files.Read(path, "ClanID"));
            Assert.Equal(kick, member.ReceiveKick); Assert.Equal(999u, member.MyTreasury);
            Assert.Equal(888u, member.Exploits); Assert.Equal(LegacyNativeUnionMember.MilitaryRanks.Emperor, member.Rank);
            files.CaptureExpectedMutation(path);
        }
    }

    [WindowsNativeIniFact]
    public void ClanRemovalAlwaysWritesThreeZerosAndDoesNotMutateTheSuppliedMember()
    {
        using var files = new LegacyNativeUnionClanOwnedProfiles();
        var path = files.Seed(0, "[Character]\r\nClanID=999\r\nClanRank=100\r\nClanDonation=1234\r\nUnionUID=678\r\nName=keep\r\n");
        var member = new LegacyNativeClanMember { UID = 0, Rank = LegacyNativeClanRanks.Leader, Donation = uint.MaxValue, Name = "not written", Online = true, Level = 255, Class = 199 };
        LegacyNativeServerDatabasePersistence.UpdateClanMember(member);
        foreach (var key in new[] { "ClanID", "ClanRank", "ClanDonation" })
            Assert.Equal("0", files.Read(path, key));
        Assert.Equal("678", files.Read(path, "UnionUID")); Assert.Equal("keep", files.Read(path, "Name"));
        Assert.Equal(0u, member.UID); Assert.Equal(LegacyNativeClanRanks.Leader, member.Rank);
        Assert.Equal(uint.MaxValue, member.Donation); Assert.Equal("not written", member.Name);
        Assert.True(member.Online); Assert.Equal((byte)255, member.Level); Assert.Equal(199u, member.Class);
        files.CaptureExpectedMutation(path);
    }

    [WindowsNativeIniFact]
    public void EachCallUsesCurrentRawDatabaseLocationAndCurrentMemberUid()
    {
        using var files = new LegacyNativeUnionClanOwnedProfiles();
        const string initial = "[Character]\r\nUnionUID=11\r\nClanID=22\r\nMarker=keep\r\n";
        var first = files.Seed(41, initial);
        var untouched = files.Seed(42, initial);
        var second = files.Seed(42, initial, secondDatabase: true);
        var member = new LegacyNativeUnionMember { UID = 41, MyTreasury = 7 };
        LegacyNativeServerDatabasePersistence.UpdateUnionMember(member);
        Assert.Equal("41", files.Read(first, "UnionUID"));
        Assert.Equal("11", files.Read(untouched, "UnionUID")); Assert.Equal("11", files.Read(second, "UnionUID"));
        files.CaptureExpectedMutation(first);
        member.UID = 42;
        LegacyNativeServerConfig.DbLocation = files.SecondDatabase;
        LegacyNativeServerDatabasePersistence.UpdateUnionMember(member);
        Assert.Equal("42", files.Read(second, "UnionUID"));
        Assert.Equal("11", files.Read(untouched, "UnionUID"));
        Assert.Equal("41", files.Read(first, "UnionUID"));
        files.CaptureExpectedMutation(second);
    }

    [WindowsNativeIniFact]
    public void NullMembersFaultBeforeIniUseAndMissingUsersDirectoryRemainsMissing()
    {
        using var files = new LegacyNativeUnionClanOwnedProfiles();
        Assert.Throws<NullReferenceException>(() => LegacyNativeServerDatabasePersistence.UpdateUnionMember(null!));
        Assert.Throws<NullReferenceException>(() => LegacyNativeServerDatabasePersistence.UpdateClanMember(null!));
        LegacyNativeServerConfig.DbLocation = files.MissingDatabase;
        Assert.Null(Record.Exception(() => LegacyNativeServerDatabasePersistence.UpdateUnionMember(new LegacyNativeUnionMember { UID = 51 })));
        Assert.Null(Record.Exception(() => LegacyNativeServerDatabasePersistence.UpdateClanMember(new LegacyNativeClanMember { UID = 52 })));
        Assert.False(Directory.Exists(files.MissingDatabase));
        Assert.False(File.Exists(files.MissingDatabase + "\\Users\\51.ini"));
        Assert.False(File.Exists(files.MissingDatabase + "\\Users\\52.ini"));
    }
}
