using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyRewardPointsMysteryFruitItemId = 3_001_044;
    private const uint LegacyRewardPointsLifeFruitBasketItemId = 723_725;
    private const int LegacyRewardPointsStackSizeFieldIndex = 46;

    private static ValueTask<bool> TryHandleLegacyRewardPointsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return TryHandleLegacyRewardPointsAsync(
            context,
            session,
            optionId,
            DateTime.Now,
            cancellationToken);
    }

    internal static async ValueTask<bool> TryHandleLegacyRewardPointsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.RewardPoints";
        var rewardPoints = ReadAttributeUInt64(session.Character.Attributes, "RewardPoints");
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    ["I can exchange your reward points for gifts.", $"You have {rewardPoints.ToString(CultureInfo.InvariantCulture)} points."],
                    LegacyRewardPointsOptions,
                    LegacyRewardServicesAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var cost = optionId switch
        {
            1 => 1UL,
            2 => 10UL,
            3 => 5UL,
            _ => 0UL,
        };
        if (cost == 0)
        {
            return false;
        }

        // The source performs this gate even for the VIP-only branch and before checking points.
        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox("Please make 1 more space in your inventory.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc RewardPoints exchange " +
                    $"{FormatSession(context)} source={source} option={optionId} reason=inventoryFull.")
                .ConfigureAwait(false);
            return true;
        }

        if (rewardPoints < cost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    [$"You need {cost.ToString(CultureInfo.InvariantCulture)} reward point(s)."],
                    [new LegacyNpcDialogOption("Thanks!", LegacyNpcClosedOption)],
                    avatarId: 166,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (context.Sessions is not { } sessions)
        {
            await LogLegacyRewardPointsFenceAsync(context, optionId, "sessionStoreMissing").ConfigureAwait(false);
            return true;
        }

        if (string.IsNullOrWhiteSpace(session.UsersRoot))
        {
            await LogLegacyRewardPointsFenceAsync(context, optionId, "usersRootMissing").ConfigureAwait(false);
            return true;
        }

        if (optionId is 1 or 3 && string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            await LogLegacyRewardPointsFenceAsync(context, optionId, "playerItemsRootMissing").ConfigureAwait(false);
            return true;
        }

        if (optionId == 2)
        {
            await ApplyLegacyRewardPointsVipAsync(
                    context,
                    session,
                    sessions,
                    optionId,
                    rewardPoints,
                    now,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        await ApplyLegacyRewardPointsItemAsync(
                context,
                session,
                sessions,
                optionId,
                rewardPoints,
                itemId: optionId == 1
                    ? LegacyRewardPointsMysteryFruitItemId
                    : LegacyRewardPointsLifeFruitBasketItemId,
                stackable: optionId == 3,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask ApplyLegacyRewardPointsVipAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        WorldSessionStore sessions,
        byte optionId,
        ulong rewardPoints,
        DateTime now,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.RewardPoints";
        const ulong cost = 10;
        var currentExpiry = ReadLegacyRewardPointsVipExpiry(session.Character);
        DateTime updatedExpiry;
        try
        {
            updatedExpiry = (now > currentExpiry ? now : currentExpiry).AddHours(1);
        }
        catch (ArgumentOutOfRangeException)
        {
            await LogLegacyRewardPointsFenceAsync(context, optionId, "vipExpiryOutOfRange").ConfigureAwait(false);
            return;
        }

        var updatedRewardPoints = rewardPoints - cost;
        var vipTime = updatedExpiry.Ticks.ToString(CultureInfo.InvariantCulture);
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithCharacterAttribute(session.Character, "RewardPoints", updatedRewardPoints),
                "VipLevel",
                6),
            "VipTime",
            vipTime);
        var updatedSession = session with { Character = updatedCharacter };
        var characterValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RewardPoints"] = updatedRewardPoints.ToString(CultureInfo.InvariantCulture),
            ["VipLevel"] = "6",
            ["VipTime"] = vipTime,
        };

        cancellationToken.ThrowIfCancellationRequested();
        if (!TryPersistLegacyRewardPointsMutation(
                context,
                updatedSession,
                characterValues,
                persistItems: false,
                out var persistenceReason))
        {
            await LogLegacyRewardPointsPersistenceFenceAsync(context, optionId, persistenceReason)
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        var vipUpdate = LegacyMsgUpdatePacket.CreateLegacyVipLevelStartupUpdate(updatedCharacter).ToFrameBytes();

        // NpcHandler sends one update explicitly, then UpdateVip sends the same update and the VIP status.
        await context.Connection.SendAsync(vipUpdate, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(vipUpdate, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyVipStatusPacket.FromSnapshot(updatedCharacter).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox("You`ve received VIP6 (1 Hours).").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc RewardPoints VIP exchange " +
                $"{FormatSession(updatedContext)} source={source} option={optionId} cost={cost} " +
                $"rewardPoints={updatedRewardPoints} vipLevel=6 vipTime={vipTime}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask ApplyLegacyRewardPointsItemAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        WorldSessionStore sessions,
        byte optionId,
        ulong rewardPoints,
        uint itemId,
        bool stackable,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.RewardPoints";
        var cost = optionId == 1 ? 1UL : 5UL;
        if (!TryReadLegacyItemType(context.WorldData, itemId, out var itemType))
        {
            await LogLegacyRewardPointsFenceAsync(context, optionId, $"itemtypeMissing:itemId={itemId}")
                .ConfigureAwait(false);
            return;
        }

        if (itemType.Id != checked((int)itemId) || itemType.LegacyDurability > ushort.MaxValue)
        {
            await LogLegacyRewardPointsFenceAsync(context, optionId, $"itemtypeInvalid:itemId={itemId}")
                .ConfigureAwait(false);
            return;
        }

        var stackLimit = stackable ? ReadLegacyRewardPointsStackLimit(itemType) : (ushort)0;
        if (stackable && stackLimit == 0)
        {
            await LogLegacyRewardPointsFenceAsync(context, optionId, $"itemtypeInvalid:itemId={itemId}:stackSize=0")
                .ConfigureAwait(false);
            return;
        }

        PlayerItemSnapshotItem grantedItem;
        PlayerItemSnapshot updatedItems;
        ushort itemMode;
        var compatibleStack = stackable
            ? session.PlayerItems.InventoryItems.FirstOrDefault(item =>
                item.ItemId == itemId &&
                item.Bound == 0 &&
                (uint)item.StackSize + 1 <= stackLimit)
            : null;
        if (compatibleStack is not null)
        {
            grantedItem = compatibleStack.WithStackSize(checked((ushort)(compatibleStack.StackSize + 1)));
            updatedItems = ReplaceInventoryItem(session.PlayerItems, compatibleStack, grantedItem);
            itemMode = LegacyGameItemPacket.UpdateItemMode;
        }
        else
        {
            uint itemUid;
            try
            {
                itemUid = GetNextSessionItemUid(session.PlayerItems);
            }
            catch (OverflowException)
            {
                await LogLegacyRewardPointsFenceAsync(context, optionId, $"itemUidExhausted:itemId={itemId}")
                    .ConfigureAwait(false);
                return;
            }

            var durability = checked((ushort)itemType.LegacyDurability);
            grantedItem = new PlayerItemSnapshotItem
            {
                Uid = itemUid,
                ItemId = itemId,
                Durability = durability,
                MaximumDurability = durability,
                Position = 0,
                StackSize = 1,
                Bound = 0,
                Color = checked((uint)Random.Shared.Next(3, 9)),
            };
            updatedItems = session.PlayerItems with
            {
                InventoryItems = session.PlayerItems.InventoryItems
                    .Concat([grantedItem])
                    .OrderBy(item => item.Uid)
                    .ToArray(),
            };
            itemMode = LegacyGameItemPacket.AddItemMode;
        }

        var updatedRewardPoints = rewardPoints - cost;
        var updatedCharacter = WithCharacterAttribute(session.Character, "RewardPoints", updatedRewardPoints);
        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        var characterValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RewardPoints"] = updatedRewardPoints.ToString(CultureInfo.InvariantCulture),
        };

        cancellationToken.ThrowIfCancellationRequested();
        if (!TryPersistLegacyRewardPointsMutation(
                context,
                updatedSession,
                characterValues,
                persistItems: true,
                out var persistenceReason))
        {
            await LogLegacyRewardPointsPersistenceFenceAsync(context, optionId, persistenceReason)
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(grantedItem, mode: itemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        if (optionId == 3)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox("You`ve received (LifeFruitBasket).").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc RewardPoints item exchange " +
                $"{FormatSession(updatedContext)} source={source} option={optionId} cost={cost} " +
                $"rewardPoints={updatedRewardPoints} itemId={itemId} itemUid={grantedItem.Uid} bound=false " +
                $"itemMode={(itemMode == LegacyGameItemPacket.UpdateItemMode ? "update" : "add")}.")
            .ConfigureAwait(false);
    }

    private static DateTime ReadLegacyRewardPointsVipExpiry(CharacterSnapshot character)
    {
        var raw = ReadAttributeString(character.Attributes, "VipTime", "ExpireVip");
        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var binary))
        {
            try
            {
                return DateTime.FromBinary(binary);
            }
            catch (ArgumentException)
            {
            }
        }

        return default;
    }

    private static ushort ReadLegacyRewardPointsStackLimit(ItemType itemType)
    {
        return itemType.Fields.Count > LegacyRewardPointsStackSizeFieldIndex &&
            ushort.TryParse(
                itemType.Fields[LegacyRewardPointsStackSizeFieldIndex],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var stackLimit)
            ? stackLimit
            : (ushort)0;
    }

    private static bool TryPersistLegacyRewardPointsMutation(
        WorldPacketHandlerContext context,
        WorldSession updatedSession,
        IReadOnlyDictionary<string, string> characterValues,
        bool persistItems,
        out string reason)
    {
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(updatedSession.UsersRoot))
        {
            reason = "usersRootMissing";
            return false;
        }

        if (persistItems && string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            reason = "playerItemsRootMissing";
            return false;
        }

        var characterPath = Path.Combine(
            updatedSession.UsersRoot,
            updatedSession.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
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

        if (!TryValidateLegacyCharacterUid(characterLines, updatedSession.Character.Uid, out reason) ||
            !TryApplyLegacyCharacterAttributeLines(characterLines, characterValues, out var updatedCharacterLines))
        {
            if (reason.Length == 0)
            {
                reason = "characterSectionMissing";
            }

            return false;
        }

        var transactionId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var characterTempPath = characterPath + ".reward-points-" + transactionId + ".tmp";
        var characterBackupPath = characterPath + ".reward-points-" + transactionId + ".bak";
        var itemPath = persistItems
            ? Path.Combine(
                context.PlayerItemsRoot!,
                updatedSession.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin")
            : string.Empty;
        var itemTempPath = persistItems ? itemPath + ".reward-points-" + transactionId + ".tmp" : string.Empty;
        var itemBackupPath = persistItems ? itemPath + ".reward-points-" + transactionId + ".bak" : string.Empty;
        var itemExisted = persistItems && File.Exists(itemPath);
        var characterCommitted = false;
        var itemCommitted = false;

        try
        {
            File.WriteAllLines(characterTempPath, updatedCharacterLines, LegacyCharacterSnapshotEncoding);
            if (persistItems)
            {
                Directory.CreateDirectory(context.PlayerItemsRoot!);
                File.WriteAllBytes(itemTempPath, PlayerItemSnapshotLoader.ToLegacyBytes(updatedSession.PlayerItems));
            }

            File.Replace(
                characterTempPath,
                characterPath,
                characterBackupPath,
                ignoreMetadataErrors: true);
            characterCommitted = true;

            if (persistItems)
            {
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
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException or
            OverflowException)
        {
            var itemRestored = !itemCommitted ||
                TryRestoreLegacyRewardPointsFile(itemBackupPath, itemPath, itemExisted);
            var characterRestored = !characterCommitted ||
                TryRestoreLegacyRewardPointsFile(characterBackupPath, characterPath, existed: true);
            reason = itemRestored && characterRestored
                ? "transactionWriteFailed:" + ex.GetType().Name
                : "transactionRollbackFailed:" + ex.GetType().Name;
            return false;
        }
        finally
        {
            TryDeleteLegacyRewardPointsFile(characterTempPath);
            TryDeleteLegacyRewardPointsFile(itemTempPath);
        }

        TryDeleteLegacyRewardPointsFile(characterBackupPath);
        TryDeleteLegacyRewardPointsFile(itemBackupPath);
        return true;
    }

    private static bool TryRestoreLegacyRewardPointsFile(
        string backupPath,
        string targetPath,
        bool existed)
    {
        try
        {
            if (!existed)
            {
                TryDeleteLegacyRewardPointsFile(targetPath);
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
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            return false;
        }
    }

    private static void TryDeleteLegacyRewardPointsFile(string path)
    {
        if (path.Length == 0)
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
        }
    }

    private static ValueTask LogLegacyRewardPointsFenceAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        string reason)
    {
        return new ValueTask(
            context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc RewardPoints exchange fenced " +
                $"{FormatSession(context)} source=NpcHandler.RewardPoints sourceLines=6358-6493 " +
                $"option={optionId} reason={reason}; no reward points or item/VIP state were mutated."));
    }

    private static ValueTask LogLegacyRewardPointsPersistenceFenceAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        string reason)
    {
        return new ValueTask(
            context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc RewardPoints exchange persistence " +
                $"{FormatSession(context)} source=NpcHandler.RewardPoints sourceLines=6358-6493 " +
                $"option={optionId} reason={reason}; no session mutation or reward packets were committed."));
    }
}
