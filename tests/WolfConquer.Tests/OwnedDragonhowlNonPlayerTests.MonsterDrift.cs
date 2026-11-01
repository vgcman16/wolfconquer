using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlNonPlayerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CompletedMonsterProcCannotAdoptSameUidReplacementOrRespawnOnItsRepeat(bool respawn) => Run(false, async f =>
    {
        await f.Attack();
        Assert.Equal(InitialHp - Damage, f.Hp());
        Assert.True(f.Binding.Client.OnAutoAttack);
        var actor = f.Current; var binding = f.Binding;
        var stamp = binding.Player.AttackStamp;
        if (!respawn)
            Assert.Same(f.Birth, f.Monsters.RegisterDynamicMonster(f.Birth!, f.Input.Family));
        else
        {
            // Actual component lifecycle APIs retire the old birth's life and
            // perform observed removal then respawn. This is not a proc reward.
            Assert.True(f.Monsters.ApplyDamage(f.Monster(), uint.MaxValue, f.Clock.GetUtcNow()).Killed);
            f.Clock.Advance(TimeSpan.FromSeconds(8));
            Assert.True(f.Monsters.TryConsumeDeathRemovalInstance(f.Birth!, f.Clock.GetUtcNow(), out _));
            f.Clock.Advance(TimeSpan.FromHours(1));
            Assert.True(f.Monsters.TryConsumeRespawnedInstance(f.Birth!, f.Clock.GetUtcNow(), out var returned));
            Assert.Equal(f.TargetUid, returned.Uid); Assert.Equal(InitialHp, returned.Hitpoints);
        }
        Assert.Equal(InitialHp, f.Hp());
        var files = f.Owner.Data.Files();
        var outputCount = f.Owner.ActorFrames.Count;
        await f.Repeat();
        Assert.Equal(InitialHp, f.Hp());
        Assert.Same(actor, f.Current);
        Assert.Equal(stamp, binding.Player.AttackStamp);
        Assert.False(binding.Client.OnAutoAttack);
        Assert.Equal(outputCount, f.Owner.ActorFrames.Count);
        Assert.Equal(500, f.Current.Character.MinMana);
        Assert.Equal(100, (int)binding.Player.Stamina);
        Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.False(f.Owner.Recovery.IsBlocked);
        f.Owner.Data.AssertFiles(files);
    });
}
