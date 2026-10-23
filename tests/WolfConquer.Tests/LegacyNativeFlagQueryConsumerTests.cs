using WolfConquer.Core.World;
using Disdain = WolfConquer.Core.Data.LegacyNativeDisdain;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeEquipmentQueryPrerequisiteTests
{
    [Fact]
    public Task UnavailableCycloneQueryCannotProduceAttackSpeedOrReadMissingOptionalRune() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        s.Client.Rune = null; s.Equipment.SpeedR = 17; s.Player.Agility = 123;
        var vector = LegacyNativeRankFlagCorruption.Apply(s.Client, "word-no-registration");
        Assert.Throws<InvalidDataException>(() => s.Equipment.AttackSpeed(800));
        Assert.Equal(17, s.Equipment.SpeedR); Assert.Equal((ushort)123, s.Player.Agility);
        Assert.Empty(s.Native.Queue); vector.AssertUnchanged();
    });
}

public sealed partial class LegacyNativeCombatCalculationTests
{
    [Theory]
    [InlineData("attack")]
    [InlineData("defense")]
    [InlineData("disdain-player")]
    [InlineData("disdain-monster")]
    public Task UnavailableFlagCalculationCannotReturnDamageOrMutateItsPhysicalInputs(string operation) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Power(100, 110);
        var peer = s.Peer(100, 110, 2000, 0); var monster = s.Monster(100);
        Disdain.DisdainRecords[0] = FourGroups();
        var record = Disdain.DisdainRecords[0]; var status = s.Client.Status;
        status.Defence = 123; status.PhysicalPercent = 17;
        s.Player.doFocus = true; s.Player.IntensifyDamage = 250;
        var vector = LegacyNativeRankFlagCorruption.Apply(s.Client, "word-no-registration");
        bool returned = false;
        Assert.Throws<InvalidDataException>(() =>
        {
            switch (operation)
            {
                case "attack": _ = s.Client.AjustAttack(1001, null); break;
                case "defense": _ = s.Client.AjustDefense; break;
                case "disdain-player": _ = Disdain.UserAttackUser(s.Player, peer, 1001); break;
                case "disdain-monster": _ = Disdain.UserAttackMonster(s.Player, monster, 1001); break;
                default: throw new InvalidOperationException(operation);
            }
            returned = true;
        });
        Assert.False(returned); Assert.Same(status, s.Client.Status);
        Assert.Equal(123u, status.Defence); Assert.Equal(17u, status.PhysicalPercent);
        Assert.True(s.Player.doFocus); Assert.Equal(250, s.Player.IntensifyDamage);
        Assert.Same(record, Disdain.DisdainRecords[0]);
        Assert.Empty(s.Queue); vector.AssertUnchanged();
    });
}

public sealed partial class LegacyNativeSaveSchedulerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public Task UnavailableRaceQueryDoesNotBecomeNegatedPermission(int query) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        s.Client.Vigor = 91; s.Client.RaceExtraVigor = 17;
        var vector = LegacyNativeRankFlagCorruption.Apply(s.Client, "word-no-registration");
        bool granted = false;
        Assert.Throws<InvalidDataException>(() =>
        {
            bool present = query switch
            {
                0 => s.Client.RaceGuard,
                1 => s.Client.RaceDecelerated,
                2 => s.Client.RaceExcitement,
                _ => throw new InvalidOperationException(),
            };
            if (!present) granted = true;
        });
        Assert.False(granted); Assert.Equal(91u, s.Client.Vigor); Assert.Equal(17u, s.Client.RaceExtraVigor);
        Assert.Empty(s.Queue); vector.AssertUnchanged();
    });

    [Fact]
    public Task UnavailableRideQueryCannotGrantEvenWhenTheMissingCropOwnerWouldOtherwiseBeSkipped() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Client.Equipment = null;
        Assert.True(s.Client.AllowUseSpellOnSteed(7002));
        var vector = LegacyNativeRankFlagCorruption.Apply(s.Client, "word-no-registration");
        bool granted = false;
        Assert.Throws<InvalidDataException>(() => granted = s.Client.AllowUseSpellOnSteed(7002));
        Assert.False(granted); Assert.Null(s.Client.Equipment); Assert.Empty(s.Queue); vector.AssertUnchanged();
    });
}
