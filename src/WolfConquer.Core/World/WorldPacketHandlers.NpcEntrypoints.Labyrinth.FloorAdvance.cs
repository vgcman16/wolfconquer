using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyLabyrinthFloorAdvanceRollbackMarker =
        ".legacy-labyrinth-floor-advance-rollback-";

    private static async ValueTask<bool> TryAdvanceLegacyLabyrinthFloorAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        LegacyLabyrinthFloor floor,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Sessions is not { } sessions)
        {
            await LogLegacyLabyrinthFloorAdvanceFailureAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    floor,
                    "session-store-unavailable")
                .ConfigureAwait(false);
            return true;
        }

        if (!sessions.TryGet(context.Connection, out var initialSession) || initialSession is null)
        {
            await LogLegacyLabyrinthFloorAdvanceFailureAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    floor,
                    "session-missing-before-lock")
                .ConfigureAwait(false);
            return true;
        }

        if (initialSession.AccountId != session.AccountId ||
            initialSession.Character.Uid != session.Character.Uid)
        {
            await LogLegacyLabyrinthFloorAdvanceFailureAsync(
                    context,
                    initialSession,
                    npcId,
                    optionId,
                    floor,
                    "session-identity-changed-before-lock")
                .ConfigureAwait(false);
            return true;
        }

        if (initialSession.ActiveNpcId != npcId)
        {
            await LogLegacyLabyrinthFloorAdvanceFailureAsync(
                    context,
                    initialSession,
                    npcId,
                    optionId,
                    floor,
                    $"active-npc-mismatch:{initialSession.ActiveNpcId.GetValueOrDefault()}; stale-option-replay-fenced")
                .ConfigureAwait(false);
            return true;
        }

        if (!HasUnboundLegacyLabyrinthFloorToken(initialSession.PlayerItems, floor.TokenItemId))
        {
            await SendLegacyLabyrinthFloorMissingTokenAsync(
                    context with { Session = initialSession },
                    floor,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!TryResolveLegacyLabyrinthFloorAdvancePaths(
                initialSession,
                context.PlayerItemsRoot,
                out var itemPath,
                out var characterPath,
                out var pathReason))
        {
            await LogLegacyLabyrinthFloorAdvanceFailureAsync(
                    context,
                    initialSession,
                    npcId,
                    optionId,
                    floor,
                    pathReason)
                .ConfigureAwait(false);
            return true;
        }

        var preflight = await PrepareLegacyTeleportAsync(
                context,
                initialSession,
                new LegacyTeleportRequest(floor.SourceMethod, floor.NextFloorDestination.MapId),
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflight.Allowed)
        {
            return true;
        }

        initialSession = preflight.Session;

        LegacyLabyrinthFloorAdvanceCommit commit;
        try
        {
            commit = CharacterSnapshotFileCoordinator.ExecuteLocked(
                [itemPath, characterPath],
                () => TryCommitLegacyLabyrinthFloorAdvance(
                    context,
                    sessions,
                    initialSession.AccountId,
                    initialSession.Character.Uid,
                    npcId,
                    floor,
                    itemPath,
                    characterPath));
        }
        catch (WorldSessionPublicationException failure)
        {
            await LogLegacyPublishedNpcFailureAsync(context, failure, "Labyrinth")
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (IsLegacyLabyrinthFloorAdvanceInfrastructureException(exception))
        {
            commit = LegacyLabyrinthFloorAdvanceCommit.Failed(
                initialSession,
                itemPath,
                characterPath,
                "transaction-" + exception.GetType().Name);
        }

        if (commit.Status == LegacyLabyrinthFloorAdvanceCommitStatus.MissingToken)
        {
            await SendLegacyLabyrinthFloorMissingTokenAsync(
                    context with { Session = commit.SourceSession ?? initialSession },
                    floor,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (commit.Status != LegacyLabyrinthFloorAdvanceCommitStatus.Success ||
            commit.SourceSession is not { } sourceSession ||
            commit.UpdatedSession is not { } updatedSession)
        {
            await LogLegacyLabyrinthFloorAdvanceFailureAsync(
                    context,
                    commit.SourceSession ?? initialSession,
                    npcId,
                    optionId,
                    floor,
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
                    floor.NextFloorDestination.MapId,
                    floor.NextFloorDestination.X,
                    floor.NextFloorDestination.Y),
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
                "WorldServer applied LegacyNpc Labyrinth floor advance " +
                $"{FormatSession(updatedContext)} source={floor.SourceMethod} sourceLines={floor.SourceLines} " +
                $"npcId={npcId} option={optionId} tokenId={floor.TokenItemId} tokenUid={commit.TokenUid} " +
                $"map={sourceSession.Character.MapId}/{sourceSession.Character.X}/{sourceSession.Character.Y}" +
                $"->{appliedSession.Character.MapId}/{appliedSession.Character.X}/{appliedSession.Character.Y} " +
                $"itemFile={Path.GetFileName(commit.ItemPath)} characterFile={Path.GetFileName(commit.CharacterPath)} " +
                $"tokenUnits={commit.SourceStackSize}->{commit.UpdatedStackSize} " +
                "eligibility=unbound-token consumeRule=lowest-uid-any-binding-one-unit " +
                "persistenceBeforeSessionCommit=true activeNpcId=cleared dynamicId=0 " +
                $"packetOrder={commit.ItemMutationPacket},GameClient.Teleport.Leave," +
                "GameClient.Teleport.Transition.")
            .ConfigureAwait(false);
        return true;
    }

    private static LegacyLabyrinthFloorAdvanceCommit TryCommitLegacyLabyrinthFloorAdvance(
        WorldPacketHandlerContext context,
        WorldSessionStore sessions,
        long expectedAccountId,
        long expectedCharacterUid,
        uint npcId,
        LegacyLabyrinthFloor floor,
        string expectedItemPath,
        string expectedCharacterPath)
    {
        if (!sessions.TryGet(context.Connection, out var sourceSession) || sourceSession is null)
        {
            return LegacyLabyrinthFloorAdvanceCommit.Failed(
                null,
                expectedItemPath,
                expectedCharacterPath,
                "session-missing-after-lock");
        }

        if (sourceSession.AccountId != expectedAccountId ||
            sourceSession.Character.Uid != expectedCharacterUid)
        {
            return LegacyLabyrinthFloorAdvanceCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                "session-identity-changed-after-lock");
        }

        if (sourceSession.ActiveNpcId != npcId)
        {
            return LegacyLabyrinthFloorAdvanceCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                $"active-npc-mismatch:{sourceSession.ActiveNpcId.GetValueOrDefault()}; stale-option-replay-fenced");
        }

        if (!TryResolveLegacyLabyrinthFloorAdvancePaths(
                sourceSession,
                context.PlayerItemsRoot,
                out var currentItemPath,
                out var currentCharacterPath,
                out var pathReason))
        {
            return LegacyLabyrinthFloorAdvanceCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                pathReason);
        }

        if (!LegacyLabyrinthFloorAdvancePathEquals(currentItemPath, expectedItemPath) ||
            !LegacyLabyrinthFloorAdvancePathEquals(currentCharacterPath, expectedCharacterPath))
        {
            return LegacyLabyrinthFloorAdvanceCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                "session-path-changed-after-lock");
        }

        if (!HasUnboundLegacyLabyrinthFloorToken(sourceSession.PlayerItems, floor.TokenItemId))
        {
            return LegacyLabyrinthFloorAdvanceCommit.MissingToken(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath);
        }

        // Source Contain(id, 1, 0) checks an unbound token, while Remove(id, 1) has no bound filter.
        // Lowest UID makes that asymmetry deterministic without weakening the unbound eligibility gate.
        var token = sourceSession.PlayerItems.InventoryItems
            .Where(item => item.ItemId == floor.TokenItemId)
            .OrderBy(item => item.Uid)
            .FirstOrDefault();
        if (token is null)
        {
            return LegacyLabyrinthFloorAdvanceCommit.MissingToken(
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
        catch (Exception exception) when (IsLegacyLabyrinthFloorAdvanceInfrastructureException(exception))
        {
            return LegacyLabyrinthFloorAdvanceCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                "baseline-read-" + exception.GetType().Name);
        }

        if (originalItemBytes is null)
        {
            return LegacyLabyrinthFloorAdvanceCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                "player-items-snapshot-missing");
        }

        if (originalCharacterBytes is null)
        {
            return LegacyLabyrinthFloorAdvanceCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                "character-snapshot-missing");
        }

        if (!originalItemBytes.AsSpan().SequenceEqual(expectedItemBytes))
        {
            return LegacyLabyrinthFloorAdvanceCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                "persisted-items-do-not-match-live-session");
        }

        var updatedItems = ConsumeSingleLegacyInventoryItem(
            sourceSession.PlayerItems,
            token,
            out var itemMutationFrame);
        var sourceStackSize = token.StackSize == 0 ? (ushort)1 : token.StackSize;
        var updatedStackSize = sourceStackSize > 1 ? (ushort)(sourceStackSize - 1) : (ushort)0;
        var itemMutationPacket = sourceStackSize > 1
            ? "LegacyGameItem.Update"
            : "MsgItemUsage.RemoveInventory";
        var sourceCharacter = sourceSession.Character;
        var destination = floor.NextFloorDestination;
        var teleportReduction = ReduceLegacyTeleportDestination(
            context,
            sourceCharacter,
            new LegacyTeleportDestination(destination.MapId, destination.X, destination.Y));
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

        var updatedItemBytes = PlayerItemSnapshotLoader.ToLegacyBytes(updatedItems);
        try
        {
            PlayerItemSnapshotFilePersistence.WriteAllBytesAtomically(expectedItemPath, updatedItemBytes);
        }
        catch (Exception exception) when (IsLegacyLabyrinthFloorAdvanceInfrastructureException(exception))
        {
            TryRollbackLegacyLabyrinthFloorAdvanceFiles(
                expectedItemPath,
                originalItemBytes,
                expectedCharacterPath,
                originalCharacterBytes,
                restoreCharacter: false,
                out var rollbackReason);
            return LegacyLabyrinthFloorAdvanceCommit.Failed(
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
        catch (Exception exception) when (IsLegacyLabyrinthFloorAdvanceInfrastructureException(exception))
        {
            characterPersisted = false;
            characterPersistenceReason = exception.GetType().Name;
        }

        if (!characterPersisted)
        {
            TryRollbackLegacyLabyrinthFloorAdvanceFiles(
                expectedItemPath,
                originalItemBytes,
                expectedCharacterPath,
                originalCharacterBytes,
                restoreCharacter: true,
                out var rollbackReason);
            return LegacyLabyrinthFloorAdvanceCommit.Failed(
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
        catch (Exception exception) when (IsLegacyLabyrinthFloorAdvanceInfrastructureException(exception))
        {
            TryRollbackLegacyLabyrinthFloorAdvanceFiles(
                expectedItemPath,
                originalItemBytes,
                expectedCharacterPath,
                originalCharacterBytes,
                restoreCharacter: true,
                out var rollbackReason);
            return LegacyLabyrinthFloorAdvanceCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                $"session-commit-{exception.GetType().Name}; rollback={rollbackReason}");
        }

        if (!sessionCommitted)
        {
            TryRollbackLegacyLabyrinthFloorAdvanceFiles(
                expectedItemPath,
                originalItemBytes,
                expectedCharacterPath,
                originalCharacterBytes,
                restoreCharacter: true,
                out var rollbackReason);
            return LegacyLabyrinthFloorAdvanceCommit.Failed(
                sourceSession,
                expectedItemPath,
                expectedCharacterPath,
                "session-changed-before-commit; rollback=" + rollbackReason);
        }

        return LegacyLabyrinthFloorAdvanceCommit.Succeeded(
            sourceSession,
            updatedSession,
            token.Uid,
            sourceStackSize,
            updatedStackSize,
            itemMutationFrame,
            itemMutationPacket,
            expectedItemPath,
            expectedCharacterPath);
    }

    private static bool HasUnboundLegacyLabyrinthFloorToken(PlayerItemSnapshot items, uint tokenItemId)
    {
        return items.InventoryItems.Any(item => item.ItemId == tokenItemId && item.Bound == 0);
    }

    private static ValueTask SendLegacyLabyrinthFloorMissingTokenAsync(
        WorldPacketHandlerContext context,
        LegacyLabyrinthFloor floor,
        CancellationToken cancellationToken)
    {
        return SendLegacyNpcDialogAsync(
            context,
            floor.SourceMethod,
            [floor.MissingTokenConfirmText],
            floor.MissingTokenConfirmOptions,
            floor.MissingTokenConfirmAvatarId,
            cancellationToken);
    }

    private static bool TryResolveLegacyLabyrinthFloorAdvancePaths(
        WorldSession session,
        string? playerItemsRoot,
        out string itemPath,
        out string characterPath,
        out string reason)
    {
        itemPath = string.Empty;
        characterPath = string.Empty;
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(playerItemsRoot))
        {
            reason = "player-items-root-missing";
            return false;
        }

        if (string.IsNullOrWhiteSpace(session.UsersRoot))
        {
            reason = "users-root-missing";
            return false;
        }

        try
        {
            // Users snapshots are account-owned; their Character UID is validated inside the file.
            itemPath = Path.GetFullPath(Path.Combine(
                playerItemsRoot,
                session.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin"));
            characterPath = Path.GetFullPath(Path.Combine(
                session.UsersRoot,
                session.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini"));
            return true;
        }
        catch (Exception exception) when (IsLegacyLabyrinthFloorAdvanceInfrastructureException(exception))
        {
            reason = "snapshot-path-" + exception.GetType().Name;
            return false;
        }
    }

    private static bool LegacyLabyrinthFloorAdvancePathEquals(string left, string right)
    {
        return string.Equals(
            left,
            right,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static bool TryRollbackLegacyLabyrinthFloorAdvanceFiles(
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
                    LegacyLabyrinthFloorAdvanceRollbackMarker);
            }
            catch (Exception exception) when (IsLegacyLabyrinthFloorAdvanceInfrastructureException(exception))
            {
                failures.Add("character:" + exception.GetType().Name);
            }
        }

        try
        {
            PlayerItemSnapshotFilePersistence.WriteAllBytesAtomically(itemPath, originalItemBytes);
        }
        catch (Exception exception) when (IsLegacyLabyrinthFloorAdvanceInfrastructureException(exception))
        {
            failures.Add("items:" + exception.GetType().Name);
        }

        reason = failures.Count == 0 ? "complete" : "failed:" + string.Join(',', failures);
        return failures.Count == 0;
    }

    private static bool IsLegacyLabyrinthFloorAdvanceInfrastructureException(Exception exception)
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

    private static Task LogLegacyLabyrinthFloorAdvanceFailureAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        LegacyLabyrinthFloor floor,
        string reason)
    {
        return context.Log.WriteLineAsync(
            "WorldServer skipped LegacyNpc Labyrinth floor advance " +
            $"{FormatSession(context with { Session = session })} source={floor.SourceMethod} " +
            $"sourceLines={floor.SourceLines} npcId={npcId} option={optionId} " +
            $"tokenId={floor.TokenItemId} destination={floor.NextFloorDestination.MapId}/" +
            $"{floor.NextFloorDestination.X}/{floor.NextFloorDestination.Y} reason={reason}; " +
            "session was not published and no success packets were sent.");
    }

    private enum LegacyLabyrinthFloorAdvanceCommitStatus
    {
        Success,
        MissingToken,
        Failure,
    }

    private sealed record LegacyLabyrinthFloorAdvanceCommit(
        LegacyLabyrinthFloorAdvanceCommitStatus Status,
        WorldSession? SourceSession,
        WorldSession? UpdatedSession,
        uint TokenUid,
        ushort SourceStackSize,
        ushort UpdatedStackSize,
        byte[] ItemMutationFrame,
        string ItemMutationPacket,
        string ItemPath,
        string CharacterPath,
        string Reason)
    {
        public static LegacyLabyrinthFloorAdvanceCommit Succeeded(
            WorldSession sourceSession,
            WorldSession updatedSession,
            uint tokenUid,
            ushort sourceStackSize,
            ushort updatedStackSize,
            byte[] itemMutationFrame,
            string itemMutationPacket,
            string itemPath,
            string characterPath)
        {
            return new(
                LegacyLabyrinthFloorAdvanceCommitStatus.Success,
                sourceSession,
                updatedSession,
                tokenUid,
                sourceStackSize,
                updatedStackSize,
                itemMutationFrame,
                itemMutationPacket,
                itemPath,
                characterPath,
                string.Empty);
        }

        public static LegacyLabyrinthFloorAdvanceCommit MissingToken(
            WorldSession sourceSession,
            string itemPath,
            string characterPath)
        {
            return new(
                LegacyLabyrinthFloorAdvanceCommitStatus.MissingToken,
                sourceSession,
                null,
                0,
                0,
                0,
                [],
                string.Empty,
                itemPath,
                characterPath,
                string.Empty);
        }

        public static LegacyLabyrinthFloorAdvanceCommit Failed(
            WorldSession? sourceSession,
            string itemPath,
            string characterPath,
            string reason)
        {
            return new(
                LegacyLabyrinthFloorAdvanceCommitStatus.Failure,
                sourceSession,
                null,
                0,
                0,
                0,
                [],
                string.Empty,
                itemPath,
                characterPath,
                reason);
        }
    }
}
