namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<LegacyViewEquipmentRuneCounts> SendLegacyViewEquipmentRunesAsync(
        WorldPacketHandlerContext context,
        WorldSession targetSession,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var targetUid = CheckedUInt32(targetSession.Character.Uid, nameof(targetSession.Character.Uid));
        var runeViewPacketCount = 0;
        foreach (var item in targetSession.PlayerItems.RuneItems)
        {
            await context.Connection.SendAsync(
                    LegacyItemViewPacket.Create(
                            targetUid,
                            monsterUid: 0,
                            item,
                            LegacyItemViewActionMode.Rune,
                            LegacyGameItemPacket.CreateRuneSpellOverride(item, context.WorldData?.ItemsById))
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            runeViewPacketCount++;
        }

        return new LegacyViewEquipmentRuneCounts(runeViewPacketCount);
    }
}
