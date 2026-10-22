using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using Opt = WolfConquer.Core.World.LegacyNativeMsgCombatGearOpt;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatGearOptTests
{
    [Fact]
    public void TaoistMasteryAppliesOnlyAffordableAmountIncludingZeroAndUncheckedScoreWrap()
    {
        foreach (var c in new[] { (Points: 10u, Amount: 7u, Score: 20u, After: 27u, Allowed: true),
            (Points: 6u, Amount: 7u, Score: 20u, After: 20u, Allowed: false),
            (Points: 0u, Amount: 0u, Score: 20u, After: 20u, Allowed: true),
            (Points: uint.MaxValue, Amount: uint.MaxValue, Score: 20u, After: 19u, Allowed: true) })
        {
            using var s = new Scope();
            var weapon = s.Weapon(2);
            weapon.MasteryScore = c.Score;
            s.User.MasteryPoints = c.Points;
            Table.Info = null!;
            var info = Request(Opt.Types.MASTERY_SCORE_TAOIST);
            info.Transform = c.Amount;
            var frames = Opt.Process(info, s.User, 17, 7000);
            Assert.Equal(c.Allowed ? 2 : 0, frames.Count);
            if (c.Allowed)
            {
                Updated(frames[0], 2, mastery: c.After);
                Operation(frames[1], Opt.Types.MASTERY_SCORE_TAOIST, transform: c.Amount);
            }
            Assert.Equal(c.Allowed ? c.Points - c.Amount : c.Points, s.User.MasteryPoints);
            Assert.Equal(c.After, weapon.MasteryScore);
            Assert.Equal(c.Allowed ? 1u : 77u, info.UID);
        }
    }

    [Fact]
    public void PirateUpgradeSubtractsPreviousPhysicalLevelCostThenUpdatesWithoutChangingWeaponExp()
    {
        using var s = new Scope();
        var weapon = s.Weapon(2, 13, id: 701);
        Scope.Row(1, 70, id: 701);
        Scope.Row(2, 100, id: 701);
        Table.Info[701][3] = null!; // Successful increment does not read next-level metadata.
        s.User.PirateExp = 50;
        var info = Request(Opt.Types.UPGRADE_PIRATE);

        var frames = Opt.Process(info, s.User, 17, 2000);

        Assert.Equal(3, frames.Count);
        Pirate(frames[0], 20);
        Updated(frames[1], 3, exp: 13, id: 701);
        Operation(frames[2], Opt.Types.UPGRADE_PIRATE);
        Assert.Equal(20u, s.User.PirateExp);
        Assert.Equal(3u, weapon.Level);
        Assert.Equal(13u, weapon.Exp);
        Assert.Equal(1u, info.UID);
    }

    [Fact]
    public void PirateUpgradeKeepsMissingOrNullPreviousRowAndUnsignedLevelZeroCostSemantics()
    {
        foreach (string kind in new[] { "missing", "null", "level-zero" })
        {
            using var s = new Scope();
            bool zero = kind == "level-zero";
            uint level = zero ? 0u : 1u;
            var weapon = s.Weapon(level, 13);
            Scope.Row(level, 40);
            if (kind == "null") Table.Info[201][0] = null!;
            if (zero) Scope.Row(uint.MaxValue, 99);
            s.User.PirateExp = zero ? uint.MaxValue : 40;
            var info = Request(Opt.Types.UPGRADE_PIRATE);
            var frames = Opt.Process(info, s.User, 17, 2000);
            Assert.Equal(3, frames.Count);
            Pirate(frames[0], zero ? 58u : 0u);
            Updated(frames[1], level + 1, exp: 13);
            Operation(frames[2], Opt.Types.UPGRADE_PIRATE);
            Assert.Equal(zero ? 58u : 0u, s.User.PirateExp);
            Assert.Equal(level + 1, weapon.Level);
            Assert.Equal(13u, weapon.Exp);
            if (kind == "null") Assert.Null(Table.Info[201][0]);
        }
    }

    [Fact]
    public void PirateUpgradeMutatesUidEvenWhenCostOrLevelCapPreventsAnyOutput()
    {
        foreach (var c in new[] { (Level: 3u, Points: 29u), (Level: 4u, Points: 100u) })
        {
            using var s = new Scope();
            var weapon = s.Weapon(c.Level, 13);
            Scope.Row(c.Level - 1, 70);
            Scope.Row(c.Level, 100);
            s.User.PirateExp = c.Points;
            var info = Request(Opt.Types.UPGRADE_PIRATE);
            Assert.Empty(Opt.Process(info, s.User, 17, 2000));
            Assert.Equal(1u, info.UID);
            Assert.Equal(c.Level, weapon.Level);
            Assert.Equal(13u, weapon.Exp);
            Assert.Equal(c.Points, s.User.PirateExp);
        }
    }

    [Fact]
    public void PirateUpgradeMetadataFaultsPrecedeRequestUidAndCurrencyMutation()
    {
        foreach (string kind in new[] { "owner", "levels", "current" })
        {
            using var s = new Scope();
            var weapon = s.Weapon(2, 13);
            Scope.Row(1, 70);
            Scope.Row(2, 100);
            if (kind == "owner") Table.Info = null!;
            else if (kind == "levels") Table.Info[201] = null!;
            else Table.Info[201][2] = null!;
            s.User.PirateExp = 50;
            var info = Request(Opt.Types.UPGRADE_PIRATE);
            Assert.Throws<NullReferenceException>(() => Opt.Process(info, s.User, 17, 2000));
            Assert.Equal(77u, info.UID);
            Assert.Equal(2u, weapon.Level);
            Assert.Equal(13u, weapon.Exp);
            Assert.Equal(50u, s.User.PirateExp);
        }
    }

    [Fact]
    public void PirateMasteryRetainsZeroOneAndUnknownModesAndAlwaysEmitsExistingWeaponAck()
    {
        foreach (var c in new[]
        {
            (Mode: 0u, Points: 2u, Score: uint.MaxValue - 10, AfterPoints: 1u, AfterScore: 4u),
            (Mode: 0u, Points: 0u, Score: 7u, AfterPoints: 0u, AfterScore: 7u),
            (Mode: 1u, Points: 9u, Score: uint.MaxValue - 5, AfterPoints: 0u, AfterScore: 3u),
            (Mode: 99u, Points: 9u, Score: 7u, AfterPoints: 9u, AfterScore: 7u)
        })
        {
            using var s = new Scope();
            var weapon = s.Weapon(2, 13);
            weapon.MasteryScore = c.Score;
            s.User.PirateExp = c.Points;
            Table.Info = null!;
            var info = Request(Opt.Types.MASTERY_SCORE_PIRATE);
            info.Exp = c.Mode;
            var frames = Opt.Process(info, s.User, 17, 2000);
            Assert.Equal(3, frames.Count);
            Pirate(frames[0], c.AfterPoints);
            Updated(frames[1], 2, exp: 13, mastery: c.AfterScore);
            Operation(frames[2], Opt.Types.MASTERY_SCORE_PIRATE, exp: c.Mode);
            Assert.Equal(c.AfterPoints, s.User.PirateExp);
            Assert.Equal(c.AfterScore, weapon.MasteryScore);
            Assert.Equal(1u, info.UID);
        }
    }

    [Fact]
    public void PirateMasteryLateBuilderFaultKeepsCurrencyScoreAndUidMutations()
    {
        using var s = new Scope();
        var weapon = s.Weapon(2, 13);
        weapon.MasteryScore = 7;
        weapon.BlackJades = null!;
        s.User.PirateExp = 5;
        var info = Request(Opt.Types.MASTERY_SCORE_PIRATE);
        info.Exp = 1;
        Assert.Throws<NullReferenceException>(() => Opt.Process(info, s.User, 17, 2000));
        Assert.Equal(0u, s.User.PirateExp);
        Assert.Equal(12u, weapon.MasteryScore);
        Assert.Equal(1u, info.UID);
        Assert.Null(weapon.BlackJades);
    }

    [Fact]
    public void NullWeaponMasteryFaultLeavesEarlierCurrencyDebitButNotRequestUidMutation()
    {
        foreach (var type in new[] { Opt.Types.MASTERY_SCORE_TAOIST, Opt.Types.MASTERY_SCORE_PIRATE })
        {
            using var s = new Scope();
            s.User.Items[Gear.TypeID.Dragonhowl] = null!;
            s.User.MasteryPoints = 10;
            s.User.PirateExp = 5;
            var info = Request(type);
            info.Exp = 0;
            Assert.Throws<NullReferenceException>(() => Opt.Process(info, s.User, 17, 2000));
            Assert.Equal(type == Opt.Types.MASTERY_SCORE_TAOIST ? 3u : 10u, s.User.MasteryPoints);
            Assert.Equal(type == Opt.Types.MASTERY_SCORE_PIRATE ? 4u : 5u, s.User.PirateExp);
            Assert.Equal(77u, info.UID);
            Assert.Null(Assert.Single(s.User.Items).Value);
        }
    }
}
