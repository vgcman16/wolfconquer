using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlWornSuccessorTests
{
    [Theory]
    [InlineData(false, "flashing")]
    [InlineData(false, "front-break")]
    [InlineData(true, "flashing")]
    public Task ActualCapturedWornRowDriftAfterCommittedPublicationCannotBecomeAFreshSuccessor(bool targetRune, string stage) =>
        Run(targetRune, async f =>
        {
            var beforeActor = f.Current(Actor); var beforeTarget = f.Current(Target);
            var actor = beforeActor.NativeGameClient!.OrdinarySpellBinding!;
            var target = beforeTarget.NativeGameClient!.OrdinarySpellBinding!;
            var alias = WornAlias(targetRune ? target : actor);
            var originalPosition = alias.Position;
            var initialStamp = actor.Player.AttackStamp; var targetStamp = target.Player.AttackStamp;
            var actorStart = f.ActorFrames.Count; var targetStart = f.Frames(Target).Count;
            Dictionary<string, byte[]>? files = null;
            DateTime committedStamp = default;
            var changed = false;
            f.BeforeSend = (recipient, bytes) =>
            {
                if (recipient != Actor || changed || !Stage(bytes, stage)) return;
                Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                Assert.Equal((ushort)2176, OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode);
                Assert.Equal(100_000, f.Current(Target).Character.MinHitPoints);
                Assert.Equal(100_000, target.Player.HitPoints);
                Assert.True(LegacyPlayerDeathState.HasStatusFlag(f.Current(Actor).Character.Attributes, Flashing));
                if (stage == "front-break")
                    Assert.Equal((uint?)66, LegacyPhysicalStatusOverlay.ForOwner(f.Current(Actor)).Breakthrough);
                committedStamp = actor.Player.AttackStamp;
                Assert.NotEqual(initialStamp, committedStamp);
                files = f.Data.Files();
                // Objects returns a defensive array, but its actual admitted
                // item rows remain exposed aliases. Position is a public field,
                // not a fabricated setter or a newly constructed native row.
                Assert.Same(alias, Assert.Single((targetRune ? target : actor).Items.Rune.Objects));
                alias.Position = 211;
                Assert.Equal((ushort)211, alias.Position);
                changed = true;
            };
            try { await f.SendAsync(Actor, Request()); }
            finally { f.BeforeSend = null; alias.Position = originalPosition; }
            Assert.True(changed, f.Log.ToString());
            var capturedFiles = Assert.IsType<Dictionary<string, byte[]>>(files);
            AssertFencedBeforeReceive(f, actor, target, capturedFiles, committedStamp, targetStamp, actorStart, targetStart);
            await AssertRestoredDataCannotReviveTheRetiredCast(f, actor, target, capturedFiles);
        });
}
