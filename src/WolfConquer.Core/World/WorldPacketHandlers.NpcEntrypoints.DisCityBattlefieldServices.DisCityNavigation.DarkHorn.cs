using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyDisCityDarkHornItemId = 790_001;
    private const int LegacyDisCityDarkHornRequiredFreeSlots = 8;

    private static readonly LegacyDisCityDarkHornRewardGroup[] LegacyDisCityDarkHornRewardGroups =
    [
        new(722_057, 5),
        new(720_028, 2),
        new(1_200_006, 1),
        new(3_005_129, 1),
    ];

    private static async ValueTask LegacyDisCityBattlefieldServicesClaimDarkHornRewardAsync(
        WorldPacketHandlerContext context,
        uint npcId,
        byte optionId,
        LegacyDisCityBattlefieldServicesSource source,
        ushort missingHornAvatarId,
        ushort fullInventoryAvatarId,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions ||
            !sessions.TryGet(context.Connection, out var initialSession) ||
            initialSession is null)
        {
            await LegacyDisCityBattlefieldServicesLogDarkHornFenceAsync(
                    context,
                    npcId,
                    optionId,
                    source,
                    "session-store-unavailable; no item mutation, packets, or broadcast were committed")
                .ConfigureAwait(false);
            return;
        }

        if (initialSession.ActiveNpcId != npcId)
        {
            await LegacyDisCityBattlefieldServicesLogDarkHornFenceAsync(
                    context,
                    npcId,
                    optionId,
                    source,
                    $"active-npc-mismatch:{initialSession.ActiveNpcId.GetValueOrDefault()}; stale option replay was fenced")
                .ConfigureAwait(false);
            return;
        }

        var initialGate = LegacyDisCityBattlefieldServicesGetDarkHornGate(initialSession.PlayerItems);
        if (initialGate == LegacyDisCityDarkHornGate.MissingHorn)
        {
            await LegacyDisCityBattlefieldServicesSendDarkHornMissingAsync(
                    context,
                    source.SourceMethod,
                    missingHornAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (initialGate == LegacyDisCityDarkHornGate.InventoryFull)
        {
            await LegacyDisCityBattlefieldServicesSendDarkHornInventoryFullAsync(
                    context,
                    source.SourceMethod,
                    fullInventoryAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            await LegacyDisCityBattlefieldServicesLogDarkHornFenceAsync(
                    context,
                    npcId,
                    optionId,
                    source,
                    "player-items-root-missing; no item mutation, packets, or broadcast were committed")
                .ConfigureAwait(false);
            return;
        }

        string itemPath;
        try
        {
            itemPath = Path.Combine(
                context.PlayerItemsRoot,
                initialSession.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        }
        catch (Exception exception) when (LegacyDisCityBattlefieldServicesIsDarkHornInfrastructureException(exception))
        {
            await LegacyDisCityBattlefieldServicesLogDarkHornFenceAsync(
                    context,
                    npcId,
                    optionId,
                    source,
                    $"item-path-{exception.GetType().Name}; no item mutation, packets, or broadcast were committed")
                .ConfigureAwait(false);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        LegacyDisCityDarkHornCommit commit;
        try
        {
            commit = SnapshotFileLockCoordinator.ExecuteLocked(
                itemPath,
                () => LegacyDisCityBattlefieldServicesTryCommitDarkHornReward(
                    context,
                    sessions,
                    initialSession.AccountId,
                    initialSession.Character.Uid,
                    npcId,
                    itemPath));
        }
        catch (Exception exception) when (LegacyDisCityBattlefieldServicesIsDarkHornInfrastructureException(exception))
        {
            commit = LegacyDisCityDarkHornCommit.Failure(
                itemPath,
                "transaction-" + exception.GetType().Name);
        }

        switch (commit.Status)
        {
            case LegacyDisCityDarkHornCommitStatus.MissingHorn:
                await LegacyDisCityBattlefieldServicesSendDarkHornMissingAsync(
                        context,
                        source.SourceMethod,
                        missingHornAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;

            case LegacyDisCityDarkHornCommitStatus.InventoryFull:
                await LegacyDisCityBattlefieldServicesSendDarkHornInventoryFullAsync(
                        context,
                        source.SourceMethod,
                        fullInventoryAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;

            case LegacyDisCityDarkHornCommitStatus.Failure:
                await LegacyDisCityBattlefieldServicesLogDarkHornFenceAsync(
                        context,
                        npcId,
                        optionId,
                        source,
                        commit.Reason + "; no success packets or broadcast were committed")
                    .ConfigureAwait(false);
                return;

            case LegacyDisCityDarkHornCommitStatus.Success:
                break;

            default:
                throw new InvalidOperationException($"Unknown Dark Horn commit status {commit.Status}.");
        }

        var updatedSession = commit.UpdatedSession!;
        var updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(commit.RemovalFrame, cancellationToken).ConfigureAwait(false);
        foreach (var rewardItem in commit.RewardItems)
        {
            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(rewardItem).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var broadcastText =
            $"{updatedSession.Character.Name} has claimed the reward from DisCity.[5PowerExpBalls, 2DBScrolls, 1StarDrill and 3000ChiPoints.]";
        var broadcastFrame = LegacyMsgMessagePacket.SystemTalk(broadcastText).ToFrameBytes();
        var activeSessions = sessions.Snapshot();
        if (activeSessions.Count == 0)
        {
            await context.Connection.SendAsync(broadcastFrame, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            foreach (var active in activeSessions)
            {
                await active.Connection.SendAsync(broadcastFrame, cancellationToken).ConfigureAwait(false);
            }
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc DisCityBattlefieldServices Dark Horn reward " +
                $"{FormatSession(updatedContext)} source={source.SourceMethod} sourceLines={source.SourceLines} " +
                $"npcId={npcId} optionId={optionId} itemFile={Path.GetFileName(commit.ItemPath)} " +
                "eligibility=unbound-horn removalRule=lowest-uid-any-binding rewardItems=9 netSlots=8 " +
                "colors=random-3-through-8 " +
                "sourceOrder=Remove790001,Add722057x5,Add720028x2,Add1200006x1,Add3005129x1,TalkBroadcast.")
            .ConfigureAwait(false);
    }

    private static LegacyDisCityDarkHornCommit LegacyDisCityBattlefieldServicesTryCommitDarkHornReward(
        WorldPacketHandlerContext context,
        WorldSessionStore sessions,
        long expectedAccountId,
        long expectedCharacterUid,
        uint npcId,
        string itemPath)
    {
        if (!sessions.TryGet(context.Connection, out var sourceSession) || sourceSession is null)
        {
            return LegacyDisCityDarkHornCommit.Failure(itemPath, "session-missing-inside-transaction");
        }

        if (sourceSession.AccountId != expectedAccountId || sourceSession.Character.Uid != expectedCharacterUid)
        {
            return LegacyDisCityDarkHornCommit.Failure(itemPath, "session-identity-changed-inside-transaction");
        }

        if (sourceSession.ActiveNpcId != npcId)
        {
            return LegacyDisCityDarkHornCommit.Failure(
                itemPath,
                $"active-npc-mismatch:{sourceSession.ActiveNpcId.GetValueOrDefault()}; stale option replay was fenced");
        }

        var gate = LegacyDisCityBattlefieldServicesGetDarkHornGate(sourceSession.PlayerItems);
        if (gate == LegacyDisCityDarkHornGate.MissingHorn)
        {
            return LegacyDisCityDarkHornCommit.MissingHorn(itemPath);
        }

        if (gate == LegacyDisCityDarkHornGate.InventoryFull)
        {
            return LegacyDisCityDarkHornCommit.InventoryFull(itemPath);
        }

        var originalBytes = PlayerItemSnapshotFilePersistence.ReadAllBytesIfExists(itemPath);
        var sourceBytes = PlayerItemSnapshotLoader.ToLegacyBytes(sourceSession.PlayerItems);
        if (originalBytes is not null && !originalBytes.AsSpan().SequenceEqual(sourceBytes))
        {
            return LegacyDisCityDarkHornCommit.Failure(
                itemPath,
                "persisted-items-do-not-match-live-session");
        }

        if (!LegacyDisCityBattlefieldServicesTryCreateDarkHornMutation(
                context,
                sourceSession.PlayerItems,
                out var updatedItems,
                out var removalFrame,
                out var rewardItems,
                out var mutationReason))
        {
            return LegacyDisCityDarkHornCommit.Failure(itemPath, mutationReason);
        }

        var updatedSession = sourceSession with
        {
            PlayerItems = updatedItems,
            ActiveNpcId = null,
        };
        var updatedBytes = PlayerItemSnapshotLoader.ToLegacyBytes(updatedItems);
        try
        {
            PlayerItemSnapshotFilePersistence.WriteAllBytesAtomically(itemPath, updatedBytes);
        }
        catch (Exception exception) when (LegacyDisCityBattlefieldServicesIsDarkHornInfrastructureException(exception))
        {
            var rollback = LegacyDisCityBattlefieldServicesTryRollbackDarkHornItems(
                itemPath,
                originalBytes,
                out var rollbackReason);
            return LegacyDisCityDarkHornCommit.Failure(
                itemPath,
                $"item-persistence-{exception.GetType().Name}; rollback={(rollback ? "complete" : rollbackReason)}");
        }

        bool sessionCommitted;
        try
        {
            sessionCommitted = sessions.TryCompareAndSet(context.Connection, sourceSession, updatedSession);
        }
        catch (Exception exception) when (LegacyDisCityBattlefieldServicesIsDarkHornInfrastructureException(exception))
        {
            if (sessions.TryGet(context.Connection, out var publishedSession) &&
                ReferenceEquals(publishedSession, updatedSession))
            {
                return LegacyDisCityDarkHornCommit.Success(
                    updatedSession,
                    removalFrame,
                    rewardItems,
                    itemPath);
            }

            var rollback = LegacyDisCityBattlefieldServicesTryRollbackDarkHornItems(
                itemPath,
                originalBytes,
                out var rollbackReason);
            return LegacyDisCityDarkHornCommit.Failure(
                itemPath,
                $"session-commit-{exception.GetType().Name}; rollback={(rollback ? "complete" : rollbackReason)}");
        }

        if (sessionCommitted)
        {
            return LegacyDisCityDarkHornCommit.Success(
                updatedSession,
                removalFrame,
                rewardItems,
                itemPath);
        }

        var rollbackSucceeded = LegacyDisCityBattlefieldServicesTryRollbackDarkHornItems(
            itemPath,
            originalBytes,
            out var sessionRollbackReason);
        return LegacyDisCityDarkHornCommit.Failure(
            itemPath,
            "session-changed-before-commit; rollback=" +
            (rollbackSucceeded ? "complete" : sessionRollbackReason));
    }

    private static LegacyDisCityDarkHornGate LegacyDisCityBattlefieldServicesGetDarkHornGate(
        PlayerItemSnapshot items)
    {
        var hasEligibleUnboundHorn = items.InventoryItems.Any(item =>
            item.ItemId == LegacyDisCityDarkHornItemId &&
            item.Bound == 0 &&
            item.StackSize >= 1);
        if (!hasEligibleUnboundHorn)
        {
            return LegacyDisCityDarkHornGate.MissingHorn;
        }

        return HasLegacyInventorySpace(items, LegacyDisCityDarkHornRequiredFreeSlots)
            ? LegacyDisCityDarkHornGate.Eligible
            : LegacyDisCityDarkHornGate.InventoryFull;
    }

    private static bool LegacyDisCityBattlefieldServicesTryCreateDarkHornMutation(
        WorldPacketHandlerContext context,
        PlayerItemSnapshot sourceItems,
        out PlayerItemSnapshot updatedItems,
        out byte[] removalFrame,
        out IReadOnlyList<PlayerItemSnapshotItem> rewardItems,
        out string reason)
    {
        updatedItems = sourceItems;
        removalFrame = [];
        rewardItems = [];
        reason = string.Empty;

        // Source Remove qualifies on an unbound horn, then removes without a bound filter.
        // Lowest UID mirrors the stable local inventory order while making that asymmetry deterministic.
        var horn = sourceItems.InventoryItems
            .Where(item => item.ItemId == LegacyDisCityDarkHornItemId)
            .OrderBy(item => item.Uid)
            .FirstOrDefault();
        if (horn is null)
        {
            reason = "dark-horn-disappeared-before-mutation";
            return false;
        }

        var workingItems = RemoveInventoryItem(sourceItems, horn);
        var grants = new List<PlayerItemSnapshotItem>(9);
        foreach (var rewardGroup in LegacyDisCityDarkHornRewardGroups)
        {
            if (!TryReadLegacyItemType(context.WorldData, rewardGroup.ItemId, out var itemType))
            {
                reason = $"reward-item-type-{rewardGroup.ItemId}-missing";
                return false;
            }

            var groupItems = CreatePurchasedItems(
                    workingItems,
                    itemType,
                    rewardGroup.Count,
                    isBound: false)
                .Select(item => item.WithColor(checked((uint)Random.Shared.Next(3, 9))))
                .ToArray();
            workingItems = workingItems with
            {
                InventoryItems = workingItems.InventoryItems.Concat(groupItems).ToArray(),
            };
            grants.AddRange(groupItems);
        }

        removalFrame = new MsgItemUsagePacket(
            horn.Uid,
            DwParam: 0,
            MsgItemUsageAction.RemoveInventory,
            Timestamp: 0,
            DwParam2: 0,
            DwParam3: 0,
            DwParam4: 0).ToFrameBytes();
        updatedItems = workingItems;
        rewardItems = grants;
        return true;
    }

    private static bool LegacyDisCityBattlefieldServicesTryRollbackDarkHornItems(
        string itemPath,
        byte[]? originalBytes,
        out string reason)
    {
        reason = string.Empty;
        try
        {
            if (originalBytes is null)
            {
                File.Delete(itemPath);
            }
            else
            {
                PlayerItemSnapshotFilePersistence.WriteAllBytesAtomically(itemPath, originalBytes);
            }

            return true;
        }
        catch (Exception exception) when (LegacyDisCityBattlefieldServicesIsDarkHornInfrastructureException(exception))
        {
            reason = "failed:" + exception.GetType().Name;
            return false;
        }
    }

    private static ValueTask LegacyDisCityBattlefieldServicesSendDarkHornMissingAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        ushort avatarId,
        CancellationToken cancellationToken)
    {
        return SendLegacyNpcDialogAsync(
            context,
            sourceMethod,
            ["Sorry you not have DarkHorn."],
            [new LegacyNpcDialogOption("Ah. ok", LegacyNpcClosedOption)],
            avatarId,
            cancellationToken);
    }

    private static ValueTask LegacyDisCityBattlefieldServicesSendDarkHornInventoryFullAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        ushort avatarId,
        CancellationToken cancellationToken)
    {
        return SendLegacyNpcDialogAsync(
            context,
            sourceMethod,
            ["Please make 8 more spaces in your inventory"],
            [new LegacyNpcDialogOption("Let me check.", LegacyNpcClosedOption)],
            avatarId,
            cancellationToken);
    }

    private static Task LegacyDisCityBattlefieldServicesLogDarkHornFenceAsync(
        WorldPacketHandlerContext context,
        uint npcId,
        byte optionId,
        LegacyDisCityBattlefieldServicesSource source,
        string reason)
    {
        return context.Log.WriteLineAsync(
            "PARITY-GAP LegacyNpc DisCityBattlefieldServices Dark Horn reward " +
            $"{FormatSession(context)} source={source.SourceMethod} sourceLines={source.SourceLines} " +
            $"npcId={npcId} optionId={optionId} reason={reason}.");
    }

    private static bool LegacyDisCityBattlefieldServicesIsDarkHornInfrastructureException(
        Exception exception)
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

    private enum LegacyDisCityDarkHornGate
    {
        Eligible,
        MissingHorn,
        InventoryFull,
    }

    private enum LegacyDisCityDarkHornCommitStatus
    {
        Success,
        MissingHorn,
        InventoryFull,
        Failure,
    }

    private readonly record struct LegacyDisCityDarkHornRewardGroup(uint ItemId, int Count);

    private sealed record LegacyDisCityDarkHornCommit(
        LegacyDisCityDarkHornCommitStatus Status,
        WorldSession? UpdatedSession,
        byte[] RemovalFrame,
        IReadOnlyList<PlayerItemSnapshotItem> RewardItems,
        string ItemPath,
        string Reason)
    {
        public static LegacyDisCityDarkHornCommit Success(
            WorldSession updatedSession,
            byte[] removalFrame,
            IReadOnlyList<PlayerItemSnapshotItem> rewardItems,
            string itemPath)
        {
            return new(
                LegacyDisCityDarkHornCommitStatus.Success,
                updatedSession,
                removalFrame,
                rewardItems,
                itemPath,
                string.Empty);
        }

        public static LegacyDisCityDarkHornCommit MissingHorn(string itemPath)
        {
            return Empty(LegacyDisCityDarkHornCommitStatus.MissingHorn, itemPath, string.Empty);
        }

        public static LegacyDisCityDarkHornCommit InventoryFull(string itemPath)
        {
            return Empty(LegacyDisCityDarkHornCommitStatus.InventoryFull, itemPath, string.Empty);
        }

        public static LegacyDisCityDarkHornCommit Failure(string itemPath, string reason)
        {
            return Empty(LegacyDisCityDarkHornCommitStatus.Failure, itemPath, reason);
        }

        private static LegacyDisCityDarkHornCommit Empty(
            LegacyDisCityDarkHornCommitStatus status,
            string itemPath,
            string reason)
        {
            return new(status, null, [], [], itemPath, reason);
        }
    }
}
