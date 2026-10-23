using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Disdain = WolfConquer.Core.Data.LegacyNativeDisdain;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    [Theory]
    [InlineData(-1, 10000)] [InlineData(0, 10000)] [InlineData(1, 1000)] [InlineData(4, 1000)]
    [InlineData(5, 500)] [InlineData(9, 500)] [InlineData(10, 100)] [InlineData(50, 100)]
    public Task UserMonsterPolicyUsesActualBattlePowerAndAllDifferenceThresholds(int delta, int expected)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Power(100); var monster = s.Monster(100 + delta);
        Disdain.DisdainRecords[-delta] = new() { max_atk = 250 };
        Assert.Equal(expected, Disdain.UserAttackMonster(s.Player, monster, 10000));
    });

    [Fact]
    public Task MonsterCapDividesPercentBeforeMultiplyAndXpUsesDistinctColumn() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Power(100); var monster = s.Monster(100);
        Disdain.DisdainRecords[0] = new() { max_atk = 99, max_xp_atk = 199 };
        Assert.Equal(0, Disdain.UserAttackMonster(s.Player, monster, 30000));
        s.Flag(Flag.Cyclone);
        Assert.Equal(10000, Disdain.UserAttackMonster(s.Player, monster, 30000));
        Disdain.DisdainRecords[0].max_xp_atk = 250;
        Assert.Equal(20000, Disdain.UserAttackMonster(s.Player, monster, 30000));
        Disdain.DisdainRecords.Clear();
        Assert.Throws<NullReferenceException>(() => Disdain.UserAttackMonster(s.Player, monster, 30000));
    });

    [Theory]
    [InlineData(-1, 1000)] [InlineData(4, 1000)] [InlineData(5, 800)] [InlineData(9, 800)]
    [InlineData(10, 600)] [InlineData(14, 600)] [InlineData(15, 400)] [InlineData(19, 400)]
    [InlineData(20, 300)]
    public Task MonsterUserPolicyUsesActualMaxHpExtraDamageAndThresholds(int delta, int expected)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Power(100 + delta); s.Client.Status.MaxHitpoints = 10000;
        var monster = s.Monster(100, extraDamage: 1000);
        Assert.Equal(expected, Disdain.MonsterAttackUser(monster, s.Player, 1));
        Assert.Equal(2000, Disdain.MonsterAttackUser(monster, s.Player, 2000));
    });

    private static Disdain.DisdainItem FourGroups() => new()
    {
        usr_atk_usr_min = 100, usr_atk_usr_max = 200, usr_atk_usr_overadj = 50,
        usr_atk_usrx_min = 200, usr_atk_usrx_max = 400, usr_atk_usrx_overadj = 25,
        usrx_atk_usr_min = 300, usrx_atk_usr_max = 600, usrx_atk_usr_overadj = 10,
        usrx_atk_usrx_min = 400, usrx_atk_usrx_max = 800, usrx_atk_usrx_overadj = 5
    };

    [Theory]
    [InlineData(109, 109, 654)] [InlineData(109, 110, 1320)]
    [InlineData(110, 109, 1962)] [InlineData(110, 110, 2640)]
    public Task UserPolicySelectsAllFourLevelGroupsUsingGenuinePlayerOwners(ushort attackerLevel, ushort targetLevel, int expected)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Power(200, attackerLevel); var target = s.Peer(200, targetLevel, 2000, 0);
        Disdain.DisdainRecords[0] = FourGroups(); Assert.Equal(expected, Disdain.UserAttackUser(s.Player, target, 0));
    });

    [Theory]
    [InlineData(1000u, 0, 1000)] [InlineData(2000u, 0, 600)] [InlineData(4000u, 0, 600)]
    [InlineData(10000u, 0, 600)] [InlineData(14002u, 0, 600)] [InlineData(13002u, 0, 600)] [InlineData(9999u, 0, 600)]
    [InlineData(1000u, 1, 1800)] [InlineData(2000u, 1, 1800)] [InlineData(4000u, 1, 1400)]
    [InlineData(10000u, 1, 2700)] [InlineData(14002u, 1, 2700)] [InlineData(13002u, 1, 1800)] [InlineData(9999u, 1, 1800)]
    public Task UserPolicyUsesCompleteClassAndRebirthFactorTables(uint job, byte reborn, int expected) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Power(200, 100); var target = s.Peer(200, 100, job, reborn);
        Disdain.DisdainRecords[0] = FourGroups(); Assert.Equal(expected, Disdain.UserAttackUser(s.Player, target, 0));
    });

    [Fact]
    public Task DeathSighChangesActualLookupOnlyWhenTargetPowerIsGreater() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Power(100, 110); var target = s.Peer(200, 110, 2000, 0);
        Disdain.DisdainRecords[-100] = new() { usrx_atk_usrx_min = 100 };
        Disdain.DisdainRecords[0] = new() { usrx_atk_usrx_min = 500 };
        Assert.Equal(660, Disdain.UserAttackUser(s.Player, target, 0));
        s.Flag(Flag.DeathSigh); Assert.Equal(3300, Disdain.UserAttackUser(s.Player, target, 0));
        target.Owner.Equipment!.BattlePower -= 150;
        Disdain.DisdainRecords[50] = new() { usrx_atk_usrx_min = 200 };
        Assert.Equal(1320, Disdain.UserAttackUser(s.Player, target, 0));
    });

    [Theory]
    [InlineData(0, 2520)] [InlineData(4000, 4000)] [InlineData(10000, 6280)]
    public Task ScalarUserPolicyPreservesFloorInteriorAndOverCapAdjustment(int damage, int expected) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); Disdain.DisdainRecords[0] = new()
        { usrx_atk_usrx_min = 100, usrx_atk_usrx_max = 200, usrx_atk_usrx_overadj = 25 };
        Assert.Equal(expected, Disdain.UserAttackUser(100, 100, damage));
    });

    [Fact]
    public Task UserFormulaKeepsSignedOverflowAndMissingRowsAreHardFaults() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); Disdain.DisdainRecords[0] = new()
        { usrx_atk_usrx_min = int.MaxValue, usrx_atk_usrx_max = 200 };
        Assert.Equal(-25, Disdain.UserAttackUser(100, 100, -100));
        Disdain.DisdainRecords.Clear(); Assert.Throws<NullReferenceException>(() => Disdain.UserAttackUser(1, 1, 0));
    });
}
