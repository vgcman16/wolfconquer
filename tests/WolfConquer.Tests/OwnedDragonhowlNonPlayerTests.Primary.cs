using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlNonPlayerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualNonlethalPrimaryAndOwnedRepeatUseOriginalDamageAndPacketOrder(bool sob) => Run(sob, async f =>
    {
        var files = f.Owner.Data.Files();
        var beforeStamp = f.Binding.Player.AttackStamp;
        var initial = f.Owner.ActorFrames.Count;
        f.Owner.BeforeSend = (uid, bytes) =>
        {
            Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
            if (uid != Actor || !LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _)) return;
            Assert.Equal(InitialHp - Damage, f.Hp());
            Assert.Equal(f.Clock.GetLocalNow().DateTime, f.Binding.Player.AttackStamp);
        };
        await f.Attack();
        f.Owner.BeforeSend = null;
        Assert.NotEqual(beforeStamp, f.Binding.Player.AttackStamp);
        Assert.Equal(InitialHp - Damage, f.Hp());
        Assert.True(f.Binding.Client.OnAutoAttack);
        AssertOneOutput(f, initial, sob);
        f.Owner.Data.AssertFiles(files);
        initial = f.Owner.ActorFrames.Count;
        var firstStamp = f.Binding.Player.AttackStamp;
        await f.Repeat();
        Assert.Equal(InitialHp - 2 * Damage, f.Hp());
        Assert.NotEqual(firstStamp, f.Binding.Player.AttackStamp);
        Assert.Equal(f.Clock.GetLocalNow().DateTime, f.Binding.Player.AttackStamp);
        Assert.True(f.Binding.Client.OnAutoAttack);
        AssertOneOutput(f, initial, sob);
        Assert.Equal(500, f.Current.Character.MinMana);
        Assert.Equal(100, (int)f.Binding.Player.Stamina);
        Assert.Equal(100_000, f.Binding.Player.HitPoints);
        Assert.Equal(0, f.Current.Character.Experience);
        Assert.Equal(0, Assert.Single(f.Current.PlayerSpells.Spells).Experience);
        Assert.False(f.Owner.Recovery.IsBlocked);
        f.Owner.Data.AssertFiles(files);
    });

    private static void AssertOneOutput(NonPlayerFixture f, int start, bool sob)
    {
        var frames = f.Owner.ActorFrames.Skip(start).ToArray();
        var bytes = Assert.Single(frames, value => LegacySpellAnimationPacket.TryParse(
            OwnedEquipmentBehaviorFixture.ReadFrame(value), out var packet) && packet.SpellId == 17_490);
        Assert.Equal((ushort)2239, OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode);
        Assert.True(LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var animation));
        Assert.Equal(Actor, animation.AttackerUid); Assert.Equal((ushort)17_490, animation.SpellId);
        Assert.Equal((ushort)831, animation.X); Assert.Equal((ushort)584, animation.Y);
        var hit = Assert.Single(animation.Targets);
        Assert.Equal(f.TargetUid, hit.Uid); Assert.Equal(Damage, hit.Damage);
        Assert.True(hit.Hit); Assert.Equal(MsgAttackEffect.None, hit.Effect);
        var hpIndices = Enumerable.Range(0, frames.Length).Where(i => LegacyMsgUpdatePacket.TryParse(
            OwnedEquipmentBehaviorFixture.ReadFrame(frames[i]), out var packet) && packet.Uid == f.TargetUid &&
            packet.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.Hitpoints)).ToArray();
        if (sob) Assert.Empty(hpIndices); // Ordinary SOB setter has no invented generic HP packet.
        else
        {
            Assert.NotEmpty(hpIndices);
            Assert.All(hpIndices, i => Assert.Equal((ushort)2176, OwnedEquipmentBehaviorFixture.ReadFrame(frames[i]).Header.Opcode));
            Assert.True(hpIndices.Max() < Array.IndexOf(frames, bytes));
        }
        Assert.DoesNotContain(frames, value => MsgAttackPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(value), out var packet) &&
            packet.Query.AttackType == MsgAttackType.Physical);
    }
}
