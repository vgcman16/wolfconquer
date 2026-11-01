using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlPhysicalLifecycleTests
{
    [Fact]
    public Task ActualPhysicalFlashingPublicationPrecedesFrontBreakTargetHpAndAnimation() => Run(async f =>
    {
        var actor = f.Current(Actor); var target = f.Current(Target);
        AssertNoFlashing(actor);
        Assert.False(Flag(actor, FrontBreak));
        Assert.Equal(100_000, target.Character.MinHitPoints);
        var targetPlayer = target.NativeGameClient!.Player;
        var stages = new List<string>();
        var earliest = DateTime.Now;
        var actorStart = f.ActorFrames.Count;
        f.BeforeSend = (recipient, bytes) =>
        {
            Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
            if (recipient == Actor && WireFlag(bytes, Actor, Flashing))
            {
                Assert.Equal(2176, (int)OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode);
                AssertTimedFlashing(f.Current(Actor), earliest, DateTime.Now);
                if (!WireFlag(bytes, Actor, FrontBreak))
                {
                    stages.Add("flashing");
                    Assert.False(Flag(f.Current(Actor), FrontBreak));
                    Assert.Equal(100_000, f.Current(Target).Character.MinHitPoints);
                    Assert.Equal(100_000, targetPlayer.HitPoints);
                }
                else
                {
                    stages.Add("front-break");
                    Assert.Equal(100_000, f.Current(Target).Character.MinHitPoints);
                    Assert.Equal(100_000, targetPlayer.HitPoints);
                }
            }
            if (recipient == Target && Hp(bytes, Target))
            {
                stages.Add("target-hp");
                Assert.Equal(2176, (int)OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode);
                Assert.Equal(99_418, f.Current(Target).Character.MinHitPoints);
                Assert.Equal(99_418, targetPlayer.HitPoints);
                Assert.True(Flag(f.Current(Actor), Flashing));
                Assert.True(Flag(f.Current(Actor), FrontBreak));
            }
            if (recipient == Actor && Animation(bytes))
            {
                stages.Add("animation");
                Assert.Equal(2239, (int)OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode);
                Assert.Equal(99_418, f.Current(Target).Character.MinHitPoints);
                Assert.Equal(99_418, targetPlayer.HitPoints);
            }
        };
        await f.SendAsync(Actor, Request());
        f.BeforeSend = null;
        Assert.Equal(new[] { "flashing", "front-break", "target-hp", "animation" }, stages);
        var bytes = Assert.Single(f.ActorFrames.Skip(actorStart), Animation);
        Assert.True(LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var animation));
        var hit = Assert.Single(animation.Targets);
        Assert.Equal(Target, hit.Uid);
        Assert.Equal(582U, hit.Damage); // Existing independent physical regression golden.
        Assert.Equal(MsgAttackEffect.Penetration, hit.Effect);
        AssertTimedFlashing(f.Current(Actor), earliest, DateTime.Now);
        Assert.False(f.Recovery.IsBlocked);
    });
}
