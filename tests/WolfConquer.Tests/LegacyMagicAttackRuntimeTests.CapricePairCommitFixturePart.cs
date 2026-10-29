using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    // Actual existing numeric preparation over the genuine admitted records.
    internal static (WorldSession Actor, WorldSession Peer, WorldPacketHandlers.LegacyCapricePairIntent Intent)
        CapricePairDraft(OwnedEquipmentBehaviorFixture f)
    {
        var prepared = ReceiveStaminaPrepare(f, true);
        Assert.True(prepared.Applied);
        var actor = f.Current(PlayerUid); var peer = f.Current(TargetPlayerUid);
        var nextActor = actor with { Character = prepared.Actor }; var nextPeer = peer with { Character = prepared.Peer };
        Assert.True(WorldPacketHandlers.LegacyCapricePairIntent.TryCreate(actor, peer, nextActor, nextPeer, out var intent));
        return (nextActor, nextPeer, intent!);
    }
}
