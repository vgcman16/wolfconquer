using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask TrySendLegacyMonsterDeathDropAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        var roll = ReadLegacyMonsterDeathDropRoll(session.Character, out var forcedRoll);
        var moneyAmount = ReadLegacyMonsterDeathDropMoneyAmount(session.Character);
        if (context.WorldData is not { } worldData)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.MonsterDrop(
                        monster.MonsterId,
                        mapId: TryGetLegacyParityMapId(monster.MapId),
                        resource: WorldDataResource,
                        source: "MonsterRole.Drop",
                        reason: "world data is not loaded, so monster death drops cannot be validated"))
                .ConfigureAwait(false);
            return;
        }

        await TrySendLegacyMapSpecialMonsterDeathDropsAsync(
                context,
                session,
                monster,
                monsterFamily,
                roll,
                forcedRoll,
                cancellationToken)
            .ConfigureAwait(false);

        var isBossFamilyMonster = IsLegacyGenerateBossFamilyMonster(monster);
        if (isBossFamilyMonster &&
            await TrySendLegacyBossFamilyGenerateBossFamilyDropsAsync(
                    context,
                    session,
                    monster,
                    monsterFamily,
                    worldData,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        var hasSourceSpecialDropBranch = HasLegacySourceSpecialDropBranch(monster, monsterFamily);
        if (isBossFamilyMonster || hasSourceSpecialDropBranch)
        {
            if (await TrySendLegacyBossSpecialDeathDropsAsync(
                    context,
                    session,
                    monster,
                    monsterFamily,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                return;
            }

            if (isBossFamilyMonster)
            {
                await LogParityGapAsync(
                        context,
                        LegacyParityGapDiagnostics.MonsterDrop(
                            monster.MonsterId,
                            mapId: TryGetLegacyParityMapId(monster.MapId),
                            dropKind: "boss-family",
                            source: "MonsterRole.GenerateBossFamily",
                            reason: "boss-family GenerateBossFamily requires deterministic generated item attributes or the remaining source random sort mapping"))
                    .ConfigureAwait(false);
                return;
            }
        }

        if (IsLegacyMonsterDropBlockedMap(monster.MapId))
        {
            return;
        }

        var dropCount = GetLegacyMonsterDeathDropCount(roll, forcedRoll);
        var dropKind = GetLegacyMonsterDeathDropKind(roll);
        var dropX = monster.X;
        var dropY = monster.Y;
        var sentCount = 0;
        var loggedMissingFloorItemRuntime = false;
        var lastSelection = default(LegacyMonsterDeathDropSelection);
        var lastNoDropReason = "";
        for (var index = 0; index < dropCount; index++)
        {
            if (!TrySelectLegacyMonsterDeathDropItemId(
                    context,
                    session.Character,
                    worldData,
                    monsterFamily,
                    roll,
                    moneyAmount,
                    out var selection,
                    out var noDropReason))
            {
                lastNoDropReason = noDropReason;
                continue;
            }

            var floorItem = LegacyFloorItemPacket.FromMonsterDrop(
                selection.ItemId,
                dropX,
                dropY,
                session.Character,
                NextLegacyFloorItemUid());
            var entry = selection.Kind == FloorItemRuntimePickupKind.Money
                ? FloorItemRuntimeEntry.ForMoney(monster.MapId, floorItem, selection.Amount, ownerOnly: true)
                : FloorItemRuntimeEntry.ForItem(monster.MapId, floorItem, ownerOnly: true);
            if (context.FloorItems is { } floorItems)
            {
                if (!floorItems.TryAddVisibleOnGround(
                        entry,
                        DateTimeOffset.UtcNow,
                        0,
                        (mapId, x, y) => TryValidateStaticTerrainLocation(context, mapId, x, y, out _),
                        out var added))
                {
                    break;
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
                            "monster-death-drop",
                            "monster death drop was sent but floor item runtime state is not loaded for pickup tracking",
                            facts:
                            [
                                LegacyParityGapDiagnostics.Fact("dropKind", selection.Kind),
                                LegacyParityGapDiagnostics.Fact("itemId", selection.ItemId),
                                LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                                LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            ],
                            dedupeFacts:
                            [
                                LegacyParityGapDiagnostics.Fact("dropKind", selection.Kind),
                                LegacyParityGapDiagnostics.Fact("itemId", selection.ItemId),
                                LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            ]))
                    .ConfigureAwait(false);
                loggedMissingFloorItemRuntime = true;
            }

            dropX = floorItem.X;
            dropY = floorItem.Y;
            await SendFloorItemVisibleAsync(context, session.Character, floorItem, cancellationToken)
                .ConfigureAwait(false);
            sentCount++;
            lastSelection = selection;
        }

        if (sentCount == 0)
        {
            if (dropKind is not null)
            {
                if (dropKind == "item")
                {
                    await context.Log.WriteLineAsync(
                            "WorldServer skipped legacy generated monster death drop " +
                            $"{FormatSession(context)} monsterId={monster.MonsterId} map={monster.MapId} roll={roll} " +
                            $"reason={FormatLegacyMonsterDropNoDropReason(lastNoDropReason)}.")
                        .ConfigureAwait(false);
                    return;
                }

                await LogParityGapAsync(
                        context,
                        LegacyParityGapDiagnostics.MonsterDrop(
                            monster.MonsterId,
                            itemId: dropKind == "money" ? GetLegacyMoneyItemId(moneyAmount) : null,
                            mapId: TryGetLegacyParityMapId(monster.MapId),
                            dropKind: dropKind,
                            resource: WorldDataResource,
                            source: "MonsterRole.Drop",
                            reason: FormatLegacyMonsterDropNoDropReason(lastNoDropReason),
                            extraFacts: CreateLegacyFixedMonsterDropNoDropFacts(worldData, monsterFamily, dropKind)))
                    .ConfigureAwait(false);
            }

            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer sent legacy monster death drop " +
                $"{FormatSession(context)} monsterUid={monster.Uid} itemId={lastSelection.ItemId} kind={lastSelection.Kind} amount={lastSelection.Amount} count={sentCount}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TrySendLegacyBossFamilyGenerateBossFamilyDropsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        WorldDataCatalog worldData,
        CancellationToken cancellationToken)
    {
        if (!ShouldTryLegacyBossFamilyGeneratedDropHook(monster, monsterFamily, session.Character) ||
            !TryCreateLegacyBossFamilyGeneratedDropRolls(session.Character, out var rolls))
        {
            return false;
        }

        var selection = LegacyMonsterDeathDropSelector.Select(
            monster,
            monsterFamily,
            rolls,
            LegacyMonsterDeathDropContext.FromWorldData(worldData));
        if (selection.Kind is not LegacyMonsterDeathDropKind.Item and not LegacyMonsterDeathDropKind.SpecialItem ||
            selection.DropCount == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy boss-family GenerateBossFamily drop " +
                    $"{FormatSession(context)} monsterId={monster.MonsterId} map={monster.MapId} " +
                    $"reason={FormatLegacyMonsterDropNoDropReason(selection.Reason)}.")
                .ConfigureAwait(false);
            return true;
        }

        var sentCount = 0;
        var loggedMissingFloorItemRuntime = false;
        for (var index = 0; index < selection.DropCount; index++)
        {
            var floorItem = LegacyFloorItemPacket.FromMonsterDrop(
                selection.ItemId,
                monster,
                session.Character,
                NextLegacyFloorItemUid());
            var entry = FloorItemRuntimeEntry.ForItem(monster.MapId, floorItem, ownerOnly: true);
            if (context.FloorItems is { } floorItems)
            {
                if (!floorItems.TryAddVisibleOnGround(
                        entry,
                        DateTimeOffset.UtcNow,
                        0,
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
                            "boss-family-generate-boss-family-drop",
                            "boss-family GenerateBossFamily drop was sent but floor item runtime state is not loaded for pickup tracking",
                            facts:
                            [
                                LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                                LegacyParityGapDiagnostics.Fact("itemId", selection.ItemId),
                                LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                                LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            ],
                            dedupeFacts:
                            [
                                LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                                LegacyParityGapDiagnostics.Fact("itemId", selection.ItemId),
                                LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            ]))
                    .ConfigureAwait(false);
                loggedMissingFloorItemRuntime = true;
            }

            await SendFloorItemVisibleAsync(context, session.Character, floorItem, cancellationToken)
                .ConfigureAwait(false);
            sentCount++;
        }

        await context.Log.WriteLineAsync(
                "WorldServer sent legacy boss-family GenerateBossFamily drops " +
                $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} map={monster.MapId} " +
                $"itemId={selection.ItemId} sent={sentCount} branch=\"MonsterRole.cs GenerateBossFamily()\".")
            .ConfigureAwait(false);
        return true;
    }

    private static bool ShouldTryLegacyBossFamilyGeneratedDropHook(
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CharacterSnapshot character)
    {
        if (!HasLegacyBossFamilyGeneratedDropHook(character) ||
            IsLegacyMonsterDropBlockedMap(monster.MapId))
        {
            return false;
        }

        if (!monster.Boss)
        {
            return !HasLegacySourceSpecialDropBranch(monster, monsterFamily);
        }

        if (monsterFamily.Id < 0 || monster.MapId < 0)
        {
            return false;
        }

        return LegacyBossSpecialDeathDropPolicy.TryCreateFixedDropPlan(
                checked((uint)monsterFamily.Id),
                checked((uint)monster.MapId),
                monster.Boss,
                default,
                LegacyBossSpecialDeathDropCatalog.Empty,
                out var plan) &&
            plan.SourceBranch == "MonsterRole.cs GenerateBossFamily()" &&
            plan.TotalItemCount == 0 &&
            plan.PendingSourceBehaviors.Count == 0;
    }

    private static bool HasLegacyBossFamilyGeneratedDropHook(CharacterSnapshot character)
    {
        return TryReadUInt32Attribute(
                character.Attributes,
                out var forcedSort,
                RuntimeLegacyMonsterDropItemSortAttribute) &&
            forcedSort <= ushort.MaxValue &&
            TryReadUInt32Attribute(
                character.Attributes,
                out _,
                RuntimeLegacyMonsterDropItemLevelAttribute);
    }

    private static bool TryCreateLegacyBossFamilyGeneratedDropRolls(
        CharacterSnapshot character,
        out LegacyMonsterDeathDropRolls rolls)
    {
        if (!TryReadUInt32Attribute(
                character.Attributes,
                out var forcedSort,
                RuntimeLegacyMonsterDropItemSortAttribute) ||
            forcedSort > ushort.MaxValue ||
            !TryReadUInt32Attribute(
                character.Attributes,
                out var forcedLevel,
                RuntimeLegacyMonsterDropItemLevelAttribute))
        {
            rolls = default!;
            return false;
        }

        byte? forcedQuality = null;
        if (TryReadUInt32Attribute(
                character.Attributes,
                out var forcedQualityValue,
                RuntimeLegacyMonsterDropItemQualityAttribute) &&
            forcedQualityValue <= 9)
        {
            forcedQuality = (byte)forcedQualityValue;
        }

        rolls = new LegacyMonsterDeathDropRolls(
            DropRoll: 0,
            GeneratedSort: (ushort)forcedSort,
            GeneratedBaseLevel: forcedLevel,
            GeneratedQuality: forcedQuality);
        return true;
    }

    private static bool IsLegacyGenerateBossFamilyMonster(LegacyMonsterInstance monster)
    {
        return monster.Boss || monster.MaxLife is > 100_000 and < 7_000_000;
    }

    private static async ValueTask<int> TrySendLegacyMapSpecialMonsterDeathDropsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        int roll,
        bool forcedRoll,
        CancellationToken cancellationToken)
    {
        var sentCount = 0;
        if (IsLegacyMap1011Or1020Family3130Or3134DropBranch(monster, monsterFamily) &&
            CheckLegacyMapSpecialDropRate(session.Character))
        {
            sentCount += await SendLegacyMapSpecialMonsterDeathDropBundleAsync(
                    context,
                    session,
                    monster,
                    monsterFamily,
                    SelectLegacyMap1011Or1020Family3130DropItemId(session.Character),
                    count: 1,
                    scatterRange: 4,
                    sourceBranch: "MonsterRole.cs Map != 3846 && (Map == 1011 || Map == 1020) && (Family.ID == 3130 || Family.ID == 3134)",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (IsLegacyMap11127SpecialDropMonster(monster) &&
            !IsLegacyMap11127SpecialDropSuppressedByEarlierSourceBranch(monster, monsterFamily))
        {
            var dropCount = GetLegacyMonsterDeathDropCount(roll, forcedRoll);
            const string sourceBranch = "MonsterRole.cs Map == 11127";
            if (roll > 100 && roll < 200)
            {
                uint itemId = 722_725;
                if (roll % 4 == 0)
                {
                    itemId = 722_723;
                }
                else if (roll % 4 == 2)
                {
                    itemId = 722_724;
                }

                sentCount += await SendLegacyMapSpecialMonsterDeathDropBundleAsync(
                        context,
                        session,
                        monster,
                        monsterFamily,
                        itemId,
                        dropCount,
                        scatterRange: 0,
                        sourceBranch,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (roll < 50)
            {
                sentCount += await SendLegacyMapSpecialMonsterDeathDropBundleAsync(
                        context,
                        session,
                        monster,
                        monsterFamily,
                        722_732U,
                        dropCount,
                        scatterRange: 0,
                        sourceBranch,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (roll > 300 && roll < 350)
            {
                sentCount += await SendLegacyMapSpecialMonsterDeathDropBundleAsync(
                        context,
                        session,
                        monster,
                        monsterFamily,
                        722_736U,
                        dropCount,
                        scatterRange: 0,
                        sourceBranch,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (monster.MapId == 10_250 &&
            string.Equals(monster.Name, "MeteorDove", StringComparison.OrdinalIgnoreCase))
        {
            sentCount += await SendLegacyMapSpecialMonsterDeathDropBundleAsync(
                    context,
                    session,
                    monster,
                    monsterFamily,
                    1_088_001U,
                    count: 5,
                    scatterRange: 3,
                    sourceBranch: "MonsterRole.cs killer.Player.Map == 10250 && Name == \"MeteorDove\"",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (monster.MapId == 10_250 &&
            CheckLegacyDeityLandRandomDropRate(session.Character))
        {
            var itemId = SelectLegacyDeityLandRandomDropItemId(session.Character);
            sentCount += await SendLegacyMapSpecialMonsterDeathDropBundleAsync(
                    context,
                    session,
                    monster,
                    monsterFamily,
                    itemId,
                    count: 1,
                    scatterRange: 3,
                    sourceBranch: "MonsterRole.cs killer.Player.Map == 10250 Core.Rate(3) random item ladder",
                    cancellationToken)
                .ConfigureAwait(false);

            var itemName = ResolveLegacyItemName(context.WorldData, itemId);
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket
                        .SystemTalk($"{monster.Name} that you killed dropped 1 {itemName}(s)!")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Connection.SendAsync(
                    LegacyStringPacket
                        .PlayerEffect(CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)), "angelwing")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return sentCount;
    }

    private static bool IsLegacyMap11127SpecialDropMonster(LegacyMonsterInstance monster)
    {
        return monster.MapId == 11_127 && !monster.Boss;
    }

    private static async ValueTask<int> SendLegacyMapSpecialMonsterDeathDropBundleAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        uint itemId,
        int count,
        byte scatterRange,
        string sourceBranch,
        CancellationToken cancellationToken)
    {
        if (count <= 0)
        {
            return 0;
        }

        var sentCount = 0;
        var loggedMissingFloorItemRuntime = false;
        for (var index = 0; index < count; index++)
        {
            var floorItem = LegacyFloorItemPacket.FromMonsterDrop(
                itemId,
                monster,
                session.Character,
                NextLegacyFloorItemUid());
            var entry = FloorItemRuntimeEntry.ForItem(monster.MapId, floorItem, ownerOnly: true);
            if (context.FloorItems is { } floorItems)
            {
                if (!floorItems.TryAddVisibleOnGround(
                        entry,
                        DateTimeOffset.UtcNow,
                        scatterRange,
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
                            "map-special-monster-death-drop",
                            "map special monster death drop was sent but floor item runtime state is not loaded for pickup tracking",
                            facts:
                            [
                                LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                                LegacyParityGapDiagnostics.Fact("itemId", itemId),
                                LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                                LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            ],
                            dedupeFacts:
                            [
                                LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                                LegacyParityGapDiagnostics.Fact("itemId", itemId),
                                LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            ]))
                    .ConfigureAwait(false);
                loggedMissingFloorItemRuntime = true;
            }

            await SendFloorItemVisibleAsync(context, session.Character, floorItem, cancellationToken)
                .ConfigureAwait(false);
            sentCount++;
        }

        if (sentCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer sent legacy map special monster death drops " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} map={monster.MapId} " +
                    $"itemId={itemId} sent={sentCount} scatterRange={scatterRange} branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        return sentCount;
    }

    private static async ValueTask<int> SendLegacyMapSpecialMonsterDeathMoneyDropAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        uint itemId,
        uint amount,
        byte scatterRange,
        string sourceBranch,
        CancellationToken cancellationToken)
    {
        if (amount == 0)
        {
            return 0;
        }

        var floorItem = LegacyFloorItemPacket.FromMonsterDrop(
            itemId,
            monster,
            session.Character,
            NextLegacyFloorItemUid());
        var entry = FloorItemRuntimeEntry.ForMoney(monster.MapId, floorItem, amount, ownerOnly: true);
        if (context.FloorItems is { } floorItems)
        {
            if (!floorItems.TryAddVisibleOnGround(
                    entry,
                    DateTimeOffset.UtcNow,
                    scatterRange,
                    (mapId, x, y) => TryValidateStaticTerrainLocation(context, mapId, x, y, out _),
                    out var added))
            {
                return 0;
            }

            entry = added;
            floorItem = added.Packet;
        }
        else
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        FloorItemRuntimeStateResource,
                        "map-special-monster-death-money-drop",
                        "map special monster money drop was sent but floor item runtime state is not loaded for pickup tracking",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                            LegacyParityGapDiagnostics.Fact("amount", amount),
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                        ]))
                .ConfigureAwait(false);
        }

        await SendFloorItemVisibleAsync(context, session.Character, floorItem, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent legacy map special monster death money drop " +
                $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} map={monster.MapId} " +
                $"itemId={itemId} amount={amount} scatterRange={scatterRange} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return 1;
    }
}
