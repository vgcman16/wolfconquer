using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using Opt = WolfConquer.Core.World.LegacyNativeMsgCombatGearOpt;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatGearOptTests
{
    [Fact]
    public void SingleTaoistUsesPhysicalItemAndCurrentLevelMetadataThenEmitsReikiUpdateAndAck()
    {
        using var s = new Scope();
        var weapon = s.Weapon(59, 20, id: 202);
        var row = Scope.Row(59, 100, id: 202);
        Scope.Row(59, 9000); // Requested dictionary key is not the physical metadata identity.
        Table.Info[202][60] = null!; // No DBItem read occurs after this single increment.
        s.User.TaoistReiki = 100;
        var info = Request(Opt.Types.UPGRADE_TAOIST);

        var frames = Opt.Process(info, s.User, 17, 7000);

        Assert.Equal(3, frames.Count);
        Reiki(frames[0], 20);
        Updated(frames[1], 60, exp: 100, id: 202);
        Operation(frames[2], Opt.Types.UPGRADE_TAOIST);
        Assert.Same(weapon, Assert.Single(s.User.Items).Value);
        Assert.Equal(20u, s.User.TaoistReiki);
        Assert.Equal(60u, weapon.Level);
        Assert.Equal(100u, weapon.Exp);
        Assert.Equal(999u, row.Level);
        Assert.Equal(100u, row.Exp);
        Assert.Null(Table.Info[202][60]);
    }

    [Fact]
    public void SingleTaoistPreservesUnsignedCreditMissingZeroFallbackAndLevelSixtyCap()
    {
        foreach (var c in new[]
        {
            (Level: 1u, Exp: 15u, Reiki: 0u, Metadata: (uint?)10, AfterLevel: 2u, AfterExp: 10u, AfterReiki: 5u),
            (Level: 1u, Exp: 0u, Reiki: 7u, Metadata: (uint?)null, AfterLevel: 2u, AfterExp: 0u, AfterReiki: 7u),
            (Level: 60u, Exp: 20u, Reiki: 100u, Metadata: (uint?)100, AfterLevel: 60u, AfterExp: 100u, AfterReiki: 20u)
        })
        {
            using var s = new Scope();
            var weapon = s.Weapon(c.Level, c.Exp);
            if (c.Metadata.HasValue) Scope.Row(c.Level, c.Metadata.Value);
            s.User.TaoistReiki = c.Reiki;
            var info = Request(Opt.Types.UPGRADE_TAOIST);
            var frames = Opt.Process(info, s.User, 17, 2000);
            Assert.Equal(3, frames.Count);
            Reiki(frames[0], c.AfterReiki);
            Updated(frames[1], c.AfterLevel, exp: c.AfterExp);
            Operation(frames[2], Opt.Types.UPGRADE_TAOIST);
            Assert.Equal(c.AfterLevel, weapon.Level);
            Assert.Equal(c.AfterExp, weapon.Exp);
            Assert.Equal(c.AfterReiki, s.User.TaoistReiki);
        }
    }

    [Fact]
    public void SingleTaoistDeniesInsufficientOrOverflowedUnsignedSumWithoutChangingRequest()
    {
        foreach (var c in new[] { (Reiki: 79u, Exp: 20u, Need: 100u), (Reiki: uint.MaxValue, Exp: 1u, Need: 1u) })
        {
            using var s = new Scope();
            var weapon = s.Weapon(1, c.Exp);
            Scope.Row(1, c.Need);
            s.User.TaoistReiki = c.Reiki;
            var info = Request(Opt.Types.UPGRADE_TAOIST);
            Assert.Empty(Opt.Process(info, s.User, 17, 2000));
            Assert.Equal(c.Reiki, s.User.TaoistReiki);
            Assert.Equal(c.Exp, weapon.Exp);
            Assert.Equal(1u, weapon.Level);
            Assert.Equal(77u, info.UID);
        }
    }

    [Fact]
    public void SingleTaoistMetadataAndLateBuilderFaultsHaveDifferentMutationPrefixes()
    {
        foreach (bool late in new[] { false, true })
        {
            using var s = new Scope();
            var weapon = s.Weapon(59, 20);
            Scope.Row(59, 100);
            if (late) weapon.BlackJades[0] = null!;
            else Table.Info[201][59] = null!;
            s.User.TaoistReiki = 100;
            var info = Request(Opt.Types.UPGRADE_TAOIST);
            Assert.Throws<NullReferenceException>(() => Opt.Process(info, s.User, 17, 2000));
            Assert.Equal(late ? 20u : 100u, s.User.TaoistReiki);
            Assert.Equal(late ? 60u : 59u, weapon.Level);
            Assert.Equal(late ? 100u : 20u, weapon.Exp);
            Assert.Equal(late ? 1u : 77u, info.UID);
            if (late) Assert.Null(weapon.BlackJades[0]);
            else Assert.Null(Table.Info[201][59]);
        }
    }

    [Fact]
    public void TaoistAllReadsNewCurrentLevelEachIterationAndNaturallyFinishesAtSixty()
    {
        using var s = new Scope();
        var weapon = s.Weapon(58, 90);
        Scope.Row(58, 100);
        Scope.Row(59, 150);
        Scope.Row(60, 1000);
        s.User.TaoistReiki = 70;
        var info = Request(Opt.Types.UPGRADE_TAOIST_ALL);

        var frames = Opt.Process(info, s.User, 17, 2000);

        Assert.Equal(4, frames.Count);
        Reiki(frames[0], 60);
        Reiki(frames[1], 10);
        Updated(frames[2], 60, exp: 150);
        Operation(frames[3], Opt.Types.UPGRADE_TAOIST_ALL);
        Assert.Equal(60u, weapon.Level);
        Assert.Equal(150u, weapon.Exp);
        Assert.Equal(10u, s.User.TaoistReiki);
        Assert.Equal(1u, info.UID);
    }

    [Fact]
    public void TaoistAllStillAcknowledgesExistingWeaponWhenInsufficientOrAlreadyAtCap()
    {
        foreach (var c in new[] { (Level: 58u, Reiki: 9u), (Level: 60u, Reiki: 100u), (Level: 61u, Reiki: 100u) })
        {
            using var s = new Scope();
            var weapon = s.Weapon(c.Level, 90);
            Scope.Row(c.Level, 100);
            s.User.TaoistReiki = c.Reiki;
            var info = Request(Opt.Types.UPGRADE_TAOIST_ALL);
            var frames = Opt.Process(info, s.User, 17, 2000);
            Assert.Equal(2, frames.Count);
            Updated(frames[0], c.Level, exp: 90);
            Operation(frames[1], Opt.Types.UPGRADE_TAOIST_ALL);
            Assert.Equal(c.Reiki, s.User.TaoistReiki);
            Assert.Equal(c.Level, weapon.Level);
            Assert.Equal(90u, weapon.Exp);
            Assert.Equal(1u, info.UID);
        }
    }

    [Fact]
    public void TaoistAllDereferencesNextMetadataBeforeItsCapCheckAndKeepsEarlierProgressOnFault()
    {
        using var s = new Scope();
        var weapon = s.Weapon(59, 90);
        Scope.Row(59, 100);
        Table.Info[201][60] = null!;
        s.User.TaoistReiki = 20;
        var info = Request(Opt.Types.UPGRADE_TAOIST_ALL);

        Assert.Throws<NullReferenceException>(() => Opt.Process(info, s.User, 17, 2000));

        Assert.Equal(10u, s.User.TaoistReiki);
        Assert.Equal(60u, weapon.Level);
        Assert.Equal(100u, weapon.Exp);
        Assert.Equal(1u, info.UID);
        Assert.Null(Table.Info[201][60]);
    }
}
