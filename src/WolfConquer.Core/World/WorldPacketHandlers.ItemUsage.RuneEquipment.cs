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

        var requestEcho = CreateLegacyItemUsageFrameWithArgs(packet, ReadLegacyItemUsageArgs(frame));
        if (!TryCommitLegacyItemSpell(context, session, (stable, identity) =>
            {
                if (stable.Character.Reborn < 2 || stable.Character.Level < 15 || packet.DwParam > ushort.MaxValue ||
                    !TryGetRuneItem(stable.PlayerItems, packet.Id, out var owned) || owned.ItemId != selectedRune.ItemId ||
                    owned.Locked != 0 || owned.StackSize == 0 || owned.DepositedItems.Count != 0 ||
                    !IsLegacyRuneEquipSlot(owned.ItemId, requestedPosition) ||
                    !TryReadItemType(context.WorldData, owned.ItemId, out var metadata) ||
                    !PassesLegacyItemClassRequirement(stable.Character, metadata) || !PassesLegacyItemLevelRequirement(stable.Character, metadata))
                    throw InvalidSpellMutation();
                if (stable.PlayerItems.RuneItems.Any(row => row.Locked != 0 && row.Position != LegacyRuneBagPosition &&
                    (row.ItemId == owned.ItemId || row.Position == requestedPosition))) throw InvalidSpellMutation();
                var items = stable.PlayerItems; var output = new List<byte[]>();
                if (TryUnequipFirstMatchingRune(items, row => row.ItemId == owned.ItemId && row.Position != LegacyRuneBagPosition,
                    out items, out var same)) output.Add(same);
                if (TryUnequipFirstMatchingRune(items, row => row.Position == requestedPosition && row.Position != LegacyRuneBagPosition,
                    out items, out var slot)) output.Add(slot);
                var chosen = items.RuneItems.Single(row => row.Uid == owned.Uid);
                items = ReplaceRuneItem(items, chosen, chosen.WithPosition(requestedPosition));
                var changed = ClampLegacyEquipmentRuntimeHitpoints(stable with { PlayerItems = items }, context.WorldData);
                changed = NormalizeItemSpellHitpoints(stable, changed);
                var refresh = BuildItemSpellRefresh(context, changed, identity);
                output.Add(requestEcho); output.AddRange(refresh);
                return new ItemSpellProjection(changed, output.ToArray(), refresh);
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

        var requestEcho = CreateLegacyItemUsageFrameWithArgs(packet, ReadLegacyItemUsageArgs(frame));
        if (!TryCommitLegacyItemSpell(context, session, (stable, identity) =>
            {
                if (stable.Character.Reborn < 2 || stable.Character.Level < 15 || !HasLegacyInventorySpace(stable.PlayerItems, 1) ||
                    TryReadLegacyVendorState(stable.Character, out _) || !TryGetRuneItem(stable.PlayerItems, packet.Id, out var owned) ||
                    owned.ItemId != rune.ItemId || owned.Locked != 0 || owned.StackSize == 0 || owned.DepositedItems.Count != 0 ||
                    !TryReadItemType(context.WorldData, owned.ItemId, out _))
                    throw InvalidSpellMutation();
                var currentRank = stable;
                var updateRank = context.RuneRankings is not null &&
                    TryRefreshLegacyPlayerMagicResourceSession(context, stable.Character.Uid, ref currentRank) &&
                    currentRank.Character.Reborn >= 2 && currentRank.Character.Level >= 15 &&
                    HasLegacyInventorySpace(currentRank.PlayerItems, 1) && !TryReadLegacyVendorState(currentRank.Character, out _) &&
                    TryGetRuneItem(currentRank.PlayerItems, packet.Id, out _);
                var items = ReplaceRuneItem(stable.PlayerItems, owned, owned.WithPosition(LegacyRuneStoragePosition));
                var spells = RemoveLegacyUnequippedRuneSpell(context, stable.PlayerSpells, owned, out var removed);
                var changed = ClampLegacyEquipmentRuntimeHitpoints(stable with { PlayerItems = items, PlayerSpells = spells }, context.WorldData);
                var output = new List<byte[]>();
                if (removed is { } id) output.Add(new MsgActionPacket { ObjId = CheckedUInt32(changed.Character.Uid, nameof(changed.Character.Uid)),
                    DwParam = id, Type = (MsgActionType)109 }.ToFrameBytes());
                changed = NormalizeItemSpellHitpoints(stable, changed);
                var refresh = BuildItemSpellRefresh(context, changed, identity);
                output.Add(requestEcho); output.AddRange(refresh);
                return new ItemSpellProjection(changed, output.ToArray(), refresh, updateRank);
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
    {
        return (itemId / 10_000) switch
        {
            401 => position is 101 or 121,
            402 => position is 102 or 122,
            403 => position is (>= 103 and <= 111) or (>= 123 and <= 131),
            _ => false,
        };
    }
}
