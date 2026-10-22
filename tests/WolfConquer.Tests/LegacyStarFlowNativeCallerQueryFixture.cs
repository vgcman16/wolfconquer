using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private sealed partial class ArrowLifecycleFixture
    {
        private async Task PrepareStarFlowNativeCallerQueriesAsync()
        {
            if (!nativeStarFlowCallerQueryControls || nativeStarFlowCallerQueryPrepared) return;
            Assert.False(started); // Initial input preparation only; no timer advance or runtime owner.
            Assert.Equal(ArrowLifeOrigin, Now);
            foreach (var input in new[]
            {
                (Uid: PlayerUid, MagicAttack: 9002U, Hitpoints: 5000L),
                (Uid: TargetPlayerUid, MagicAttack: 1000U, Hitpoints: 100000L),
            })
            {
                var current = input.Uid == PlayerUid ? Owner : Peer;
                Assert.Equal(input.Hitpoints, current.Character.MinHitPoints);
                Assert.Equal(1000L, current.Character.MinMana);
                var begin = Order.Count;
                await PacketHandlers.HandleAsync(context with
                    { Connection = connections[input.Uid], Session = current },
                    ReadFrame(new MsgActionPacket { ObjId = input.Uid, Type = (MsgActionType)251 }.ToFrameBytes()));
                var bytes = Assert.Single(FramesSince(input.Uid, begin),
                    frame => LegacyPlayerStatusPacket.TryParse(ReadFrame(frame), out _));
                Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var status));
                Assert.Equal(input.Uid, status.Uid);
                Assert.Equal(input.MagicAttack, status.MagicAttack);
                Assert.Equal(checked((uint)input.Hitpoints), status.MaxHP);
                Assert.Equal(1000U, status.MaxMana);
                var refreshed = input.Uid == PlayerUid ? Owner : Peer;
                Assert.Equal(input.Hitpoints, refreshed.Character.MinHitPoints);
                Assert.Equal(1000L, refreshed.Character.MinMana);
            }
            Assert.Equal(0U, ArrowLifeCounter(Owner, "ArrowBlades"));
            Assert.Equal(0U, ArrowLifeCounter(Owner, "StarFlow"));
            Assert.Equal(ArrowLifeOrigin, Now);
            nativeStarFlowCallerQueryPrepared = true;
        }
    }
}
