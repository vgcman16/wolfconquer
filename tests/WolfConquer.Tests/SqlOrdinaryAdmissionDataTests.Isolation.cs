using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class SqlOrdinaryAdmissionDataTests
{
    [Fact]
    public void EveryPreparedReadOwnsItsMutableQuestItemsPerfectionAndNestedGearGraph()
    {
        var plan = Plan(CompleteBundle()); var original = RawImages(plan.After);
        var first = plan.ReadPreparedData(); var second = plan.ReadPreparedData();
        Assert.NotSame(first, second); Assert.Same(plan.After, first.Source); Assert.Same(plan.After, second.Source);
        Assert.NotSame(first.Character.Attributes, second.Character.Attributes);
        Assert.NotSame(first.Character.PlayerQuests.Quests[0].Intentions, second.Character.PlayerQuests.Quests[0].Intentions);
        for (int index = 0; index < 7; index++)
        {
            var a = Assert.Single(Locations(first.Items)[index]); var b = Assert.Single(Locations(second.Items)[index]);
            Assert.NotSame(a, b); Assert.NotSame(a.DepositedItems[0], b.DepositedItems[0]);
        }
        Assert.NotSame(first.CombatGear.Items[0].Animas[0], second.CombatGear.Items[0].Animas[0]);
        MutateCallerData(first);
        AssertCompleteData(second); AssertCompleteData(plan.ReadPreparedData());
        AssertRawUnchanged(original, plan.After);
        Assert.All(plan.After.Sidecars.ToArray(), row => Assert.Equal(SqlWorldStateSidecarPresence.Present, row.Presence));
    }

    internal static void MutateCallerData(SqlOrdinaryAdmissionData data)
    {
        Assert.IsType<Dictionary<string, string>>(data.Character.Attributes)["Money"] = "999999";
        Assert.IsType<Dictionary<string, string>>(data.Character.PaidNobility)["OpaquePaid"] = "foreign";
        foreach (var location in Locations(data.Items))
        {
            var root = Assert.Single(location); root.PerfectionLevel = 999; root.OwnerName = "foreign-root";
            var child = Assert.Single(root.DepositedItems); child.PerfectionProgress = 999; child.OwnerName = "foreign-child";
        }
        Assert.IsAssignableFrom<IList<PlayerItemSnapshotItem>>(data.Items.InventoryItems)[0] = new() { Uid = 999, ItemId = 999 };
        Assert.IsAssignableFrom<IList<PlayerSpellSnapshotEntry>>(data.Spells.Spells)[0] = new(6001, 9, 9, 9, 9, 9);
        Assert.IsAssignableFrom<IList<PlayerProficiencySnapshotEntry>>(data.Proficiencies.Proficiencies)[0] = new(999, 9, 9, 9, 9);
        Assert.IsAssignableFrom<IList<uint>>(data.Character.PlayerQuests.Quests[0].Intentions)[0] = 999;
        Assert.IsAssignableFrom<IList<PlayerQuestSnapshotEntry>>(data.Character.PlayerQuests.Quests)[1] = new(999, PlayerQuestStatus.Available, 9, [999U]);
        Assert.IsAssignableFrom<IList<uint>>(data.CombatGear.Items[0].Animas[0])[0] = 999;
        Assert.IsAssignableFrom<IList<uint>>(data.CombatGear.Items[0].BlackJades)[0] = 999;
        Assert.IsAssignableFrom<IList<uint>>(data.CombatGear.BlackJadeBag.BlackJades)[0] = 999;
        var rawCopy = data.Source.Core.CopyItemBytes(); rawCopy.AsSpan().Fill(0);
    }
}
