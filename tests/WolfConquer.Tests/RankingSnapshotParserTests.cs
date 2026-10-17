using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class RankingSnapshotParserTests
{
    [Fact]
    public void RankingSnapshotParsersReadLiveLikeCountedRowsAndPreserveMetadata()
    {
        var combatGear = Assert.Single(RankingSnapshotParser.ParseCombatGearRanks(
            "Count=1\n1296473/HeroOne/26/0/4000/101/161008/27/0/0/0/0/500006/0/0/132009/204009/0/0/0//0/\n",
            "CombatGearRank.txt"));

        Assert.Equal(1_296_473, combatGear.CharacterId);
        Assert.Equal("HeroOne", combatGear.CharacterName);
        Assert.Equal(23, combatGear.Fields.Count);
        Assert.Equal("", combatGear.Fields[20]);
        Assert.Equal("", combatGear.Fields[^1]);
        Assert.Equal(["26", "0", "4000", "101"], combatGear.RankingFields.Take(4).ToArray());
        Assert.Equal("CombatGearRank.txt", combatGear.SourceFile);
        Assert.Equal(2, combatGear.LineNumber);
        Assert.Equal("1296473/HeroOne/26/0/4000/101/161008/27/0/0/0/0/500006/0/0/132009/204009/0/0/0//0/", combatGear.RawLine);

        var prestige = Assert.Single(RankingSnapshotParser.ParsePrestigeRanks(
            "Count=1\n1293601/xXSHAD3NXx/3/4022/140/3372007/1065657/10/113309/194395/613429/360208/613429/360208/200407/133309/60126012/12/20922092/0/WeAreCharmers/43/0/64000/0/937322/3500/0/3835/2000/0/0/0/0/0/0/0/5400/50/50/0/0/3000/9900/22000/0/0/0/0/0/0/0/0/0/0/0/0/0/0/14600/0/0/0/0/0/\n",
            "PrestigeRanking.txt"));

        Assert.Equal(1_293_601, prestige.CharacterId);
        Assert.Equal("xXSHAD3NXx", prestige.CharacterName);
        Assert.Equal(66, prestige.Fields.Count);
        Assert.Equal("", prestige.Fields[^1]);
        Assert.Equal("WeAreCharmers", prestige.Fields[20]);
        Assert.Equal("14600", prestige.RankingFields[^6]);

        var innerPower = Assert.Single(RankingSnapshotParser.ParseInnerPowerRanks(
            "Count=1\nTest/1296459/0/0/\n",
            "InnerPower.txt"));

        Assert.Equal("Test", innerPower.CharacterName);
        Assert.Equal(1_296_459, innerPower.CharacterId);
        Assert.Equal(["0", "0"], innerPower.RankingFields);
        Assert.Equal("", innerPower.Fields[^1]);

        var reward = Assert.Single(RankingSnapshotParser.ParseNewPlayerRewards(
            "Count=1\n198.51.100.12/020000CB0245/\n",
            "NewPlayerRewords.txt"));

        Assert.Equal("198.51.100.12", reward.IpAddress);
        Assert.Equal("020000CB0245", reward.DeviceId);
        Assert.Equal("", reward.Fields[^1]);

        var vote = Assert.Single(RankingSnapshotParser.ParseVotes(
            "Count=1\n1296463/198.51.100.15/638379291346047487/\n",
            "Votes.txt"));

        Assert.Equal(1_296_463, vote.CharacterId);
        Assert.Equal("198.51.100.15", vote.IpAddress);
        Assert.Equal("638379291346047487", vote.Timestamp);
        Assert.Equal("638379291346047487", vote.Fields[2]);

        var couplesPk = Assert.Single(RankingSnapshotParser.ParseCouplesPk(
            "Count=1\nThe_ScorpiooN/sdsadsa/\n",
            "CouplesPK.ini"));

        Assert.Equal("The_ScorpiooN", couplesPk.PlayerName);
        Assert.Equal("sdsadsa", couplesPk.PartnerName);
        Assert.Equal("", couplesPk.Fields[^1]);
    }

    [Fact]
    public void SplitSlashRowPreservesIntermediateAndTrailingEmptyFields()
    {
        var fields = RankingSnapshotParser.SplitSlashRow("1296473/HeroOne//0/");

        Assert.Equal(["1296473", "HeroOne", "", "0", ""], fields);
    }

    [Fact]
    public void InnerPowerParserPreservesSerializedStageAndGongFields()
    {
        var row = Assert.Single(RankingSnapshotParser.ParseInnerPowerRanks(
            "Count=1\nHero/1293600/7/1/1/1/2/11/100/1/5/1/12/100/1/7/0/\n",
            "InnerPower.txt"));

        Assert.Equal(18, row.Fields.Count);
        Assert.Equal("1", row.Fields[3]);
        Assert.Equal(["11", "100", "1", "5", "1"], row.Fields.Skip(7).Take(5).ToArray());
        Assert.Equal(row.Fields.Skip(2).Take(row.Fields.Count - 3), row.RankingFields);
        Assert.Equal(string.Empty, row.Fields[^1]);
    }

    [Fact]
    public void CountedParserRejectsDeclaredCountMismatch()
    {
        var ex = Assert.Throws<FormatException>(() => RankingSnapshotParser.ParseInnerPowerRanks(
            "Count=2\nTest/1296459/0/0/\n",
            "InnerPower.txt"));

        Assert.Contains("Count", ex.Message);
        Assert.Contains("2", ex.Message);
        Assert.Contains("1", ex.Message);
    }

    [Fact]
    public void RowParsersRejectWrongFieldCounts()
    {
        Assert.Throws<FormatException>(() => RankingSnapshotParser.ParseCombatGearRankRow("CombatGearRank.txt", 2, "1296473/HeroOne/26/"));
        Assert.Throws<FormatException>(() => RankingSnapshotParser.ParsePrestigeRankRow("PrestigeRanking.txt", 2, "1293601/xXSHAD3NXx/3/"));
        Assert.Throws<FormatException>(() => RankingSnapshotParser.ParseInnerPowerRankRow("InnerPower.txt", 2, "Test/1296459/0/0"));
        Assert.Throws<FormatException>(() => RankingSnapshotParser.ParseNewPlayerRewardRow("NewPlayerRewords.txt", 2, "198.51.100.12/020000CB0245/extra/"));
        Assert.Throws<FormatException>(() => RankingSnapshotParser.ParseVoteRow("Votes.txt", 2, "1296463/198.51.100.15/638379291346047487"));
        Assert.Throws<FormatException>(() => RankingSnapshotParser.ParseCouplesPkRow("CouplesPK.ini", 2, "The_ScorpiooN/sdsadsa/extra/"));
    }

    [Fact]
    public void RowParsersReportInvalidSafeNumericIds()
    {
        var combatGear = Assert.Throws<FormatException>(() => RankingSnapshotParser.ParseCombatGearRankRow(
            "CombatGearRank.txt",
            2,
            "not-id/HeroOne/26/0/4000/101/161008/27/0/0/0/0/500006/0/0/132009/204009/0/0/0//0/"));

        Assert.Contains("combat gear rank character id", combatGear.Message);
        Assert.Contains("not-id", combatGear.Message);

        var prestige = Assert.Throws<FormatException>(() => RankingSnapshotParser.ParsePrestigeRankRow(
            "PrestigeRanking.txt",
            2,
            "not-id/xXSHAD3NXx/3/4022/140/3372007/1065657/10/113309/194395/613429/360208/613429/360208/200407/133309/60126012/12/20922092/0/WeAreCharmers/43/0/64000/0/937322/3500/0/3835/2000/0/0/0/0/0/0/0/5400/50/50/0/0/3000/9900/22000/0/0/0/0/0/0/0/0/0/0/0/0/0/0/14600/0/0/0/0/0/"));

        Assert.Contains("prestige rank character id", prestige.Message);
        Assert.Contains("not-id", prestige.Message);

        var innerPower = Assert.Throws<FormatException>(() => RankingSnapshotParser.ParseInnerPowerRankRow(
            "InnerPower.txt",
            2,
            "Test/not-id/0/0/"));

        Assert.Contains("inner power rank character id", innerPower.Message);
        Assert.Contains("not-id", innerPower.Message);

        var vote = Assert.Throws<FormatException>(() => RankingSnapshotParser.ParseVoteRow(
            "Votes.txt",
            2,
            "not-id/198.51.100.15/638379291346047487/"));

        Assert.Contains("vote character id", vote.Message);
        Assert.Contains("not-id", vote.Message);
    }
}
