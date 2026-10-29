using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyMagicAttackRuntimeTests;

namespace WolfConquer.Tests;

public sealed partial class CapricePairContinuationOwnedTests
{
    [Fact]
    public Task ActualCapriceAndLightCoexistenceKeepsFinalActor100AndCurrentTarget60ReplyAndDamage() =>
        F.CapriceContinuationLightRunAsync(async (f, world) =>
        {
            await F.CapricePairPrepareQueriesAsync(f, world);
            var actorBinding = Binding(f, Actor); var peerBinding = Binding(f, Peer);
            Assert.Equal("100", f.Current(Actor).Character.Attributes["PerfectionStatus.LightOfStamina"]);
            Assert.Equal("0", f.Current(Actor).Character.Attributes["LegacyLightOfStaminaRoll"]);
            Assert.Equal("100", Saved(f, Actor).Attributes["PerfectionStatus.LightOfStamina"]);
            bool lightObserved = false, targetObserved = false;
            f.BeforeSend = (uid, bytes) =>
            {
                if (uid == Actor && Stamina(bytes, 100)) lightObserved = true;
                else if (uid == Peer && Stamina(bytes, 60))
                {
                    targetObserved = true; Assert.True(lightObserved);
                    Assert.Equal("100", f.Current(Actor).Character.Attributes["Stamina"]);
                    Assert.Equal("60", f.Current(Peer).Character.Attributes["Stamina"]);
                    Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate); DurablePair(f);
                }
            };
            await F.CapricePairAttackAsync(f); f.BeforeSend = null;

            Assert.True(lightObserved); Assert.True(targetObserved);
            Assert.Equal("100", f.Current(Actor).Character.Attributes["Stamina"]);
            Assert.Equal("60", f.Current(Peer).Character.Attributes["Stamina"]);
            Assert.DoesNotContain(f.ActorFrames, bytes => Stamina(bytes, 60));
            Assert.Contains(f.ActorFrames, bytes => Convert.ToHexString(bytes) == "0E00800808A0FA4E120408081064");
            Assert.Single(f.Frames(Peer), bytes => Convert.ToHexString(bytes) == "0E00800808A1FA4E12040808103C");
            Assert.Contains(f.ActorFrames, bytes => Convert.ToHexString(bytes) == "0E000F0908A1FA4E10A1FA4E1807");
            foreach (var uid in new[] { Actor, Peer })
            {
                var frames = f.Frames(uid).Select(OwnedEquipmentBehaviorFixture.ReadFrame)
                    .Where(frame => LegacySpellAnimationPacket.TryParse(frame, out _)).ToArray();
                var frame = Assert.Single(frames); Assert.True(LegacySpellAnimationPacket.TryParse(frame, out var animation));
                Assert.Equal((ushort)1000, animation.SpellId); Assert.Equal(Actor, animation.AttackerUid);
                var target = Assert.Single(animation.Targets); Assert.Equal(Peer, target.Uid); Assert.True(target.Hit); Assert.Equal(421U, target.Damage);
            }
            Assert.Equal(5000L - 421, f.Current(Peer).Character.MinHitPoints);
            Assert.Equal(5000L - 421, Saved(f, Peer).MinHitPoints);
            Assert.Equal(5000 - 421, peerBinding.Player.HitPoints);
            Assert.Same(actorBinding, Binding(f, Actor)); Assert.Same(peerBinding, Binding(f, Peer));
            DurablePair(f); Assert.False(f.Recovery.IsBlocked);
        });
}
