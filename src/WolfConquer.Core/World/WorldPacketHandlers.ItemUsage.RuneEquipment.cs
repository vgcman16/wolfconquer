using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyRuneEquipmentSource = "MsgItemUsuagePacket.EquipRune/UnequipRune";
    private const string LegacyEquipRuneLines = "2190-2218";
    private const string LegacyUnequipRuneLines = "2219-2242";

    private static async ValueTask HandleLegacyEquipRuneAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgItemUsagePacket packet,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        if (session.Character.Reborn < 2 || session.Character.Level < 15)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage equip rune " +
                    $"{FormatSession(context)} source={LegacyRuneEquipmentSource} sourceLines={LegacyEquipRuneLines} " +
                    $"itemUid={packet.Id}; source requires second reborn and level 15.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.DwParam > ushort.MaxValue)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage equip rune " +
                    $"{FormatSession(context)} source={LegacyRuneEquipmentSource} sourceLines={LegacyEquipRuneLines} " +
                    $"itemUid={packet.Id} position={packet.DwParam}; requested position is outside legacy range.")
                .ConfigureAwait(false);
            return;
        }

        var requestedPosition = checked((ushort)packet.DwParam);
        var selectedRune = session.PlayerItems.RuneItems.FirstOrDefault(item => item.Uid == packet.Id);
        if (selectedRune is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage equip rune " +
                    $"{FormatSession(context)} source={LegacyRuneEquipmentSource} sourceLines={LegacyEquipRuneLines} " +
                    $"itemUid={packet.Id}; rune item was not found.")
                .ConfigureAwait(false);
            return;
        }

        if (!IsLegacyRuneEquipSlot(selectedRune.ItemId, requestedPosition))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage equip rune " +
                    $"{FormatSession(context)} source={LegacyRuneEquipmentSource} sourceLines={LegacyEquipRuneLines} " +
                    $"itemUid={selectedRune.Uid} itemId={selectedRune.ItemId} position={requestedPosition}; " +
                    "source rune family does not match the requested slot.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadItemType(context.WorldData, selectedRune.ItemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.UnsupportedItemUse(
                        selectedRune.ItemId,
                        action: (uint)packet.Action,
                        actionName: packet.Action.ToString(),
                        itemUid: selectedRune.Uid,
                        source: LegacyRuneEquipmentSource,
                        reason: "rune itemtype metadata is not loaded, so legacy rune job requirements cannot be checked"))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "PARITY-GAP MsgItemUsage equip rune missing itemtype " +
                    $"{FormatSession(context)} source={LegacyRuneEquipmentSource} sourceLines={LegacyEquipRuneLines} " +
                    $"itemUid={selectedRune.Uid} itemId={selectedRune.ItemId}.")
                .ConfigureAwait(false);
            return;
        }

        if (!PassesLegacyItemClassRequirement(session.Character, itemType) ||
            !PassesLegacyItemLevelRequirement(session.Character, itemType))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage equip rune " +
                    $"{FormatSession(context)} source={LegacyRuneEquipmentSource} sourceLines={LegacyEquipRuneLines} " +
                    $"itemUid={selectedRune.Uid} itemId={selectedRune.ItemId}; " +
                    "character does not meet legacy rune item requirements.")
                .ConfigureAwait(false);
            return;
        }

        // Native Inventory.ContainItemWithStack treats raw0 as one physical
        // item for Count1. Keep that raw field; a stacked Rune needs a separate
        // split transaction and cannot equip/unequip through this single slot.
        var requestEcho = CreateLegacyItemUsageFrameWithArgs(packet, ReadLegacyItemUsageArgs(frame));
        if (!TryCommitLegacyItemSpell(context, session, (stable, identity) =>
            {
                if (!TryGetRuneItem(stable.PlayerItems, packet.Id, out var owned) || owned.ItemId != selectedRune.ItemId)
                    throw InvalidSpellMutation();
                return PrepareOwnedRuneProjection(context, stable, identity, true, packet.Id, requestedPosition, requestEcho);
            }, cancellationToken, out var updatedSession, out var preparedFrames, out var publicationIdentity, out var reason))
        {
            await context.Log.WriteLineAsync($"WorldServer held MsgItemUsage equip rune reason={reason}; no success was published.").ConfigureAwait(false);
            return;
        }
        await PublishItemSpellFramesAsync(context, updatedSession, preparedFrames, publicationIdentity, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync($"WorldServer committed MsgItemUsage equip rune {FormatSession(context with { Session = updatedSession })} itemUid={selectedRune.Uid}.").ConfigureAwait(false);
    }
    private static async ValueTask HandleLegacyUnequipRuneAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgItemUsagePacket packet,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        if (session.Character.Reborn < 2 || session.Character.Level < 15)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket
                        .SystemTalk("You need to be a second reborn hero to interact with rune system!")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage unequip rune " +
                    $"{FormatSession(context)} source={LegacyRuneEquipmentSource} sourceLines={LegacyUnequipRuneLines} " +
                    $"itemUid={packet.Id}; source requires second reborn and level 15.")
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox("Please make 1 more space in your inventory.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage unequip rune " +
                    $"{FormatSession(context)} source={LegacyRuneEquipmentSource} sourceLines={LegacyUnequipRuneLines} " +
                    $"itemUid={packet.Id}; inventory is full.")
                .ConfigureAwait(false);
            return;
        }

        if (TryReadLegacyVendorState(session.Character, out _))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox("Sorry, you cannot do this while vending.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage unequip rune " +
                    $"{FormatSession(context)} source={LegacyRuneEquipmentSource} sourceLines={LegacyUnequipRuneLines} " +
                    $"itemUid={packet.Id}; source rejects active vending.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryGetRuneItem(session.PlayerItems, packet.Id, out var rune))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage unequip rune " +
                    $"{FormatSession(context)} source={LegacyRuneEquipmentSource} sourceLines={LegacyUnequipRuneLines} " +
                    $"itemUid={packet.Id}; rune item was not found.")
                .ConfigureAwait(false);
            return;
        }

        // Native Inventory.ContainItemWithStack treats raw0 as one physical
        // item for Count1. Keep that raw field; a stacked Rune needs a separate
        // split transaction and cannot equip/unequip through this single slot.
        var requestEcho = CreateLegacyItemUsageFrameWithArgs(packet, ReadLegacyItemUsageArgs(frame));
        if (!TryCommitLegacyItemSpell(context, session, (stable, identity) =>
            {
                if (!TryGetRuneItem(stable.PlayerItems, packet.Id, out var owned) || owned.ItemId != rune.ItemId)
                    throw InvalidSpellMutation();
                return PrepareOwnedRuneProjection(context, stable, identity, false, packet.Id, LegacyRuneStoragePosition, requestEcho);
            }, cancellationToken, out var updatedSession, out var preparedFrames, out var publicationIdentity, out var reason))
        {
            await context.Log.WriteLineAsync($"WorldServer held MsgItemUsage unequip rune reason={reason}; no success was published.").ConfigureAwait(false);
            return;
        }
        await PublishItemSpellFramesAsync(context, updatedSession, preparedFrames, publicationIdentity, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync($"WorldServer committed MsgItemUsage unequip rune {FormatSession(context with { Session = updatedSession })} itemUid={rune.Uid}.").ConfigureAwait(false);
    }
    private static bool TryUnequipFirstMatchingRune(
        PlayerItemSnapshot items,
        Func<PlayerItemSnapshotItem, bool> predicate,
        out PlayerItemSnapshot updatedItems,
        out byte[] frame)
    {
        updatedItems = items;
        frame = [];
        var rune = items.RuneItems.FirstOrDefault(predicate);
        if (rune is null)
        {
            return false;
        }

        updatedItems = ReplaceRuneItem(items, rune, rune.WithPosition(LegacyRuneStoragePosition));
        frame = new MsgItemUsagePacket(
            rune.Uid,
            DwParam: rune.Position,
            MsgItemUsageAction.UnequipRune,
            Timestamp: 0,
            DwParam2: 0,
            DwParam3: 0,
            DwParam4: 0).ToFrameBytes();
        return true;
    }

    private static bool IsLegacyRuneEquipSlot(uint itemId, ushort position)
        => LegacyNativeItemTypeRuneMetadata.isRuneCheck(itemId, position);
}
