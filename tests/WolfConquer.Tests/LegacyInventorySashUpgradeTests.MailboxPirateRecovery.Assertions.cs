using System.Text;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private static void AssertPirateRecoveryNative(LegacyNativeMyCombatGear gear, CombatGearSnapshot expected)
    {
        Assert.Equal(7051U, gear.GetClass());
        Assert.Equal(expected.MasteryPoints, gear.MasteryPoints);
        Assert.Equal(expected.TaoistReiki, gear.TaoistReiki);
        Assert.Equal(expected.PirateExp, gear.PirateExp);
        Assert.Equal(expected.Lottery, gear.Lottery);
        var air = gear.AirPowers;
        Assert.Equal(0U, air.Id);
        Assert.Equal(expected.AirPowers, new CombatGearAirPowersSnapshot(air.SenseLevel, air.ArmedLevel,
            air.OverLoadLevel, air.Points, air.SenseAipPower, air.ArmedAipPower, air.OverLoadAipPower));
        Assert.Equal(expected.Count, gear.Items.Count);
        foreach (var row in expected.Items)
        {
            var item = Assert.IsType<LegacyNativeCombatGear.Weapon>(gear.Items[(LegacyNativeCombatGear.TypeID)row.ItemID]);
            Assert.Equal(row.ItemID, (uint)item.ItemID); Assert.Equal(row.Level, item.Level); Assert.Equal(row.Exp, item.Exp);
            Assert.Equal(row.Equip, item.Equip); Assert.Equal(row.Transform, item.Transform); Assert.Equal(row.MasteryScore, item.MasteryScore);
            for (var index = 0; index < 2; index++) Assert.Equal(row.Animas[index], item.TypeAnima[index]);
            Assert.Equal(row.BlackJades, item.BlackJades.Select(jade => jade.Type));
            Assert.All(item.BlackJades, jade => Assert.Equal(0U, jade.Data));
        }
        Assert.Equal(expected.BlackJadeBag.BlackJades.Order(), gear.BlackJadeBag.Keys.Order());
        Assert.All(gear.BlackJadeBag, pair => { Assert.Equal(pair.Key, pair.Value.Type); Assert.Equal(0U, pair.Value.Data); });
        Assert.Empty(gear.Info);
        Assert.Equal(0U, gear.StarFlow); Assert.Equal(0U, gear.Archshadow); Assert.Equal(0U, gear.ArrowBlades);
    }

    private static void AssertPirateRecoveryCommitted(Fixture fixture, PackageJoin join, PackageJoinBefore before, bool claimAll)
    {
        var intent = AssertPackageJoinAllocation(fixture, join);
        var expected = ExpectedPackageJoinUnallocatedFiles(fixture, before, claimAll, 7051);
        var items = ExpectedPackageJoinItems(before, intent, claimAll);
        var gear = PirateRecoveryAfter(PirateRecoveryGear());
        expected[Path.GetRelativePath(fixture.Root, fixture.ItemPath)] = PlayerItemSnapshotLoader.ToLegacyBytes(items);
        expected[Path.GetRelativePath(fixture.Root, fixture.PackageGearPath)] = new UTF8Encoding(false, true).GetBytes(CombatGearSnapshotLoader.ToLegacyText(gear));
        AssertPackageJoinLedger(fixture, before, expected);
        IncludeDebtCompletedWorkFile(fixture, join, before, intent, expected);
        AssertPackageJoinFiles(fixture, expected);
        Assert.Equal(JsonSerializer.Serialize(items), JsonSerializer.Serialize(join.Current.PlayerItems));
        Assert.Equal(JsonSerializer.Serialize(gear), JsonSerializer.Serialize(join.Current.CombatGear));
        Assert.Equal("1031", join.Current.Character.Attributes["ClassExperience"]);
        Assert.Equal(before.Owner.Character.Money + (claimAll ? (long)AtomicMailMoney : 0), join.Current.Character.Money);
        Assert.Equal(before.Owner.Character.ConquerPoints + (claimAll ? AtomicMailCp : 0), join.Current.Character.ConquerPoints);
        AssertPirateRecoveryNative(Assert.IsType<LegacyNativeMyCombatGear>(join.NativeClient.CombatGear), gear);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    }
}
