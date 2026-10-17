using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyCliamCupRewardItemId = 2_100_075;
    private const string LegacyCliamCupGoldPrizeSaveAttribute = "Goldprizesave";
    private const string LegacyCliamCupSuccessEffect = "zf2-e128";
    private const uint LegacyCliamCupMinimumItemColor = 3;
    private const int LegacyCliamCupMaximumItemColorExclusive = 9;

    private static async ValueTask<bool> TryHandleLegacyShopRilecAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.ShopRilec";
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    ["You can buy a random relic here for 1,000 Wonder Herbs."],
                    LegacyShopRilecOptions,
                    LegacyRewardServicesAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 1)
        {
            if (!HasLegacyInventorySpace(session.PlayerItems, 1))
            {
                await SendLegacyRewardServicesNoticeAsync(
                        context,
                        source,
                        "Please make one more space in your inventory.",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            await SendLegacyRewardServicesGapAsync(
                    context,
                    source,
                    "5450-5498",
                    optionId,
                    "remove 1,000 Wonder Herbs item 3303483 and add a random relic",
                    "Random relic purchases are unavailable here until Wonder Herb stack removal and random relic creation are modeled safely.",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacyCliamCupAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.CliamCup";
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    ["You can collect your gold prize cup here."],
                    LegacyCliamCupOptions,
                    LegacyRewardServicesAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 1)
        {
            if (!HasLegacyInventorySpace(session.PlayerItems, 1))
            {
                await SendLegacyCliamCupMessageBoxAsync(
                        context,
                        "Please make 1 more space in your inventory.",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            if (ReadAttributeUInt64(session.Character.Attributes, LegacyCliamCupGoldPrizeSaveAttribute) != 1)
            {
                await SendLegacyCliamCupMessageBoxAsync(
                        context,
                        "You are not registered in the GoldPrize .",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            if (context.Sessions is not { } sessions)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc CliamCup claim fenced " +
                        $"{FormatSession(context)} source={source} option={optionId} reason=sessionStoreMissing; " +
                        "Goldprizesave and item state were preserved.")
                    .ConfigureAwait(false);
                return true;
            }

            if (!TryCreateLegacyCliamCupRewardItem(
                    context,
                    session,
                    out var rewardItem,
                    out var rewardReason,
                    out var playerUid))
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc CliamCup claim fenced " +
                        $"{FormatSession(context)} source={source} option={optionId} " +
                        $"itemId={LegacyCliamCupRewardItemId} reason={rewardReason}; " +
                        "Goldprizesave and item state were preserved.")
                    .ConfigureAwait(false);
                return true;
            }

            var updatedAttributes = new Dictionary<string, string>(
                session.Character.Attributes,
                StringComparer.OrdinalIgnoreCase)
            {
                [LegacyCliamCupGoldPrizeSaveAttribute] = "0",
            };
            var updatedCharacter = session.Character with { Attributes = updatedAttributes };
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
            };

            cancellationToken.ThrowIfCancellationRequested();
            if (!TryPersistLegacyCliamCupClaim(context, updatedSession, out var persistenceReason))
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc CliamCup claim persistence " +
                        $"{FormatSession(context)} source={source} option={optionId} " +
                        $"itemId={LegacyCliamCupRewardItemId} reason={persistenceReason}; " +
                        "no session mutation or reward packets were committed.")
                    .ConfigureAwait(false);
                return true;
            }

            sessions.Set(context.Connection, updatedSession);
            var updatedContext = context with { Session = updatedSession };

            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(rewardItem).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyItemFollowupsAsync(updatedContext, rewardItem, cancellationToken).ConfigureAwait(false);
            await SendLegacyCliamCupMessageBoxAsync(
                    updatedContext,
                    "Congratulations #52 .",
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Connection.SendAsync(
                    LegacyStringPacket.PlayerEffect(playerUid, LegacyCliamCupSuccessEffect).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);

            await context.Log.WriteLineAsync(
                    "WorldServer applied LegacyNpc CliamCup claim " +
                    $"{FormatSession(updatedContext)} source={source} option={optionId} " +
                    $"Goldprizesave=1->0 itemId={rewardItem.ItemId} itemUid={rewardItem.Uid}.")
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static ValueTask SendLegacyCliamCupMessageBoxAsync(
        WorldPacketHandlerContext context,
        string text,
        CancellationToken cancellationToken)
    {
        return context.Connection.SendAsync(
            LegacyNpcReplyPacket.ForMessageBox(text).ToFrameBytes(),
            cancellationToken);
    }

    private static bool TryCreateLegacyCliamCupRewardItem(
        WorldPacketHandlerContext context,
        WorldSession session,
        out PlayerItemSnapshotItem rewardItem,
        out string reason,
        out uint playerUid)
    {
        rewardItem = null!;
        reason = string.Empty;
        playerUid = 0;

        if (!TryReadItemType(context.WorldData, LegacyCliamCupRewardItemId, out var itemType))
        {
            reason = "itemtypeMissing";
            return false;
        }

        if (itemType.LegacyDurability > ushort.MaxValue)
        {
            reason = "itemtypeDurabilityOutOfRange";
            return false;
        }

        if (session.Character.Uid is < uint.MinValue or > uint.MaxValue)
        {
            reason = "characterUidOutOfRange";
            return false;
        }

        uint itemUid;
        try
        {
            itemUid = GetNextSessionItemUid(session.PlayerItems);
        }
        catch (OverflowException)
        {
            reason = "itemUidExhausted";
            return false;
        }

        var durability = checked((ushort)itemType.LegacyDurability);
        playerUid = checked((uint)session.Character.Uid);
        rewardItem = new PlayerItemSnapshotItem
        {
            Uid = itemUid,
            ItemId = LegacyCliamCupRewardItemId,
            Durability = durability,
            MaximumDurability = durability,
            Position = 0,
            StackSize = 1,
            Color = checked((uint)Random.Shared.Next(
                checked((int)LegacyCliamCupMinimumItemColor),
                LegacyCliamCupMaximumItemColorExclusive)),
        };
        return true;
    }

    private static bool TryPersistLegacyCliamCupClaim(
        WorldPacketHandlerContext context,
        WorldSession session,
        out string reason)
    {
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(session.UsersRoot))
        {
            reason = "usersRootMissing";
            return false;
        }

        if (string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            reason = "playerItemsRootMissing";
            return false;
        }

        var characterPath = Path.Combine(
            session.UsersRoot,
            session.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        if (!File.Exists(characterPath))
        {
            reason = "snapshotMissing";
            return false;
        }

        string[] characterLines;
        try
        {
            characterLines = File.ReadAllLines(characterPath, LegacyCharacterSnapshotEncoding);
        }
        catch (IOException)
        {
            reason = "snapshotReadFailed";
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            reason = "snapshotReadDenied";
            return false;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [LegacyCliamCupGoldPrizeSaveAttribute] = "0",
        };
        if (!TryValidateLegacyCharacterUid(characterLines, session.Character.Uid, out reason) ||
            !TryApplyLegacyCharacterAttributeLines(characterLines, values, out var updatedCharacterLines))
        {
            if (reason.Length == 0)
            {
                reason = "characterSectionMissing";
            }

            return false;
        }

        var itemPath = Path.Combine(
            context.PlayerItemsRoot,
            session.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        var transactionId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var characterTempPath = characterPath + ".claim-cup-" + transactionId + ".tmp";
        var characterBackupPath = characterPath + ".claim-cup-" + transactionId + ".bak";
        var itemTempPath = itemPath + ".claim-cup-" + transactionId + ".tmp";
        var itemBackupPath = itemPath + ".claim-cup-" + transactionId + ".bak";
        var itemExisted = File.Exists(itemPath);
        var characterCommitted = false;
        var itemCommitted = false;

        try
        {
            Directory.CreateDirectory(context.PlayerItemsRoot);
            File.WriteAllLines(characterTempPath, updatedCharacterLines, LegacyCharacterSnapshotEncoding);
            File.WriteAllBytes(itemTempPath, PlayerItemSnapshotLoader.ToLegacyBytes(session.PlayerItems));

            File.Replace(
                characterTempPath,
                characterPath,
                characterBackupPath,
                ignoreMetadataErrors: true);
            characterCommitted = true;

            if (itemExisted)
            {
                File.Replace(itemTempPath, itemPath, itemBackupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(itemTempPath, itemPath);
            }

            itemCommitted = true;
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is ArgumentException ||
            ex is NotSupportedException ||
            ex is OverflowException)
        {
            var itemRestored = !itemCommitted ||
                TryRestoreLegacyCliamCupFile(itemBackupPath, itemPath, itemExisted);
            var characterRestored = !characterCommitted ||
                TryRestoreLegacyCliamCupFile(characterBackupPath, characterPath, existed: true);
            reason = itemRestored && characterRestored
                ? "transactionWriteFailed"
                : "transactionRollbackFailed";
            return false;
        }
        finally
        {
            TryDeleteLegacyCliamCupFile(characterTempPath);
            TryDeleteLegacyCliamCupFile(itemTempPath);
        }

        TryDeleteLegacyCliamCupFile(characterBackupPath);
        TryDeleteLegacyCliamCupFile(itemBackupPath);
        return true;
    }

    private static bool TryRestoreLegacyCliamCupFile(
        string backupPath,
        string targetPath,
        bool existed)
    {
        try
        {
            if (!existed)
            {
                TryDeleteLegacyCliamCupFile(targetPath);
                return true;
            }

            if (!File.Exists(backupPath))
            {
                return false;
            }

            if (File.Exists(targetPath))
            {
                File.Replace(backupPath, targetPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(backupPath, targetPath);
            }

            return true;
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is ArgumentException ||
            ex is NotSupportedException)
        {
            return false;
        }
    }

    private static void TryDeleteLegacyCliamCupFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
        }
    }

    private static async ValueTask<bool> TryHandleLegacyRelicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.Relic";
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    ["You can buy a random relic here for 100,000 CPs."],
                    LegacyRelicOptions,
                    LegacyRewardServicesAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 1)
        {
            if (session.Character.ConquerPoints < 100_000)
            {
                await SendLegacyRewardServicesNoticeAsync(
                        context,
                        source,
                        "You do not have 100,000 CPs.",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            await SendLegacyRewardServicesGapAsync(
                    context,
                    source,
                    "5550-5581",
                    optionId,
                    "spend 100,000 CPs and add a random relic",
                    "Random relic purchases are unavailable here until CP persistence and random relic creation are modeled safely.",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacyHosuuAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.HosuuNpcs";
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    ["I can master your Hossu's perfection level for 600,000 CPs."],
                    LegacyHosuuOptions,
                    LegacyHosuuAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 1)
        {
            if (session.Character.ConquerPoints < 600_000)
            {
                await SendLegacyRewardServicesNoticeAsync(
                        context,
                        source,
                        "You do not have 600,000 CPs.",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            await SendLegacyRewardServicesGapAsync(
                    context,
                    source,
                    "6266-6356",
                    optionId,
                    "set equipped Hossu perfection level to 54 and spend 600,000 CPs",
                    "Hossu perfection upgrades are unavailable here until equipped-item mutation and CP persistence are modeled safely.",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }
}
