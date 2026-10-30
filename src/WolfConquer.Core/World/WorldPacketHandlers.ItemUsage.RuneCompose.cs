using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyRuneComposeSource = "MsgItemUsuagePacket.ComposeRune";
    private const string LegacyRuneComposeLines = "2074-2128";

    private static async ValueTask HandleLegacyComposeRuneAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgItemUsagePacket packet,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        var args = ReadLegacyItemUsageArgs(frame);
        if (packet.DwParam2 is 0 or >= 50)
        {
            args = [];
        }

        if (packet.DwParam2 is > 0 and < 50 && args.Count > packet.DwParam2)
        {
            args = args.Take(checked((int)packet.DwParam2)).ToArray();
        }

        if (!TryGetRuneItem(session.PlayerItems, packet.Id, out var targetRune))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage compose rune " +
                    $"{FormatSession(context)} source={LegacyRuneComposeSource} " +
                    $"sourceLines={LegacyRuneComposeLines} itemUid={packet.Id}; target rune was not found.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyRuneComposeMaterials(
                session.PlayerItems,
                args,
                out var materials,
                out var missingMaterialUid))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage compose rune " +
                    $"{FormatSession(context)} source={LegacyRuneComposeSource} " +
                    $"sourceLines={LegacyRuneComposeLines} itemUid={packet.Id} missingMaterialUid={missingMaterialUid}; " +
                    "source requires every selected material to exist in inventory or rune storage before mutating.")
                .ConfigureAwait(false);
            return;
        }

        var updatedItems = session.PlayerItems;
        var updatedTarget = targetRune;
        var mutationFrames = new List<byte[]>();
        var consumedMaterials = 0;
        var acceptedMaterials = 0;
        foreach (var material in materials)
        {
            if (updatedTarget.ItemId % 100 >= GetLegacyMaxRuneLevel(updatedTarget.ItemId))
            {
                break;
            }

            if (!IsLegacyRuneComposeMaterialItemId(material.ItemId))
            {
                if (acceptedMaterials > 0)
                {
                    await CommitLegacyRuneComposeAcceptedPrefixAsync(
                            context, session, targetRune, updatedTarget, updatedItems, mutationFrames, cancellationToken)
                        .ConfigureAwait(false);
                }

                await context.Log.WriteLineAsync(
                        "WorldServer skipped MsgItemUsage compose rune " +
                        $"{FormatSession(context)} source={LegacyRuneComposeSource} " +
                        $"sourceLines={LegacyRuneComposeLines} itemUid={packet.Id} materialUid={material.Uid} " +
                        $"materialItemId={material.ItemId}; source material item id is outside 4010000-4100000.")
                    .ConfigureAwait(false);
                return;
            }

            if (!TryGetLegacyRuneLevelRequiredProgress(
                    context.WorldData,
                    GetLegacyRuneDecomposeFamily(updatedTarget.ItemId),
                    updatedTarget.ItemId % 100,
                    out var currentRequiredProgress) ||
                !TryGetLegacyRuneLevelRequiredProgress(
                    context.WorldData,
                    GetLegacyRuneDecomposeFamily(updatedTarget.ItemId),
                    updatedTarget.ItemId % 100 + 1,
                    out var nextRequiredProgress) ||
                nextRequiredProgress < currentRequiredProgress)
            {
                await LogParityGapAsync(
                        context,
                        LegacyParityGapDiagnostics.UnsupportedItemUse(
                            updatedTarget.ItemId,
                            action: (uint)packet.Action,
                            actionName: packet.Action.ToString(),
                            itemUid: updatedTarget.Uid,
                            source: LegacyRuneComposeSource,
                            reason: "target rune level experience metadata is missing"))
                    .ConfigureAwait(false);
                await context.Log.WriteLineAsync(
                        "PARITY-GAP MsgItemUsage compose rune missing target level experience " +
                        $"{FormatSession(context)} source={LegacyRuneComposeSource} " +
                        $"sourceLines={LegacyRuneComposeLines} itemUid={updatedTarget.Uid} " +
                        $"itemId={updatedTarget.ItemId}.")
                    .ConfigureAwait(false);
                return;
            }

            var requiredDelta = checked(nextRequiredProgress - currentRequiredProgress);
            var materialPoints = CalculateLegacyRuneMaterialPoints(material, context.WorldData);
            var runeExperience = checked(updatedTarget.RuneExperience + materialPoints);
            var itemId = updatedTarget.ItemId;
            while (runeExperience >= requiredDelta)
            {
                runeExperience = checked(runeExperience - requiredDelta);
                itemId++;
                if (itemId % 100 >= GetLegacyMaxRuneLevel(itemId))
                {
                    runeExperience = 0;
                    break;
                }

                if (!TryGetLegacyRuneLevelRequiredProgress(
                        context.WorldData,
                        GetLegacyRuneDecomposeFamily(itemId),
                        itemId % 100,
                        out currentRequiredProgress) ||
                    !TryGetLegacyRuneLevelRequiredProgress(
                        context.WorldData,
                        GetLegacyRuneDecomposeFamily(itemId),
                        itemId % 100 + 1,
                        out nextRequiredProgress) ||
                    nextRequiredProgress < currentRequiredProgress)
                {
                    await LogParityGapAsync(
                            context,
                            LegacyParityGapDiagnostics.UnsupportedItemUse(
                                itemId,
                                action: (uint)packet.Action,
                                actionName: packet.Action.ToString(),
                                itemUid: updatedTarget.Uid,
                                source: LegacyRuneComposeSource,
                                reason: "next target rune level experience metadata is missing"))
                        .ConfigureAwait(false);
                    await context.Log.WriteLineAsync(
                            "PARITY-GAP MsgItemUsage compose rune missing next level experience " +
                            $"{FormatSession(context)} source={LegacyRuneComposeSource} " +
                            $"sourceLines={LegacyRuneComposeLines} itemUid={updatedTarget.Uid} itemId={itemId}.")
                        .ConfigureAwait(false);
                    return;
                }

                requiredDelta = checked(nextRequiredProgress - currentRequiredProgress);
            }

            updatedTarget = updatedTarget
                .WithItemId(itemId)
                .WithRuneExperience(runeExperience);
            acceptedMaterials++;

            if (TryConsumeLegacyRuneComposeInventoryMaterial(
                    updatedItems,
                    material,
                    out updatedItems,
                    out var consumeFrame))
            {
                mutationFrames.Add(consumeFrame);
                consumedMaterials++;
                continue;
            }

            if (TryGetRuneItem(updatedItems, material.Uid, out var runeMaterial))
            {
                updatedItems = RemoveLegacyRuneItem(updatedItems, runeMaterial);
                mutationFrames.Add(
                    new MsgItemUsagePacket(
                        runeMaterial.Uid,
                        DwParam: 0,
                        MsgItemUsageAction.RemoveInventory,
                        Timestamp: 0,
                        DwParam2: 0,
                        DwParam3: 0,
                        DwParam4: 0).ToFrameBytes());
                consumedMaterials++;
            }
        }

        updatedItems = ReplaceRuneItem(updatedItems, targetRune, updatedTarget);
        var updatedSession = session with { PlayerItems = updatedItems };
        updatedSession = PublishLegacyItemUsageSession(context, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await TryPersistLegacyItemUsageMutationAsync(
                updatedContext,
                updatedSession,
                "compose rune",
                characterValues: null,
                persistItems: true)
            .ConfigureAwait(false);

        foreach (var mutationFrame in mutationFrames)
        {
            await context.Connection.SendAsync(mutationFrame, cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyGameItemPacket
                    .FromSnapshotItem(
                        updatedTarget,
                        LegacyGameItemPacket.CreateRuneSpellOverride(updatedTarget, context.WorldData?.ItemsById),
                        LegacyGameItemPacket.UpdateItemMode)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        UpdateLegacyRuneRanking(context, updatedSession);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage compose rune " +
                $"{FormatSession(updatedContext)} source={LegacyRuneComposeSource} " +
                $"sourceLines={LegacyRuneComposeLines} itemUid={targetRune.Uid} oldItemId={targetRune.ItemId} " +
                $"newItemId={updatedTarget.ItemId} oldRuneExp={targetRune.RuneExperience} " +
                $"newRuneExp={updatedTarget.RuneExperience} selectedMaterials={materials.Count} " +
                $"consumedMaterials={consumedMaterials}.")
            .ConfigureAwait(false);
    }

    private static bool TryReadLegacyRuneComposeMaterials(
        PlayerItemSnapshot items,
        IReadOnlyList<uint> materialUids,
        out IReadOnlyList<PlayerItemSnapshotItem> materials,
        out uint missingMaterialUid)
    {
        var selected = new List<PlayerItemSnapshotItem>();
        foreach (var materialUid in materialUids)
        {
            var material = items.InventoryItems.FirstOrDefault(item => item.Uid == materialUid) ??
                items.RuneItems.FirstOrDefault(item => item.Uid == materialUid);
            if (material is null)
            {
                materials = [];
                missingMaterialUid = materialUid;
                return false;
            }

            selected.Add(material);
        }

        materials = selected;
        missingMaterialUid = 0;
        return true;
    }

    private static bool TryConsumeLegacyRuneComposeInventoryMaterial(
        PlayerItemSnapshot items,
        PlayerItemSnapshotItem material,
        out PlayerItemSnapshot updatedItems,
        out byte[] frame)
    {
        updatedItems = items;
        frame = [];
        var inventoryItems = items.InventoryItems.ToList();
        var materialIndex = inventoryItems.FindIndex(item => item.Uid == material.Uid);
        if (materialIndex < 0)
        {
            return false;
        }

        frame = new MsgItemUsagePacket(
            material.Uid,
            DwParam: 0,
            MsgItemUsageAction.RemoveInventory,
            Timestamp: 0,
            DwParam2: 0,
            DwParam3: 0,
            DwParam4: 0).ToFrameBytes();
        inventoryItems.RemoveAt(materialIndex);
        updatedItems = items with { InventoryItems = inventoryItems.ToArray() };
        return true;
    }

    private static uint CalculateLegacyRuneMaterialPoints(
        PlayerItemSnapshotItem material,
        WorldDataCatalog? worldData)
    {
        if (!TryGetLegacyRuneLevelRequiredProgress(
            worldData,
            GetLegacyRuneDecomposeFamily(material.ItemId),
            material.ItemId % 100,
            out var requiredProgress))
        {
            return 0;
        }

        var stackSize = material.StackSize == 0 ? 1U : material.StackSize;
        return checked((requiredProgress * stackSize) + material.RuneExperience);
    }

    private static bool TryGetLegacyRuneLevelRequiredProgress(
        WorldDataCatalog? worldData,
        uint runeFamily,
        uint runeLevel,
        out uint requiredProgress)
    {
        requiredProgress = 0;
        if (worldData is null ||
            runeFamily > int.MaxValue ||
            runeLevel > int.MaxValue ||
            !worldData.RuneLevelExperienceByRuneTypeAndLevel.TryGetValue(
                (checked((int)runeFamily), checked((int)runeLevel)),
                out var levelExperience) ||
            levelExperience.Experience < 0 ||
            levelExperience.Experience > uint.MaxValue)
        {
            return false;
        }

        requiredProgress = checked((uint)levelExperience.Experience);
        return true;
    }

    private static byte GetLegacyMaxRuneLevel(uint itemId)
    {
        var runeFamily = itemId / 10_000;
        var maxLevel = runeFamily == 403 ? 9 : 27;
        if (runeFamily == 401 || runeFamily == 401)
        {
            maxLevel = 1;
        }

        return checked((byte)maxLevel);
    }

    private static bool IsLegacyRuneComposeMaterialItemId(uint itemId)
    {
        return itemId is >= 4_010_000 and <= 4_100_000;
    }
}
