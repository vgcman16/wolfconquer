using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyDragonFurnaceFusionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        var dragonFurnace = ReadAttributeUInt64(session.Character.Attributes, "DragonFurnace", "DragonFurnaceItemId");
        if (dragonFurnace == 0 ||
            dragonFurnace > uint.MaxValue ||
            CountLegacyInventoryItem(session.PlayerItems, checked((uint)dragonFurnace)) < 2)
        {
            return session;
        }

        var dragonFurnaceItemId = checked((uint)dragonFurnace);
        var upgradeSucceeded = CheckLegacyDragonFurnaceUpgradeSuccess(session.Character, dragonFurnaceItemId);
        if (upgradeSucceeded &&
            TrySelectLegacyDragonFurnaceUpgrade(context.WorldData, dragonFurnaceItemId, out var upgradedItemId) &&
            TryReadItemType(context.WorldData, upgradedItemId, out var upgradedItemType))
        {
            var consumeFrames = new List<byte[]>(capacity: 2);
            var updatedItems = ConsumeLegacyInventoryItemCount(
                session.PlayerItems,
                dragonFurnaceItemId,
                count: 2,
                consumeFrames);
            var rewardItem = CreatePurchasedItems(updatedItems, upgradedItemType, count: 1)[0];
            updatedItems = updatedItems with
            {
                InventoryItems = updatedItems.InventoryItems.Concat([rewardItem]).OrderBy(item => item.Uid).ToArray(),
            };

            var updatedSession = session with { PlayerItems = updatedItems };
            context.Sessions?.Set(context.Connection, updatedSession);
            foreach (var frame in consumeFrames)
            {
                await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            }

            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(rewardItem).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox(
                            "+------- Fusing Succeeded -------+\u0090\n" +
                            "              Congratulations! \n" +
                            $"        You received a {upgradedItemType.Name}!\n" +
                            "+----------------------------------+")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);

            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy DragonFurnace monster death fusion success " +
                    $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"map={monster.MapId} dragonFurnace={dragonFurnaceItemId} rewardItemId={upgradedItemId} " +
                    "branch=\"MonsterRole.cs Map == 10428 && Family.ID == 2984\".")
                .ConfigureAwait(false);
            return updatedSession;
        }

        if (!upgradeSucceeded &&
            TrySelectLegacyDragonFurnaceFailedExperience(context.WorldData, dragonFurnaceItemId, out var failedExperience) &&
            TryReadItemType(context.WorldData, dragonFurnaceItemId, out var dragonFurnaceItemType))
        {
            var consumeFrames = new List<byte[]>(capacity: 2);
            var updatedItems = ConsumeLegacyInventoryItemCount(
                session.PlayerItems,
                dragonFurnaceItemId,
                count: 2,
                consumeFrames);
            var updatedSession = session with { PlayerItems = updatedItems };
            context.Sessions?.Set(context.Connection, updatedSession);
            foreach (var frame in consumeFrames)
            {
                await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            }

            updatedSession = await ApplyLegacyExperienceGainAsync(
                    context,
                    context.Connection,
                    updatedSession,
                    checked((ulong)failedExperience),
                    cancellationToken)
                .ConfigureAwait(false);

            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox(
                            "+------- Fusing Failed -------+\u0090\n" +
                            $"What a pity, you lost 2 {dragonFurnaceItemType.Name}.\n" +
                            "+----------------------------------+")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);

            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy DragonFurnace monster death fusion failure " +
                    $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"map={monster.MapId} dragonFurnace={dragonFurnaceItemId} experience={failedExperience} " +
                    "branch=\"MonsterRole.cs Map == 10428 && Family.ID == 2984\".")
                .ConfigureAwait(false);
            return updatedSession;
        }

        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                    "spirit_rate.txt",
                    "dragon-furnace-monster-death",
                    reason:
                        "source resolves Player.DragonFurnace through BaseFunc.AnimaUpgradeRate and SpiritTable.SpiritRates, " +
                        "then consumes furnace items, grants a new anima item or experience, and opens a box dialog; " +
                        "modern runtime models the source teleport, success-item branch, and data-reachable failed-experience branch, " +
                        "but unresolved source data prevents choosing either modeled fusion path",
                    facts:
                    [
                        LegacyParityGapDiagnostics.Fact("map", TryGetLegacyParityMapId(monster.MapId)),
                        LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                        LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                        LegacyParityGapDiagnostics.Fact("dragonFurnace", dragonFurnace),
                    ],
                    dedupeFacts:
                    [
                        LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                        LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                        LegacyParityGapDiagnostics.Fact("dragonFurnace", dragonFurnace),
                    ]))
            .ConfigureAwait(false);
        return session;
    }

    private static bool CheckLegacyDragonFurnaceUpgradeSuccess(CharacterSnapshot character, uint dragonFurnaceItemId)
    {
        if (TryReadAttributeBool(
                character.Attributes,
                out var forced,
                "LegacyDragonFurnaceUpgradeSuccess"))
        {
            return forced;
        }

        return Random.Shared.Next(0, 100) < GetLegacyDragonFurnaceAnimaUpgradeRate(dragonFurnaceItemId);
    }

    private static int GetLegacyDragonFurnaceAnimaUpgradeRate(uint dragonFurnaceItemId)
    {
        return (int)(dragonFurnaceItemId % 100) switch
        {
            10 or 11 => 35,
            12 => 25,
            >= 13 and <= 18 => 100,
            19 => 0,
            _ => 55,
        };
    }

    private static bool TrySelectLegacyDragonFurnaceUpgrade(
        WorldDataCatalog? worldData,
        uint dragonFurnaceItemId,
        out uint upgradedItemId)
    {
        upgradedItemId = 0;
        if (worldData is null ||
            dragonFurnaceItemId > int.MaxValue ||
            !worldData.SpiritRatesByItemTypeId.TryGetValue((int)dragonFurnaceItemId, out var rates))
        {
            return false;
        }

        foreach (var rate in rates
                     .Where(rate => GetLegacySpiritRateType(rate) != 0)
                     .OrderBy(GetLegacySpiritRateNewAnimaId))
        {
            if (GetLegacySpiritRateType(rate) == 2)
            {
                continue;
            }

            var newAnimaId = GetLegacySpiritRateNewAnimaId(rate);
            if (newAnimaId <= 0)
            {
                continue;
            }

            upgradedItemId = checked((uint)newAnimaId);
            return true;
        }

        return false;
    }

    private static bool TrySelectLegacyDragonFurnaceFailedExperience(
        WorldDataCatalog? worldData,
        uint dragonFurnaceItemId,
        out int failedExperience)
    {
        failedExperience = 0;
        if (worldData is null ||
            dragonFurnaceItemId > int.MaxValue ||
            !worldData.SpiritRatesByItemTypeId.TryGetValue((int)dragonFurnaceItemId, out var rates))
        {
            return false;
        }

        var failedRate = rates
            .Where(rate => GetLegacySpiritRateType(rate) == 0)
            .Where(rate => !GetLegacySpiritRateIsItem(rate))
            .OrderBy(rate => rate.Id)
            .FirstOrDefault();
        if (failedRate is null)
        {
            return false;
        }

        var experience = GetLegacySpiritRateNewAnimaId(failedRate);
        if (experience <= 0)
        {
            return false;
        }

        failedExperience = experience;
        return true;
    }

    private static int GetLegacySpiritRateType(SpiritRate rate)
    {
        return rate.Parameters.Count > 0 ? rate.Parameters[0] : -1;
    }

    private static bool GetLegacySpiritRateIsItem(SpiritRate rate)
    {
        return rate.Parameters.Count > 1 && rate.Parameters[1] != 0;
    }

    private static int GetLegacySpiritRateNewAnimaId(SpiritRate rate)
    {
        return rate.Parameters.Count > 2 ? rate.Parameters[2] : 0;
    }
}
