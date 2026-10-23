using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record DragonSkinMutationPlan(WorldSession Updated, uint ItemId, byte[] InventoryFrame,
        byte[] Acknowledgement, LegacyDragonSkinInventory BeforeItems, LegacyDragonSkinInventory AfterItems);

    private static bool TryPrepareDragonSkinMutation(WorldPacketHandlerContext context, DragonSkinOwner owner,
        LegacyDragonSkinPacket packet, out DragonSkinMutationPlan? plan, out byte[]? rejection, out string reason)
    {
        plan = null; rejection = null;
        reason = "dragonSkinPreparationInvalid";
        try
        {
            var session = owner.Session;
            var state = owner.Book.State;
            var unlock = packet.Type == LegacyDragonSkinType.Unlock;
            if (unlock && state.Owns(packet.SkinType))
            { reason = "skin is already owned"; return false; }
            if (!unlock && !state.Owns(packet.SkinType))
                return RejectDragonSkinMutation(session, packet, "skin is not owned", out rejection, out reason);
            if (!TryGetLegacyDragonSkinItemId(packet.SkinType, out var itemId))
                return RejectDragonSkinMutation(session, packet, "skin has no legacy item mapping", out rejection, out reason);
            if (session.LegacyTradeActive || !LegacyTradeEscrowClaims.TryRead(owner.Book.CopyFields(), out var escrow, out _) || escrow is not null)
            { reason = "dragonSkinTradeEscrowActiveOrInvalid"; return false; }
            if (!LegacyDragonSkinInventory.TryCapture(session.PlayerItems, out var before, out reason)) return false;
            PlayerItemSnapshot items;
            LegacyDragonSkinState next;
            byte[] inventoryFrame;
            if (unlock)
            {
                var cost = session.PlayerItems.InventoryItems.FirstOrDefault(row => row.ItemId == itemId && row.Bound == 0 && row.StackSize > 0);
                if (cost is null)
                    return RejectDragonSkinMutation(session, packet, $"itemId={itemId} has no positive unbound inventory stack", out rejection, out reason);
                if (cost.DepositedItems.Count != 0) { reason = "dragonSkinCostHasDeposits"; return false; }
                items = ConsumeSingleLegacyInventoryItem(session.PlayerItems, cost, out inventoryFrame);
                next = state.AddOwned(packet.SkinType);
            }
            else
            {
                if (!HasLegacyInventorySpace(session.PlayerItems, 1))
                    return RejectDragonSkinMutation(session, packet, "inventory is full", out rejection, out reason);
                if (!TryCreateLegacyDragonSkinRetrievedItem(context, session, itemId, out var row, out reason)) return false;
                inventoryFrame = LegacyGameItemPacket.FromSnapshotItem(row).ToFrameBytes();
                items = session.PlayerItems with { InventoryItems = session.PlayerItems.InventoryItems.Concat([row]).ToArray() };
                next = state.RemoveOwned(packet.SkinType);
            }
            var fields = owner.Book.CopyFields();
            fields[LegacyDragonSkinBook.Key] = next.ToSnapshotValue();
            var updated = session with { Character = session.Character with { Attributes = fields }, PlayerItems = items };
            var acknowledgement = new LegacyDragonSkinPacket(packet.Type, checked((uint)session.Character.Uid), packet.SkinType, true).ToFrameBytes();
            if (!LegacyDragonSkinInventory.TryCapture(items, out var after, out reason)) return false;
            if (!before.IsCurrent(session.PlayerItems) || !owner.Book.IsCurrent(session.Character.Attributes) ||
                !IsCurrentDragonSkinOwner(context, owner, session))
            { reason = "dragonSkinPreparedOwnerChanged"; return false; }
            plan = new(updated, itemId, inventoryFrame, acknowledgement, before, after);
            reason = string.Empty;
            return true;
        }
        catch (Exception error) when (LegacyDragonSkinPersistence.IsPreparationFailure(error))
        { reason = "dragonSkinPreparation:" + error.GetType().Name; return false; }
    }

    private static bool RejectDragonSkinMutation(WorldSession session, LegacyDragonSkinPacket packet, string why,
        out byte[]? rejection, out string reason)
    {
        rejection = new LegacyDragonSkinPacket(packet.Type, checked((uint)session.Character.Uid), packet.SkinType, false).ToFrameBytes();
        reason = why;
        return false;
    }
}
