using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyBattleFieldAdvanceUnavailableText =
        "BattleField map travel is not available from this runtime yet.";

    private static async ValueTask LegacyDisCityBattlefieldServicesTryApplyBattleFieldAdvanceAsync(
        WorldPacketHandlerContext context,
        uint npcId,
        LegacyDisCityBattlefieldServicesSource source,
        byte optionId,
        uint pointCost,
        LegacyNpcTeleportDestination destination,
        string insufficientPointsText,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions)
        {
            await LegacyDisCityBattlefieldServicesSendBattleFieldAdvanceUnavailableAsync(
                    context,
                    npcId,
                    source,
                    optionId,
                    "session-store-missing",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!sessions.TryGet(context.Connection, out var storedSession) || storedSession is null)
        {
            await LegacyDisCityBattlefieldServicesSendBattleFieldAdvanceUnavailableAsync(
                    context,
                    npcId,
                    source,
                    optionId,
                    "session-missing",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(storedSession.UsersRoot))
        {
            await LegacyDisCityBattlefieldServicesSendBattleFieldAdvanceUnavailableAsync(
                    context,
                    npcId,
                    source,
                    optionId,
                    "usersRootMissing",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var path = Path.Combine(
            storedSession.UsersRoot,
            storedSession.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");

        if (!TryReadLegacyBattleFieldPoints(storedSession.Character.Attributes, out var battleFieldPoints))
        {
            await LegacyDisCityBattlefieldServicesSendBattleFieldAdvanceUnavailableAsync(
                    context,
                    npcId,
                    source,
                    optionId,
                    "battlefield-points-invalid",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (battleFieldPoints < pointCost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source.SourceMethod,
                    [insufficientPointsText],
                    [],
                    LegacyDisCityBattlefieldServicesDefaultAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var preflight = await PrepareLegacyTeleportAsync(
                context,
                storedSession,
                new LegacyTeleportRequest(source.SourceMethod, destination.MapId),
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflight.Allowed)
        {
            return;
        }

        storedSession = preflight.Session;

        var commit = CharacterSnapshotFileCoordinator.ExecuteLocked(
            path,
            () => LegacyDisCityBattlefieldServicesTryCommitBattleFieldAdvance(
                sessions,
                context,
                path,
                pointCost,
                destination));
        if (commit.InsufficientPoints)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source.SourceMethod,
                    [insufficientPointsText],
                    [],
                    LegacyDisCityBattlefieldServicesDefaultAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!commit.Applied)
        {
            await LegacyDisCityBattlefieldServicesSendBattleFieldAdvanceUnavailableAsync(
                    context,
                    npcId,
                    source,
                    optionId,
                    commit.Reason,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var updatedSession = commit.UpdatedSession!;
        var updatedContext = context with { Session = updatedSession };
        var teleportedSession = await ExecuteLegacyTeleportDestinationAsync(
                updatedContext,
                updatedSession,
                new LegacyTeleportDestination(destination.MapId, destination.X, destination.Y),
                timestamp: 0,
                cancellationToken,
                expectedSession: commit.SourceSession)
            .ConfigureAwait(false);
        if (teleportedSession is null)
        {
            return;
        }

        updatedSession = teleportedSession;
        updatedContext = context with { Session = updatedSession };

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc BattleField map advance " +
                $"{FormatSession(updatedContext)} source={source.SourceMethod} sourceLines={source.SourceLines} " +
                $"npcId={npcId} optionId={optionId} " +
                $"battleFieldPoints={commit.SourceBattleFieldPoints}->{commit.UpdatedBattleFieldPoints} " +
                $"pointCost={pointCost} map={destination.MapId} x={destination.X} y={destination.Y} " +
                $"path={Path.GetFileName(commit.Path)} persistenceBeforeSessionCommit=true " +
                "packetOrder=GameClient.Teleport.")
            .ConfigureAwait(false);
    }

    private static LegacyBattleFieldAdvanceCommit LegacyDisCityBattlefieldServicesTryCommitBattleFieldAdvance(
        WorldSessionStore sessions,
        WorldPacketHandlerContext context,
        string expectedPath,
        uint pointCost,
        LegacyNpcTeleportDestination destination)
    {
        if (!sessions.TryGet(context.Connection, out var sourceSession) || sourceSession is null)
        {
            return LegacyBattleFieldAdvanceCommit.Failed("session-missing-after-lock");
        }

        if (string.IsNullOrWhiteSpace(sourceSession.UsersRoot))
        {
            return LegacyBattleFieldAdvanceCommit.Failed("usersRootMissing");
        }

        var currentPath = Path.Combine(
            sourceSession.UsersRoot,
            sourceSession.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        if (!string.Equals(
                Path.GetFullPath(currentPath),
                Path.GetFullPath(expectedPath),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            return LegacyBattleFieldAdvanceCommit.Failed("session-path-changed");
        }

        if (!TryReadLegacyBattleFieldPoints(sourceSession.Character.Attributes, out var battleFieldPoints))
        {
            return LegacyBattleFieldAdvanceCommit.Failed("battlefield-points-invalid");
        }

        if (battleFieldPoints < pointCost)
        {
            return LegacyBattleFieldAdvanceCommit.Insufficient(battleFieldPoints);
        }

        var updatedBattleFieldPoints = battleFieldPoints - pointCost;
        var sourceCharacter = sourceSession.Character;
        var updatedAttributes = CopyCharacterAttributes(sourceCharacter.Attributes);
        updatedAttributes["BattleFieldPoints"] = updatedBattleFieldPoints.ToString(CultureInfo.InvariantCulture);
        var updatedCharacter = sourceCharacter with
        {
            Attributes = updatedAttributes,
        };
        var persistedCharacter = ReduceLegacyTeleportDestination(
                context,
                updatedCharacter,
                new LegacyTeleportDestination(destination.MapId, destination.X, destination.Y))
            .FinalCharacter;
        var persistenceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BattleFieldPoints"] = updatedBattleFieldPoints.ToString(CultureInfo.InvariantCulture),
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
            return LegacyBattleFieldAdvanceCommit.Failed("persistence:" + persistenceReason);
        }

        return LegacyBattleFieldAdvanceCommit.Succeeded(
            sourceSession,
            updatedSession,
            battleFieldPoints,
            updatedBattleFieldPoints,
            persistedPath);
    }

    private static ValueTask LegacyDisCityBattlefieldServicesSendBattleFieldAdvanceUnavailableAsync(
        WorldPacketHandlerContext context,
        uint npcId,
        LegacyDisCityBattlefieldServicesSource source,
        byte optionId,
        string reason,
        CancellationToken cancellationToken)
    {
        return LegacyDisCityBattlefieldServicesSendUnavailableAsync(
            context,
            npcId,
            optionId,
            source,
            LegacyBattleFieldAdvanceUnavailableText,
            reason + "; no BattleField points, map, session state, or success packets were committed.",
            LegacyDisCityBattlefieldServicesDefaultAvatarId,
            cancellationToken);
    }

    private readonly record struct LegacyBattleFieldAdvanceCommit(
        bool Applied,
        bool InsufficientPoints,
        WorldSession? SourceSession,
        WorldSession? UpdatedSession,
        uint SourceBattleFieldPoints,
        uint UpdatedBattleFieldPoints,
        string Path,
        string Reason)
    {
        public static LegacyBattleFieldAdvanceCommit Failed(string reason)
        {
            return new LegacyBattleFieldAdvanceCommit(
                false,
                false,
                null,
                null,
                0,
                0,
                string.Empty,
                reason);
        }

        public static LegacyBattleFieldAdvanceCommit Insufficient(uint battleFieldPoints)
        {
            return new LegacyBattleFieldAdvanceCommit(
                false,
                true,
                null,
                null,
                battleFieldPoints,
                battleFieldPoints,
                string.Empty,
                string.Empty);
        }

        public static LegacyBattleFieldAdvanceCommit Succeeded(
            WorldSession sourceSession,
            WorldSession updatedSession,
            uint sourceBattleFieldPoints,
            uint updatedBattleFieldPoints,
            string path)
        {
            return new LegacyBattleFieldAdvanceCommit(
                true,
                false,
                sourceSession,
                updatedSession,
                sourceBattleFieldPoints,
                updatedBattleFieldPoints,
                path,
                string.Empty);
        }
    }
}
