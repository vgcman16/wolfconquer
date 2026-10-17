using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<LegacyViewEquipmentItemCounts> SendLegacyViewEquipmentItemsAsync(
        WorldPacketHandlerContext context,
        WorldSession target,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var targetUid = CheckedUInt32(target.Character.Uid, nameof(target.Character.Uid));
        var itemViewFrameCount = 0;
        var itemExtraFrameCount = 0;

        foreach (var item in LegacyEquipmentSelector.SelectCurrentEquipment(
            target.PlayerItems,
            target.AlternateEquipmentActive))
        {
            await context.Connection.SendAsync(
                    LegacyItemViewPacket.Create(
                        targetUid,
                        monsterUid: 0,
                        item,
                        LegacyItemViewActionMode.ViewEquip).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            itemViewFrameCount++;

            if (LegacyItemExtraPacket.TryCreate(item, DateTime.Now, out var itemExtra))
            {
                await context.Connection.SendAsync(itemExtra.ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
                itemExtraFrameCount++;
            }
        }

        return new LegacyViewEquipmentItemCounts(itemViewFrameCount, itemExtraFrameCount);
    }
}
