using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyItemPackServicesBlackFridayAccesoryNpcId = 4_294_967_266;
    private const uint LegacyItemPackServicesBlackFridayMountPackNpcId = 4_294_967_267;
    private const uint LegacyItemPackServicesBlackFridayGarmentPackNpcId = 4_294_967_268;
    private const uint LegacyItemPackServicesMountPacket3NpcId = 4_294_967_270;
    private const uint LegacyItemPackServicesAccesoryPacket2NpcId = 4_294_967_271;
    private const uint LegacyItemPackServicesGarmentPacket2NpcId = 4_294_967_272;
    private const uint LegacyItemPackServicesAccesoryPacketNpcId = 4_294_967_273;
    private const uint LegacyItemPackServicesMountPacket2NpcId = 4_294_967_274;
    private const uint LegacyItemPackServicesMountPacketNpcId = 4_294_967_275;
    private const uint LegacyItemPackServicesGarmentPacketNpcId = 4_294_967_276;
    private const uint LegacyItemPackServicesBlackFridayAccesoryPackItemId = 3_100_053;
    private const uint LegacyItemPackServicesBlackFridayMountPackItemId = 3_100_054;
    private const uint LegacyItemPackServicesBlackFridayGarmentPackItemId = 3_100_055;
    private const string LegacyItemPackServicesRechargeProgressAttribute = "RechargeProgress";
    private const string LegacyItemPackServicesCatalogInventorySpaceText =
        "Please make 1 more space in your inventory.";

    private static readonly uint[] LegacyItemPackServicesAccessoryCatalogItemIds =
    [
        360_071, 360_072, 360_073, 360_074, 360_150, 360_151, 380_028, 200_501, 360_102, 360_085,
        360_084, 360_091, 360_092, 350_081, 350_082, 360_152, 360_153, 360_154, 360_101, 360_144,
        350_054, 360_149, 360_150, 360_159, 360_186, 380_046, 360_176, 360_185, 350_103, 350_104,
        350_105, 360_201, 360_202, 360_203,
    ];

    private static async ValueTask<bool> LegacyItemPackServicesTryHandleCatalogSelectorAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        LegacyItemPackServicesCatalogService service,
        CancellationToken cancellationToken)
    {
        if (service.RequiredPackItemId.HasValue &&
            !LegacyItemPackServicesCatalogHasBindingClassStack(
                session.PlayerItems,
                service.RequiredPackItemId.Value,
                count: 1,
                requiredBound: 0))
        {
            await LegacyItemPackServicesLogNoOpAsync(
                    context,
                    service.NpcId,
                    optionId,
                    service.Source,
                    service.SourceLines)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 250)
        {
            await LegacyItemPackServicesApplyCatalogGrantAsync(
                    context,
                    session,
                    optionId,
                    service,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 251)
        {
            var selectedItemId = session.LegacyCatalogSelectedItemId;
            if (selectedItemId == 0)
            {
                await LegacyItemPackServicesLogCatalogFenceAsync(
                        context,
                        service,
                        optionId,
                        "selected-item-missing; source preview branch is silent")
                    .ConfigureAwait(false);
                return true;
            }

            if (!TryReadLegacyItemType(context.WorldData, selectedItemId, out var selectedItemType) ||
                selectedItemType.Id != checked((int)selectedItemId) ||
                selectedItemType.LegacyDurability > ushort.MaxValue)
            {
                await LegacyItemPackServicesLogCatalogFenceAsync(
                        context,
                        service,
                        optionId,
                        $"preview-itemtype-missing-or-invalid:itemId={selectedItemId.ToString(CultureInfo.InvariantCulture)}; no packet sent")
                    .ConfigureAwait(false);
                return true;
            }

            var durability = checked((ushort)selectedItemType.LegacyDurability);
            await context.Connection.SendAsync(
                    LegacyGameItemPacket.CreateChatItem(selectedItemId, durability).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await LegacyItemPackServicesSendCatalogConfirmationAsync(
                    context,
                    service,
                    selectedItemType.Name,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyNpc ItemPackServices catalog preview " +
                    $"{FormatSession(context)} source={service.Source} sourceLines={service.SourceLines} " +
                    $"npcId={service.NpcId.ToString(CultureInfo.InvariantCulture)} " +
                    $"optionId={optionId.ToString(CultureInfo.InvariantCulture)} " +
                    $"selectedItemId={selectedItemId.ToString(CultureInfo.InvariantCulture)} " +
                    "sourceOrder=FakeChatItem,ConfirmationDialog.")
                .ConfigureAwait(false);
            return true;
        }

        if (!LegacyItemPackServicesTryBuildCatalog(
                context.WorldData,
                service,
                out var catalog,
                out var catalogReason))
        {
            await LegacyItemPackServicesLogCatalogFenceAsync(
                    context,
                    service,
                    optionId,
                    catalogReason)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId >= 100)
        {
            var selectedIndex = optionId - 100;
            if (selectedIndex >= catalog.Count)
            {
                return false;
            }

            var selected = catalog[selectedIndex];
            if (!TryReadLegacyItemType(context.WorldData, selected.ItemId, out var selectedItemType))
            {
                await LegacyItemPackServicesLogCatalogFenceAsync(
                        context,
                        service,
                        optionId,
                        $"selected-itemtype-missing:itemId={selected.ItemId.ToString(CultureInfo.InvariantCulture)}")
                    .ConfigureAwait(false);
                return true;
            }

            if (context.Sessions is not { } sessions)
            {
                await LegacyItemPackServicesLogCatalogFenceAsync(
                        context,
                        service,
                        optionId,
                        "session-store-missing; selected item was not retained")
                    .ConfigureAwait(false);
                return true;
            }

            var updatedSession = session with { LegacyCatalogSelectedItemId = selected.ItemId };
            sessions.Set(context.Connection, updatedSession);
            await LegacyItemPackServicesSendCatalogConfirmationAsync(
                    context with { Session = updatedSession },
                    service,
                    selectedItemType.Name,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer retained LegacyNpc ItemPackServices catalog selection " +
                    $"{FormatSession(context with { Session = updatedSession })} source={service.Source} " +
                    $"sourceLines={service.SourceLines} npcId={service.NpcId} optionId={optionId} " +
                    $"catalogIndex={selectedIndex} selectedItemId={selected.ItemId}.")
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 0 && !HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            if (service.NpcId == LegacyItemPackServicesBlackFridayAccesoryNpcId)
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        service.Source,
                        [LegacyItemPackServicesCatalogInventorySpaceText],
                        [new LegacyNpcDialogOption("Let me check.", LegacyNpcClosedOption)],
                        LegacyItemPackServicesDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            await LegacyItemPackServicesSendMessageBoxAsync(
                    context,
                    service.Source,
                    LegacyItemPackServicesCatalogInventorySpaceText,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var startIndex = optionId * 10;
        if (startIndex >= catalog.Count)
        {
            return false;
        }

        var options = new List<LegacyNpcDialogOption>();
        var endIndex = Math.Min(catalog.Count, startIndex + 10);
        for (var index = startIndex; index < endIndex; index++)
        {
            var entry = catalog[index];
            if (!TryReadLegacyItemType(context.WorldData, entry.ItemId, out var itemType))
            {
                continue;
            }

            options.Add(new LegacyNpcDialogOption(
                LegacyItemPackServicesCatalogOptionText(service, itemType.Name, entry.Stars),
                checked((byte)(100 + index))));
        }

        if (optionId == 0 || endIndex % 10 == 0)
        {
            options.Add(new LegacyNpcDialogOption("Next Page", checked((byte)(optionId + 1))));
        }

        await SendLegacyNpcDialogAsync(
                context,
                service.Source,
                [LegacyItemPackServicesCatalogPromptText(service, optionId)],
                options,
                LegacyItemPackServicesDefaultAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask LegacyItemPackServicesApplyCatalogGrantAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        LegacyItemPackServicesCatalogService service,
        CancellationToken cancellationToken)
    {
        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await LegacyItemPackServicesSendMessageBoxAsync(
                    context,
                    service.Source,
                    LegacyItemPackServicesCatalogInventorySpaceText,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var selectedItemId = session.LegacyCatalogSelectedItemId;
        if (selectedItemId == 0)
        {
            await LegacyItemPackServicesLogCatalogFenceAsync(
                    context,
                    service,
                    optionId,
                    "selected-item-missing; source grant branch is silent")
                .ConfigureAwait(false);
            return;
        }

        var rechargeProgress = ReadAttributeUInt64(
            session.Character.Attributes,
            LegacyItemPackServicesRechargeProgressAttribute);
        if (service.ClaimFlag != 0 && (rechargeProgress & service.ClaimFlag) == service.ClaimFlag)
        {
            await LegacyItemPackServicesLogCatalogFenceAsync(
                    context,
                    service,
                    optionId,
                    $"claim-already-used:flag=0x{service.ClaimFlag:X}; source grant branch is silent")
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyItemType(context.WorldData, selectedItemId, out var rewardItemType) ||
            rewardItemType.Id != checked((int)selectedItemId) ||
            rewardItemType.LegacyDurability > ushort.MaxValue)
        {
            await LegacyItemPackServicesLogCatalogFenceAsync(
                    context,
                    service,
                    optionId,
                    $"reward-itemtype-missing-or-invalid:itemId={selectedItemId.ToString(CultureInfo.InvariantCulture)}; no pack or claim flag was consumed")
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await LegacyItemPackServicesLogCatalogFenceAsync(
                    context,
                    service,
                    optionId,
                    "session-store-missing; no persistence or mutation occurred")
                .ConfigureAwait(false);
            return;
        }

        if (!sessions.TryGet(context.Connection, out var registeredSession) ||
            registeredSession is null ||
            !ReferenceEquals(registeredSession, session))
        {
            await LegacyItemPackServicesLogCatalogFenceAsync(
                    context,
                    service,
                    optionId,
                    "session-registration-missing-or-stale; no persistence or mutation occurred")
                .ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            await LegacyItemPackServicesLogCatalogFenceAsync(
                    context,
                    service,
                    optionId,
                    "player-items-root-missing; no persistence or mutation occurred")
                .ConfigureAwait(false);
            return;
        }

        if (session.Character.Uid <= 0)
        {
            await LegacyItemPackServicesLogCatalogFenceAsync(
                    context,
                    service,
                    optionId,
                    "character-uid-unavailable; no persistence or mutation occurred")
                .ConfigureAwait(false);
            return;
        }

        PlayerItemSnapshotItem rewardItem;
        try
        {
            var durability = checked((ushort)rewardItemType.LegacyDurability);
            rewardItem = new PlayerItemSnapshotItem
            {
                Uid = GetNextSessionItemUid(session.PlayerItems),
                ItemId = selectedItemId,
                Durability = durability,
                MaximumDurability = durability,
                Position = 0,
                Plus = 0,
                Bless = service.RewardBless,
                Bound = 0,
                Enchant = 0,
                StackSize = 1,
                Color = checked((uint)Random.Shared.Next(3, 9)),
            };
        }
        catch (OverflowException)
        {
            await LegacyItemPackServicesLogCatalogFenceAsync(
                    context,
                    service,
                    optionId,
                    "item-uid-exhausted; no pack or claim flag was consumed")
                .ConfigureAwait(false);
            return;
        }

        if (service.RequiredPackItemId.HasValue)
        {
            await LegacyItemPackServicesApplyPackedCatalogGrantAsync(
                    context,
                    session,
                    sessions,
                    optionId,
                    service,
                    rewardItemType,
                    rewardItem,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await LegacyItemPackServicesApplyOneTimeCatalogGrantAsync(
                context,
                session,
                sessions,
                optionId,
                service,
                rewardItemType,
                rewardItem,
                rechargeProgress,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask LegacyItemPackServicesApplyPackedCatalogGrantAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        WorldSessionStore sessions,
        byte optionId,
        LegacyItemPackServicesCatalogService service,
        ItemType rewardItemType,
        PlayerItemSnapshotItem rewardItem,
        CancellationToken cancellationToken)
    {
        var packItemId = service.RequiredPackItemId!.Value;
        var packItem = session.PlayerItems.InventoryItems.FirstOrDefault(item => item.ItemId == packItemId);
        if (packItem is null)
        {
            await LegacyItemPackServicesLogCatalogFenceAsync(
                    context,
                    service,
                    optionId,
                    "pack-count-drift; no pack was consumed")
                .ConfigureAwait(false);
            return;
        }

        var consumedItems = ConsumeSingleLegacyInventoryItem(
            session.PlayerItems,
            packItem,
            out var consumptionFrame);
        var updatedItems = consumedItems with
        {
            InventoryItems = consumedItems.InventoryItems
                .Concat([rewardItem])
                .OrderBy(item => item.Uid)
                .ToArray(),
        };
        var updatedSession = session with
        {
            PlayerItems = updatedItems,
            LegacyCatalogSelectedItemId = 0,
        };

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            new PlayerItemSnapshotLoader(context.PlayerItemsRoot!).SaveByCharacterUid(
                session.Character.Uid,
                updatedItems);
        }
        catch (Exception ex) when (LegacyItemPackServicesIsCatalogPersistenceException(ex))
        {
            await LegacyItemPackServicesLogCatalogFenceAsync(
                    context,
                    service,
                    optionId,
                    $"item-persistence-{ex.GetType().Name}; no live session mutation or packet was committed")
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(consumptionFrame, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await LegacyItemPackServicesSendMessageBoxAsync(
                updatedContext,
                service.Source,
                $"You received 1 {rewardItemType.Name}",
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc ItemPackServices packed catalog grant " +
                $"{FormatSession(updatedContext)} source={service.Source} sourceLines={service.GrantSourceLines} " +
                $"npcId={service.NpcId} optionId={optionId} packItemId={packItemId} " +
                $"rewardItemId={rewardItem.ItemId} bless={rewardItem.Bless} bound={rewardItem.Bound} " +
                $"color={rewardItem.Color} sourceOrder=RemovePack,AddItem,MessageBox.")
            .ConfigureAwait(false);
    }

    private static async ValueTask LegacyItemPackServicesApplyOneTimeCatalogGrantAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        WorldSessionStore sessions,
        byte optionId,
        LegacyItemPackServicesCatalogService service,
        ItemType rewardItemType,
        PlayerItemSnapshotItem rewardItem,
        ulong rechargeProgress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(session.UsersRoot))
        {
            await LegacyItemPackServicesLogCatalogFenceAsync(
                    context,
                    service,
                    optionId,
                    "users-root-missing; no claim flag or item was persisted")
                .ConfigureAwait(false);
            return;
        }

        var updatedRechargeProgress = rechargeProgress | service.ClaimFlag;
        var updatedCharacter = WithCharacterAttribute(
            session.Character,
            LegacyItemPackServicesRechargeProgressAttribute,
            updatedRechargeProgress);
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems
                .Concat([rewardItem])
                .OrderBy(item => item.Uid)
                .ToArray(),
        };
        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
            LegacyCatalogSelectedItemId = 0,
        };

        var characterPath = Path.Combine(
            session.UsersRoot,
            session.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        byte[] originalCharacterBytes;
        try
        {
            originalCharacterBytes = File.ReadAllBytes(characterPath);
        }
        catch (Exception ex) when (LegacyItemPackServicesIsCatalogPersistenceException(ex))
        {
            await LegacyItemPackServicesLogCatalogFenceAsync(
                    context,
                    service,
                    optionId,
                    $"character-persistence-preflight-{ex.GetType().Name}; no claim flag or item was persisted")
                .ConfigureAwait(false);
            return;
        }

        var itemLoader = new PlayerItemSnapshotLoader(context.PlayerItemsRoot!);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            itemLoader.SaveByCharacterUid(session.Character.Uid, updatedItems);
        }
        catch (Exception ex) when (LegacyItemPackServicesIsCatalogPersistenceException(ex))
        {
            await LegacyItemPackServicesLogCatalogFenceAsync(
                    context,
                    service,
                    optionId,
                    $"item-persistence-{ex.GetType().Name}; no claim flag or live mutation was committed")
                .ConfigureAwait(false);
            return;
        }

        string characterReason;
        bool characterPersisted;
        try
        {
            characterPersisted = TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                session.Character.Uid,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [LegacyItemPackServicesRechargeProgressAttribute] =
                        updatedRechargeProgress.ToString(CultureInfo.InvariantCulture),
                },
                out _,
                out characterReason);
        }
        catch (Exception ex) when (LegacyItemPackServicesIsCatalogPersistenceException(ex))
        {
            characterPersisted = false;
            characterReason = ex.GetType().Name;
        }

        if (!characterPersisted)
        {
            var itemRollback = "complete";
            try
            {
                itemLoader.SaveByCharacterUid(session.Character.Uid, session.PlayerItems);
            }
            catch (Exception ex) when (LegacyItemPackServicesIsCatalogPersistenceException(ex))
            {
                itemRollback = "failed:" + ex.GetType().Name;
            }

            var characterRollback = "complete";
            try
            {
                File.WriteAllBytes(characterPath, originalCharacterBytes);
            }
            catch (Exception ex) when (LegacyItemPackServicesIsCatalogPersistenceException(ex))
            {
                characterRollback = "failed:" + ex.GetType().Name;
            }

            await LegacyItemPackServicesLogCatalogFenceAsync(
                    context,
                    service,
                    optionId,
                    $"character-persistence-failed:{characterReason}; itemRollback={itemRollback} " +
                    $"characterRollback={characterRollback}; no claim flag or live mutation was committed")
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await LegacyItemPackServicesSendMessageBoxAsync(
                updatedContext,
                service.Source,
                $"You received 1 {rewardItemType.Name}",
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc ItemPackServices one-time catalog grant " +
                $"{FormatSession(updatedContext)} source={service.Source} sourceLines={service.GrantSourceLines} " +
                $"npcId={service.NpcId} optionId={optionId} rewardItemId={rewardItem.ItemId} " +
                $"claimFlag=0x{service.ClaimFlag:X} rechargeProgress={updatedRechargeProgress} " +
                $"color={rewardItem.Color} sourceOrder=AddRechargeFlag,AddItem,MessageBox.")
            .ConfigureAwait(false);
    }

    private static bool LegacyItemPackServicesTryBuildCatalog(
        WorldDataCatalog? worldData,
        LegacyItemPackServicesCatalogService service,
        out IReadOnlyList<LegacyItemPackServicesCatalogEntry> catalog,
        out string reason)
    {
        reason = string.Empty;
        if (service.CatalogKind == LegacyItemPackServicesCatalogKind.Accessory)
        {
            catalog = LegacyItemPackServicesAccessoryCatalogItemIds
                .Select(itemId => new LegacyItemPackServicesCatalogEntry(itemId, Stars: 0))
                .ToArray();
            return true;
        }

        if (worldData is null)
        {
            catalog = [];
            reason = "coat-storage-catalog-missing; WorldData was not loaded";
            return false;
        }

        var entries = new List<LegacyItemPackServicesCatalogEntry>();
        foreach (var coatStorageType in worldData.CoatStorageTypes)
        {
            if (coatStorageType.Parameters.Count < 6)
            {
                continue;
            }

            var isGarment = coatStorageType.Parameters[0] == 1;
            var expectedGarment = service.CatalogKind == LegacyItemPackServicesCatalogKind.Garment;
            var stars = coatStorageType.Parameters[5];
            if (isGarment != expectedGarment || stars < 4 || coatStorageType.ItemTypeId <= 0)
            {
                continue;
            }

            entries.Add(new LegacyItemPackServicesCatalogEntry(
                checked((uint)coatStorageType.ItemTypeId),
                checked((uint)stars)));
        }

        if (entries.Count == 0)
        {
            catalog = [];
            reason = $"coat-storage-{service.CatalogKind.ToString().ToLowerInvariant()}-catalog-empty";
            return false;
        }

        catalog = entries;
        return true;
    }

    private static bool LegacyItemPackServicesCatalogHasBindingClassStack(
        PlayerItemSnapshot items,
        uint itemId,
        ushort count,
        byte requiredBound)
    {
        var remaining = count;
        foreach (var item in items.InventoryItems)
        {
            if (item.ItemId != itemId ||
                (requiredBound == 0 ? item.Bound != 0 : item.Bound == 0))
            {
                continue;
            }

            var stackSize = item.StackSize;
            if (stackSize >= remaining)
            {
                return true;
            }

            remaining = checked((ushort)(remaining - stackSize));
        }

        return false;
    }

    private static string LegacyItemPackServicesCatalogPromptText(
        LegacyItemPackServicesCatalogService service,
        byte optionId)
    {
        return service.CatalogKind == LegacyItemPackServicesCatalogKind.Accessory && optionId == 0
            ? "Select the Accesory you need."
            : $"Select the {service.Noun} you need.";
    }

    private static string LegacyItemPackServicesCatalogOptionText(
        LegacyItemPackServicesCatalogService service,
        string itemName,
        uint stars)
    {
        return service.CatalogKind == LegacyItemPackServicesCatalogKind.Accessory
            ? itemName
            : $"{itemName}({stars.ToString(CultureInfo.InvariantCulture)}Stars)";
    }

    private static async ValueTask LegacyItemPackServicesSendCatalogConfirmationAsync(
        WorldPacketHandlerContext context,
        LegacyItemPackServicesCatalogService service,
        string itemName,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                service.Source,
                [LegacyItemPackServicesCatalogConfirmationText(service, itemName)],
                [
                    new("Sure.", 250),
                    new($"Show me the {service.PreviewNoun}.", 251),
                    new("Let me check.", LegacyNpcClosedOption),
                ],
                LegacyItemPackServicesDefaultAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static string LegacyItemPackServicesCatalogConfirmationText(
        LegacyItemPackServicesCatalogService service,
        string itemName)
    {
        var noun = service.CatalogKind == LegacyItemPackServicesCatalogKind.Garment
            ? "garment"
            : service.Noun;
        return $"Are you sure you wanna buy this {noun} ({itemName})?!";
    }

    private static bool LegacyItemPackServicesIsCatalogPersistenceException(Exception ex)
    {
        return ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException or
            System.Security.SecurityException;
    }

    private static async ValueTask LegacyItemPackServicesLogCatalogFenceAsync(
        WorldPacketHandlerContext context,
        LegacyItemPackServicesCatalogService service,
        byte optionId,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc ItemPackServices catalog selector " +
                $"{FormatSession(context)} source={service.Source} sourceLines={service.SourceLines} " +
                $"npcId={service.NpcId.ToString(CultureInfo.InvariantCulture)} " +
                $"optionId={optionId.ToString(CultureInfo.InvariantCulture)} reason={reason}.")
            .ConfigureAwait(false);
    }

    private enum LegacyItemPackServicesCatalogKind : byte
    {
        Accessory,
        Mount,
        Garment,
    }

    private readonly record struct LegacyItemPackServicesCatalogEntry(uint ItemId, uint Stars);

    private sealed record LegacyItemPackServicesCatalogService(
        uint NpcId,
        string Source,
        string SourceLines,
        string GrantSourceLines,
        string Noun,
        string PreviewNoun,
        LegacyItemPackServicesCatalogKind CatalogKind,
        uint? RequiredPackItemId,
        byte RewardBless,
        ulong ClaimFlag);

    private static readonly LegacyItemPackServicesCatalogService LegacyItemPackServicesBlackFridayAccessoryCatalogService =
        new(
            LegacyItemPackServicesBlackFridayAccesoryNpcId,
            "NpcHandler.BlackFridayAccesory",
            "21480-21578",
            "21512-21533",
            "Accessory",
            "Accessory",
            LegacyItemPackServicesCatalogKind.Accessory,
            LegacyItemPackServicesBlackFridayAccesoryPackItemId,
            RewardBless: 0,
            ClaimFlag: 0);

    private static readonly LegacyItemPackServicesCatalogService LegacyItemPackServicesBlackFridayMountCatalogService =
        new(
            LegacyItemPackServicesBlackFridayMountPackNpcId,
            "NpcHandler.BlackFridayMountPack",
            "21580-21684",
            "21618-21639",
            "Mount",
            "Mount",
            LegacyItemPackServicesCatalogKind.Mount,
            LegacyItemPackServicesBlackFridayMountPackItemId,
            RewardBless: 1,
            ClaimFlag: 0);

    private static readonly LegacyItemPackServicesCatalogService LegacyItemPackServicesBlackFridayGarmentCatalogService =
        new(
            LegacyItemPackServicesBlackFridayGarmentPackNpcId,
            "NpcHandler.BlackFridayGarmentPack",
            "21686-21790",
            "21724-21745",
            "Garment",
            "garment",
            LegacyItemPackServicesCatalogKind.Garment,
            LegacyItemPackServicesBlackFridayGarmentPackItemId,
            RewardBless: 1,
            ClaimFlag: 0);

    private static readonly LegacyItemPackServicesCatalogService LegacyItemPackServicesAccessoryPacket2CatalogService =
        new(
            LegacyItemPackServicesAccesoryPacket2NpcId,
            "NpcHandler.AccesoryPacket2",
            "21792-21892",
            "21826-21847",
            "Accessory",
            "Accessory",
            LegacyItemPackServicesCatalogKind.Accessory,
            RequiredPackItemId: null,
            RewardBless: 0,
            ClaimFlag: 0x10_000);

    private static readonly LegacyItemPackServicesCatalogService LegacyItemPackServicesAccessoryPacketCatalogService =
        new(
            LegacyItemPackServicesAccesoryPacketNpcId,
            "NpcHandler.AccesoryPacket",
            "21996-22096",
            "22030-22051",
            "Accessory",
            "Accessory",
            LegacyItemPackServicesCatalogKind.Accessory,
            RequiredPackItemId: null,
            RewardBless: 0,
            ClaimFlag: 0x200);

    private static readonly LegacyItemPackServicesCatalogService LegacyItemPackServicesMountPacket3CatalogService =
        new(
            LegacyItemPackServicesMountPacket3NpcId,
            "NpcHandler.MountPacket3",
            "21894-21994",
            "21928-21949",
            "Mount",
            "Mount",
            LegacyItemPackServicesCatalogKind.Mount,
            RequiredPackItemId: null,
            RewardBless: 0,
            ClaimFlag: 0x40_000);

    private static readonly LegacyItemPackServicesCatalogService LegacyItemPackServicesMountPacket2CatalogService =
        new(
            LegacyItemPackServicesMountPacket2NpcId,
            "NpcHandler.MountPacket2",
            "22098-22198",
            "22132-22153",
            "Mount",
            "Mount",
            LegacyItemPackServicesCatalogKind.Mount,
            RequiredPackItemId: null,
            RewardBless: 0,
            ClaimFlag: 0x100);

    private static readonly LegacyItemPackServicesCatalogService LegacyItemPackServicesMountPacketCatalogService =
        new(
            LegacyItemPackServicesMountPacketNpcId,
            "NpcHandler.MountPacket",
            "22200-22300",
            "22234-22255",
            "Mount",
            "Mount",
            LegacyItemPackServicesCatalogKind.Mount,
            RequiredPackItemId: null,
            RewardBless: 0,
            ClaimFlag: 0x4);

    private static readonly LegacyItemPackServicesCatalogService LegacyItemPackServicesGarmentPacket2CatalogService =
        new(
            LegacyItemPackServicesGarmentPacket2NpcId,
            "NpcHandler.GarmentPacket2",
            "22302-22402",
            "22336-22357",
            "Garment",
            "garment",
            LegacyItemPackServicesCatalogKind.Garment,
            RequiredPackItemId: null,
            RewardBless: 0,
            ClaimFlag: 0x400);

    private static readonly LegacyItemPackServicesCatalogService LegacyItemPackServicesGarmentPacketCatalogService =
        new(
            LegacyItemPackServicesGarmentPacketNpcId,
            "NpcHandler.GarmentPacket",
            "22795-22895",
            "22829-22850",
            "Garment",
            "garment",
            LegacyItemPackServicesCatalogKind.Garment,
            RequiredPackItemId: null,
            RewardBless: 0,
            ClaimFlag: 0x1);
}
