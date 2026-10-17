using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask ApplyLegacyAccountSecurityFullAccountRandomRelicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        const byte optionId = 15;
        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc AccountSecurity FullAccount max random relic " +
                    $"{FormatSession(context)} source={LegacyAccountSecurityFullAccountSource} " +
                    $"sourceLines={LegacyAccountSecurityFullAccountSourceLines} optionId={optionId} " +
                    "reason=inventory-full; source AddRandomRelic Update is silent when no slot is available.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await LogLegacyAccountSecurityFullAccountRandomRelicFenceAsync(
                    context,
                    "session-store-missing")
                .ConfigureAwait(false);
            return;
        }

        if (context.WorldData is not { } worldData)
        {
            await LogLegacyAccountSecurityFullAccountRandomRelicFenceAsync(
                    context,
                    "world-data-missing")
                .ConfigureAwait(false);
            return;
        }

        var relicCatalog = LegacyBossSpecialDeathDropCatalog.FromWorldData(worldData);
        if (!LegacyBossSpecialDeathDropPolicy.TryCreateMaxRandomRelicItem(
                relicCatalog,
                CreateLegacyBossSpecialDeathDropRolls(session.Character),
                out var generated,
                out var generationReason))
        {
            await LogLegacyAccountSecurityFullAccountRandomRelicFenceAsync(
                    context,
                    "random-relic-data-missing:" + generationReason)
                .ConfigureAwait(false);
            return;
        }

        PlayerItemSnapshotItem rewardItem;
        try
        {
            rewardItem = CreateLegacyGeneratedBossSpecialDropItem(
                generated,
                GetNextSessionItemUid(session.PlayerItems));
        }
        catch (OverflowException)
        {
            await LogLegacyAccountSecurityFullAccountRandomRelicFenceAsync(
                    context,
                    "item-uid-exhausted")
                .ConfigureAwait(false);
            return;
        }

        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems
                .Concat([rewardItem])
                .OrderBy(item => item.Uid)
                .ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };
        var itemFrame = LegacyGameItemPacket
            .FromSnapshotItem(
                rewardItem,
                LegacyGameItemPacket.CreateRuneSpellOverride(rewardItem, worldData.ItemsById),
                LegacyGameItemPacket.AddItemMode)
            .ToFrameBytes();

        cancellationToken.ThrowIfCancellationRequested();
        if (!TryPersistLegacyRewardPointsMutation(
                context,
                updatedSession,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                persistItems: true,
                out var persistenceReason))
        {
            await LogLegacyAccountSecurityFullAccountRandomRelicFenceAsync(
                    context,
                    persistenceReason)
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(itemFrame, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc AccountSecurity FullAccount max random relic " +
                $"{FormatSession(updatedContext)} source={LegacyAccountSecurityFullAccountSource} " +
                $"sourceLines={LegacyAccountSecurityFullAccountSourceLines} optionId={optionId} " +
                $"itemId={rewardItem.ItemId} itemUid={rewardItem.Uid} bound=false " +
                $"durability={rewardItem.Durability}/{rewardItem.MaximumDurability} " +
                $"attributes={rewardItem.Attribute1},{rewardItem.Attribute2},{rewardItem.Attribute3}," +
                $"{rewardItem.Attribute4},{rewardItem.Attribute5} packetOrder=GameItemAdd " +
                $"generatedSource=\"{generated.Source}\".")
            .ConfigureAwait(false);
    }

    private static Task LogLegacyAccountSecurityFullAccountRandomRelicFenceAsync(
        WorldPacketHandlerContext context,
        string reason)
    {
        return context.Log.WriteLineAsync(
            "PARITY-GAP LegacyNpc AccountSecurity FullAccount max random relic " +
            $"{FormatSession(context)} source={LegacyAccountSecurityFullAccountSource} " +
            $"sourceLines={LegacyAccountSecurityFullAccountSourceLines} optionId=15 " +
            $"reason={reason}; no item, session state, or packet mutation was committed.");
    }
}
