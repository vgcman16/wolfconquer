using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask ApplyLegacyAccountSecurityFullAccountRunesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!TryGetLegacyFullAccountRuneGrant(optionId, out var grant))
        {
            return;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, grant.RequiredSpaces))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox(grant.FullBagMessage).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc AccountSecurity FullAccount runes " +
                    $"{FormatSession(context)} source={LegacyAccountSecurityFullAccountSource} " +
                    $"sourceLines={LegacyAccountSecurityFullAccountSourceLines} optionId={optionId} " +
                    $"color={grant.ColorName} requiredSpaces={grant.RequiredSpaces} " +
                    $"loadedInventoryItems={session.PlayerItems.InventoryItems.Count} " +
                    $"message={grant.FullBagMessage}; source bag check precedes catalog and persistence.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await LogLegacyAccountSecurityFullAccountRuneFenceAsync(
                    context,
                    optionId,
                    grant,
                    "session-store-missing")
                .ConfigureAwait(false);
            return;
        }

        if (context.WorldData is not { } worldData)
        {
            await LogLegacyAccountSecurityFullAccountRuneFenceAsync(
                    context,
                    optionId,
                    grant,
                    "world-data-missing")
                .ConfigureAwait(false);
            return;
        }

        var candidates = worldData.Items
            .Where(item => IsLegacyFullAccountMaxRune(item, grant))
            .GroupBy(item => item.Id)
            .Select(group => group.First())
            .ToArray();
        if (candidates.Length == 0)
        {
            await LogLegacyAccountSecurityFullAccountRuneFenceAsync(
                    context,
                    optionId,
                    grant,
                    "matching-rune-catalog-empty")
                .ConfigureAwait(false);
            return;
        }

        if (candidates.Any(item => item.Id <= 0 || item.LegacyDurability > ushort.MaxValue))
        {
            await LogLegacyAccountSecurityFullAccountRuneFenceAsync(
                    context,
                    optionId,
                    grant,
                    "itemtype-invalid")
                .ConfigureAwait(false);
            return;
        }

        var availableSpaces = LegacyInventorySlots - session.PlayerItems.InventoryItems.Count;
        var grantTypes = candidates.Take(availableSpaces).ToArray();
        var workingItems = session.PlayerItems;
        var grantedItems = new List<PlayerItemSnapshotItem>(grantTypes.Length);
        try
        {
            foreach (var itemType in grantTypes)
            {
                var item = CreatePurchasedItems(
                        workingItems,
                        itemType,
                        count: 1,
                        isBound: false)[0]
                    .WithColor(checked((uint)Random.Shared.Next(3, 9)));
                workingItems = workingItems with
                {
                    InventoryItems = workingItems.InventoryItems
                        .Concat([item])
                        .OrderBy(candidate => candidate.Uid)
                        .ToArray(),
                };
                grantedItems.Add(item);
            }
        }
        catch (OverflowException)
        {
            await LogLegacyAccountSecurityFullAccountRuneFenceAsync(
                    context,
                    optionId,
                    grant,
                    "item-uid-or-field-exhausted")
                .ConfigureAwait(false);
            return;
        }

        byte[][] itemFrames;
        try
        {
            itemFrames = grantedItems
                .Select(item => LegacyGameItemPacket
                    .FromSnapshotItem(
                        item,
                        LegacyGameItemPacket.CreateRuneSpellOverride(item, worldData.ItemsById),
                        LegacyGameItemPacket.AddItemMode)
                    .ToFrameBytes())
                .ToArray();
        }
        catch (OverflowException)
        {
            await LogLegacyAccountSecurityFullAccountRuneFenceAsync(
                    context,
                    optionId,
                    grant,
                    "item-packet-invalid")
                .ConfigureAwait(false);
            return;
        }

        var updatedSession = session with { PlayerItems = workingItems };
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryPersistLegacyRewardPointsMutation(
                context,
                updatedSession,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                persistItems: true,
                out var persistenceReason))
        {
            await LogLegacyAccountSecurityFullAccountRuneFenceAsync(
                    context,
                    optionId,
                    grant,
                    persistenceReason)
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        foreach (var itemFrame in itemFrames)
        {
            await context.Connection.SendAsync(itemFrame, cancellationToken).ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc AccountSecurity FullAccount runes " +
                $"{FormatSession(updatedContext)} source={LegacyAccountSecurityFullAccountSource} " +
                $"sourceLines={LegacyAccountSecurityFullAccountSourceLines} optionId={optionId} " +
                $"color={grant.ColorName} family={grant.ItemFamily} maxLevel={grant.MaxLevel} " +
                $"requiredSpaces={grant.RequiredSpaces} availableSpaces={availableSpaces} " +
                $"catalogMatches={candidates.Length} granted={grantedItems.Count} " +
                $"capacitySkipped={candidates.Length - grantedItems.Count} " +
                $"itemIds={string.Join(',', grantedItems.Select(item => item.ItemId))} " +
                "bound=false colors=3-8 packetOrder=GameItemAdd*.")
            .ConfigureAwait(false);
    }

    private static bool IsLegacyFullAccountMaxRune(
        ItemType item,
        LegacyFullAccountRuneGrant grant)
    {
        if (item.Id <= 0)
        {
            return false;
        }

        var itemId = checked((uint)item.Id);
        return itemId / 10_000 == grant.ItemFamily &&
            itemId % 100 == grant.MaxLevel;
    }

    private static bool TryGetLegacyFullAccountRuneGrant(
        byte optionId,
        out LegacyFullAccountRuneGrant grant)
    {
        grant = optionId switch
        {
            12 => new LegacyFullAccountRuneGrant(
                "Red",
                ItemFamily: 401,
                MaxLevel: 1,
                RequiredSpaces: 12,
                FullBagMessage: "Please make 12 more space in your inventory."),
            13 => new LegacyFullAccountRuneGrant(
                "Blue",
                ItemFamily: 402,
                MaxLevel: 27,
                RequiredSpaces: 22,
                FullBagMessage: "Please make 2 more space in your inventory."),
            14 => new LegacyFullAccountRuneGrant(
                "Yellow",
                ItemFamily: 403,
                MaxLevel: 9,
                RequiredSpaces: 36,
                FullBagMessage: "Please make 36 more space in your inventory."),
            _ => default!,
        };
        return grant is not null;
    }

    private static Task LogLegacyAccountSecurityFullAccountRuneFenceAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        LegacyFullAccountRuneGrant grant,
        string reason)
    {
        return context.Log.WriteLineAsync(
            "PARITY-GAP LegacyNpc AccountSecurity FullAccount runes " +
            $"{FormatSession(context)} source={LegacyAccountSecurityFullAccountSource} " +
            $"sourceLines={LegacyAccountSecurityFullAccountSourceLines} optionId={optionId} " +
            $"color={grant.ColorName} family={grant.ItemFamily} maxLevel={grant.MaxLevel} " +
            $"reason={reason}; no item, session state, or packet mutation was committed.");
    }

    private sealed record LegacyFullAccountRuneGrant(
        string ColorName,
        uint ItemFamily,
        uint MaxLevel,
        int RequiredSpaces,
        string FullBagMessage);
}
