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

        var updatedItems = session.PlayerItems;
        var mutationFrames = new List<byte[]>();
        var unequippedCount = 0;
        if (TryUnequipFirstMatchingRune(
            updatedItems,
            item => item.ItemId == selectedRune.ItemId && item.Position != LegacyRuneBagPosition,
            out updatedItems,
            out var sameRuneFrame))
        {
            mutationFrames.Add(sameRuneFrame);
            unequippedCount++;
        }

        if (TryUnequipFirstMatchingRune(
            updatedItems,
            item => item.Position == requestedPosition && item.Position != LegacyRuneBagPosition,
            out updatedItems,
            out var slotRuneFrame))
        {
            mutationFrames.Add(slotRuneFrame);
            unequippedCount++;
        }

        var latestSelected = updatedItems.RuneItems.First(item => item.Uid == selectedRune.Uid);
        updatedItems = ReplaceRuneItem(updatedItems, latestSelected, latestSelected.WithPosition(requestedPosition));

        var updatedSession = ClampLegacyEquipmentRuntimeHitpoints(
            session with { PlayerItems = updatedItems },
            context.WorldData);
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await TryPersistLegacyItemUsageMutationAsync(
                updatedContext,
                updatedSession,
                "equip rune",
                characterValues: null,
                persistItems: true)
            .ConfigureAwait(false);

        foreach (var mutationFrame in mutationFrames)
        {
            await context.Connection.SendAsync(mutationFrame, cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                CreateLegacyItemUsageFrameWithArgs(packet, ReadLegacyItemUsageArgs(frame)),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyEquipmentRuntimeRefreshAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage equip rune " +
                $"{FormatSession(updatedContext)} source={LegacyRuneEquipmentSource} sourceLines={LegacyEquipRuneLines} " +
                $"itemUid={selectedRune.Uid} itemId={selectedRune.ItemId} position={requestedPosition} " +
                $"unequippedCount={unequippedCount}.")
            .ConfigureAwait(false);
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

        // Ranking admission is read-only; retain the existing managed transfer even if native would reject.
        var rankAdmissionSession = session;
        var publishRuneRank = TryRefreshLegacyPlayerMagicResourceSession(
                context, session.Character.Uid, ref rankAdmissionSession) &&
            rankAdmissionSession.Character.Reborn >= 2 &&
            rankAdmissionSession.Character.Level >= 15 &&
            HasLegacyInventorySpace(rankAdmissionSession.PlayerItems, 1) &&
            !TryReadLegacyVendorState(rankAdmissionSession.Character, out _) &&
            TryGetRuneItem(rankAdmissionSession.PlayerItems, packet.Id, out _);

        var updatedItems = ReplaceRuneItem(session.PlayerItems, rune, rune.WithPosition(LegacyRuneStoragePosition));
        var updatedSpells = RemoveLegacyUnequippedRuneSpell(context, session.PlayerSpells, rune, out var removedSpellId);
        var updatedSession = ClampLegacyEquipmentRuntimeHitpoints(
            session with { PlayerItems = updatedItems, PlayerSpells = updatedSpells },
            context.WorldData);
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await TryPersistLegacyItemUsageMutationAsync(
                updatedContext,
                updatedSession,
                "unequip rune",
                characterValues: null,
                persistItems: true)
            .ConfigureAwait(false);

        if (removedSpellId is { } spellId)
        {
            await context.Connection.SendAsync(new MsgActionPacket
                {
                    ObjId = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
                    DwParam = spellId,
                    Type = (MsgActionType)109,
                }.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                CreateLegacyItemUsageFrameWithArgs(packet, ReadLegacyItemUsageArgs(frame)),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyEquipmentRuntimeRefreshAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        if (publishRuneRank)
        {
            UpdateLegacyRuneRanking(updatedContext, updatedSession);
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage unequip rune " +
                $"{FormatSession(updatedContext)} source={LegacyRuneEquipmentSource} sourceLines={LegacyUnequipRuneLines} " +
                $"itemUid={rune.Uid} itemId={rune.ItemId} position={LegacyRuneStoragePosition}.")
            .ConfigureAwait(false);
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
