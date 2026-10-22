using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TrySendLegacyBossSpecialDeathDropsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monsterFamily.Id < 0 ||
            monster.MapId < 0)
        {
            return false;
        }

        var rolls = CreateLegacyBossSpecialDeathDropRolls(session.Character);
        var catalog = context.WorldData is { } worldData
            ? LegacyBossSpecialDeathDropCatalog.FromWorldData(worldData)
            : LegacyBossSpecialDeathDropCatalog.Empty;
        if (!LegacyBossSpecialDeathDropPolicy.TryCreateFixedDropPlan(
            checked((uint)monsterFamily.Id),
            checked((uint)monster.MapId),
            monster.Boss,
            monsterFamily.Name,
            rolls,
            catalog,
            out var plan))
        {
            return false;
        }

        var sentCount = 0;
        var loggedMissingFloorItemRuntime = false;
        foreach (var item in plan.FixedItems)
        {
            for (var count = 0; count < item.Count; count++)
            {
                var floorItem = LegacyFloorItemPacket.FromMonsterDrop(
                    item.ItemId,
                    monster,
                    session.Character,
                    NextLegacyFloorItemUid());
                var entry = FloorItemRuntimeEntry.ForItem(monster.MapId, floorItem, ownerOnly: true, dynamicId: monster.DynamicId);
                if (context.FloorItems is { } floorItems)
                {
                    if (!floorItems.TryAddVisibleOnGround(
                            entry,
                            DateTimeOffset.UtcNow,
                            item.ScatterRange,
                            (mapId, x, y) => TryValidateStaticTerrainLocation(context, mapId, x, y, out _),
                            out var added))
                    {
                        continue;
                    }

                    entry = added;
                    floorItem = added.Packet;
                }
                else if (!loggedMissingFloorItemRuntime)
                {
                    await LogParityGapAsync(
                            context,
                            LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                                FloorItemRuntimeStateResource,
                                "boss-special-death-drop",
                                "boss special death drop was sent but floor item runtime state is not loaded for pickup tracking",
                                facts:
                                [
                                    LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                                    LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                                    LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                                ],
                                dedupeFacts:
                                [
                                    LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                                    LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                                ]))
                        .ConfigureAwait(false);
                    loggedMissingFloorItemRuntime = true;
                }

                await SendFloorItemVisibleAsync(context, session.Character, entry, cancellationToken)
                    .ConfigureAwait(false);
                sentCount++;
            }
        }

        var generatedItemsSent = 0;
        var nextGeneratedItemUid = GetNextSessionItemUid(session.PlayerItems);
        foreach (var generated in plan.GeneratedItems)
        {
            var pickupItem = CreateLegacyGeneratedBossSpecialDropItem(
                generated,
                checked(nextGeneratedItemUid + (uint)generatedItemsSent));
            var floorItem = LegacyFloorItemPacket.FromMonsterDrop(
                pickupItem.ItemId,
                monster,
                session.Character,
                NextLegacyFloorItemUid());
            var entry = FloorItemRuntimeEntry.ForItem(monster.MapId, floorItem, pickupItem, ownerOnly: true, dynamicId: monster.DynamicId);
            if (context.FloorItems is { } floorItems)
            {
                if (!floorItems.TryAddVisibleOnGround(
                        entry,
                        DateTimeOffset.UtcNow,
                        generated.ScatterRange,
                        (mapId, x, y) => TryValidateStaticTerrainLocation(context, mapId, x, y, out _),
                        out var added))
                {
                    continue;
                }

                entry = added;
                floorItem = added.Packet;
            }
            else if (!loggedMissingFloorItemRuntime)
            {
                await LogParityGapAsync(
                        context,
                        LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                            FloorItemRuntimeStateResource,
                            "boss-special-generated-death-drop",
                            "boss special generated death drop was sent but floor item runtime state is not loaded for pickup tracking",
                            facts:
                            [
                                LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                                LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                                LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                                LegacyParityGapDiagnostics.Fact("itemId", pickupItem.ItemId),
                            ],
                            dedupeFacts:
                            [
                                LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                                LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                                LegacyParityGapDiagnostics.Fact("itemId", pickupItem.ItemId),
                            ]))
                    .ConfigureAwait(false);
                loggedMissingFloorItemRuntime = true;
            }

            await SendFloorItemVisibleAsync(context, session.Character, entry, cancellationToken)
                .ConfigureAwait(false);
            generatedItemsSent++;
            sentCount++;
        }

        foreach (var pending in plan.PendingSourceBehaviors)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.MonsterDrop(
                        monster.MonsterId,
                        mapId: TryGetLegacyParityMapId(monster.MapId),
                        dropKind: "boss-special",
                        source: plan.SourceBranch,
                        reason: pending))
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer sent legacy boss special death drops " +
                $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} map={monster.MapId} " +
                $"fixedItems={plan.FixedItems.Count} generatedItems={plan.GeneratedItems.Count} sent={sentCount} branch=\"{plan.SourceBranch}\".")
            .ConfigureAwait(false);
        return true;
    }

    private static PlayerItemSnapshotItem CreateLegacyGeneratedBossSpecialDropItem(
        LegacyBossSpecialGeneratedDropItem generated,
        uint uid)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = generated.ItemId,
            Durability = generated.Durability,
            MaximumDurability = generated.MaximumDurability,
            Position = 0,
            StackSize = 1,
            Attribute1 = generated.Attribute1,
            Attribute2 = generated.Attribute2,
            Attribute3 = generated.Attribute3,
            Attribute4 = generated.Attribute4,
            Attribute5 = generated.Attribute5,
        };
    }

    private static LegacyBossSpecialDeathDropRolls CreateLegacyBossSpecialDeathDropRolls(
        CharacterSnapshot character)
    {
        var attributes = character.Attributes;
        var rare4212Drop = TryReadAttributeBool(
                attributes,
                out var forcedRare4212Drop,
                RuntimeLegacyBoss4212RareDropAttribute)
            ? forcedRare4212Drop
            : Random.Shared.Next(0, 100) < 10;
        var rare4171Drop = TryReadAttributeBool(
                attributes,
                out var forcedRare4171Drop,
                RuntimeLegacyBoss4171RareDropAttribute)
            ? forcedRare4171Drop
            : Random.Shared.Next(0, 100) < 10;
        var rare4220Drop = TryReadAttributeBool(
                attributes,
                out var forcedRare4220Drop,
                RuntimeLegacyBoss4220RareDropAttribute)
            ? forcedRare4220Drop
            : Random.Shared.Next(0, 100) < 20;
        var family20055Add711679 = TryReadAttributeBool(
                attributes,
                out var forcedFamily20055Add711679,
                RuntimeLegacyBoss20055Add711679Attribute)
            ? forcedFamily20055Add711679
            : Random.Shared.Next(0, 100) < 30;
        var family20055Add711188 = TryReadAttributeBool(
                attributes,
                out var forcedFamily20055Add711188,
                RuntimeLegacyBoss20055Add711188Attribute)
            ? forcedFamily20055Add711188
            : Random.Shared.Next(0, 100) < 30;
        var dynamicId = ReadCharacterDynamicId(character);
        var dynamicInstanceReward = character.Uid >= 0 &&
            dynamicId != 0 &&
            dynamicId != checked((ulong)character.Uid);

        return new LegacyBossSpecialDeathDropRolls(
            Family4212RareDrop: rare4212Drop,
            Family4171RareDrop: rare4171Drop,
            DynamicInstanceReward: dynamicInstanceReward,
            DragonIslandFamily4220RareDrop: rare4220Drop,
            Family20055Add711679: family20055Add711679,
            Family20055Add711188: family20055Add711188,
            DeityLandPrimaryDropIndex: TryReadAttributeInt(attributes, RuntimeLegacyBoss10250PrimaryDropIndexAttribute),
            DeityLandSecondaryDropIndex: TryReadAttributeInt(attributes, RuntimeLegacyBoss10250SecondaryDropIndexAttribute),
            DeityLandRelicItemIndex: TryReadAttributeInt(attributes, RuntimeLegacyBoss10250RelicItemIndexAttribute),
            DeityLandRelicDurabilityRoll: TryReadAttributeInt(attributes, RuntimeLegacyBoss10250RelicDurabilityRollAttribute),
            DeityLandRelicAttributeCount: TryReadAttributeInt(attributes, RuntimeLegacyBoss10250RelicAttributeCountAttribute),
            DeityLandRelicAttributeIndexes: ReadAttributeIntList(attributes, RuntimeLegacyBoss10250RelicAttributeIndexesAttribute),
            DeityLandRelicAttributeValues: ReadAttributeIntList(attributes, RuntimeLegacyBoss10250RelicAttributeValuesAttribute),
            QueenOfEvilDragonBallCount: TryReadAttributeInt(attributes, RuntimeLegacyBoss3970DragonBallCountAttribute),
            QueenOfEvilTalismanIndexes: ReadAttributeIntList(attributes, RuntimeLegacyBoss3970TalismanIndexesAttribute),
            SoulsPurificationRollModulo: TryReadAttributeModulo(attributes, RuntimeLegacyBossSoulsPurificationRollModuloAttribute),
            SoulsPurificationIndexes: ReadAttributeIntList(attributes, RuntimeLegacyBossSoulsPurificationIndexesAttribute),
            SoulsAccessoryRollModulo: TryReadAttributeModulo(attributes, RuntimeLegacyBossSoulsAccessoryRollModuloAttribute),
            SoulsAccessoryIndexes: ReadAttributeIntList(attributes, RuntimeLegacyBossSoulsAccessoryIndexesAttribute));
    }

    private static async ValueTask<WorldSession> TryApplyLegacyBossDeathRewardsAsync(
        WorldPacketHandlerContext context,
        WorldSession killerSession,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken,
        bool mapSideEffectsAlreadyApplied = false)
    {
        var updatedKillerSession = mapSideEffectsAlreadyApplied
            ? killerSession
            : await TryApplyLegacyMonsterDeathMapSideEffectsAsync(
                context,
                killerSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedKillerSession };

        updatedKillerSession = await TryApplyLegacyDeityLandInventoryRewardsAsync(
                updatedContext,
                updatedKillerSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedKillerSession };

        updatedKillerSession = await TryApplyLegacyDeityLandWarDevilRewardAsync(
                updatedContext,
                updatedKillerSession,
                monster,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedKillerSession };

        await TryApplyLegacyDeityLandSpriteRewardAsync(
                updatedContext,
                updatedKillerSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);

        updatedKillerSession = await TryApplyLegacyMonsterDeathRandomConquerPointRewardAsync(
                updatedContext,
                updatedKillerSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedKillerSession };

        updatedKillerSession = await TryApplyLegacyQueenOfEvilClassRewardAsync(
                updatedContext,
                updatedKillerSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedKillerSession };

        updatedKillerSession = await TryApplyLegacyBossDeathConquerPointRewardAsync(
                updatedContext,
                updatedKillerSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedKillerSession };

        await TryApplyLegacyBossHarmRankMailRewardsAsync(
                updatedContext,
                monster,
                cancellationToken)
            .ConfigureAwait(false);

        updatedKillerSession = await TryApplyLegacyBossDeathStudyPointInnerPowerRewardAsync(
                updatedContext,
                updatedKillerSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedKillerSession };

        updatedKillerSession = TryApplyLegacyMonsterDeathHeroRewardGoal(
            updatedContext,
            updatedKillerSession,
            monster,
            monsterFamily);
        updatedContext = updatedContext with { Session = updatedKillerSession };

        return await TryApplyLegacyBossDeathStudyPointRewardAsync(
                updatedContext,
                updatedKillerSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMonsterDeathRandomConquerPointRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession killerSession,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId < 0)
        {
            return killerSession;
        }

        var vipLevel = ReadAttributeUInt64(killerSession.Character.Attributes, "VIPLevel", "VipLevel");
        var reward = LegacyBossDeathRewardPolicy.GetMonsterDeathRandomConquerPointReward(
            monsterFamily.Id < 0 ? 0U : checked((uint)monsterFamily.Id),
            checked((uint)monster.MapId),
            monster.Boss,
            ReadAttributeBool(killerSession.Character.Attributes, RuntimeAutoHuntAttribute),
            vipLevel > int.MaxValue ? int.MaxValue : (int)vipLevel);
        var ratePercent = ReadLegacyMonsterDeathRandomConquerPointRatePercent(killerSession.Character, reward);
        var rateRoll = ReadLegacyMonsterDeathRandomConquerPointRateRoll(killerSession.Character);
        if (!CheckLegacyMonsterDeathRandomConquerPointRate(ratePercent, rateRoll))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy monster-death random Conquer Point reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} mapId={monster.MapId} " +
                    $"branch={reward.Branch} range={reward.MinimumInclusive}..{reward.MaximumInclusive} " +
                    $"ratePercent={ratePercent} rateRoll={rateRoll}; " +
                    "source=MonsterRole.cs lines 4406-4433; WolfConquer tuned economy keeps generic kill CP chance-gated.")
                .ConfigureAwait(false);
            return killerSession;
        }

        var conquerPoints = ReadLegacyMonsterDeathRandomConquerPoints(killerSession.Character, reward);
        var updatedCharacter = killerSession.Character with
        {
            ConquerPoints = checked(killerSession.Character.ConquerPoints + conquerPoints),
        };
        var updatedSession = killerSession with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendConquerPointsUpdateAsync(
                updatedContext,
                updatedCharacter,
                cancellationToken)
            .ConfigureAwait(false);

        if (reward.FormatSystemMessage(conquerPoints) is { } message)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk(message).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy monster-death random Conquer Point reward " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} mapId={monster.MapId} " +
                $"branch={reward.Branch} points={conquerPoints} range={reward.MinimumInclusive}..{reward.MaximumInclusive} " +
                $"updatedConquerPoints={updatedCharacter.ConquerPoints} walletUpdate=LegacyMsgUpdate.ConquerPoints " +
                $"ratePercent={ratePercent} rateRoll={rateRoll}; " +
                "source=MonsterRole.cs lines 4406-4433.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static int ReadLegacyMonsterDeathRandomConquerPointRatePercent(
        CharacterSnapshot character,
        LegacyMonsterDeathRandomConquerPointReward reward)
    {
        var configuredRate = TryReadAttributeInt(
            character.Attributes,
            RuntimeLegacyMonsterDeathConquerPointRatePercentAttribute);

        if (configuredRate is null &&
            reward.Branch != LegacyMonsterDeathRandomConquerPointRewardBranch.NonBossVipLadder)
        {
            return 0;
        }

        return Math.Clamp(
            configuredRate ?? LegacyBossDeathRewardPolicy.TunedMonsterDeathRandomConquerPointRatePercent,
            0,
            100);
    }

    private static bool CheckLegacyMonsterDeathRandomConquerPointRate(int ratePercent, int rateRoll)
    {
        if (ratePercent <= 0)
        {
            return false;
        }

        return ratePercent >= 100 ||
            rateRoll < ratePercent;
    }

    private static int ReadLegacyMonsterDeathRandomConquerPointRateRoll(CharacterSnapshot character)
    {
        if (TryReadInt64Attribute(
                character.Attributes,
                RuntimeLegacyMonsterDeathConquerPointRateRollAttribute,
                out var forcedRoll) &&
            forcedRoll is >= 0 and < 100)
        {
            return checked((int)forcedRoll);
        }

        return Random.Shared.Next(0, 100);
    }

    private static uint ReadLegacyMonsterDeathRandomConquerPoints(
        CharacterSnapshot character,
        LegacyMonsterDeathRandomConquerPointReward reward)
    {
        if (TryReadInt64Attribute(
                character.Attributes,
                RuntimeLegacyMonsterDeathConquerPointRollAttribute,
                out var forcedConquerPoints) &&
            forcedConquerPoints >= reward.MinimumInclusive &&
            forcedConquerPoints < reward.MaximumExclusive)
        {
            return checked((uint)forcedConquerPoints);
        }

        return checked((uint)Random.Shared.Next(reward.MinimumInclusive, reward.MaximumExclusive));
    }

    private static WorldSession TryApplyLegacyMonsterDeathHeroRewardGoal(
        WorldPacketHandlerContext context,
        WorldSession killerSession,
        LegacyMonsterInstance monster,
        Monster monsterFamily)
    {
        if (monsterFamily.Id < 0 ||
            !LegacyBossDeathRewardPolicy.TryGetStudyPointInnerPowerHeroRewardGoal(
                checked((uint)monsterFamily.Id),
                monster.Boss,
                out var goalId))
        {
            return killerSession;
        }

        var beforeHeroRewards = killerSession.Character.Attributes.GetValueOrDefault(RuntimeHeroRewardsAttribute);
        var updatedSession = AddLegacyHeroRewardGoal(killerSession, goalId);
        var afterHeroRewards = updatedSession.Character.Attributes.GetValueOrDefault(RuntimeHeroRewardsAttribute);
        var added = !StringComparer.Ordinal.Equals(beforeHeroRewards, afterHeroRewards);
        context.Sessions?.Set(context.Connection, updatedSession);

        context.Log.WriteLine(
            "WorldServer applied legacy monster-death HeroReward goal " +
            $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} " +
            $"familyId={monsterFamily.Id} goalId={goalId} added={added}; " +
            "source=MonsterRole Family.ID 4171/20060/8311 AddGoal(709).");
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyBossDeathConquerPointRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession killerSession,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monsterFamily.Id < 0 ||
            monster.MapId < 0 ||
            !LegacyBossDeathRewardPolicy.TryGetConquerPointReward(
                checked((uint)monsterFamily.Id),
                checked((uint)monster.MapId),
                monster.Boss,
                out var reward))
        {
            return killerSession;
        }

        var updatedCharacter = killerSession.Character with
        {
            ConquerPoints = checked(killerSession.Character.ConquerPoints + reward.TotalConquerPoints),
        };
        var updatedSession = killerSession with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendConquerPointsUpdateAsync(
                updatedContext,
                updatedCharacter,
                cancellationToken)
            .ConfigureAwait(false);
        var lastHitterMessage = reward.FormatLastHitterMessage(monster.Name);
        for (var invocation = 0; invocation < reward.InvocationCount; invocation++)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk(lastHitterMessage).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy boss last-hitter Conquer Point reward " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} mapId={monster.MapId} " +
                $"points={reward.TotalConquerPoints} invocations={reward.InvocationCount}; " +
                "source=MonsterRole.GetBossReward direct ConquerPoints award.")
            .ConfigureAwait(false);
        return updatedSession;
    }

}
