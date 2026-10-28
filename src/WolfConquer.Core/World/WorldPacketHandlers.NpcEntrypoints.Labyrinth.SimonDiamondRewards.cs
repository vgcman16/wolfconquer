using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacySimonDiamondRewardSourceMethod = "NpcHandler.Simon";
    private const string LegacySimonDiamondRewardSourceLines = "NpcHandler.cs:34151-34272";
    private const uint LegacySimonDiamondRequiredCount = 17;
    private const uint LegacySimonAncestorBoxItemId = 721_540;
    private const uint LegacySimonNormalGemItemBaseId = 700_000;

    private static readonly LegacySimonNormalGemReward[] LegacySimonNormalGemRewards =
    [
        new("Dragon", 11),
        new("Fury", 21),
        new("Glory", 121),
        new("Kylin", 41),
        new("Moon", 61),
        new("Phoenix", 1),
        new("Rainbow", 31),
        new("Thunder", 101),
        new("Tortoise", 71),
        new("Violet", 51),
    ];

    private static async ValueTask<bool> TryClaimLegacySimonDiamondRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession expectedSession,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (ResolveLegacySimonDiamondRewardSpec(optionId) is not { } spec)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (context.Sessions is not { } sessions)
        {
            await LogLegacySimonDiamondRewardFailureAsync(
                    context,
                    expectedSession,
                    spec,
                    "session-store-unavailable")
                .ConfigureAwait(false);
            return true;
        }

        if (!sessions.TryGet(context.Connection, out var initialSession) || initialSession is null)
        {
            await LogLegacySimonDiamondRewardFailureAsync(
                    context,
                    expectedSession,
                    spec,
                    "session-missing-before-lock")
                .ConfigureAwait(false);
            return true;
        }

        if (initialSession.AccountId != expectedSession.AccountId ||
            initialSession.Character.Uid != expectedSession.Character.Uid)
        {
            await LogLegacySimonDiamondRewardFailureAsync(
                    context,
                    initialSession,
                    spec,
                    "session-identity-changed-before-lock")
                .ConfigureAwait(false);
            return true;
        }

        if (initialSession.ActiveNpcId != LegacySimonNpcId)
        {
            await LogLegacySimonDiamondRewardFailureAsync(
                    context,
                    initialSession,
                    spec,
                    $"active-npc-mismatch:{initialSession.ActiveNpcId.GetValueOrDefault()}; stale-option-replay-fenced")
                .ConfigureAwait(false);
            return true;
        }

        if (!HasUnboundLegacySimonDiamonds(initialSession.PlayerItems, spec.DiamondItemId))
        {
            await SendLegacySimonDiamondMissingAsync(
                    context with { Session = initialSession },
                    spec,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!TryResolveLegacySimonDiamondItemPath(
                initialSession,
                context.PlayerItemsRoot,
                out var itemPath,
                out var pathReason))
        {
            await LogLegacySimonDiamondRewardFailureAsync(
                    context,
                    initialSession,
                    spec,
                    pathReason)
                .ConfigureAwait(false);
            return true;
        }

        LegacySimonDiamondRewardCommit commit;
        try
        {
            commit = SnapshotFileLockCoordinator.ExecuteLocked(
                itemPath,
                () => TryCommitLegacySimonDiamondReward(
                    context,
                    sessions,
                    initialSession.AccountId,
                    initialSession.Character.Uid,
                    spec,
                    itemPath));
        }
        catch (WorldSessionPublicationException failure)
        {
            await LogLegacyPublishedNpcFailureAsync(context, failure, "Labyrinth")
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (IsLegacySimonDiamondRewardInfrastructureException(exception))
        {
            commit = LegacySimonDiamondRewardCommit.Failed(
                initialSession,
                itemPath,
                "transaction-" + exception.GetType().Name);
        }

        if (commit.Status == LegacySimonDiamondRewardCommitStatus.MissingDiamonds)
        {
            await SendLegacySimonDiamondMissingAsync(
                    context with { Session = commit.SourceSession ?? initialSession },
                    spec,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (commit.Status != LegacySimonDiamondRewardCommitStatus.Success ||
            commit.UpdatedSession is not { } updatedSession)
        {
            await LogLegacySimonDiamondRewardFailureAsync(
                    context,
                    commit.SourceSession ?? initialSession,
                    spec,
                    commit.Reason)
                .ConfigureAwait(false);
            return true;
        }

        var updatedContext = context with { Session = updatedSession };
        foreach (var consumptionFrame in commit.ConsumptionFrames)
        {
            await context.Connection.SendAsync(consumptionFrame, cancellationToken).ConfigureAwait(false);
        }

        foreach (var rewardItem in commit.RewardItems)
        {
            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(rewardItem).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacySimonDiamondRewardSourceMethod,
                ["Well done. Here is the reward I promised you."],
                [new LegacyNpcDialogOption("Thanks.", LegacyNpcClosedOption)],
                LegacyLabyrinthQuestAvatarId,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc Simon diamond reward " +
                $"{FormatSession(updatedContext)} source={LegacySimonDiamondRewardSourceMethod} " +
                $"sourceLines={LegacySimonDiamondRewardSourceLines} npcId={LegacySimonNpcId} " +
                $"option={optionId} consumedItemId={spec.DiamondItemId} " +
                $"consumedCount={LegacySimonDiamondRequiredCount} " +
                $"consumedStackUids={string.Join(',', commit.ConsumedItemUids)} " +
                $"rewardItemId={commit.RewardItemId} rewardCount={commit.RewardItems.Count} " +
                $"rewardEvidence={commit.RewardEvidence} itemFile={Path.GetFileName(commit.ItemPath)} " +
                "eligibility=unbound-diamonds removalRule=lowest-uid-positive-unbound " +
                "persistenceBeforeSessionCommit=true activeNpcId=cleared " +
                "packetOrder=all-diamond-consumption-frames,all-reward-Add-frames,success-dialog broadcast=false.")
            .ConfigureAwait(false);
        return true;
    }

    private static LegacySimonDiamondRewardCommit TryCommitLegacySimonDiamondReward(
        WorldPacketHandlerContext context,
        WorldSessionStore sessions,
        long expectedAccountId,
        long expectedCharacterUid,
        LegacySimonDiamondRewardSpec spec,
        string expectedItemPath)
    {
        if (!sessions.TryGet(context.Connection, out var sourceSession) || sourceSession is null)
        {
            return LegacySimonDiamondRewardCommit.Failed(
                null,
                expectedItemPath,
                "session-missing-after-lock");
        }

        if (sourceSession.AccountId != expectedAccountId ||
            sourceSession.Character.Uid != expectedCharacterUid)
        {
            return LegacySimonDiamondRewardCommit.Failed(
                sourceSession,
                expectedItemPath,
                "session-identity-changed-after-lock");
        }

        if (sourceSession.ActiveNpcId != LegacySimonNpcId)
        {
            return LegacySimonDiamondRewardCommit.Failed(
                sourceSession,
                expectedItemPath,
                $"active-npc-mismatch:{sourceSession.ActiveNpcId.GetValueOrDefault()}; stale-option-replay-fenced");
        }

        if (!TryResolveLegacySimonDiamondItemPath(
                sourceSession,
                context.PlayerItemsRoot,
                out var currentItemPath,
                out var pathReason))
        {
            return LegacySimonDiamondRewardCommit.Failed(
                sourceSession,
                expectedItemPath,
                pathReason);
        }

        if (!LegacySimonDiamondRewardPathEquals(currentItemPath, expectedItemPath))
        {
            return LegacySimonDiamondRewardCommit.Failed(
                sourceSession,
                expectedItemPath,
                "session-item-path-changed-after-lock");
        }

        if (!HasUnboundLegacySimonDiamonds(sourceSession.PlayerItems, spec.DiamondItemId))
        {
            return LegacySimonDiamondRewardCommit.MissingDiamonds(sourceSession, expectedItemPath);
        }

        byte[]? originalBytes;
        byte[] expectedBytes;
        try
        {
            originalBytes = PlayerItemSnapshotFilePersistence.ReadAllBytesIfExists(expectedItemPath);
            expectedBytes = PlayerItemSnapshotLoader.ToLegacyBytes(sourceSession.PlayerItems);
        }
        catch (Exception exception) when (IsLegacySimonDiamondRewardInfrastructureException(exception))
        {
            return LegacySimonDiamondRewardCommit.Failed(
                sourceSession,
                expectedItemPath,
                "baseline-read-" + exception.GetType().Name);
        }

        if (originalBytes is null)
        {
            return LegacySimonDiamondRewardCommit.Failed(
                sourceSession,
                expectedItemPath,
                "player-items-snapshot-missing");
        }

        if (!originalBytes.AsSpan().SequenceEqual(expectedBytes))
        {
            return LegacySimonDiamondRewardCommit.Failed(
                sourceSession,
                expectedItemPath,
                "persisted-items-do-not-match-live-session");
        }

        if (!TryResolveLegacySimonReward(
                context.WorldData,
                spec,
                out var resolvedReward,
                out var rewardItemType,
                out var rewardReason))
        {
            return LegacySimonDiamondRewardCommit.Failed(
                sourceSession,
                expectedItemPath,
                rewardReason);
        }

        if (!TryCreateLegacySimonDiamondRewardMutation(
                sourceSession.PlayerItems,
                spec,
                resolvedReward,
                rewardItemType,
                out var updatedItems,
                out var consumptionFrames,
                out var rewardItems,
                out var consumedItemUids,
                out var mutationReason))
        {
            return LegacySimonDiamondRewardCommit.Failed(
                sourceSession,
                expectedItemPath,
                mutationReason);
        }

        var updatedSession = sourceSession with
        {
            PlayerItems = updatedItems,
            ActiveNpcId = null,
        };
        var updatedBytes = PlayerItemSnapshotLoader.ToLegacyBytes(updatedItems);
        try
        {
            PlayerItemSnapshotFilePersistence.WriteAllBytesAtomically(expectedItemPath, updatedBytes);
        }
        catch (Exception exception) when (IsLegacySimonDiamondRewardInfrastructureException(exception))
        {
            TryRollbackLegacySimonDiamondRewardItems(
                expectedItemPath,
                originalBytes,
                out var rollbackReason);
            return LegacySimonDiamondRewardCommit.Failed(
                sourceSession,
                expectedItemPath,
                $"item-persistence-{exception.GetType().Name}; rollback={rollbackReason}");
        }

        bool sessionCommitted;
        try
        {
            sessionCommitted = sessions.TryCompareAndSet(
                context.Connection,
                sourceSession,
                updatedSession);
        }
        catch (WorldSessionPublicationException)
        {
            // Published outcomes never enter compensating rollback.
            throw;
        }
        catch (Exception exception) when (IsLegacySimonDiamondRewardInfrastructureException(exception))
        {
            TryRollbackLegacySimonDiamondRewardItems(
                expectedItemPath,
                originalBytes,
                out var rollbackReason);
            return LegacySimonDiamondRewardCommit.Failed(
                sourceSession,
                expectedItemPath,
                $"session-commit-{exception.GetType().Name}; rollback={rollbackReason}");
        }

        if (!sessionCommitted)
        {
            TryRollbackLegacySimonDiamondRewardItems(
                expectedItemPath,
                originalBytes,
                out var rollbackReason);
            return LegacySimonDiamondRewardCommit.Failed(
                sourceSession,
                expectedItemPath,
                "session-changed-before-commit; rollback=" + rollbackReason);
        }

        return LegacySimonDiamondRewardCommit.Succeeded(
            sourceSession,
            updatedSession,
            consumptionFrames,
            rewardItems,
            consumedItemUids,
            resolvedReward,
            expectedItemPath);
    }

    private static bool TryCreateLegacySimonDiamondRewardMutation(
        PlayerItemSnapshot sourceItems,
        LegacySimonDiamondRewardSpec spec,
        LegacySimonResolvedReward resolvedReward,
        ItemType rewardItemType,
        out PlayerItemSnapshot updatedItems,
        out IReadOnlyList<byte[]> consumptionFrames,
        out IReadOnlyList<PlayerItemSnapshotItem> rewardItems,
        out IReadOnlyList<uint> consumedItemUids,
        out string reason)
    {
        updatedItems = sourceItems;
        consumptionFrames = [];
        rewardItems = [];
        consumedItemUids = [];
        reason = string.Empty;

        var inventoryItems = sourceItems.InventoryItems.ToList();
        var frames = new List<byte[]>();
        var consumedUids = new List<uint>();
        var remaining = LegacySimonDiamondRequiredCount;

        // Authorization and debit must use the same positive unbound units.
        foreach (var diamond in sourceItems.InventoryItems
                     .Where(item => item.ItemId == spec.DiamondItemId && item.Bound == 0 && item.StackSize > 0)
                     .OrderBy(item => item.Uid))
        {
            if (remaining == 0)
            {
                break;
            }

            var index = inventoryItems.FindIndex(item => ReferenceEquals(item, diamond));
            if (index < 0)
            {
                reason = "diamond-inventory-changed-during-mutation";
                return false;
            }

            var available = (uint)diamond.StackSize;
            var consumed = Math.Min(available, remaining);
            consumedUids.Add(diamond.Uid);
            if (consumed == available)
            {
                frames.Add(CreateLegacySimonDiamondRemovalFrame(diamond.Uid));
                inventoryItems.RemoveAt(index);
            }
            else
            {
                var updatedDiamond = diamond.WithStackSize(checked((ushort)(available - consumed)));
                inventoryItems[index] = updatedDiamond;
                frames.Add(
                    LegacyGameItemPacket.FromSnapshotItem(
                        updatedDiamond,
                        mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes());
            }

            remaining -= consumed;
        }

        if (remaining != 0)
        {
            reason = $"diamond-count-changed-before-mutation:missing-{remaining}";
            return false;
        }

        if (inventoryItems.Count + resolvedReward.Count > LegacyInventorySlots)
        {
            reason = "reward-inventory-capacity-exceeded";
            return false;
        }

        var consumedItems = sourceItems with { InventoryItems = inventoryItems.ToArray() };
        var grants = CreatePurchasedItems(
                consumedItems,
                rewardItemType,
                resolvedReward.Count,
                isBound: false)
            .ToArray();
        updatedItems = consumedItems with
        {
            InventoryItems = consumedItems.InventoryItems.Concat(grants).ToArray(),
        };
        consumptionFrames = frames;
        rewardItems = grants;
        consumedItemUids = consumedUids;
        return true;
    }

    private static byte[] CreateLegacySimonDiamondRemovalFrame(uint itemUid)
    {
        return new MsgItemUsagePacket(
            itemUid,
            DwParam: 0,
            MsgItemUsageAction.RemoveInventory,
            Timestamp: 0,
            DwParam2: 0,
            DwParam3: 0,
            DwParam4: 0).ToFrameBytes();
    }

    private static bool HasUnboundLegacySimonDiamonds(PlayerItemSnapshot items, uint diamondItemId)
    {
        var available = 0UL;
        foreach (var item in items.InventoryItems.Where(item =>
                     item.ItemId == diamondItemId && item.Bound == 0 && item.StackSize > 0))
        {
            available += item.StackSize;
            if (available >= LegacySimonDiamondRequiredCount)
            {
                return true;
            }
        }

        return false;
    }

    private static LegacySimonDiamondRewardSpec? ResolveLegacySimonDiamondRewardSpec(byte optionId)
    {
        return optionId switch
        {
            12 => new(
                optionId,
                LegacyLabyrinthSunDiamondItemId,
                "SunDiamond",
                LegacyLabyrinthMeteorItemId,
                RewardCount: 2,
                RandomNormalGem: false),
            13 => new(
                optionId,
                LegacyLabyrinthMoonDiamondItemId,
                "MoonDiamond",
                LegacyLabyrinthMeteorItemId,
                RewardCount: 4,
                RandomNormalGem: false),
            14 => new(
                optionId,
                LegacyLabyrinthStarDiamondItemId,
                "StarDiamond",
                RewardItemId: 0,
                RewardCount: 1,
                RandomNormalGem: true),
            15 => new(
                optionId,
                LegacyLabyrinthCloudDiamondItemId,
                "CloudDiamond",
                LegacySimonAncestorBoxItemId,
                RewardCount: 1,
                RandomNormalGem: false),
            _ => null,
        };
    }

    private static bool TryResolveLegacySimonReward(
        WorldDataCatalog? worldData,
        LegacySimonDiamondRewardSpec spec,
        out LegacySimonResolvedReward resolvedReward,
        out ItemType rewardItemType,
        out string reason)
    {
        if (!spec.RandomNormalGem)
        {
            resolvedReward = new(
                spec.RewardItemId,
                spec.RewardCount,
                "fixed-item-id");
            if (TryReadLegacyItemType(worldData, resolvedReward.ItemId, out rewardItemType))
            {
                reason = string.Empty;
                return true;
            }

            reason = $"reward-item-type-{resolvedReward.ItemId}-missing";
            return false;
        }

        var candidates = new (LegacySimonResolvedReward Reward, ItemType ItemType)[
            LegacySimonNormalGemRewards.Length];
        for (var index = 0; index < LegacySimonNormalGemRewards.Length; index++)
        {
            var gem = LegacySimonNormalGemRewards[index];
            var itemId = checked(LegacySimonNormalGemItemBaseId + gem.EnumValue);
            var candidate = new LegacySimonResolvedReward(
                itemId,
                spec.RewardCount,
                $"{LegacySimonNormalGemItemBaseId}+Flags.Gem.Normal{gem.Name}Gem({gem.EnumValue})");
            if (!TryReadLegacyItemType(worldData, itemId, out var candidateItemType))
            {
                resolvedReward = default;
                rewardItemType = default!;
                reason = $"reward-item-type-{itemId}-missing";
                return false;
            }

            candidates[index] = (candidate, candidateItemType);
        }

        var selected = candidates[Random.Shared.Next(candidates.Length)];
        resolvedReward = selected.Reward;
        rewardItemType = selected.ItemType;
        reason = string.Empty;
        return true;
    }

    private static ValueTask SendLegacySimonDiamondMissingAsync(
        WorldPacketHandlerContext context,
        LegacySimonDiamondRewardSpec spec,
        CancellationToken cancellationToken)
    {
        return SendLegacyNpcDialogAsync(
            context,
            LegacySimonDiamondRewardSourceMethod,
            [$"You are kidding me. How dare you come here to claim a prize with so few {spec.DiamondName}s?"],
            [new LegacyNpcDialogOption("Wait.~I~will~get~more.", LegacyNpcClosedOption)],
            LegacyLabyrinthQuestAvatarId,
            cancellationToken);
    }

    private static bool TryResolveLegacySimonDiamondItemPath(
        WorldSession session,
        string? playerItemsRoot,
        out string itemPath,
        out string reason)
    {
        itemPath = string.Empty;
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(playerItemsRoot))
        {
            reason = "player-items-root-missing";
            return false;
        }

        try
        {
            itemPath = Path.GetFullPath(Path.Combine(
                playerItemsRoot,
                session.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin"));
            return true;
        }
        catch (Exception exception) when (IsLegacySimonDiamondRewardInfrastructureException(exception))
        {
            reason = "item-path-" + exception.GetType().Name;
            return false;
        }
    }

    private static bool LegacySimonDiamondRewardPathEquals(string left, string right)
    {
        return string.Equals(
            left,
            right,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static bool TryRollbackLegacySimonDiamondRewardItems(
        string itemPath,
        byte[] originalBytes,
        out string reason)
    {
        try
        {
            PlayerItemSnapshotFilePersistence.WriteAllBytesAtomically(itemPath, originalBytes);
            reason = "complete";
            return true;
        }
        catch (Exception exception) when (IsLegacySimonDiamondRewardInfrastructureException(exception))
        {
            reason = "failed:" + exception.GetType().Name;
            return false;
        }
    }

    private static bool IsLegacySimonDiamondRewardInfrastructureException(Exception exception)
    {
        return exception is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException or
            OverflowException or
            FormatException or
            InvalidOperationException or
            System.Security.SecurityException;
    }

    private static Task LogLegacySimonDiamondRewardFailureAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacySimonDiamondRewardSpec spec,
        string reason)
    {
        return context.Log.WriteLineAsync(
            "PARITY-GAP WorldServer fenced parity failure for LegacyNpc Simon diamond reward " +
            $"{FormatSession(context with { Session = session })} " +
            $"source={LegacySimonDiamondRewardSourceMethod} sourceLines={LegacySimonDiamondRewardSourceLines} " +
            $"npcId={LegacySimonNpcId} option={spec.OptionId} diamondItemId={spec.DiamondItemId} " +
            $"requiredCount={LegacySimonDiamondRequiredCount} reason={reason}; " +
            "session was not published and no item/dialog success packets were sent.");
    }

    private enum LegacySimonDiamondRewardCommitStatus
    {
        Success,
        MissingDiamonds,
        Failure,
    }

    private readonly record struct LegacySimonDiamondRewardSpec(
        byte OptionId,
        uint DiamondItemId,
        string DiamondName,
        uint RewardItemId,
        int RewardCount,
        bool RandomNormalGem);

    private readonly record struct LegacySimonNormalGemReward(string Name, byte EnumValue);

    private readonly record struct LegacySimonResolvedReward(
        uint ItemId,
        int Count,
        string Evidence);

    private sealed record LegacySimonDiamondRewardCommit(
        LegacySimonDiamondRewardCommitStatus Status,
        WorldSession? SourceSession,
        WorldSession? UpdatedSession,
        IReadOnlyList<byte[]> ConsumptionFrames,
        IReadOnlyList<PlayerItemSnapshotItem> RewardItems,
        IReadOnlyList<uint> ConsumedItemUids,
        uint RewardItemId,
        string RewardEvidence,
        string ItemPath,
        string Reason)
    {
        public static LegacySimonDiamondRewardCommit Succeeded(
            WorldSession sourceSession,
            WorldSession updatedSession,
            IReadOnlyList<byte[]> consumptionFrames,
            IReadOnlyList<PlayerItemSnapshotItem> rewardItems,
            IReadOnlyList<uint> consumedItemUids,
            LegacySimonResolvedReward resolvedReward,
            string itemPath)
        {
            return new(
                LegacySimonDiamondRewardCommitStatus.Success,
                sourceSession,
                updatedSession,
                consumptionFrames,
                rewardItems,
                consumedItemUids,
                resolvedReward.ItemId,
                resolvedReward.Evidence,
                itemPath,
                string.Empty);
        }

        public static LegacySimonDiamondRewardCommit MissingDiamonds(
            WorldSession sourceSession,
            string itemPath)
        {
            return Empty(
                LegacySimonDiamondRewardCommitStatus.MissingDiamonds,
                sourceSession,
                itemPath,
                string.Empty);
        }

        public static LegacySimonDiamondRewardCommit Failed(
            WorldSession? sourceSession,
            string itemPath,
            string reason)
        {
            return Empty(
                LegacySimonDiamondRewardCommitStatus.Failure,
                sourceSession,
                itemPath,
                reason);
        }

        private static LegacySimonDiamondRewardCommit Empty(
            LegacySimonDiamondRewardCommitStatus status,
            WorldSession? sourceSession,
            string itemPath,
            string reason)
        {
            return new(
                status,
                sourceSession,
                null,
                [],
                [],
                [],
                RewardItemId: 0,
                string.Empty,
                itemPath,
                reason);
        }
    }
}
