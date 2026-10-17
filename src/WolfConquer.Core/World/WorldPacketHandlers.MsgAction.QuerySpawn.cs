using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleQuerySpawnActionAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryGetSessionByUid(context, packet.DwParam, out var target))
        {
            return;
        }

        var targetSession = target.Session;
        var currentHitpoints = CheckedUInt32(
            targetSession.Character.MinHitPoints,
            nameof(targetSession.Character.MinHitPoints));
        var maxHitpoints = ComputeLegacyRuntimeMaxHitpoints(
            targetSession.Character,
            currentHitpoints,
            targetSession.PlayerItems,
            targetSession.CombatGear ?? CombatGearSnapshot.Empty,
            context.WorldData,
            targetSession.AlternateEquipmentActive);

        await context.Connection.SendAsync(
                LegacyPlayerSpawnPacket.FromSnapshot(
                    targetSession.Character,
                    targetSession.PlayerItems,
                    alternateEquipmentActive: targetSession.AlternateEquipmentActive,
                    windowSpawn: true,
                    maxLife: maxHitpoints).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
