using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryPreparePackageStackWork(LegacyMailboxPackageProducer producer, LegacyMailboxPackageRecipe recipe,
        LegacyItemUidReservationReceipt? receipt, IReadOnlyList<LegacyTradeJournalChange> changes, ref bool admitted,
        out LegacyMailboxPackageProducer.PreparedJointWork? work, out string reason)
    {
        if (recipe.Count == 0)
        {
            if (receipt is not null) throw new InvalidOperationException("No-allocation work cannot carry a UID receipt.");
            var prepared = producer.TryPrepareNoAllocationWork(recipe, changes, out work, out var workAdmitted, out reason);
            admitted |= workAdmitted;
            return prepared;
        }
        if (receipt is null) throw new InvalidOperationException("Allocating package work requires its real range receipt.");
        return producer.TryPrepareJointWork(recipe, receipt, changes, out work, out reason);
    }
}
