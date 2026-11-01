using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlWornSuccessorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task UsedRuneCatalogBackingArrayDriftAfterFlashingCannotRefreshTheCapturedEffect(bool targetRune) =>
        Run(targetRune, async f =>
        {
            var actor = f.Current(Actor).NativeGameClient!.OrdinarySpellBinding!;
            var target = f.Current(Target).NativeGameClient!.OrdinarySpellBinding!;
            var alias = WornAlias(targetRune ? target : actor);
            var item = Assert.Single(actor.ItemCatalog!.ItemsById[checked((int)alias.ITEM_ID)]);
            var fields = Assert.IsType<string[]>(item.Fields);
            var name = fields[1];
            Assert.Contains(targetRune ? "Impregnability" : "Front Break", name, StringComparison.OrdinalIgnoreCase);
            var initialStamp = actor.Player.AttackStamp; var targetStamp = target.Player.AttackStamp;
            var actorStart = f.ActorFrames.Count; var targetStart = f.Frames(Target).Count;
            Dictionary<string, byte[]>? files = null; DateTime committedStamp = default;
            var changed = false;
            f.BeforeSend = (recipient, bytes) =>
            {
                if (recipient != Actor || changed || !Stage(bytes, "flashing")) return;
                Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                Assert.Equal(100_000, f.Current(Target).Character.MinHitPoints);
                Assert.Equal(100_000, target.Player.HitPoints);
                Assert.Equal(name, fields[1]);
                committedStamp = actor.Player.AttackStamp;
                Assert.NotEqual(initialStamp, committedStamp);
                files = f.Data.Files();
                // This is the actual adopted, used catalog row backing array.
                // It changes effect classification after the cast captured it.
                fields[1] = "Unrelated Rune";
                changed = true;
            };
            try { await f.SendAsync(Actor, Request()); }
            finally { f.BeforeSend = null; fields[1] = name; }
            Assert.True(changed, f.Log.ToString());
            var capturedFiles = Assert.IsType<Dictionary<string, byte[]>>(files);
            AssertFencedBeforeReceive(f, actor, target, capturedFiles, committedStamp, targetStamp, actorStart, targetStart);
            await AssertRestoredDataCannotReviveTheRetiredCast(f, actor, target, capturedFiles);
        });
}
