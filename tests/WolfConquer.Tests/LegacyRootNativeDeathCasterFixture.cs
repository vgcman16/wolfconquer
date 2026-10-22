using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private sealed partial class RootMagicEntryFixture
    {
        public async Task PrepareRootNativeDeathCasterAsync()
        {
            Assert.True(nativeDeathCasterQueryControls);
            Assert.Equal(100_000L, Peer.Character.MinHitPoints);
            var start = Order.Count;
            await PacketHandlers.HandleAsync(context with
                { Connection = connections[TargetPlayerUid], Session = Peer },
                ReadFrame(new MsgActionPacket { ObjId = TargetPlayerUid, Type = (MsgActionType)251 }.ToFrameBytes()));
            var bytes = Assert.Single(FramesSince(TargetPlayerUid, start),
                frame => LegacyPlayerStatusPacket.TryParse(ReadFrame(frame), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var status));
            Assert.Equal(TargetPlayerUid, status.Uid);
            Assert.Equal(100_000U, status.MaxHP);
            Assert.Equal(1_000U, status.MaxMana);
            Assert.Equal(1U, status.MagicAttack);
            Assert.Equal(100_000L, Peer.Character.MinHitPoints);
        }
    }
}
