using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacySimonEntrySourceMethod = "NpcHandler.Simon";
    private const string LegacySimonVirtueEntrySourceLines = "NpcHandler.cs:34322-34334";
    private const string LegacySimonMeteorEntrySourceLines = "NpcHandler.cs:34335-34345";
    private const string LegacySimonEntryRollbackMarker = ".legacy-simon-entry-rollback-";
    private const uint LegacySimonVirtueEntryCost = 2_000;
    private const uint LegacySimonMeteorEntryCount = 5;
    private const uint LegacySimonMeteorScrollItemId = 1_088_002;
    private const LegacyMsgUpdateDataType LegacySimonVirtutePointsUpdateType =
        (LegacyMsgUpdateDataType)0x17;

    private static async ValueTask<bool> TryApplyLegacySimonVirtueEntryMutationAsync(
        WorldPacketHandlerContext context,
        WorldSession expectedSession,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Sessions is not { } sessions)
        {
            await LogLegacySimonEntryFailureAsync(
                    context,
                    expectedSession,
                    optionId: 6,
                    LegacySimonVirtueEntrySourceLines,
                    "session-store-unavailable")
                .ConfigureAwait(false);
            return true;
        }

        if (!sessions.TryGet(context.Connection, out var initialSession) || initialSession is null)
        {
            await LogLegacySimonEntryFailureAsync(
                    context,
                    expectedSession,
                    optionId: 6,
                    LegacySimonVirtueEntrySourceLines,
                    "session-missing-before-lock")
                .ConfigureAwait(false);
            return true;
        }

        if (!IsExpectedLegacySimonEntrySession(initialSession, expectedSession))
        {
            await LogLegacySimonEntryFailureAsync(
                    context,
                    initialSession,
                    optionId: 6,
                    LegacySimonVirtueEntrySourceLines,
                    "session-identity-changed-before-lock")
                .ConfigureAwait(false);
            return true;
        }

        if (initialSession.ActiveNpcId != LegacySimonNpcId)
        {
            await LogLegacySimonEntryFailureAsync(
                    context,
                    initialSession,
                    optionId: 6,
                    LegacySimonVirtueEntrySourceLines,
                    $"active-npc-mismatch:{initialSession.ActiveNpcId.GetValueOrDefault()}; stale-option-replay-fenced")
                .ConfigureAwait(false);
            return true;
        }

        if (ReadLegacySimonVirtutePoints(initialSession.Character) <= LegacySimonVirtueEntryCost)
        {
            await SendLegacySimonVirtueEntryMissingAsync(
                    context with { Session = initialSession },
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!TryResolveLegacySimonEntryCharacterPath(
                initialSession,
                out var characterPath,
                out var pathReason))
        {
            await LogLegacySimonEntryFailureAsync(
                    context,
                    initialSession,
                    optionId: 6,
                    LegacySimonVirtueEntrySourceLines,
                    pathReason)
                .ConfigureAwait(false);
            return true;
        }

        var preflight = await PrepareLegacyTeleportAsync(
                context,
                initialSession,
                new LegacyTeleportRequest(
                    LegacySimonEntrySourceMethod,
                    LegacyLabyrinthEntranceDestination.MapId),
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflight.Allowed)
        {
            return true;
        }

        initialSession = preflight.Session;

        LegacySimonVirtueEntryCommit commit;
        try
        {
            commit = CharacterSnapshotFileCoordinator.ExecuteLocked(
                characterPath,
                () => TryCommitLegacySimonVirtueEntry(
                    context,
                    sessions,
                    initialSession.AccountId,
                    initialSession.Character.Uid,
                    characterPath));
        }
        catch (Exception exception) when (IsLegacySimonEntryInfrastructureException(exception))
        {
            commit = LegacySimonVirtueEntryCommit.Failed(
                initialSession,
                characterPath,
                "transaction-" + exception.GetType().Name);
        }

        if (commit.Status == LegacySimonEntryCommitStatus.InsufficientPayment)
        {
            await SendLegacySimonVirtueEntryMissingAsync(
                    context with { Session = commit.SourceSession ?? initialSession },
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (commit.Status != LegacySimonEntryCommitStatus.Success ||
            commit.SourceSession is not { } sourceSession ||
            commit.UpdatedSession is not { } updatedSession)
        {
            await LogLegacySimonEntryFailureAsync(
                    context,
                    commit.SourceSession ?? initialSession,
                    optionId: 6,
                    LegacySimonVirtueEntrySourceLines,
                    commit.Reason)
                .ConfigureAwait(false);
            return true;
        }

        await context.Connection.SendAsync(
                CreateLegacySimonVirtutePointsUpdateFrame(
                    updatedSession.Character,
                    commit.UpdatedVirtutePoints),
                cancellationToken)
            .ConfigureAwait(false);
        var teleportSourceSession = CreateLegacyTeleportMutationSourceSession(
            sourceSession,
            updatedSession);
        var appliedSession = await ExecuteLegacyTeleportDestinationAsync(
                context with { Session = teleportSourceSession },
                teleportSourceSession,
                new LegacyTeleportDestination(
                    LegacyLabyrinthEntranceDestination.MapId,
                    LegacyLabyrinthEntranceDestination.X,
                    LegacyLabyrinthEntranceDestination.Y),
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
                "WorldServer applied LegacyNpc Simon virtue entry " +
                $"{FormatSession(updatedContext)} source={LegacySimonEntrySourceMethod} " +
                $"sourceLines={LegacySimonVirtueEntrySourceLines} npcId={LegacySimonNpcId} option=6 " +
                $"virtutePoints={commit.SourceVirtutePoints}->{commit.UpdatedVirtutePoints} " +
                $"map={sourceSession.Character.MapId}/{sourceSession.Character.X}/{sourceSession.Character.Y}" +
                $"->{appliedSession.Character.MapId}/{appliedSession.Character.X}/{appliedSession.Character.Y} " +
                $"characterFile={Path.GetFileName(commit.CharacterPath)} eligibility=VirtutePoints>2000 " +
                "persistenceBeforeSessionCommit=true activeNpcId=cleared dynamicId=0 " +
                "packetOrder=MsgUpdate.VirtutePoints,GameClient.Teleport.Leave,GameClient.Teleport.Transition.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryApplyLegacySimonMeteorEntryMutationAsync(
        WorldPacketHandlerContext context,
        WorldSession expectedSession,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Sessions is not { } sessions)
        {
            await LogLegacySimonEntryFailureAsync(
                    context,
                    expectedSession,
                    optionId: 7,
                    LegacySimonMeteorEntrySourceLines,
                    "session-store-unavailable")
                .ConfigureAwait(false);
            return true;
        }

        if (!sessions.TryGet(context.Connection, out var initialSession) || initialSession is null)
        {
            await LogLegacySimonEntryFailureAsync(
                    context,
                    expectedSession,
                    optionId: 7,
                    LegacySimonMeteorEntrySourceLines,
                    "session-missing-before-lock")
                .ConfigureAwait(false);
            return true;
        }

        if (!IsExpectedLegacySimonEntrySession(initialSession, expectedSession))
        {
            await LogLegacySimonEntryFailureAsync(
                    context,
                    initialSession,
                    optionId: 7,
                    LegacySimonMeteorEntrySourceLines,
                    "session-identity-changed-before-lock")
                .ConfigureAwait(false);
            return true;
        }

        if (initialSession.ActiveNpcId != LegacySimonNpcId)
        {
            await LogLegacySimonEntryFailureAsync(
                    context,
                    initialSession,
                    optionId: 7,
                    LegacySimonMeteorEntrySourceLines,
                    $"active-npc-mismatch:{initialSession.ActiveNpcId.GetValueOrDefault()}; stale-option-replay-fenced")
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacySimonMeteorEntryPayment(initialSession.PlayerItems))
        {
            await SendLegacySimonMeteorEntryMissingAsync(
                    context with { Session = initialSession },
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!TryResolveLegacySimonMeteorEntryPaths(
                initialSession,
                context.PlayerItemsRoot,
                out var itemPath,
                out var characterPath,
                out var pathReason))
        {
            await LogLegacySimonEntryFailureAsync(
                    context,
                    initialSession,
                    optionId: 7,
                    LegacySimonMeteorEntrySourceLines,
                    pathReason)
                .ConfigureAwait(false);
            return true;
        }

        var preflight = await PrepareLegacyTeleportAsync(
                context,
                initialSession,
                new LegacyTeleportRequest(
                    LegacySimonEntrySourceMethod,
                    LegacyLabyrinthEntranceDestination.MapId),
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflight.Allowed)
        {
            return true;
        }

        initialSession = preflight.Session;

        LegacySimonMeteorEntryCommit commit;
        try
        {
            commit = CharacterSnapshotFileCoordinator.ExecuteLocked(
                [itemPath, characterPath],
                () => TryCommitLegacySimonMeteorEntry(
                    context,
                    sessions,
                    initialSession.AccountId,
                    initialSession.Character.Uid,
                    itemPath,
                    characterPath));
        }
        catch (Exception exception) when (IsLegacySimonEntryInfrastructureException(exception))
        {
            commit = LegacySimonMeteorEntryCommit.Failed(
                initialSession,
                itemPath,
                characterPath,
                "transaction-" + exception.GetType().Name);
        }

        if (commit.Status == LegacySimonEntryCommitStatus.InsufficientPayment)
        {
            await SendLegacySimonMeteorEntryMissingAsync(
                    context with { Session = commit.SourceSession ?? initialSession },
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (commit.Status != LegacySimonEntryCommitStatus.Success ||
            commit.SourceSession is not { } sourceSession ||
            commit.UpdatedSession is not { } updatedSession)
        {
            await LogLegacySimonEntryFailureAsync(
                    context,
                    commit.SourceSession ?? initialSession,
                    optionId: 7,
                    LegacySimonMeteorEntrySourceLines,
                    commit.Reason)
                .ConfigureAwait(false);
            return true;
        }

        foreach (var itemMutationFrame in commit.ItemMutationFrames)
        {
            await context.Connection.SendAsync(itemMutationFrame, cancellationToken).ConfigureAwait(false);
        }

        var teleportSourceSession = CreateLegacyTeleportMutationSourceSession(
            sourceSession,
            updatedSession);
        var appliedSession = await ExecuteLegacyTeleportDestinationAsync(
                context with { Session = teleportSourceSession },
                teleportSourceSession,
                new LegacyTeleportDestination(
                    LegacyLabyrinthEntranceDestination.MapId,
                    LegacyLabyrinthEntranceDestination.X,
                    LegacyLabyrinthEntranceDestination.Y),
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
                "WorldServer applied LegacyNpc Simon meteor entry " +
                $"{FormatSession(updatedContext)} source={LegacySimonEntrySourceMethod} " +
                $"sourceLines={LegacySimonMeteorEntrySourceLines} npcId={LegacySimonNpcId} option=7 " +
                $"itemOperations={string.Join(',', commit.ItemOperations.Select(FormatLegacySimonMeteorOperation))} " +
                $"map={sourceSession.Character.MapId}/{sourceSession.Character.X}/{sourceSession.Character.Y}" +
                $"->{appliedSession.Character.MapId}/{appliedSession.Character.X}/{appliedSession.Character.Y} " +
                $"itemFile={Path.GetFileName(commit.ItemPath)} characterFile={Path.GetFileName(commit.CharacterPath)} " +
                "eligibility=itemId-1088001-or-1088002-any-binding-stack-total>=5 " +
                "removalRule=five-source-Update-REMOVE-operations-lowest-uid-per-pass " +
                "persistenceOrder=items,character,session activeNpcId=cleared dynamicId=0 " +
                "packetOrder=five-item-mutation-frames,GameClient.Teleport.Leave,GameClient.Teleport.Transition.")
            .ConfigureAwait(false);
        return true;
    }

    private static LegacySimonVirtueEntryCommit TryCommitLegacySimonVirtueEntry(
        WorldPacketHandlerContext context,
        WorldSessionStore sessions,
        long expectedAccountId,
        long expectedCharacterUid,
        string expectedCharacterPath)
    {
        if (!sessions.TryGet(context.Connection, out var sourceSession) || sourceSession is null)
        {
            return LegacySimonVirtueEntryCommit.Failed(
                null,
                expectedCharacterPath,
                "session-missing-after-lock");
        }

        if (sourceSession.AccountId != expectedAccountId ||
            sourceSession.Character.Uid != expectedCharacterUid)
        {
            return LegacySimonVirtueEntryCommit.Failed(
                sourceSession,
                expectedCharacterPath,
                "session-identity-changed-after-lock");
        }

        if (sourceSession.ActiveNpcId != LegacySimonNpcId)
        {
            return LegacySimonVirtueEntryCommit.Failed(
                sourceSession,
                expectedCharacterPath,
                $"active-npc-mismatch:{sourceSession.ActiveNpcId.GetValueOrDefault()}; stale-option-replay-fenced");
        }

        if (!TryResolveLegacySimonEntryCharacterPath(
                sourceSession,
                out var currentCharacterPath,
                out var pathReason))
        {
            return LegacySimonVirtueEntryCommit.Failed(
                sourceSession,
                expectedCharacterPath,
                pathReason);
        }

        if (!LegacySimonEntryPathEquals(currentCharacterPath, expectedCharacterPath))
        {
            return LegacySimonVirtueEntryCommit.Failed(
                sourceSession,
                expectedCharacterPath,
                "session-character-path-changed-after-lock");
        }

        var sourceVirtutePoints = ReadLegacySimonVirtutePoints(sourceSession.Character);
        if (sourceVirtutePoints <= LegacySimonVirtueEntryCost)
        {
            return LegacySimonVirtueEntryCommit.Insufficient(
                sourceSession,
                expectedCharacterPath,
                sourceVirtutePoints);
        }

        byte[]? originalCharacterBytes;
        try
        {
            originalCharacterBytes = SnapshotFilePersistence.ReadAllBytesIfExists(expectedCharacterPath);
        }
        catch (Exception exception) when (IsLegacySimonEntryInfrastructureException(exception))
        {
            return LegacySimonVirtueEntryCommit.Failed(
                sourceSession,
                expectedCharacterPath,
                "baseline-read-" + exception.GetType().Name);
        }

        if (originalCharacterBytes is null)
        {
            return LegacySimonVirtueEntryCommit.Failed(
                sourceSession,
                expectedCharacterPath,
                "character-snapshot-missing");
        }

        var updatedVirtutePoints = checked(sourceVirtutePoints - LegacySimonVirtueEntryCost);
        var teleportReduction = ReduceLegacyTeleportDestination(
            context,
            sourceSession.Character,
            new LegacyTeleportDestination(
                LegacyLabyrinthEntranceDestination.MapId,
                LegacyLabyrinthEntranceDestination.X,
                LegacyLabyrinthEntranceDestination.Y));
        var reducedCharacter = teleportReduction.FinalCharacter;
        var persistenceValues = CreateLegacySimonEntryPersistenceValues(
            reducedCharacter,
            updatedVirtutePoints);
        var updatedCharacter = CreateLegacySimonEntryCharacter(
            reducedCharacter,
            persistenceValues);
        var updatedSession = sourceSession with
        {
            Character = updatedCharacter,
            ActiveNpcId = null,
        };

        bool characterPersisted;
        string characterPersistenceReason;
        try
        {
            characterPersisted = CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(
                expectedCharacterPath,
                sourceSession.Character.Uid,
                persistenceValues,
                detailedIoFailures: true,
                out characterPersistenceReason);
        }
        catch (Exception exception) when (IsLegacySimonEntryInfrastructureException(exception))
        {
            characterPersisted = false;
            characterPersistenceReason = exception.GetType().Name;
        }

        if (!characterPersisted)
        {
            TryRollbackLegacySimonEntryCharacter(
                expectedCharacterPath,
                originalCharacterBytes,
                out var rollbackReason);
            return LegacySimonVirtueEntryCommit.Failed(
                sourceSession,
                expectedCharacterPath,
                $"character-persistence-{characterPersistenceReason}; rollback={rollbackReason}");
        }

        bool sessionCommitted;
        try
        {
            sessionCommitted = sessions.TryCompareAndSet(context.Connection, sourceSession, updatedSession);
        }
        catch (Exception exception) when (IsLegacySimonEntryInfrastructureException(exception))
        {
            if (sessions.TryGet(context.Connection, out var publishedSession) &&
                ReferenceEquals(publishedSession, updatedSession))
            {
                return LegacySimonVirtueEntryCommit.Succeeded(
                    sourceSession,
                    updatedSession,
                    sourceVirtutePoints,
                    updatedVirtutePoints,
                    expectedCharacterPath);
            }

            TryRollbackLegacySimonEntryCharacter(
                expectedCharacterPath,
                originalCharacterBytes,
                out var rollbackReason);
            return LegacySimonVirtueEntryCommit.Failed(
                sourceSession,
                expectedCharacterPath,
                $"session-commit-{exception.GetType().Name}; rollback={rollbackReason}");
        }

        if (!sessionCommitted)
        {
            TryRollbackLegacySimonEntryCharacter(
                expectedCharacterPath,
                originalCharacterBytes,
                out var rollbackReason);
            return LegacySimonVirtueEntryCommit.Failed(
                sourceSession,
                expectedCharacterPath,
                "session-changed-before-commit; rollback=" + rollbackReason);
        }

        return LegacySimonVirtueEntryCommit.Succeeded(
            sourceSession,
            updatedSession,
            sourceVirtutePoints,
            updatedVirtutePoints,
            expectedCharacterPath);
    }

    private static LegacySimonMeteorEntryCommit TryCommitLegacySimonMeteorEntry(
        WorldPacketHandlerContext context,
        WorldSessionStore sessions,
        long expectedAccountId,
        long expectedCharacterUid,
        string expectedItemPath,
        string expectedCharacterPath)
    {
        if (!sessions.TryGet(context.Connection, out var sourceSession) || sourceSession is null)
        {
            return LegacySimonMeteorEntryCommit.Failed(
                null,
                expectedItemPath,
                expectedCharacterPath,
                "session-missing-after-lock");
        }

        if (sourceSession.AccountId != expectedAccountId ||
            sourceSession.Character.Uid != expectedCharacterUid)
        {
            return LegacySimonMeteorEntryCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                "session-identity-changed-after-lock");
        }

        if (sourceSession.ActiveNpcId != LegacySimonNpcId)
        {
            return LegacySimonMeteorEntryCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                $"active-npc-mismatch:{sourceSession.ActiveNpcId.GetValueOrDefault()}; stale-option-replay-fenced");
        }

        if (!TryResolveLegacySimonMeteorEntryPaths(
                sourceSession,
                context.PlayerItemsRoot,
                out var currentItemPath,
                out var currentCharacterPath,
                out var pathReason))
        {
            return LegacySimonMeteorEntryCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                pathReason);
        }

        if (!LegacySimonEntryPathEquals(currentItemPath, expectedItemPath) ||
            !LegacySimonEntryPathEquals(currentCharacterPath, expectedCharacterPath))
        {
            return LegacySimonMeteorEntryCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                "session-snapshot-path-changed-after-lock");
        }

        if (!HasLegacySimonMeteorEntryPayment(sourceSession.PlayerItems))
        {
            return LegacySimonMeteorEntryCommit.Insufficient(
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
        catch (Exception exception) when (IsLegacySimonEntryInfrastructureException(exception))
        {
            return LegacySimonMeteorEntryCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                "baseline-read-" + exception.GetType().Name);
        }

        if (originalItemBytes is null)
        {
            return LegacySimonMeteorEntryCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                "player-items-snapshot-missing");
        }

        if (originalCharacterBytes is null)
        {
            return LegacySimonMeteorEntryCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                "character-snapshot-missing");
        }

        if (!originalItemBytes.AsSpan().SequenceEqual(expectedItemBytes))
        {
            return LegacySimonMeteorEntryCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                "persisted-items-do-not-match-live-session");
        }

        if (!TryCreateLegacySimonMeteorEntryMutation(
                sourceSession.PlayerItems,
                out var updatedItems,
                out var itemMutationFrames,
                out var itemOperations,
                out var mutationReason))
        {
            return LegacySimonMeteorEntryCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                mutationReason);
        }

        var teleportReduction = ReduceLegacyTeleportDestination(
            context,
            sourceSession.Character,
            new LegacyTeleportDestination(
                LegacyLabyrinthEntranceDestination.MapId,
                LegacyLabyrinthEntranceDestination.X,
                LegacyLabyrinthEntranceDestination.Y));
        var reducedCharacter = teleportReduction.FinalCharacter;
        var persistenceValues = CreateLegacySimonEntryPersistenceValues(
            reducedCharacter,
            updatedVirtutePoints: null);
        var updatedCharacter = CreateLegacySimonEntryCharacter(
            reducedCharacter,
            persistenceValues);
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
        catch (Exception exception) when (IsLegacySimonEntryInfrastructureException(exception))
        {
            TryRollbackLegacySimonEntryFiles(
                expectedItemPath,
                originalItemBytes,
                expectedCharacterPath,
                originalCharacterBytes,
                restoreCharacter: false,
                out var rollbackReason);
            return LegacySimonMeteorEntryCommit.Failed(
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
                sourceSession.Character.Uid,
                persistenceValues,
                detailedIoFailures: true,
                out characterPersistenceReason);
        }
        catch (Exception exception) when (IsLegacySimonEntryInfrastructureException(exception))
        {
            characterPersisted = false;
            characterPersistenceReason = exception.GetType().Name;
        }

        if (!characterPersisted)
        {
            TryRollbackLegacySimonEntryFiles(
                expectedItemPath,
                originalItemBytes,
                expectedCharacterPath,
                originalCharacterBytes,
                restoreCharacter: true,
                out var rollbackReason);
            return LegacySimonMeteorEntryCommit.Failed(
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
        catch (Exception exception) when (IsLegacySimonEntryInfrastructureException(exception))
        {
            if (sessions.TryGet(context.Connection, out var publishedSession) &&
                ReferenceEquals(publishedSession, updatedSession))
            {
                return LegacySimonMeteorEntryCommit.Succeeded(
                    sourceSession,
                    updatedSession,
                    itemMutationFrames,
                    itemOperations,
                    expectedItemPath,
                    expectedCharacterPath);
            }

            TryRollbackLegacySimonEntryFiles(
                expectedItemPath,
                originalItemBytes,
                expectedCharacterPath,
                originalCharacterBytes,
                restoreCharacter: true,
                out var rollbackReason);
            return LegacySimonMeteorEntryCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                $"session-commit-{exception.GetType().Name}; rollback={rollbackReason}");
        }

        if (!sessionCommitted)
        {
            TryRollbackLegacySimonEntryFiles(
                expectedItemPath,
                originalItemBytes,
                expectedCharacterPath,
                originalCharacterBytes,
                restoreCharacter: true,
                out var rollbackReason);
            return LegacySimonMeteorEntryCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                "session-changed-before-commit; rollback=" + rollbackReason);
        }

        return LegacySimonMeteorEntryCommit.Succeeded(
            sourceSession,
            updatedSession,
            itemMutationFrames,
            itemOperations,
            expectedItemPath,
            expectedCharacterPath);
    }

    private static bool TryCreateLegacySimonMeteorEntryMutation(
        PlayerItemSnapshot sourceItems,
        out PlayerItemSnapshot updatedItems,
        out IReadOnlyList<byte[]> itemMutationFrames,
        out IReadOnlyList<LegacySimonMeteorRemovalOperation> itemOperations,
        out string reason)
    {
        updatedItems = sourceItems;
        itemMutationFrames = [];
        itemOperations = [];
        reason = string.Empty;

        var inventoryItems = sourceItems.InventoryItems.ToList();
        var frames = new List<byte[]>((int)LegacySimonMeteorEntryCount);
        var operations = new List<LegacySimonMeteorRemovalOperation>((int)LegacySimonMeteorEntryCount);
        var operationCount = 0U;

        // Source Remove loops over the whole dictionary repeatedly and invokes Update(..., REMOVE)
        // once per matching entry per pass. UID ordering makes that otherwise unstable enumeration deterministic.
        while (operationCount < LegacySimonMeteorEntryCount)
        {
            var pass = inventoryItems
                .Where(IsLegacySimonMeteorOrScroll)
                .OrderBy(item => item.Uid)
                .ToArray();
            if (pass.Length == 0)
            {
                reason = $"meteor-removal-exhausted-after-{operationCount}-operations";
                return false;
            }

            foreach (var passItem in pass)
            {
                var index = inventoryItems.FindIndex(item => item.Uid == passItem.Uid);
                if (index < 0)
                {
                    continue;
                }

                var currentItem = inventoryItems[index];
                var sourceStackSize = currentItem.StackSize;
                ushort updatedStackSize;
                string packet;
                byte[] frame;
                if (sourceStackSize > 1 && currentItem.Position < 40)
                {
                    updatedStackSize = checked((ushort)(sourceStackSize - 1));
                    var updatedItem = currentItem.WithStackSize(updatedStackSize);
                    inventoryItems[index] = updatedItem;
                    packet = "LegacyGameItem.Update";
                    frame = LegacyGameItemPacket.FromSnapshotItem(
                        updatedItem,
                        mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes();
                }
                else
                {
                    updatedStackSize = 0;
                    inventoryItems.RemoveAt(index);
                    packet = "MsgItemUsage.RemoveInventory";
                    frame = CreateLegacySimonMeteorRemovalFrame(currentItem.Uid);
                }

                frames.Add(frame);
                operations.Add(new LegacySimonMeteorRemovalOperation(
                    currentItem.Uid,
                    currentItem.ItemId,
                    currentItem.Bound,
                    sourceStackSize,
                    updatedStackSize,
                    packet));
                operationCount++;
                if (operationCount == LegacySimonMeteorEntryCount)
                {
                    break;
                }
            }
        }

        updatedItems = sourceItems with { InventoryItems = inventoryItems.ToArray() };
        itemMutationFrames = frames;
        itemOperations = operations;
        return true;
    }

    private static bool HasLegacySimonMeteorEntryPayment(PlayerItemSnapshot items)
    {
        var available = 0U;
        foreach (var item in items.InventoryItems.Where(IsLegacySimonMeteorOrScroll))
        {
            // Decompiled Inventory.Contain(1088001, 5, 0) includes both IDs and all binding states.
            available += item.StackSize;
            if (available >= LegacySimonMeteorEntryCount)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLegacySimonMeteorOrScroll(PlayerItemSnapshotItem item)
    {
        return item.ItemId is LegacyLabyrinthMeteorItemId or LegacySimonMeteorScrollItemId;
    }

    private static byte[] CreateLegacySimonMeteorRemovalFrame(uint itemUid)
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

    private static byte[] CreateLegacySimonVirtutePointsUpdateFrame(
        CharacterSnapshot character,
        uint virtutePoints)
    {
        return new LegacyMsgUpdatePacket(
            checked((uint)character.Uid),
            [new LegacyMsgUpdateEntry(LegacySimonVirtutePointsUpdateType, virtutePoints)]).ToFrameBytes();
    }

    private static uint ReadLegacySimonVirtutePoints(CharacterSnapshot character)
    {
        return ReadAttributeUInt32(character.Attributes, "VirtutePoints", "VirtuePoints");
    }

    private static Dictionary<string, string> CreateLegacySimonEntryPersistenceValues(
        CharacterSnapshot teleportedCharacter,
        uint? updatedVirtutePoints)
    {
        var values = CreateLegacyCommittedTeleportPersistenceValues(teleportedCharacter);
        if (updatedVirtutePoints is { } value)
        {
            values["VirtutePoints"] = value.ToString(CultureInfo.InvariantCulture);
        }

        return values;
    }

    private static CharacterSnapshot CreateLegacySimonEntryCharacter(
        CharacterSnapshot teleportedCharacter,
        IReadOnlyDictionary<string, string> persistenceValues)
    {
        var updatedAttributes = CopyCharacterAttributes(teleportedCharacter.Attributes);
        foreach (var value in persistenceValues)
        {
            updatedAttributes[value.Key] = value.Value;
        }

        return teleportedCharacter with
        {
            Attributes = updatedAttributes,
        };
    }

    private static bool TryResolveLegacySimonEntryCharacterPath(
        WorldSession session,
        out string characterPath,
        out string reason)
    {
        characterPath = string.Empty;
        reason = string.Empty;
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
        catch (Exception exception) when (IsLegacySimonEntryInfrastructureException(exception))
        {
            reason = "character-path-" + exception.GetType().Name;
            return false;
        }
    }

    private static bool TryResolveLegacySimonMeteorEntryPaths(
        WorldSession session,
        string? playerItemsRoot,
        out string itemPath,
        out string characterPath,
        out string reason)
    {
        itemPath = string.Empty;
        if (!TryResolveLegacySimonEntryCharacterPath(session, out characterPath, out reason))
        {
            return false;
        }

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
        catch (Exception exception) when (IsLegacySimonEntryInfrastructureException(exception))
        {
            reason = "item-path-" + exception.GetType().Name;
            return false;
        }
    }

    private static bool IsExpectedLegacySimonEntrySession(
        WorldSession actual,
        WorldSession expected)
    {
        return actual.AccountId == expected.AccountId &&
            actual.Character.Uid == expected.Character.Uid;
    }

    private static bool LegacySimonEntryPathEquals(string left, string right)
    {
        return string.Equals(
            left,
            right,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static bool TryRollbackLegacySimonEntryCharacter(
        string characterPath,
        byte[] originalCharacterBytes,
        out string reason)
    {
        try
        {
            SnapshotFilePersistence.WriteAllBytesAtomically(
                characterPath,
                originalCharacterBytes,
                LegacySimonEntryRollbackMarker);
            reason = "complete";
            return true;
        }
        catch (Exception exception) when (IsLegacySimonEntryInfrastructureException(exception))
        {
            reason = "failed:" + exception.GetType().Name;
            return false;
        }
    }

    private static bool TryRollbackLegacySimonEntryFiles(
        string itemPath,
        byte[] originalItemBytes,
        string characterPath,
        byte[] originalCharacterBytes,
        bool restoreCharacter,
        out string reason)
    {
        var failures = new List<string>();
        if (restoreCharacter &&
            !TryRollbackLegacySimonEntryCharacter(
                characterPath,
                originalCharacterBytes,
                out var characterReason))
        {
            failures.Add("character:" + characterReason);
        }

        try
        {
            PlayerItemSnapshotFilePersistence.WriteAllBytesAtomically(itemPath, originalItemBytes);
        }
        catch (Exception exception) when (IsLegacySimonEntryInfrastructureException(exception))
        {
            failures.Add("items:" + exception.GetType().Name);
        }

        reason = failures.Count == 0 ? "complete" : "failed:" + string.Join(',', failures);
        return failures.Count == 0;
    }

    private static bool IsLegacySimonEntryInfrastructureException(Exception exception)
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

    private static ValueTask SendLegacySimonVirtueEntryMissingAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        return SendLegacyNpcDialogAsync(
            context,
            LegacySimonEntrySourceMethod,
            ["Sorry, you don't have enough virtue points."],
            [new LegacyNpcDialogOption("Sigh...", LegacyNpcClosedOption)],
            LegacyLabyrinthQuestAvatarId,
            cancellationToken);
    }

    private static ValueTask SendLegacySimonMeteorEntryMissingAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        return SendLegacyNpcDialogAsync(
            context,
            LegacySimonEntrySourceMethod,
            ["Sorry, you don't have 5 Meteors."],
            [new LegacyNpcDialogOption("Sigh...", LegacyNpcClosedOption)],
            LegacyLabyrinthQuestAvatarId,
            cancellationToken);
    }

    private static Task LogLegacySimonEntryFailureAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string sourceLines,
        string reason)
    {
        return context.Log.WriteLineAsync(
            "PARITY-GAP WorldServer fenced parity failure for LegacyNpc Simon Labyrinth entry " +
            $"{FormatSession(context with { Session = session })} source={LegacySimonEntrySourceMethod} " +
            $"sourceLines={sourceLines} npcId={LegacySimonNpcId} option={optionId} reason={reason}; " +
            "session was not published and no payment/update/teleport success packets were sent.");
    }

    private static string FormatLegacySimonMeteorOperation(LegacySimonMeteorRemovalOperation operation)
    {
        return operation.ItemUid.ToString(CultureInfo.InvariantCulture) + ':' +
            operation.ItemId.ToString(CultureInfo.InvariantCulture) + ':' +
            operation.Bound.ToString(CultureInfo.InvariantCulture) + ':' +
            operation.SourceStackSize.ToString(CultureInfo.InvariantCulture) + "->" +
            operation.UpdatedStackSize.ToString(CultureInfo.InvariantCulture) + ':' +
            operation.Packet;
    }

    private enum LegacySimonEntryCommitStatus
    {
        Success,
        InsufficientPayment,
        Failure,
    }

    private readonly record struct LegacySimonMeteorRemovalOperation(
        uint ItemUid,
        uint ItemId,
        byte Bound,
        ushort SourceStackSize,
        ushort UpdatedStackSize,
        string Packet);

    private sealed record LegacySimonVirtueEntryCommit(
        LegacySimonEntryCommitStatus Status,
        WorldSession? SourceSession,
        WorldSession? UpdatedSession,
        uint SourceVirtutePoints,
        uint UpdatedVirtutePoints,
        string CharacterPath,
        string Reason)
    {
        public static LegacySimonVirtueEntryCommit Succeeded(
            WorldSession sourceSession,
            WorldSession updatedSession,
            uint sourceVirtutePoints,
            uint updatedVirtutePoints,
            string characterPath)
        {
            return new(
                LegacySimonEntryCommitStatus.Success,
                sourceSession,
                updatedSession,
                sourceVirtutePoints,
                updatedVirtutePoints,
                characterPath,
                string.Empty);
        }

        public static LegacySimonVirtueEntryCommit Insufficient(
            WorldSession sourceSession,
            string characterPath,
            uint sourceVirtutePoints)
        {
            return new(
                LegacySimonEntryCommitStatus.InsufficientPayment,
                sourceSession,
                null,
                sourceVirtutePoints,
                sourceVirtutePoints,
                characterPath,
                string.Empty);
        }

        public static LegacySimonVirtueEntryCommit Failed(
            WorldSession? sourceSession,
            string characterPath,
            string reason)
        {
            return new(
                LegacySimonEntryCommitStatus.Failure,
                sourceSession,
                null,
                0,
                0,
                characterPath,
                reason);
        }
    }

    private sealed record LegacySimonMeteorEntryCommit(
        LegacySimonEntryCommitStatus Status,
        WorldSession? SourceSession,
        WorldSession? UpdatedSession,
        IReadOnlyList<byte[]> ItemMutationFrames,
        IReadOnlyList<LegacySimonMeteorRemovalOperation> ItemOperations,
        string ItemPath,
        string CharacterPath,
        string Reason)
    {
        public static LegacySimonMeteorEntryCommit Succeeded(
            WorldSession sourceSession,
            WorldSession updatedSession,
            IReadOnlyList<byte[]> itemMutationFrames,
            IReadOnlyList<LegacySimonMeteorRemovalOperation> itemOperations,
            string itemPath,
            string characterPath)
        {
            return new(
                LegacySimonEntryCommitStatus.Success,
                sourceSession,
                updatedSession,
                itemMutationFrames,
                itemOperations,
                itemPath,
                characterPath,
                string.Empty);
        }

        public static LegacySimonMeteorEntryCommit Insufficient(
            WorldSession sourceSession,
            string itemPath,
            string characterPath)
        {
            return new(
                LegacySimonEntryCommitStatus.InsufficientPayment,
                sourceSession,
                null,
                [],
                [],
                itemPath,
                characterPath,
                string.Empty);
        }

        public static LegacySimonMeteorEntryCommit Failed(
            WorldSession? sourceSession,
            string itemPath,
            string characterPath,
            string reason)
        {
            return new(
                LegacySimonEntryCommitStatus.Failure,
                sourceSession,
                null,
                [],
                [],
                itemPath,
                characterPath,
                reason);
        }
    }
}
