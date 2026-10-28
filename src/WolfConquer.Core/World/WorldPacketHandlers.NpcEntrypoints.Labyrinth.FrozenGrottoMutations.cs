using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyFrozenGrottoGuardian5SourceMethod = "NpcHandler.FrozenGrottoGuardian5";
    private const string LegacyFrozenGrottoGuardian5SourceLines = "NpcHandler.cs:34371-34389";
    private const string LegacyFrozenFroto4TeleporterSourceMethod = "NpcHandler.FrozenFroto4Teleporter";
    private const string LegacyFrozenFroto4TeleporterSourceLines = "NpcHandler.cs:34413-34424";
    private const string LegacyFrozenGrottoMutationRollbackMarker = ".legacy-frozen-grotto-rollback-";

    private static readonly LegacyNpcTeleportDestination LegacyFrozenGrottoFifthFloorDestination =
        new(2_055, 526, 562, LegacyFrozenFroto4TeleporterSourceMethod);

    private static async ValueTask<bool> TryGrantLegacyFrozenGrottoShadowJadeAsync(
        WorldPacketHandlerContext context,
        WorldSession expectedSession,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Sessions is not { } sessions)
        {
            await LogLegacyFrozenGrottoMutationFailureAsync(
                    context,
                    expectedSession,
                    LegacyFrozenGrottoGuardian5NpcId,
                    optionId: 2,
                    LegacyFrozenGrottoGuardian5SourceMethod,
                    LegacyFrozenGrottoGuardian5SourceLines,
                    "session-store-unavailable")
                .ConfigureAwait(false);
            return true;
        }

        if (!sessions.TryGet(context.Connection, out var initialSession) || initialSession is null)
        {
            await LogLegacyFrozenGrottoMutationFailureAsync(
                    context,
                    expectedSession,
                    LegacyFrozenGrottoGuardian5NpcId,
                    optionId: 2,
                    LegacyFrozenGrottoGuardian5SourceMethod,
                    LegacyFrozenGrottoGuardian5SourceLines,
                    "session-missing-before-lock")
                .ConfigureAwait(false);
            return true;
        }

        if (!IsExpectedLegacyFrozenGrottoSession(initialSession, expectedSession))
        {
            await LogLegacyFrozenGrottoMutationFailureAsync(
                    context,
                    initialSession,
                    LegacyFrozenGrottoGuardian5NpcId,
                    optionId: 2,
                    LegacyFrozenGrottoGuardian5SourceMethod,
                    LegacyFrozenGrottoGuardian5SourceLines,
                    "session-identity-changed-before-lock")
                .ConfigureAwait(false);
            return true;
        }

        if (initialSession.ActiveNpcId != LegacyFrozenGrottoGuardian5NpcId)
        {
            await LogLegacyFrozenGrottoMutationFailureAsync(
                    context,
                    initialSession,
                    LegacyFrozenGrottoGuardian5NpcId,
                    optionId: 2,
                    LegacyFrozenGrottoGuardian5SourceMethod,
                    LegacyFrozenGrottoGuardian5SourceLines,
                    $"active-npc-mismatch:{initialSession.ActiveNpcId.GetValueOrDefault()}; stale-option-replay-fenced")
                .ConfigureAwait(false);
            return true;
        }

        // Source checks capacity before checking whether the player already owns an unbound jade.
        if (!HasLegacyInventorySpace(initialSession.PlayerItems, 1))
        {
            await SendLegacyFrozenGrottoShadowJadeInventoryFullAsync(
                    context with { Session = initialSession },
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (HasUnboundLegacyFrozenGrottoItem(initialSession.PlayerItems, LegacyShadowJadeItemId))
        {
            await SendLegacyFrozenGrottoShadowJadeAlreadyOwnedAsync(
                    context with { Session = initialSession },
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!TryResolveLegacyFrozenGrottoItemPath(
                initialSession,
                context.PlayerItemsRoot,
                out var itemPath,
                out var pathReason))
        {
            await LogLegacyFrozenGrottoMutationFailureAsync(
                    context,
                    initialSession,
                    LegacyFrozenGrottoGuardian5NpcId,
                    optionId: 2,
                    LegacyFrozenGrottoGuardian5SourceMethod,
                    LegacyFrozenGrottoGuardian5SourceLines,
                    pathReason)
                .ConfigureAwait(false);
            return true;
        }

        LegacyFrozenGrottoShadowJadeCommit commit;
        try
        {
            commit = SnapshotFileLockCoordinator.ExecuteLocked(
                itemPath,
                () => TryCommitLegacyFrozenGrottoShadowJade(
                    context,
                    sessions,
                    initialSession.AccountId,
                    initialSession.Character.Uid,
                    itemPath));
        }
        catch (WorldSessionPublicationException failure)
        {
            await LogLegacyPublishedNpcFailureAsync(context, failure, "Labyrinth")
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (IsLegacyFrozenGrottoMutationInfrastructureException(exception))
        {
            commit = LegacyFrozenGrottoShadowJadeCommit.Failed(
                initialSession,
                itemPath,
                "transaction-" + exception.GetType().Name);
        }

        if (commit.Status == LegacyFrozenGrottoMutationCommitStatus.InventoryFull)
        {
            await SendLegacyFrozenGrottoShadowJadeInventoryFullAsync(
                    context with { Session = commit.SourceSession ?? initialSession },
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (commit.Status == LegacyFrozenGrottoMutationCommitStatus.AlreadyOwned)
        {
            await SendLegacyFrozenGrottoShadowJadeAlreadyOwnedAsync(
                    context with { Session = commit.SourceSession ?? initialSession },
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (commit.Status != LegacyFrozenGrottoMutationCommitStatus.Success ||
            commit.UpdatedSession is not { } updatedSession ||
            commit.GrantedItem is not { } grantedItem)
        {
            await LogLegacyFrozenGrottoMutationFailureAsync(
                    context,
                    commit.SourceSession ?? initialSession,
                    LegacyFrozenGrottoGuardian5NpcId,
                    optionId: 2,
                    LegacyFrozenGrottoGuardian5SourceMethod,
                    LegacyFrozenGrottoGuardian5SourceLines,
                    commit.Reason)
                .ConfigureAwait(false);
            return true;
        }

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(grantedItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc Frozen Grotto Shadow Jade grant " +
                $"{FormatSession(updatedContext)} source={LegacyFrozenGrottoGuardian5SourceMethod} " +
                $"sourceLines={LegacyFrozenGrottoGuardian5SourceLines} npcId={LegacyFrozenGrottoGuardian5NpcId} " +
                $"option=2 itemId={LegacyShadowJadeItemId} itemUid={grantedItem.Uid} " +
                $"itemFile={Path.GetFileName(commit.ItemPath)} eligibilityOrder=inventory-space,unbound-item-absence " +
                "persistenceBeforeSessionCommit=true activeNpcId=cleared packetOrder=LegacyGameItem.Add.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryEnterLegacyFrozenGrottoWithKeyAsync(
        WorldPacketHandlerContext context,
        WorldSession expectedSession,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Sessions is not { } sessions)
        {
            await LogLegacyFrozenGrottoMutationFailureAsync(
                    context,
                    expectedSession,
                    LegacyFrozenFroto4TeleporterNpcId,
                    optionId: 1,
                    LegacyFrozenFroto4TeleporterSourceMethod,
                    LegacyFrozenFroto4TeleporterSourceLines,
                    "session-store-unavailable")
                .ConfigureAwait(false);
            return true;
        }

        if (!sessions.TryGet(context.Connection, out var initialSession) || initialSession is null)
        {
            await LogLegacyFrozenGrottoMutationFailureAsync(
                    context,
                    expectedSession,
                    LegacyFrozenFroto4TeleporterNpcId,
                    optionId: 1,
                    LegacyFrozenFroto4TeleporterSourceMethod,
                    LegacyFrozenFroto4TeleporterSourceLines,
                    "session-missing-before-lock")
                .ConfigureAwait(false);
            return true;
        }

        if (!IsExpectedLegacyFrozenGrottoSession(initialSession, expectedSession))
        {
            await LogLegacyFrozenGrottoMutationFailureAsync(
                    context,
                    initialSession,
                    LegacyFrozenFroto4TeleporterNpcId,
                    optionId: 1,
                    LegacyFrozenFroto4TeleporterSourceMethod,
                    LegacyFrozenFroto4TeleporterSourceLines,
                    "session-identity-changed-before-lock")
                .ConfigureAwait(false);
            return true;
        }

        if (initialSession.ActiveNpcId != LegacyFrozenFroto4TeleporterNpcId)
        {
            await LogLegacyFrozenGrottoMutationFailureAsync(
                    context,
                    initialSession,
                    LegacyFrozenFroto4TeleporterNpcId,
                    optionId: 1,
                    LegacyFrozenFroto4TeleporterSourceMethod,
                    LegacyFrozenFroto4TeleporterSourceLines,
                    $"active-npc-mismatch:{initialSession.ActiveNpcId.GetValueOrDefault()}; stale-option-replay-fenced")
                .ConfigureAwait(false);
            return true;
        }

        if (!HasUnboundLegacyFrozenGrottoItem(initialSession.PlayerItems, LegacyFrozenGrottoKeyItemId))
        {
            await SendLegacyFrozenGrottoMissingKeyAsync(
                    context with { Session = initialSession },
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!TryResolveLegacyFrozenGrottoMutationPaths(
                initialSession,
                context.PlayerItemsRoot,
                out var itemPath,
                out var characterPath,
                out var pathReason))
        {
            await LogLegacyFrozenGrottoMutationFailureAsync(
                    context,
                    initialSession,
                    LegacyFrozenFroto4TeleporterNpcId,
                    optionId: 1,
                    LegacyFrozenFroto4TeleporterSourceMethod,
                    LegacyFrozenFroto4TeleporterSourceLines,
                    pathReason)
                .ConfigureAwait(false);
            return true;
        }

        var preflight = await PrepareLegacyTeleportAsync(
                context,
                initialSession,
                new LegacyTeleportRequest(
                    LegacyFrozenFroto4TeleporterSourceMethod,
                    LegacyFrozenGrottoFifthFloorDestination.MapId),
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflight.Allowed)
        {
            return true;
        }

        initialSession = preflight.Session;

        LegacyFrozenGrottoKeyEntryCommit commit;
        try
        {
            commit = CharacterSnapshotFileCoordinator.ExecuteLocked(
                [itemPath, characterPath],
                () => TryCommitLegacyFrozenGrottoKeyEntry(
                    context,
                    sessions,
                    initialSession.AccountId,
                    initialSession.Character.Uid,
                    itemPath,
                    characterPath));
        }
        catch (WorldSessionPublicationException failure)
        {
            await LogLegacyPublishedNpcFailureAsync(context, failure, "Labyrinth")
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (IsLegacyFrozenGrottoMutationInfrastructureException(exception))
        {
            commit = LegacyFrozenGrottoKeyEntryCommit.Failed(
                initialSession,
                itemPath,
                characterPath,
                "transaction-" + exception.GetType().Name);
        }

        if (commit.Status == LegacyFrozenGrottoMutationCommitStatus.MissingKey)
        {
            await SendLegacyFrozenGrottoMissingKeyAsync(
                    context with { Session = commit.SourceSession ?? initialSession },
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (commit.Status != LegacyFrozenGrottoMutationCommitStatus.Success ||
            commit.SourceSession is not { } sourceSession ||
            commit.UpdatedSession is not { } updatedSession)
        {
            await LogLegacyFrozenGrottoMutationFailureAsync(
                    context,
                    commit.SourceSession ?? initialSession,
                    LegacyFrozenFroto4TeleporterNpcId,
                    optionId: 1,
                    LegacyFrozenFroto4TeleporterSourceMethod,
                    LegacyFrozenFroto4TeleporterSourceLines,
                    commit.Reason)
                .ConfigureAwait(false);
            return true;
        }

        await context.Connection.SendAsync(commit.ItemMutationFrame, cancellationToken).ConfigureAwait(false);
        var teleportSourceSession = CreateLegacyTeleportMutationSourceSession(
            sourceSession,
            updatedSession);
        var appliedSession = await ExecuteLegacyTeleportDestinationAsync(
                context with { Session = teleportSourceSession },
                teleportSourceSession,
                new LegacyTeleportDestination(
                    LegacyFrozenGrottoFifthFloorDestination.MapId,
                    LegacyFrozenGrottoFifthFloorDestination.X,
                    LegacyFrozenGrottoFifthFloorDestination.Y),
                timestamp: 0,
                cancellationToken,
                expectedSession: updatedSession)
            .ConfigureAwait(false);
        if (appliedSession is null)
        {
            return true;
        }

        var updatedContext = context with { Session = appliedSession };

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc Frozen Grotto key entry " +
                $"{FormatSession(updatedContext)} source={LegacyFrozenFroto4TeleporterSourceMethod} " +
                $"sourceLines={LegacyFrozenFroto4TeleporterSourceLines} npcId={LegacyFrozenFroto4TeleporterNpcId} " +
                $"option=1 keyId={LegacyFrozenGrottoKeyItemId} keyUid={commit.KeyUid} " +
                $"keyUnits={commit.SourceStackSize}->{commit.UpdatedStackSize} " +
                $"map={sourceSession.Character.MapId}/{sourceSession.Character.X}/{sourceSession.Character.Y}" +
                $"->{appliedSession.Character.MapId}/{appliedSession.Character.X}/{appliedSession.Character.Y} " +
                $"itemFile={Path.GetFileName(commit.ItemPath)} characterFile={Path.GetFileName(commit.CharacterPath)} " +
                "eligibility=unbound-key removalRule=lowest-uid-any-binding-one-unit " +
                "persistenceOrder=items,character,session activeNpcId=cleared dynamicId=0 " +
                $"packetOrder={commit.ItemMutationPacket},GameClient.Teleport.Leave,GameClient.Teleport.Transition.")
            .ConfigureAwait(false);
        return true;
    }

    private static LegacyFrozenGrottoShadowJadeCommit TryCommitLegacyFrozenGrottoShadowJade(
        WorldPacketHandlerContext context,
        WorldSessionStore sessions,
        long expectedAccountId,
        long expectedCharacterUid,
        string expectedItemPath)
    {
        if (!sessions.TryGet(context.Connection, out var sourceSession) || sourceSession is null)
        {
            return LegacyFrozenGrottoShadowJadeCommit.Failed(
                null,
                expectedItemPath,
                "session-missing-after-lock");
        }

        if (sourceSession.AccountId != expectedAccountId ||
            sourceSession.Character.Uid != expectedCharacterUid)
        {
            return LegacyFrozenGrottoShadowJadeCommit.Failed(
                sourceSession,
                expectedItemPath,
                "session-identity-changed-after-lock");
        }

        if (sourceSession.ActiveNpcId != LegacyFrozenGrottoGuardian5NpcId)
        {
            return LegacyFrozenGrottoShadowJadeCommit.Failed(
                sourceSession,
                expectedItemPath,
                $"active-npc-mismatch:{sourceSession.ActiveNpcId.GetValueOrDefault()}; stale-option-replay-fenced");
        }

        if (!TryResolveLegacyFrozenGrottoItemPath(
                sourceSession,
                context.PlayerItemsRoot,
                out var currentItemPath,
                out var pathReason))
        {
            return LegacyFrozenGrottoShadowJadeCommit.Failed(sourceSession, expectedItemPath, pathReason);
        }

        if (!LegacyFrozenGrottoPathEquals(currentItemPath, expectedItemPath))
        {
            return LegacyFrozenGrottoShadowJadeCommit.Failed(
                sourceSession,
                expectedItemPath,
                "session-item-path-changed-after-lock");
        }

        // Keep the decompiled branch order inside the transaction as well.
        if (!HasLegacyInventorySpace(sourceSession.PlayerItems, 1))
        {
            return LegacyFrozenGrottoShadowJadeCommit.InventoryFull(sourceSession, expectedItemPath);
        }

        if (HasUnboundLegacyFrozenGrottoItem(sourceSession.PlayerItems, LegacyShadowJadeItemId))
        {
            return LegacyFrozenGrottoShadowJadeCommit.AlreadyOwned(sourceSession, expectedItemPath);
        }

        if (!TryReadItemType(context.WorldData, LegacyShadowJadeItemId, out var shadowJadeType))
        {
            return LegacyFrozenGrottoShadowJadeCommit.Failed(
                sourceSession,
                expectedItemPath,
                "shadow-jade-itemtype-missing");
        }

        byte[]? originalItemBytes;
        byte[] expectedItemBytes;
        try
        {
            originalItemBytes = PlayerItemSnapshotFilePersistence.ReadAllBytesIfExists(expectedItemPath);
            expectedItemBytes = PlayerItemSnapshotLoader.ToLegacyBytes(sourceSession.PlayerItems);
        }
        catch (Exception exception) when (IsLegacyFrozenGrottoMutationInfrastructureException(exception))
        {
            return LegacyFrozenGrottoShadowJadeCommit.Failed(
                sourceSession,
                expectedItemPath,
                "baseline-read-" + exception.GetType().Name);
        }

        if (originalItemBytes is null)
        {
            return LegacyFrozenGrottoShadowJadeCommit.Failed(
                sourceSession,
                expectedItemPath,
                "player-items-snapshot-missing");
        }

        if (!originalItemBytes.AsSpan().SequenceEqual(expectedItemBytes))
        {
            return LegacyFrozenGrottoShadowJadeCommit.Failed(
                sourceSession,
                expectedItemPath,
                "persisted-items-do-not-match-live-session");
        }

        PlayerItemSnapshotItem grantedItem;
        try
        {
            grantedItem = CreatePurchasedItems(
                sourceSession.PlayerItems,
                shadowJadeType,
                count: 1,
                isBound: false)[0];
        }
        catch (Exception exception) when (IsLegacyFrozenGrottoMutationInfrastructureException(exception))
        {
            return LegacyFrozenGrottoShadowJadeCommit.Failed(
                sourceSession,
                expectedItemPath,
                "item-create-" + exception.GetType().Name);
        }

        var updatedItems = sourceSession.PlayerItems with
        {
            InventoryItems = sourceSession.PlayerItems.InventoryItems.Concat([grantedItem]).ToArray(),
        };
        var updatedSession = sourceSession with
        {
            PlayerItems = updatedItems,
            ActiveNpcId = null,
        };

        try
        {
            PlayerItemSnapshotFilePersistence.WriteAllBytesAtomically(
                expectedItemPath,
                PlayerItemSnapshotLoader.ToLegacyBytes(updatedItems));
        }
        catch (Exception exception) when (IsLegacyFrozenGrottoMutationInfrastructureException(exception))
        {
            TryRollbackLegacyFrozenGrottoItems(expectedItemPath, originalItemBytes, out var rollbackReason);
            return LegacyFrozenGrottoShadowJadeCommit.Failed(
                sourceSession,
                expectedItemPath,
                $"item-persistence-{exception.GetType().Name}; rollback={rollbackReason}");
        }

        bool sessionCommitted;
        try
        {
            sessionCommitted = sessions.TryCompareAndSet(context.Connection, sourceSession, updatedSession);
        }
        catch (WorldSessionPublicationException)
        {
            // Published outcomes never enter compensating rollback.
            throw;
        }
        catch (Exception exception) when (IsLegacyFrozenGrottoMutationInfrastructureException(exception))
        {
            TryRollbackLegacyFrozenGrottoItems(expectedItemPath, originalItemBytes, out var rollbackReason);
            return LegacyFrozenGrottoShadowJadeCommit.Failed(
                sourceSession,
                expectedItemPath,
                $"session-commit-{exception.GetType().Name}; rollback={rollbackReason}");
        }

        if (!sessionCommitted)
        {
            TryRollbackLegacyFrozenGrottoItems(expectedItemPath, originalItemBytes, out var rollbackReason);
            return LegacyFrozenGrottoShadowJadeCommit.Failed(
                sourceSession,
                expectedItemPath,
                "session-changed-before-commit; rollback=" + rollbackReason);
        }

        return LegacyFrozenGrottoShadowJadeCommit.Succeeded(
            sourceSession,
            updatedSession,
            grantedItem,
            expectedItemPath);
    }

    private static LegacyFrozenGrottoKeyEntryCommit TryCommitLegacyFrozenGrottoKeyEntry(
        WorldPacketHandlerContext context,
        WorldSessionStore sessions,
        long expectedAccountId,
        long expectedCharacterUid,
        string expectedItemPath,
        string expectedCharacterPath)
    {
        if (!sessions.TryGet(context.Connection, out var sourceSession) || sourceSession is null)
        {
            return LegacyFrozenGrottoKeyEntryCommit.Failed(
                null,
                expectedItemPath,
                expectedCharacterPath,
                "session-missing-after-lock");
        }

        if (sourceSession.AccountId != expectedAccountId ||
            sourceSession.Character.Uid != expectedCharacterUid)
        {
            return LegacyFrozenGrottoKeyEntryCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                "session-identity-changed-after-lock");
        }

        if (sourceSession.ActiveNpcId != LegacyFrozenFroto4TeleporterNpcId)
        {
            return LegacyFrozenGrottoKeyEntryCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                $"active-npc-mismatch:{sourceSession.ActiveNpcId.GetValueOrDefault()}; stale-option-replay-fenced");
        }

        if (!TryResolveLegacyFrozenGrottoMutationPaths(
                sourceSession,
                context.PlayerItemsRoot,
                out var currentItemPath,
                out var currentCharacterPath,
                out var pathReason))
        {
            return LegacyFrozenGrottoKeyEntryCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                pathReason);
        }

        if (!LegacyFrozenGrottoPathEquals(currentItemPath, expectedItemPath) ||
            !LegacyFrozenGrottoPathEquals(currentCharacterPath, expectedCharacterPath))
        {
            return LegacyFrozenGrottoKeyEntryCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                "session-snapshot-path-changed-after-lock");
        }

        if (!HasUnboundLegacyFrozenGrottoItem(sourceSession.PlayerItems, LegacyFrozenGrottoKeyItemId))
        {
            return LegacyFrozenGrottoKeyEntryCommit.MissingKey(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath);
        }

        // Contain(id, 1, 0) requires an unbound key, while source Remove(id, 1) ignores binding.
        var key = sourceSession.PlayerItems.InventoryItems
            .Where(item => item.ItemId == LegacyFrozenGrottoKeyItemId)
            .OrderBy(item => item.Uid)
            .FirstOrDefault();
        if (key is null)
        {
            return LegacyFrozenGrottoKeyEntryCommit.MissingKey(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath);
        }

        byte[]? originalItemBytes;
        byte[]? originalCharacterBytes;
        byte[] expectedItemBytes;
        try
        {
            originalItemBytes = PlayerItemSnapshotFilePersistence.ReadAllBytesIfExists(expectedItemPath);
            originalCharacterBytes = SnapshotFilePersistence.ReadAllBytesIfExists(expectedCharacterPath);
            expectedItemBytes = PlayerItemSnapshotLoader.ToLegacyBytes(sourceSession.PlayerItems);
        }
        catch (Exception exception) when (IsLegacyFrozenGrottoMutationInfrastructureException(exception))
        {
            return LegacyFrozenGrottoKeyEntryCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                "baseline-read-" + exception.GetType().Name);
        }

        if (originalItemBytes is null)
        {
            return LegacyFrozenGrottoKeyEntryCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                "player-items-snapshot-missing");
        }

        if (originalCharacterBytes is null)
        {
            return LegacyFrozenGrottoKeyEntryCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                "character-snapshot-missing");
        }

        if (!originalItemBytes.AsSpan().SequenceEqual(expectedItemBytes))
        {
            return LegacyFrozenGrottoKeyEntryCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                "persisted-items-do-not-match-live-session");
        }

        var updatedItems = ConsumeSingleLegacyInventoryItem(
            sourceSession.PlayerItems,
            key,
            out var itemMutationFrame);
        var sourceStackSize = key.StackSize == 0 ? (ushort)1 : key.StackSize;
        var updatedStackSize = sourceStackSize > 1 ? (ushort)(sourceStackSize - 1) : (ushort)0;
        var itemMutationPacket = sourceStackSize > 1
            ? "LegacyGameItem.Update"
            : "MsgItemUsage.RemoveInventory";

        var sourceCharacter = sourceSession.Character;
        var teleportReduction = ReduceLegacyTeleportDestination(
            context,
            sourceCharacter,
            new LegacyTeleportDestination(
                LegacyFrozenGrottoFifthFloorDestination.MapId,
                LegacyFrozenGrottoFifthFloorDestination.X,
                LegacyFrozenGrottoFifthFloorDestination.Y));
        var reducedCharacter = teleportReduction.FinalCharacter;
        var persistenceValues = CreateLegacyCommittedTeleportPersistenceValues(reducedCharacter);
        var updatedAttributes = CopyCharacterAttributes(reducedCharacter.Attributes);
        foreach (var value in persistenceValues)
        {
            updatedAttributes[value.Key] = value.Value;
        }

        var updatedCharacter = reducedCharacter with
        {
            Attributes = updatedAttributes,
        };
        var updatedSession = sourceSession with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
            ActiveNpcId = null,
        };

        try
        {
            PlayerItemSnapshotFilePersistence.WriteAllBytesAtomically(
                expectedItemPath,
                PlayerItemSnapshotLoader.ToLegacyBytes(updatedItems));
        }
        catch (Exception exception) when (IsLegacyFrozenGrottoMutationInfrastructureException(exception))
        {
            TryRollbackLegacyFrozenGrottoFiles(
                expectedItemPath,
                originalItemBytes,
                expectedCharacterPath,
                originalCharacterBytes,
                restoreCharacter: false,
                out var rollbackReason);
            return LegacyFrozenGrottoKeyEntryCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                $"item-persistence-{exception.GetType().Name}; rollback={rollbackReason}");
        }

        bool characterPersisted;
        string characterPersistenceReason;
        try
        {
            characterPersisted = CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(
                expectedCharacterPath,
                sourceCharacter.Uid,
                persistenceValues,
                detailedIoFailures: true,
                out characterPersistenceReason);
        }
        catch (Exception exception) when (IsLegacyFrozenGrottoMutationInfrastructureException(exception))
        {
            characterPersisted = false;
            characterPersistenceReason = exception.GetType().Name;
        }

        if (!characterPersisted)
        {
            TryRollbackLegacyFrozenGrottoFiles(
                expectedItemPath,
                originalItemBytes,
                expectedCharacterPath,
                originalCharacterBytes,
                restoreCharacter: true,
                out var rollbackReason);
            return LegacyFrozenGrottoKeyEntryCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                $"character-persistence-{characterPersistenceReason}; rollback={rollbackReason}");
        }

        bool sessionCommitted;
        try
        {
            sessionCommitted = sessions.TryCompareAndSet(context.Connection, sourceSession, updatedSession);
        }
        catch (WorldSessionPublicationException)
        {
            // Published outcomes never enter compensating rollback.
            throw;
        }
        catch (Exception exception) when (IsLegacyFrozenGrottoMutationInfrastructureException(exception))
        {
            TryRollbackLegacyFrozenGrottoFiles(
                expectedItemPath,
                originalItemBytes,
                expectedCharacterPath,
                originalCharacterBytes,
                restoreCharacter: true,
                out var rollbackReason);
            return LegacyFrozenGrottoKeyEntryCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                $"session-commit-{exception.GetType().Name}; rollback={rollbackReason}");
        }

        if (!sessionCommitted)
        {
            TryRollbackLegacyFrozenGrottoFiles(
                expectedItemPath,
                originalItemBytes,
                expectedCharacterPath,
                originalCharacterBytes,
                restoreCharacter: true,
                out var rollbackReason);
            return LegacyFrozenGrottoKeyEntryCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                "session-changed-before-commit; rollback=" + rollbackReason);
        }

        return LegacyFrozenGrottoKeyEntryCommit.Succeeded(
            sourceSession,
            updatedSession,
            key.Uid,
            sourceStackSize,
            updatedStackSize,
            itemMutationFrame,
            itemMutationPacket,
            expectedItemPath,
            expectedCharacterPath);
    }

    private static bool HasUnboundLegacyFrozenGrottoItem(PlayerItemSnapshot items, uint itemId)
    {
        return items.InventoryItems.Any(item => item.ItemId == itemId && item.Bound == 0);
    }

    private static ValueTask SendLegacyFrozenGrottoShadowJadeInventoryFullAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        return SendLegacyNpcDialogAsync(
            context,
            LegacyFrozenGrottoGuardian5SourceMethod,
            ["Please make 1 space in your inventory."],
            [new LegacyNpcDialogOption("Ah~yes.", LegacyNpcClosedOption)],
            LegacyFrozenGrottoItemAvatarId,
            cancellationToken);
    }

    private static ValueTask SendLegacyFrozenGrottoShadowJadeAlreadyOwnedAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        return SendLegacyNpcDialogAsync(
            context,
            LegacyFrozenGrottoGuardian5SourceMethod,
            ["Sorry, but you already have Shadow Jade."],
            [new LegacyNpcDialogOption("Ah~yes.", LegacyNpcClosedOption)],
            LegacyFrozenGrottoItemAvatarId,
            cancellationToken);
    }

    private static ValueTask SendLegacyFrozenGrottoMissingKeyAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        return SendLegacyNpcDialogAsync(
            context,
            LegacyFrozenFroto4TeleporterSourceMethod,
            ["Sorry, you do not have the Grotto Key in your inventory. Kill monsters to obtain that key."],
            [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
            LegacyFrozenGrottoAvatarId,
            cancellationToken);
    }

    private static bool TryResolveLegacyFrozenGrottoItemPath(
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
        catch (Exception exception) when (IsLegacyFrozenGrottoMutationInfrastructureException(exception))
        {
            reason = "item-path-" + exception.GetType().Name;
            return false;
        }
    }

    private static bool TryResolveLegacyFrozenGrottoMutationPaths(
        WorldSession session,
        string? playerItemsRoot,
        out string itemPath,
        out string characterPath,
        out string reason)
    {
        characterPath = string.Empty;
        if (!TryResolveLegacyFrozenGrottoItemPath(session, playerItemsRoot, out itemPath, out reason))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(session.UsersRoot))
        {
            reason = "users-root-missing";
            return false;
        }

        try
        {
            characterPath = Path.GetFullPath(Path.Combine(
                session.UsersRoot,
                session.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini"));
            return true;
        }
        catch (Exception exception) when (IsLegacyFrozenGrottoMutationInfrastructureException(exception))
        {
            reason = "character-path-" + exception.GetType().Name;
            return false;
        }
    }

    private static bool IsExpectedLegacyFrozenGrottoSession(WorldSession actual, WorldSession expected)
    {
        return actual.AccountId == expected.AccountId &&
            actual.Character.Uid == expected.Character.Uid;
    }

    private static bool LegacyFrozenGrottoPathEquals(string left, string right)
    {
        return string.Equals(
            left,
            right,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static bool TryRollbackLegacyFrozenGrottoItems(
        string itemPath,
        byte[] originalItemBytes,
        out string reason)
    {
        try
        {
            PlayerItemSnapshotFilePersistence.WriteAllBytesAtomically(itemPath, originalItemBytes);
            reason = "complete";
            return true;
        }
        catch (Exception exception) when (IsLegacyFrozenGrottoMutationInfrastructureException(exception))
        {
            reason = "failed:" + exception.GetType().Name;
            return false;
        }
    }

    private static bool TryRollbackLegacyFrozenGrottoFiles(
        string itemPath,
        byte[] originalItemBytes,
        string characterPath,
        byte[] originalCharacterBytes,
        bool restoreCharacter,
        out string reason)
    {
        var failures = new List<string>();
        if (restoreCharacter)
        {
            try
            {
                SnapshotFilePersistence.WriteAllBytesAtomically(
                    characterPath,
                    originalCharacterBytes,
                    LegacyFrozenGrottoMutationRollbackMarker);
            }
            catch (Exception exception) when (IsLegacyFrozenGrottoMutationInfrastructureException(exception))
            {
                failures.Add("character:" + exception.GetType().Name);
            }
        }

        if (!TryRollbackLegacyFrozenGrottoItems(itemPath, originalItemBytes, out var itemReason))
        {
            failures.Add("items:" + itemReason);
        }

        reason = failures.Count == 0 ? "complete" : "failed:" + string.Join(',', failures);
        return failures.Count == 0;
    }

    private static bool IsLegacyFrozenGrottoMutationInfrastructureException(Exception exception)
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

    private static Task LogLegacyFrozenGrottoMutationFailureAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        string sourceMethod,
        string sourceLines,
        string reason)
    {
        return context.Log.WriteLineAsync(
            "WorldServer skipped LegacyNpc Frozen Grotto mutation " +
            $"{FormatSession(context with { Session = session })} source={sourceMethod} sourceLines={sourceLines} " +
            $"npcId={npcId} option={optionId} reason={reason}; " +
            "session was not published and no success packets were sent.");
    }

    private enum LegacyFrozenGrottoMutationCommitStatus
    {
        Success,
        InventoryFull,
        AlreadyOwned,
        MissingKey,
        Failure,
    }

    private sealed record LegacyFrozenGrottoShadowJadeCommit(
        LegacyFrozenGrottoMutationCommitStatus Status,
        WorldSession? SourceSession,
        WorldSession? UpdatedSession,
        PlayerItemSnapshotItem? GrantedItem,
        string ItemPath,
        string Reason)
    {
        public static LegacyFrozenGrottoShadowJadeCommit Succeeded(
            WorldSession sourceSession,
            WorldSession updatedSession,
            PlayerItemSnapshotItem grantedItem,
            string itemPath)
        {
            return new(
                LegacyFrozenGrottoMutationCommitStatus.Success,
                sourceSession,
                updatedSession,
                grantedItem,
                itemPath,
                string.Empty);
        }

        public static LegacyFrozenGrottoShadowJadeCommit InventoryFull(
            WorldSession sourceSession,
            string itemPath)
        {
            return Empty(LegacyFrozenGrottoMutationCommitStatus.InventoryFull, sourceSession, itemPath, string.Empty);
        }

        public static LegacyFrozenGrottoShadowJadeCommit AlreadyOwned(
            WorldSession sourceSession,
            string itemPath)
        {
            return Empty(LegacyFrozenGrottoMutationCommitStatus.AlreadyOwned, sourceSession, itemPath, string.Empty);
        }

        public static LegacyFrozenGrottoShadowJadeCommit Failed(
            WorldSession? sourceSession,
            string itemPath,
            string reason)
        {
            return Empty(LegacyFrozenGrottoMutationCommitStatus.Failure, sourceSession, itemPath, reason);
        }

        private static LegacyFrozenGrottoShadowJadeCommit Empty(
            LegacyFrozenGrottoMutationCommitStatus status,
            WorldSession? sourceSession,
            string itemPath,
            string reason)
        {
            return new(status, sourceSession, null, null, itemPath, reason);
        }
    }

    private sealed record LegacyFrozenGrottoKeyEntryCommit(
        LegacyFrozenGrottoMutationCommitStatus Status,
        WorldSession? SourceSession,
        WorldSession? UpdatedSession,
        uint KeyUid,
        ushort SourceStackSize,
        ushort UpdatedStackSize,
        byte[] ItemMutationFrame,
        string ItemMutationPacket,
        string ItemPath,
        string CharacterPath,
        string Reason)
    {
        public static LegacyFrozenGrottoKeyEntryCommit Succeeded(
            WorldSession sourceSession,
            WorldSession updatedSession,
            uint keyUid,
            ushort sourceStackSize,
            ushort updatedStackSize,
            byte[] itemMutationFrame,
            string itemMutationPacket,
            string itemPath,
            string characterPath)
        {
            return new(
                LegacyFrozenGrottoMutationCommitStatus.Success,
                sourceSession,
                updatedSession,
                keyUid,
                sourceStackSize,
                updatedStackSize,
                itemMutationFrame,
                itemMutationPacket,
                itemPath,
                characterPath,
                string.Empty);
        }

        public static LegacyFrozenGrottoKeyEntryCommit MissingKey(
            WorldSession sourceSession,
            string itemPath,
            string characterPath)
        {
            return Empty(
                LegacyFrozenGrottoMutationCommitStatus.MissingKey,
                sourceSession,
                itemPath,
                characterPath,
                string.Empty);
        }

        public static LegacyFrozenGrottoKeyEntryCommit Failed(
            WorldSession? sourceSession,
            string itemPath,
            string characterPath,
            string reason)
        {
            return Empty(
                LegacyFrozenGrottoMutationCommitStatus.Failure,
                sourceSession,
                itemPath,
                characterPath,
                reason);
        }

        private static LegacyFrozenGrottoKeyEntryCommit Empty(
            LegacyFrozenGrottoMutationCommitStatus status,
            WorldSession? sourceSession,
            string itemPath,
            string characterPath,
            string reason)
        {
            return new(
                status,
                sourceSession,
                null,
                KeyUid: 0,
                SourceStackSize: 0,
                UpdatedStackSize: 0,
                [],
                string.Empty,
                itemPath,
                characterPath,
                reason);
        }
    }
}
