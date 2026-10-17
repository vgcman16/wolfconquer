using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool CheckLegacyDeityLandInventoryRewardRate(
        CharacterSnapshot character,
        string attributeName,
        double ratePercent)
    {
        return TryReadAttributeBool(character.Attributes, out var forced, attributeName)
            ? forced
            : Random.Shared.NextDouble() * 100.0 < ratePercent;
    }

    private static async ValueTask<WorldSession> TryGrantLegacyDeityLandStackedInventoryRewardIfRateHitAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint itemId,
        double ratePercent,
        string sourceMessage,
        string sourceBranch,
        CancellationToken cancellationToken)
    {
        if (!CheckLegacyDeityLandInventoryRewardRate(
                session.Character,
                FormattableString.Invariant($"LegacyDeityLandReward{itemId}Rate"),
                ratePercent))
        {
            return session;
        }

        return await TryGrantLegacyDeityLandStackedInventoryRewardAsync(
                context,
                session,
                itemId,
                sourceMessage,
                sourceBranch,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession> TryGrantLegacyDeityLandChainedInventoryRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (CheckLegacyDeityLandInventoryRewardRate(
                session.Character,
                "LegacyDeityLandReward3303483Rate",
                ratePercent: 44))
        {
            return await TryGrantLegacyDeityLandStackedInventoryRewardAsync(
                    context,
                    session,
                    itemId: 3_303_483,
                    sourceMessage: "You have got 1 WonderHerb!",
                    sourceBranch: "MonsterRole.cs killer.Player.Map == 10250 Core.Rate(44) AddItemWitchStack(3303483)",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (CheckLegacyDeityLandInventoryRewardRate(
                session.Character,
                "LegacyDeityLandReward3307083Rate",
                ratePercent: 1))
        {
            return await TryGrantLegacyDeityLandStackedInventoryRewardAsync(
                    context,
                    session,
                    itemId: 3_307_083,
                    sourceMessage: "You have got 1 JadeofFaith!",
                    sourceBranch: "MonsterRole.cs killer.Player.Map == 10250 else Core.Rate(1) Inventory.Add(3307083)",
                    cancellationToken,
                    stackable: false)
                .ConfigureAwait(false);
        }

        if (CheckLegacyDeityLandInventoryRewardRate(
                session.Character,
                "LegacyDeityLandReward3304961Rate",
                ratePercent: 1))
        {
            return await TryGrantLegacyDeityLandStackedInventoryRewardAsync(
                    context,
                    session,
                    itemId: 3_304_961,
                    sourceMessage: "You have got 1 SafeZoneScroll!",
                    sourceBranch: "MonsterRole.cs killer.Player.Map == 10250 else Core.Rate(1) AddItemWitchStack(3304961)",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return session;
    }

    private static async ValueTask<WorldSession> TryGrantLegacyDeityLandRandomRelicInventoryRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 10250 Core.Rate(1) && Core.Rate(1) Inventory.AddRandomRelic";
        var catalog = context.WorldData is { } worldData
            ? LegacyBossSpecialDeathDropCatalog.FromWorldData(worldData)
            : LegacyBossSpecialDeathDropCatalog.Empty;
        if (!LegacyBossSpecialDeathDropPolicy.TryCreateDeityLandRandomRelicItem(
                catalog,
                CreateLegacyBossSpecialDeathDropRolls(session.Character),
                out var generated,
                out var missingReason))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "xuanbao_rand_attr.txt",
                        "deityland-inventory-random-relic",
                        reason: missingReason,
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", session.Character.MapId),
                            LegacyParityGapDiagnostics.Fact("branch", sourceBranch),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("branch", sourceBranch),
                        ]))
                .ConfigureAwait(false);
            return session;
        }

        var rewardItem = CreateLegacyGeneratedBossSpecialDropItem(
            generated,
            GetNextSessionItemUid(session.PlayerItems));
        var updatedSession = session with
        {
            PlayerItems = session.PlayerItems with
            {
                InventoryItems = session.PlayerItems.InventoryItems.Concat([rewardItem]).ToArray(),
            },
        };
        context.Sessions?.Set(context.Connection, updatedSession);

        await context.Connection.SendAsync(
                LegacyGameItemPacket
                    .FromSnapshotItem(
                        rewardItem,
                        LegacyGameItemPacket.CreateRuneSpellOverride(rewardItem, context.WorldData?.ItemsById),
                        LegacyGameItemPacket.AddItemMode)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk("You have got 1 Random Relic!").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyStringPacket
                    .PlayerEffect(CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)), "angelwing")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy DeityLand random relic inventory reward " +
                $"{FormatSession(context with { Session = updatedSession })} itemId={rewardItem.ItemId} " +
                $"attribute1={rewardItem.Attribute1} attribute2={rewardItem.Attribute2} attribute3={rewardItem.Attribute3} " +
                $"branch=\"{sourceBranch}\" generatedSource=\"{generated.Source}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryGrantLegacyDeityLandChestKeyInventoryRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (CheckLegacyDeityLandInventoryRewardRate(
                session.Character,
                RuntimeLegacyDeityLandRareChestKeyFirstRateAttribute,
                ratePercent: 1) &&
            CheckLegacyDeityLandInventoryRewardRate(
                session.Character,
                RuntimeLegacyDeityLandRareChestKeySecondRateAttribute,
                ratePercent: 5))
        {
            return await TryGrantLegacyDeityLandStackedInventoryRewardAsync(
                    context,
                    session,
                    itemId: 3_303_973,
                    sourceMessage: "You have got 1 RareChestKey!",
                    sourceBranch:
                    "MonsterRole.cs killer.Player.Map == 10250 Core.Rate(1) && Core.Rate(5) AddItemWitchStack(3303973)",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (CheckLegacyDeityLandInventoryRewardRate(
                session.Character,
                RuntimeLegacyDeityLandEpicChestKeyFirstRateAttribute,
                ratePercent: 1) &&
            CheckLegacyDeityLandInventoryRewardRate(
                session.Character,
                RuntimeLegacyDeityLandEpicChestKeySecondRateAttribute,
                ratePercent: 1))
        {
            return await TryGrantLegacyDeityLandStackedInventoryRewardAsync(
                    context,
                    session,
                    itemId: 3_303_974,
                    sourceMessage: "You have got 1 EpicChestKey!",
                    sourceBranch:
                    "MonsterRole.cs killer.Player.Map == 10250 else Core.Rate(1) && Core.Rate(1) AddItemWitchStack(3303974)",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return session;
    }

    private static async ValueTask<WorldSession> TryGrantLegacyDeityLandStackedInventoryRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint itemId,
        string sourceMessage,
        string sourceBranch,
        CancellationToken cancellationToken,
        bool stackable = true)
    {
        if (!TryReadItemType(context.WorldData, itemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "deityland-inventory-reward-item",
                        reason: "source grants a DeityLand inventory reward on monster death, but the item type was not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", session.Character.MapId),
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                            LegacyParityGapDiagnostics.Fact("branch", sourceBranch),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                            LegacyParityGapDiagnostics.Fact("branch", sourceBranch),
                        ]))
                .ConfigureAwait(false);
            return session;
        }

        var updatedItems = stackable
            ? AddOrStackLegacyInventoryItem(
                session.PlayerItems,
                itemType,
                itemId,
                stackCount: 1,
                out var rewardItem,
                out var itemMode)
            : AddLegacyInventoryItem(
                session.PlayerItems,
                itemType,
                stackCount: 1,
                out rewardItem,
                out itemMode);
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);

        await context.Connection.SendAsync(
                LegacyGameItemPacket
                    .FromSnapshotItem(
                        rewardItem,
                        LegacyGameItemPacket.CreateRuneSpellOverride(rewardItem, context.WorldData?.ItemsById),
                        itemMode)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk(sourceMessage).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyStringPacket
                    .PlayerEffect(CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)), "angelwing")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy DeityLand inventory reward " +
                $"{FormatSession(context with { Session = updatedSession })} itemId={itemId} stack={rewardItem.StackSize} " +
                $"mode={itemMode} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask SendLegacyDeityLandInventoryRewardNoticeAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMessage,
        string sourceBranch,
        CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk(sourceMessage).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyStringPacket
                    .PlayerEffect(CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)), "angelwing")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent legacy DeityLand inventory reward notice " +
                $"{FormatSession(context with { Session = session })} message=\"{sourceMessage}\" branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
    }
}
