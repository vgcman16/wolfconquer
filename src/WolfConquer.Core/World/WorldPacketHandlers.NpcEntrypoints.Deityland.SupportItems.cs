using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyRelicSpiritItemNpcId = 3_307_142;
    private const uint LegacyRelicCrystalItemNpcId = 3_306_885;
    private const ushort LegacyRelicEquipmentPosition = 13;
    private const uint LegacyRelicCrystalSaintTaiyiNpcId = 22_569;

    private static readonly LegacyNpcDialogOption[] LegacyRelicSpiritPage1Options =
    [
        new("10~Meteor~Scrolls.", 1),
        new("1~Dragon~Ball.", 2),
        new("10~Super~Protection~Pills.", 3),
        new("15~Special~Training~Pills.", 4),
        new("15~Favored~Training~Pills.", 5),
        new("3~Senior~Training~Pills.", 6),
        new("3~Free~Training~Pills.", 7),
        new("Next.", 8),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRelicSpiritPage2Options =
    [
        new("1~Mystery~Fruit.", 9),
        new("1~Sash~(S).", 10),
        new("Previous.", 0),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRelicCrystalOptions =
    [
        new("Repair~my~Relic.", 1),
        new("Take~me~to~see~Saint~Taiyi.", 2),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRelicCrystalRepairOptions =
    [
        new("Repair now.", 3),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRelicCrystalSingleCloseOptions =
    [
        new("Okay.", LegacyNpcClosedOption),
    ];

    private static async ValueTask HandleLegacyDeitylandSupportItemUseAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        PlayerItemSnapshotItem item,
        CancellationToken cancellationToken)
    {
        var updatedSession = session with { ActiveNpcId = item.ItemId };
        context.Sessions?.Set(context.Connection, updatedSession);
        await TryHandleLegacyDeitylandSupportItemNpcAsync(
                context with { Session = updatedSession },
                updatedSession,
                item.ItemId,
                optionId: 0,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsLegacyDeitylandSupportItemNpc(uint npcId)
    {
        return npcId is LegacyRelicSpiritItemNpcId or LegacyRelicCrystalItemNpcId;
    }

    private static async ValueTask<bool> TryHandleLegacyDeitylandSupportItemNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return npcId switch
        {
            LegacyRelicSpiritItemNpcId => await TryHandleLegacyRelicSpiritItemNpcAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyRelicCrystalItemNpcId => await TryHandleLegacyRelicCrystalItemNpcAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask<bool> TryHandleLegacyRelicSpiritItemNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.RelicSpirit",
                    LegacyRelicSpiritDialogTexts,
                    LegacyRelicSpiritPage1Options,
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 8)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.RelicSpirit",
                    LegacyRelicSpiritDialogTexts,
                    LegacyRelicSpiritPage2Options,
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!TryGetLegacyRelicSpiritRequirement(optionId, out var requirementItemId, out var requirementCount))
        {
            return false;
        }

        await TryShapeLegacyRelicSpiritAsync(
                context,
                session,
                requirementItemId,
                requirementCount,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyRelicCrystalItemNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.RelicCrystal",
                        [LegacyRelicCrystalDialogText],
                        LegacyRelicCrystalOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 1:
                await SendLegacyRelicCrystalRepairPreviewAsync(context, session, cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 2:
                await SendLegacyRelicCrystalSaintTaiyiRouteAsync(context, session)
                    .ConfigureAwait(false);
                return true;
            case 3:
                await TryRepairLegacyEquippedRelicAsync(context, session, cancellationToken)
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static bool TryGetLegacyRelicSpiritRequirement(
        byte optionId,
        out uint itemId,
        out ushort count)
    {
        itemId = 720_027;
        count = 10;
        switch (optionId)
        {
            case 1:
                return true;
            case 2:
                itemId = 1_088_000;
                count = 1;
                return true;
            case 3:
                itemId = 3_002_030;
                return true;
            case 4:
                itemId = 3_003_125;
                count = 15;
                return true;
            case 5:
                itemId = 3_003_124;
                count = 15;
                return true;
            case 6:
                itemId = 3_003_126;
                count = 3;
                return true;
            case 7:
                itemId = 3_002_926;
                count = 3;
                return true;
            case 9:
                itemId = 3_001_044;
                count = 1;
                return true;
            case 10:
                itemId = 1_100_003;
                count = 1;
                return true;
            default:
                itemId = 0;
                count = 0;
                return false;
        }
    }

    private static async ValueTask TryShapeLegacyRelicSpiritAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint requirementItemId,
        ushort requirementCount,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RelicSpirit persistence " +
                    $"{FormatSession(context)} source=NpcHandler.RelicSpirit reason=session-store-missing; " +
                    "no item or character mutation was committed.")
                .ConfigureAwait(false);
            return;
        }

        var playerItemsRoot = context.PlayerItemsRoot;
        if (string.IsNullOrWhiteSpace(playerItemsRoot))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RelicSpirit persistence " +
                    $"{FormatSession(context)} source=NpcHandler.RelicSpirit reason=player-items-root-missing; " +
                    "no item or character mutation was committed.")
                .ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(session.UsersRoot))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RelicSpirit persistence " +
                    $"{FormatSession(context)} source=NpcHandler.RelicSpirit reason=users-root-missing; " +
                    "no item or character mutation was committed.")
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyDeitylandSupportRemovalCount(session.PlayerItems, LegacyRelicSpiritItemNpcId, 1) ||
            !HasLegacyDeitylandSupportRemovalCount(session.PlayerItems, requirementItemId, requirementCount))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc RelicSpirit shape " +
                    $"{FormatSession(context)} source=NpcHandler.RelicSpirit " +
                    $"requirementItemId={requirementItemId} requirementCount={requirementCount}; materials missing.")
                .ConfigureAwait(false);
            return;
        }

        if (context.WorldData is not { } worldData)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RelicSpirit random relic data " +
                    $"{FormatSession(context)} source=NpcHandler.RelicSpirit reason=world-data-missing; " +
                    "no materials were consumed.")
                .ConfigureAwait(false);
            return;
        }

        var catalog = LegacyBossSpecialDeathDropCatalog.FromWorldData(worldData);
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
                        "relic-spirit-random-relic",
                        missingReason,
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("npcId", LegacyRelicSpiritItemNpcId),
                            LegacyParityGapDiagnostics.Fact("source", "NpcHandler.RelicSpirit"),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("npcId", LegacyRelicSpiritItemNpcId),
                        ]))
                .ConfigureAwait(false);
            return;
        }

        var consumeFrames = new List<byte[]>();
        var updatedItems = ConsumeLegacyInventoryStack(
            session.PlayerItems,
            LegacyRelicSpiritItemNpcId,
            1,
            consumeFrames);
        updatedItems = ConsumeLegacyInventoryStack(
            updatedItems,
            requirementItemId,
            requirementCount,
            consumeFrames);
        uint rewardItemUid;
        try
        {
            rewardItemUid = GetNextSessionItemUid(updatedItems);
        }
        catch (OverflowException)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RelicSpirit random relic UID " +
                    $"{FormatSession(context)} source=NpcHandler.RelicSpirit reason=item-uid-exhausted; " +
                    "no materials were consumed.")
                .ConfigureAwait(false);
            return;
        }

        var rewardItem = CreateLegacyGeneratedBossSpecialDropItem(generated, rewardItemUid);
        updatedItems = updatedItems with
        {
            InventoryItems = updatedItems.InventoryItems.Concat([rewardItem]).ToArray(),
        };
        var updatedCharacter = WithCharacterAttribute(session.Character, "RelicSpirit", bool.TrueString);
        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };

        PlayerItemSnapshotLoader itemLoader;
        try
        {
            itemLoader = new PlayerItemSnapshotLoader(playerItemsRoot);
            itemLoader.SaveByCharacterUid(updatedCharacter.Uid, updatedItems);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RelicSpirit item persistence " +
                    $"{FormatSession(context)} source=NpcHandler.RelicSpirit reason={ex.GetType().Name}; " +
                    "no session mutation was committed.")
                .ConfigureAwait(false);
            return;
        }

        string characterPath;
        string characterReason;
        bool characterPersisted;
        try
        {
            characterPersisted = TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                updatedCharacter.Uid,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["RelicSpirit"] = bool.TrueString,
                },
                out characterPath,
                out characterReason);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            characterPersisted = false;
            characterPath = string.Empty;
            characterReason = ex.GetType().Name;
        }

        if (!characterPersisted)
        {
            var itemRollbackReason = "none";
            try
            {
                itemLoader.SaveByCharacterUid(session.Character.Uid, session.PlayerItems);
            }
            catch (Exception ex) when (
                ex is IOException or
                UnauthorizedAccessException or
                ArgumentException or
                NotSupportedException)
            {
                itemRollbackReason = ex.GetType().Name;
            }

            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RelicSpirit character persistence " +
                    $"{FormatSession(context)} source=NpcHandler.RelicSpirit " +
                    $"reason={characterReason} itemRollback={itemRollbackReason}; " +
                    "no session mutation was committed.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyNpc RelicSpirit shape " +
                $"{FormatSession(context)} source=NpcHandler.RelicSpirit " +
                $"character={Path.GetFileName(characterPath)} items={updatedCharacter.Uid}.bin keys=RelicSpirit.")
            .ConfigureAwait(false);

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        foreach (var frame in consumeFrames)
        {
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyGameItemPacket
                    .FromSnapshotItem(
                        rewardItem,
                        LegacyGameItemPacket.CreateRuneSpellOverride(rewardItem, context.WorldData?.ItemsById),
                        LegacyGameItemPacket.AddItemMode)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc RelicSpirit shape " +
                $"{FormatSession(updatedContext)} source=NpcHandler.RelicSpirit " +
                $"requirementItemId={requirementItemId} requirementCount={requirementCount} " +
                $"rewardItemId={rewardItem.ItemId} generatedSource=\"{generated.Source}\".")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyRelicCrystalRepairPreviewAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var relic = GetLegacyEquippedRelic(session.PlayerItems);
        if (relic is null)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.RelicCrystal",
                    ["Please equip a Relic, first."],
                    LegacyRelicCrystalSingleCloseOptions,
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (relic.Durability == relic.MaximumDurability)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.RelicCrystal",
                    ["Your relics is already at Max Durability!"],
                    LegacyRelicCrystalSingleCloseOptions,
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var requiredCrystals = GetLegacyRelicCrystalRepairCost(relic);
        var crystalCount = CountLegacyInventoryStack(session.PlayerItems, LegacyRelicCrystalItemNpcId);
        await SendLegacyNpcDialogAsync(
                context,
                "NpcHandler.RelicCrystal",
                CreateLegacyRelicCrystalRepairTexts(relic, requiredCrystals, crystalCount),
                LegacyRelicCrystalRepairOptions,
                avatarId: null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyRelicCrystalSaintTaiyiRouteAsync(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        if (session.Character.MapId == 10_250)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyNpc RelicCrystal SaintTaiyi route " +
                    $"{FormatSession(context)} source=NpcHandler.RelicCrystal " +
                    $"map=10250 x=1014 y=1282 npcId={LegacyRelicCrystalSaintTaiyiNpcId}; " +
                    "QuestGUI.SendAutoPatcher has no local packet model.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer ignored LegacyNpc RelicCrystal SaintTaiyi route " +
                $"{FormatSession(context)} source=NpcHandler.RelicCrystal; source only sends autopatcher in map 10250.")
            .ConfigureAwait(false);
    }

    private static async ValueTask TryRepairLegacyEquippedRelicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RelicCrystal repair persistence " +
                    $"{FormatSession(context)} source=NpcHandler.RelicCrystal reason=session-store-missing; " +
                    "no item mutation was committed.")
                .ConfigureAwait(false);
            return;
        }

        var playerItemsRoot = context.PlayerItemsRoot;
        if (string.IsNullOrWhiteSpace(playerItemsRoot))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RelicCrystal repair persistence " +
                    $"{FormatSession(context)} source=NpcHandler.RelicCrystal reason=player-items-root-missing; " +
                    "no item mutation was committed.")
                .ConfigureAwait(false);
            return;
        }

        var relic = GetLegacyEquippedRelic(session.PlayerItems);
        if (relic is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc RelicCrystal repair " +
                    $"{FormatSession(context)} source=NpcHandler.RelicCrystal; equipped relic missing.")
                .ConfigureAwait(false);
            return;
        }

        ushort requiredCrystals;
        try
        {
            requiredCrystals = GetLegacyRelicCrystalRepairCost(relic);
        }
        catch (OverflowException)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RelicCrystal repair data " +
                    $"{FormatSession(context)} source=NpcHandler.RelicCrystal reason=relic-durability-invalid " +
                    $"durability={relic.Durability} maximumDurability={relic.MaximumDurability}; " +
                    "no item mutation was committed.")
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyDeitylandSupportRemovalCount(
                session.PlayerItems,
                LegacyRelicCrystalItemNpcId,
                requiredCrystals))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc RelicCrystal repair " +
                    $"{FormatSession(context)} source=NpcHandler.RelicCrystal requiredCrystals={requiredCrystals}; " +
                    "crystals missing.")
                .ConfigureAwait(false);
            return;
        }

        var consumeFrames = new List<byte[]>();
        var updatedItems = ConsumeLegacyInventoryStack(
            session.PlayerItems,
            LegacyRelicCrystalItemNpcId,
            requiredCrystals,
            consumeFrames);
        var repaired = relic.WithDurability(relic.MaximumDurability);
        updatedItems = ReplaceEquipmentItem(updatedItems, relic, repaired);
        var updatedSession = session with { PlayerItems = updatedItems };
        try
        {
            new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(
                updatedSession.Character.Uid,
                updatedItems);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RelicCrystal repair item persistence " +
                    $"{FormatSession(context)} source=NpcHandler.RelicCrystal reason={ex.GetType().Name}; " +
                    "no session mutation was committed.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyNpc RelicCrystal repair " +
                $"{FormatSession(context)} source=NpcHandler.RelicCrystal items={updatedSession.Character.Uid}.bin.")
            .ConfigureAwait(false);

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        foreach (var frame in consumeFrames)
        {
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(
                        repaired,
                        mode: LegacyGameItemPacket.UpdateItemMode)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyEquipmentRuntimeRefreshAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc RelicCrystal repair " +
                $"{FormatSession(updatedContext)} source=NpcHandler.RelicCrystal " +
                $"relicUid={relic.Uid} crystals={requiredCrystals} durability={relic.Durability}->{repaired.Durability}.")
            .ConfigureAwait(false);
    }

    private static PlayerItemSnapshotItem? GetLegacyEquippedRelic(PlayerItemSnapshot items)
    {
        return items.EquipmentItems.FirstOrDefault(item => item.Position == LegacyRelicEquipmentPosition);
    }

    private static ushort GetLegacyRelicCrystalRepairCost(PlayerItemSnapshotItem relic)
    {
        return checked((ushort)((relic.MaximumDurability - relic.Durability) / 100));
    }

    private static IReadOnlyList<string> CreateLegacyRelicCrystalRepairTexts(
        PlayerItemSnapshotItem relic,
        ushort requiredCrystals,
        uint crystalCount)
    {
        var name = FormattableString.Invariant($"Item{relic.ItemId}");
        return
        [
            "\t\t\t\t\t\t\t\t\t\t\t\t\t*Repairing Relic*\n",
            "\t\t\t==============================================\n",
            $"\t\t\t\t\t\t\t\t\t\tEquipped Relic: {name}\n",
            $"\t\t\t\t\t\t\t\t\t\tRelic Crystals Required: {requiredCrystals.ToString(CultureInfo.InvariantCulture)}\n",
            $"\t\t\t\t\t\t\t\t\t\tYour Relic Crystals: {crystalCount.ToString(CultureInfo.InvariantCulture)}\n",
            "\t\t\t==============================================\n",
            "Tip: Each Relic Crystal increases the Durability by 1 point.",
        ];
    }

    private static bool HasLegacyDeitylandSupportRemovalCount(
        PlayerItemSnapshot items,
        uint itemId,
        uint requiredCount)
    {
        ulong unboundCount = 0;
        ulong boundCount = 0;
        foreach (var item in items.InventoryItems)
        {
            if (item.ItemId != itemId)
            {
                continue;
            }

            if (item.Bound == 0)
            {
                unboundCount += item.StackSize;
            }
            else
            {
                boundCount += item.StackSize;
            }

            if (unboundCount >= requiredCount || boundCount >= requiredCount)
            {
                return true;
            }
        }

        return false;
    }

    private static readonly string[] LegacyRelicSpiritDialogTexts =
    [
        "                      *Shaping of Relic Spirit*\n",
        "==========================================================\n",
        "\t\tRequired Material to Shape: any one of the treasures listed below\n",
        "\t\tExpect to Get: A random Relic\n",
        "==========================================================\n",
        "Please choose one from the following materials to shape your Relic Spirit into a Relic.\n",
    ];

    private const string LegacyRelicCrystalDialogText =
        "Relic Crystal can be used to repair your equipped Relic. You can also collect 100 Relic Crystals and ask Saint Taiyi (Deityland 1013,1278) help you craft a complete Relic.";
}
