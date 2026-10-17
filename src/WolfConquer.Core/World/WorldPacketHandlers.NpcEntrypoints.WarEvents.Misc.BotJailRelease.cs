using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyBotJailReleaseMapId = 1_002;
    private const long LegacyBotJailReleaseX = 410;
    private const long LegacyBotJailReleaseY = 354;
    private const string LegacyBotJailReleaseSourceLines = "NpcHandler.cs:7198-7266";

    private static async ValueTask<bool> TryApplyLegacyBotJailReleaseAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        byte optionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Sessions is not { } sessions)
        {
            await LogLegacyBotJailReleaseFailureAsync(
                    context,
                    session,
                    sourceMethod,
                    optionId,
                    "session-store-missing")
                .ConfigureAwait(false);
            return false;
        }

        if (!sessions.TryGet(context.Connection, out var storedSession) || storedSession is null)
        {
            await LogLegacyBotJailReleaseFailureAsync(
                    context,
                    session,
                    sourceMethod,
                    optionId,
                    "session-missing")
                .ConfigureAwait(false);
            return false;
        }

        if (storedSession.AccountId != session.AccountId ||
            storedSession.Character.Uid != session.Character.Uid)
        {
            await LogLegacyBotJailReleaseFailureAsync(
                    context,
                    storedSession,
                    sourceMethod,
                    optionId,
                    "session-character-changed")
                .ConfigureAwait(false);
            return false;
        }

        if (string.IsNullOrWhiteSpace(storedSession.UsersRoot))
        {
            await LogLegacyBotJailReleaseFailureAsync(
                    context,
                    storedSession,
                    sourceMethod,
                    optionId,
                    "usersRootMissing")
                .ConfigureAwait(false);
            return false;
        }

        string path;
        try
        {
            path = Path.GetFullPath(Path.Combine(
                storedSession.UsersRoot,
                storedSession.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini"));
        }
        catch (Exception exception) when (IsLegacyBotJailReleaseInfrastructureException(exception))
        {
            await LogLegacyBotJailReleaseFailureAsync(
                    context,
                    storedSession,
                    sourceMethod,
                    optionId,
                    "character-path:" + exception.GetType().Name)
                .ConfigureAwait(false);
            return false;
        }

        var preflight = await PrepareLegacyTeleportAsync(
                context,
                storedSession,
                new LegacyTeleportRequest(sourceMethod, LegacyBotJailReleaseMapId),
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflight.Allowed)
        {
            return false;
        }

        storedSession = preflight.Session;

        LegacyBotJailReleaseCommit commit;
        try
        {
            commit = CharacterSnapshotFileCoordinator.ExecuteLocked(
                path,
                () => TryCommitLegacyBotJailRelease(
                    sessions,
                    context,
                    path,
                    session.AccountId,
                    session.Character.Uid));
        }
        catch (Exception exception) when (IsLegacyBotJailReleaseInfrastructureException(exception))
        {
            await LogLegacyBotJailReleaseFailureAsync(
                    context,
                    storedSession,
                    sourceMethod,
                    optionId,
                    "infrastructure:" + exception.GetType().Name)
                .ConfigureAwait(false);
            return false;
        }

        if (!commit.Applied)
        {
            await LogLegacyBotJailReleaseFailureAsync(
                    context,
                    commit.SourceSession ?? storedSession,
                    sourceMethod,
                    optionId,
                    commit.Reason)
                .ConfigureAwait(false);
            return false;
        }

        var sourceSession = commit.SourceSession!;
        var updatedSession = commit.UpdatedSession!;
        var updatedContext = context with { Session = updatedSession };

        await SendConquerPointsUpdateAsync(updatedContext, updatedSession.Character, cancellationToken)
            .ConfigureAwait(false);
        var teleportedSession = await ExecuteLegacyTeleportDestinationAsync(
                updatedContext,
                updatedSession,
                new LegacyTeleportDestination(
                    LegacyBotJailReleaseMapId,
                    LegacyBotJailReleaseX,
                    LegacyBotJailReleaseY),
                timestamp: 0,
                cancellationToken)
            .ConfigureAwait(false);
        if (teleportedSession is null)
        {
            return false;
        }

        updatedSession = teleportedSession;
        updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox("Please Not Repeat This Again,").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.DisconnectAsync(cancellationToken).ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc BotJail release " +
                $"{FormatSession(updatedContext)} source={sourceMethod} sourceLines={LegacyBotJailReleaseSourceLines} " +
                $"npcId={LegacyBotJailNpcId} option={optionId} " +
                $"conquerPoints={commit.SourceConquerPoints}->{commit.UpdatedConquerPoints} " +
                $"cost={commit.Cost} botjail={commit.SourceBotJailCount}->{commit.UpdatedBotJailCount} " +
                $"map={sourceSession.Character.MapId}/{sourceSession.Character.X}/{sourceSession.Character.Y}" +
                $"->{LegacyBotJailReleaseMapId}/{LegacyBotJailReleaseX}/{LegacyBotJailReleaseY} " +
                $"path={Path.GetFileName(commit.Path)} stateTransition=BotJailRelease " +
                "persistenceBeforeSessionCommit=true activeNpcId=cleared dynamicId=0 " +
                "packetOrder=MsgUpdate.ConquerPoints,GameClient.Teleport,NpcReply.MessageBox,Disconnect.")
            .ConfigureAwait(false);
        return true;
    }

    private static LegacyBotJailReleaseCommit TryCommitLegacyBotJailRelease(
        WorldSessionStore sessions,
        WorldPacketHandlerContext context,
        string expectedPath,
        long expectedAccountId,
        long expectedCharacterUid)
    {
        if (!sessions.TryGet(context.Connection, out var sourceSession) || sourceSession is null)
        {
            return LegacyBotJailReleaseCommit.Failed("session-missing-after-lock");
        }

        if (sourceSession.AccountId != expectedAccountId ||
            sourceSession.Character.Uid != expectedCharacterUid)
        {
            return LegacyBotJailReleaseCommit.Failed("session-character-changed-after-lock", sourceSession);
        }

        if (string.IsNullOrWhiteSpace(sourceSession.UsersRoot))
        {
            return LegacyBotJailReleaseCommit.Failed("usersRootMissing", sourceSession);
        }

        if (sourceSession.ActiveNpcId != LegacyBotJailNpcId)
        {
            return LegacyBotJailReleaseCommit.Failed("active-npc-mismatch", sourceSession);
        }

        var currentPath = Path.GetFullPath(Path.Combine(
            sourceSession.UsersRoot,
            sourceSession.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini"));
        if (!string.Equals(
                currentPath,
                expectedPath,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            return LegacyBotJailReleaseCommit.Failed("session-path-changed", sourceSession);
        }

        var sourceBotJailCount = ReadLegacyBotJailCount(sourceSession);
        var cost = LegacyBotJailReleaseCost(sourceBotJailCount);
        var sourceCharacter = sourceSession.Character;
        if (sourceCharacter.ConquerPoints < cost)
        {
            return LegacyBotJailReleaseCommit.Failed("conquer-points-changed-after-check", sourceSession);
        }

        if (sourceBotJailCount == uint.MaxValue)
        {
            return LegacyBotJailReleaseCommit.Failed("botjail-overflow", sourceSession);
        }

        var updatedConquerPoints = checked(sourceCharacter.ConquerPoints - cost);
        var updatedBotJailCount = sourceBotJailCount + 1;
        var updatedAttributes = CopyCharacterAttributes(sourceCharacter.Attributes);
        updatedAttributes["ConquerPoints"] = updatedConquerPoints.ToString(CultureInfo.InvariantCulture);
        updatedAttributes["botjail"] = updatedBotJailCount.ToString(CultureInfo.InvariantCulture);
        var updatedCharacter = sourceCharacter with
        {
            ConquerPoints = updatedConquerPoints,
            Attributes = updatedAttributes,
        };
        var persistedCharacter = ReduceLegacyTeleportDestination(
                context,
                updatedCharacter,
                new LegacyTeleportDestination(
                    LegacyBotJailReleaseMapId,
                    LegacyBotJailReleaseX,
                    LegacyBotJailReleaseY))
            .FinalCharacter;
        var persistenceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = updatedConquerPoints.ToString(CultureInfo.InvariantCulture),
            ["botjail"] = updatedBotJailCount.ToString(CultureInfo.InvariantCulture),
            ["Map"] = persistedCharacter.MapId.ToString(CultureInfo.InvariantCulture),
            ["X"] = persistedCharacter.X.ToString(CultureInfo.InvariantCulture),
            ["Y"] = persistedCharacter.Y.ToString(CultureInfo.InvariantCulture),
            ["PMap"] = persistedCharacter.PreviousMapId.ToString(CultureInfo.InvariantCulture),
            ["PMapX"] = persistedCharacter.PreviousMapX.ToString(CultureInfo.InvariantCulture),
            ["PMapY"] = persistedCharacter.PreviousMapY.ToString(CultureInfo.InvariantCulture),
            ["DynamicID"] = persistedCharacter.Attributes.TryGetValue("DynamicID", out var dynamicId)
                ? dynamicId
                : "0",
        };
        if (persistedCharacter.Attributes.TryGetValue("PDinamycID", out var previousDynamicId))
        {
            persistenceValues["PDinamycID"] = previousDynamicId;
        }

        updatedAttributes["Map"] = persistenceValues["Map"];
        updatedAttributes["X"] = persistenceValues["X"];
        updatedAttributes["Y"] = persistenceValues["Y"];
        updatedAttributes["PMap"] = persistenceValues["PMap"];
        updatedAttributes["PMapX"] = persistenceValues["PMapX"];
        updatedAttributes["PMapY"] = persistenceValues["PMapY"];

        var updatedSession = sourceSession with
        {
            Character = updatedCharacter,
            ActiveNpcId = null,
        };

        if (!TryPersistLegacyCharacterAttributes(
                sourceSession.UsersRoot,
                sourceSession.AccountId,
                sourceCharacter.Uid,
                persistenceValues,
                out var persistedPath,
                out var persistenceReason))
        {
            return LegacyBotJailReleaseCommit.Failed(
                "persistence:" + persistenceReason,
                sourceSession);
        }

        if (!sessions.TryCompareAndSet(context.Connection, sourceSession, updatedSession))
        {
            var rollbackValues = CreateLegacyBotJailReleaseRollbackValues(
                sourceCharacter,
                sourceBotJailCount);
            var rollbackSucceeded = TryPersistLegacyCharacterAttributes(
                sourceSession.UsersRoot,
                sourceSession.AccountId,
                sourceCharacter.Uid,
                rollbackValues,
                out _,
                out var rollbackReason);
            return LegacyBotJailReleaseCommit.Failed(
                rollbackSucceeded
                    ? "session-changed-before-commit; persistence-rollback=complete"
                    : "session-changed-before-commit; persistence-rollback=" + rollbackReason,
                sourceSession);
        }

        return LegacyBotJailReleaseCommit.Succeeded(
            sourceSession,
            updatedSession,
            sourceBotJailCount,
            updatedBotJailCount,
            sourceCharacter.ConquerPoints,
            updatedConquerPoints,
            cost,
            persistedPath);
    }

    private static Dictionary<string, string> CreateLegacyBotJailReleaseRollbackValues(
        CharacterSnapshot sourceCharacter,
        uint sourceBotJailCount)
    {
        var dynamicId = sourceCharacter.Attributes.TryGetValue("DynamicID", out var rawDynamicId)
            ? rawDynamicId
            : "0";
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = sourceCharacter.ConquerPoints.ToString(CultureInfo.InvariantCulture),
            ["botjail"] = sourceBotJailCount.ToString(CultureInfo.InvariantCulture),
            ["Map"] = sourceCharacter.MapId.ToString(CultureInfo.InvariantCulture),
            ["X"] = sourceCharacter.X.ToString(CultureInfo.InvariantCulture),
            ["Y"] = sourceCharacter.Y.ToString(CultureInfo.InvariantCulture),
            ["PMap"] = sourceCharacter.PreviousMapId.ToString(CultureInfo.InvariantCulture),
            ["PMapX"] = sourceCharacter.PreviousMapX.ToString(CultureInfo.InvariantCulture),
            ["PMapY"] = sourceCharacter.PreviousMapY.ToString(CultureInfo.InvariantCulture),
            ["DynamicID"] = dynamicId,
        };
        if (sourceCharacter.Attributes.TryGetValue("PDinamycID", out var previousDynamicId))
        {
            values["PDinamycID"] = previousDynamicId;
        }

        return values;
    }

    private static bool IsLegacyBotJailReleaseInfrastructureException(Exception exception)
    {
        return exception is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException or
            System.Security.SecurityException;
    }

    private static Task LogLegacyBotJailReleaseFailureAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        byte optionId,
        string reason)
    {
        return context.Log.WriteLineAsync(
            "WorldServer skipped LegacyNpc BotJail release " +
            $"{FormatSession(context with { Session = session })} source={sourceMethod} " +
            $"sourceLines={LegacyBotJailReleaseSourceLines} npcId={LegacyBotJailNpcId} " +
            $"option={optionId} reason={reason}; " +
            "no ConquerPoints, botjail, map, session, success packets, or disconnect were committed.");
    }

    private readonly record struct LegacyBotJailReleaseCommit(
        bool Applied,
        WorldSession? SourceSession,
        WorldSession? UpdatedSession,
        uint SourceBotJailCount,
        uint UpdatedBotJailCount,
        long SourceConquerPoints,
        long UpdatedConquerPoints,
        long Cost,
        string Path,
        string Reason)
    {
        public static LegacyBotJailReleaseCommit Failed(string reason, WorldSession? sourceSession = null)
        {
            return new LegacyBotJailReleaseCommit(
                false,
                sourceSession,
                null,
                0,
                0,
                0,
                0,
                0,
                string.Empty,
                reason);
        }

        public static LegacyBotJailReleaseCommit Succeeded(
            WorldSession sourceSession,
            WorldSession updatedSession,
            uint sourceBotJailCount,
            uint updatedBotJailCount,
            long sourceConquerPoints,
            long updatedConquerPoints,
            long cost,
            string path)
        {
            return new LegacyBotJailReleaseCommit(
                true,
                sourceSession,
                updatedSession,
                sourceBotJailCount,
                updatedBotJailCount,
                sourceConquerPoints,
                updatedConquerPoints,
                cost,
                path,
                string.Empty);
        }
    }
}
