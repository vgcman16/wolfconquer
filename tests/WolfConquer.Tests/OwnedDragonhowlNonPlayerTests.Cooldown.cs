using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlNonPlayerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CurrentEarlyNonPlayerAttackIsOrdinaryRefusalThenDueHitSucceeds(bool sob) => Run(sob, async f =>
    {
        await f.Attack();
        Assert.Equal(InitialHp - Damage, f.Hp());
        var files = f.Owner.Data.Files();
        var stamp = f.Binding.Player.AttackStamp;
        var mana = f.Current.Character.MinMana;
        var stamina = f.Binding.Player.Stamina;
        var start = f.Owner.ActorFrames.Count;
        var priorLog = f.Owner.Log.ToString();
        await f.Attack();
        var earlyLog = f.Owner.Log.ToString();
        Assert.StartsWith(priorLog, earlyLog, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime-exception", earlyLog[priorLog.Length..], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WorldServer refused owned Dragonhowl physical reason=cooldown.", earlyLog[priorLog.Length..], StringComparison.Ordinal);
        Assert.Equal(InitialHp - Damage, f.Hp());
        Assert.Equal(stamp, f.Binding.Player.AttackStamp);
        Assert.Equal(mana, f.Current.Character.MinMana);
        Assert.Equal(stamina, f.Binding.Player.Stamina);
        Assert.DoesNotContain(f.Owner.ActorFrames.Skip(start), bytes =>
            LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var animation) &&
            animation.SpellId == 17_490);
        f.Owner.Data.AssertFiles(files);
        f.Clock.Advance(TimeSpan.FromMilliseconds(700));
        start = f.Owner.ActorFrames.Count;
        await f.Attack();
        Assert.Equal(InitialHp - 2 * Damage, f.Hp());
        AssertOneOutput(f, start, sob);
        Assert.False(f.Owner.Recovery.IsBlocked);
    });
}
