using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    // Reuse the numeric package's real saved inputs and ordinary admission.
    // No owner, native value or persistence callback is substituted.
    internal static Task CapricePairRunAsync(Func<OwnedEquipmentBehaviorFixture, WorldDataCatalog, Task> body) =>
        ReceiveStaminaOwnedAsync("40", "80", true, body);

    internal static async Task CapricePairPrepareQueriesAsync(OwnedEquipmentBehaviorFixture f, WorldDataCatalog world)
    {
        var context = new WorldPacketHandlerContext(f.Connection(PlayerUid), f.Log, f.Current(PlayerUid),
            WorldData: world, Sessions: f.Store, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        await PrepareRetainedNativeMagic421QueriesAsync(context, f.Connection(TargetPlayerUid),
            f.Frames(PlayerUid), f.Frames(TargetPlayerUid));
    }

    internal static Task CapricePairAttackAsync(OwnedEquipmentBehaviorFixture f)
    {
        var target = f.Current(TargetPlayerUid).Character;
        return f.SendAsync(PlayerUid, new MsgAttackPacket(new MsgAttackQuery(PlayerUid, TargetPlayerUid,
            checked((ushort)target.X), checked((ushort)target.Y), MsgAttackType.Magic, 0, 0, 0,
            MsgAttackEffect.None, 0, SpellId: ThunderSpellId)).ToFrameBytes());
    }
}
