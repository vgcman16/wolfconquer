using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeHeroRewardsRecordTests
{
    private static LegacyNativeGameClientRuntime RecordClient()
    {
        var prior = LegacyNativePoolRandom.GetRandom;
        try
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(821);
            return new LegacyNativeGameClientRuntime();
        }
        finally { LegacyNativePoolRandom.GetRandom = prior; }
    }

    [Fact]
    public void WholeOwnerRetainsActualConstructorIdentityAndIndependentMutableGoals()
    {
        var owner = RecordClient();
        Assert.Null(owner.HeroRewards);
        var first = new LegacyNativeHeroRewardsRuntime(owner);
        var other = new LegacyNativeHeroRewardsRuntime(RecordClient());
        Assert.Same(owner, typeof(LegacyNativeHeroRewardsRuntime).GetField("Owner", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(first));
        Assert.NotSame(first.ArrayGoals, other.ArrayGoals);
        first.AddGoal(101);
        var goal = first.ArrayGoals[101];
        Assert.Equal(101u, goal.UID);
        Assert.Equal(0u, goal.UnKnow);
        Assert.Equal((byte)0, goal.Claim);
        goal.Claim = 7;
        first.AddGoal(101);
        Assert.Same(goal, first.ArrayGoals[101]);
        Assert.True(first.ContainGoal(101));
        Assert.False(other.ContainGoal(101));
        Assert.Equal("1/101/7/", first.ToString());
    }

    [Fact]
    public void StageQueriesUseCoherentPhysicalRowsAndKeepExactCountEquality()
    {
        using var tables = new NativeSaveRecordTables();
        var rewards = new LegacyNativeHeroRewardsRuntime(RecordClient());
        LegacyNativePoolSaveRecords.TableHeroRewards[1] = new() { CountGoals = 2 };
        rewards.ArrayGoals[101] = new() { UID = 101, Claim = 1 };
        rewards.ArrayGoals[102] = new() { UID = 102, Claim = 2 };
        Assert.True(rewards.FinishStage(1));
        Assert.False(rewards.ComplateFullStage(1));
        rewards.ArrayGoals[1] = new() { UID = 1, Claim = 1 };
        Assert.True(rewards.ComplateFullStage(1));
        rewards.ArrayGoals[103] = new() { UID = 103, Claim = 1 };
        Assert.False(rewards.FinishStage(1));
        Assert.False(rewards.ComplateFullStage(1));
        Assert.False(rewards.FinishStage(99));
        Assert.False(rewards.ComplateFullStage(99));
    }

    [Fact]
    public void RecordUsesValueEnumerationOrderAndLoadPreservesDuplicatesAndExistingObjectState()
    {
        var rewards = new LegacyNativeHeroRewardsRuntime(RecordClient());
        rewards.ArrayGoals[102] = new() { UID = 102, Claim = 2, UnKnow = 77 };
        rewards.ArrayGoals[101] = new() { UID = 101, Claim = 1 };
        Assert.Equal("2/102/2/101/1/", rewards.ToString());
        var loaded = new LegacyNativeHeroRewardsRuntime(RecordClient());
        loaded.Load(rewards.ToString());
        var prior = loaded.ArrayGoals[102];
        loaded.Load("3/102/9/103/4/103/8/");
        Assert.Equal(3, loaded.ArrayGoals.Count);
        Assert.Same(prior, loaded.ArrayGoals[102]);
        Assert.Equal((byte)2, prior.Claim);
        Assert.Equal((byte)4, loaded.ArrayGoals[103].Claim);
        Assert.Equal(0u, loaded.ArrayGoals[102].UnKnow);
    }

    [Theory]
    [InlineData("", true)] [InlineData("-1/101/1/", false)]
    [InlineData("2/101/999/102/3/", false)]
    [InlineData("1/invalid/invalid/", false)]
    public void GoalRecordsAcceptEmptyButMalformedFieldsNeverPublishDefaultRows(string input, bool valid)
    {
        var rewards = new LegacyNativeHeroRewardsRuntime(RecordClient());
        var book = rewards.ArrayGoals;
        Assert.Equal(valid, rewards.TryLoad(input));
        Assert.Same(book, rewards.ArrayGoals); Assert.Equal("0/", rewards.ToString());
        if (!valid) Assert.Throws<InvalidDataException>(() => rewards.Load(input));
        Assert.Same(book, rewards.ArrayGoals); Assert.Empty(rewards.ArrayGoals);
    }

    [Theory]
    [InlineData(0u, (ushort)0, (byte)0, true)]
    [InlineData(2130u, (ushort)129, (byte)2, false)]
    [InlineData(2130u, (ushort)130, (byte)1, false)]
    [InlineData(2130u, (ushort)130, (byte)2, true)]
    [InlineData(uint.MaxValue, ushort.MaxValue, byte.MaxValue, false)]
    public void BothRealCatalogRowsUseTheSameUnclampedNativeOpenLevelPartition(uint open, ushort level, byte reborn, bool expected)
    {
        Assert.Equal(expected, new LegacyNativeInfoHeroRewardTable.StageGoal { OpenLev = open }.CheckOpen(level, reborn));
        Assert.Equal(expected, new LegacyNativeActivityTaskTable.Task { OpenLev = open }.CheckOpen(level, reborn));
    }

    [Fact]
    public void WholeHeroCatalogLoaderKeepsZeroOpenLevAndAppendsUsingOneBasedSectionKeys()
    {
        using var files = new NativeSaveRecordFiles();
        File.WriteAllText(Path.Combine(files.Root, "StageGoal.txt"),
            "[StageGoal]\r\nStageAmount=1\r\n[1]\r\nGoalNum=2\r\nOpenLev=2130\r\nPrize1=1088000@@3\r\n[1-1]\r\nProgress=7\r\nPrize1=1000000@@2\r\n[1-2]\r\nProgress=8\r\nPrize1=2000000@@4\r\n");
        var table = new LegacyNativeInfoHeroRewardTable();
        table.LoadInformations();
        var stage = Assert.Single(table).Value;
        Assert.Equal(1u, stage.UID);
        Assert.Equal(0u, stage.OpenLev); // Native loader never reads this field.
        Assert.Equal(2u, stage.CountGoals);
        Assert.Equal(1088000u, stage.GetPrize());
        Assert.Equal((byte)3, stage.GetCount());
        Assert.Equal(new uint[] { 101, 102 }, stage.ArrayItem.Keys);
        Assert.Equal((byte)7, stage.ArrayItem[101].Progres);
        Assert.Equal(1000000u, stage.ArrayItem[101].GetPrize());
        Assert.Equal((byte)4, stage.ArrayItem[102].GetCount());
        table.LoadInformations(); // Genuine SafeDictionary.Add replaces an existing key.
        Assert.NotSame(stage, table[1]);
        Assert.Equal(2, table[1].ArrayItem.Count);
    }

    [Theory]
    [InlineData("1@@2", 1u, (byte)2)] [InlineData("@@7@@@@8@@", 7u, (byte)8)]
    public void StageAndItemPrizeParsersBothRetainRemoveEmptyEntries(string prize, uint id, byte count)
    {
        var stage = new LegacyNativeInfoHeroRewardTable.StageGoal { Prize = prize };
        var item = new LegacyNativeInfoHeroRewardTable.StageGoal.Item { Prize = prize };
        Assert.Equal(id, stage.GetPrize()); Assert.Equal(count, stage.GetCount());
        Assert.Equal(id, item.GetPrize()); Assert.Equal(count, item.GetCount());
    }
}
