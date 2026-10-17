using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyBattleFieldPointsPerConquerPoint = 5U;
    private const long LegacyBattleFieldConquerPointsWalletCap = 2_000_000_000;
    private const long LegacyBattleFieldPayoutMapId = 1_002;
    private const long LegacyBattleFieldPayoutX = 410;
    private const long LegacyBattleFieldPayoutY = 354;
    private const string LegacyBattleFieldPayoutUnavailableText =
        "BattleField point payout is not available from this runtime yet.";

    private static async ValueTask LegacyDisCityBattlefieldServicesTryApplyBattleFieldPointPayoutAsync(
        WorldPacketHandlerContext context,
        uint npcId,
        LegacyDisCityBattlefieldServicesSource source,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions)
        {
            await LegacyDisCityBattlefieldServicesSendBattleFieldPayoutUnavailableAsync(
                    context,
                    source,
                    npcId,
                    optionId,
                    "session-store-missing",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!sessions.TryGet(context.Connection, out var storedSession) || storedSession is null)
        {
            await LegacyDisCityBattlefieldServicesSendBattleFieldPayoutUnavailableAsync(
                    context,
                    source,
                    npcId,
                    optionId,
                    "session-missing",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(storedSession.UsersRoot))
        {
            await LegacyDisCityBattlefieldServicesSendBattleFieldPayoutUnavailableAsync(
                    context,
                    source,
                    npcId,
                    optionId,
                    "usersRootMissing",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var path = Path.Combine(
            storedSession.UsersRoot,
            storedSession.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        var preflight = await PrepareLegacyTeleportAsync(
                context,
                storedSession,
                new LegacyTeleportRequest(source.SourceMethod, LegacyBattleFieldPayoutMapId),
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflight.Allowed)
        {
            return;
        }

        storedSession = preflight.Session;

        var commit = CharacterSnapshotFileCoordinator.ExecuteLocked(
            path,
            () => LegacyDisCityBattlefieldServicesTryCommitBattleFieldPointPayout(
                sessions,
                context,
                path));
        if (!commit.Applied)
        {
            await LegacyDisCityBattlefieldServicesSendBattleFieldPayoutUnavailableAsync(
                    context,
                    source,
                    npcId,
                    optionId,
                    commit.Reason,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var sourceSession = commit.SourceSession!;
        var updatedSession = commit.UpdatedSession!;
        var updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket
                    .ForMessageBox(
                        $"You received {commit.RewardConquerPoints.ToString(CultureInfo.InvariantCulture)} ConquerPoints.")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendConquerPointsUpdateAsync(updatedContext, updatedSession.Character, cancellationToken)
            .ConfigureAwait(false);
        var teleportedSession = await ExecuteLegacyTeleportDestinationAsync(
                updatedContext,
                updatedSession,
                new LegacyTeleportDestination(
                    LegacyBattleFieldPayoutMapId,
                    LegacyBattleFieldPayoutX,
                    LegacyBattleFieldPayoutY),
                timestamp: 0,
                cancellationToken)
            .ConfigureAwait(false);
        if (teleportedSession is null)
        {
            return;
        }

        updatedSession = teleportedSession;
        updatedContext = context with { Session = updatedSession };

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc BattleField point payout " +
                $"{FormatSession(updatedContext)} source={source.SourceMethod} sourceLines={source.SourceLines} " +
                $"npcId={npcId} optionId={optionId} " +
                $"battleFieldPoints={commit.BattleFieldPoints}->0 " +
                $"pointsPerConquerPoint={LegacyBattleFieldPointsPerConquerPoint} " +
                $"rewardConquerPoints={commit.RewardConquerPoints} " +
                $"conquerPoints={sourceSession.Character.ConquerPoints}->{updatedSession.Character.ConquerPoints} " +
                $"map={LegacyBattleFieldPayoutMapId} x={LegacyBattleFieldPayoutX} " +
                $"y={LegacyBattleFieldPayoutY} path={Path.GetFileName(commit.Path)} " +
                "persistenceBeforeSessionCommit=true " +
                "packetOrder=NpcReply.MessageBox,MsgUpdate.ConquerPoints,GameClient.Teleport.")
            .ConfigureAwait(false);
    }

    private static LegacyBattleFieldPayoutCommit
        LegacyDisCityBattlefieldServicesTryCommitBattleFieldPointPayout(
            WorldSessionStore sessions,
            WorldPacketHandlerContext context,
            string expectedPath)
    {
        if (!sessions.TryGet(context.Connection, out var sourceSession) || sourceSession is null)
        {
            return LegacyBattleFieldPayoutCommit.Failed("session-missing-after-lock");
        }

        if (string.IsNullOrWhiteSpace(sourceSession.UsersRoot))
        {
            return LegacyBattleFieldPayoutCommit.Failed("usersRootMissing");
        }

        var currentPath = Path.Combine(
            sourceSession.UsersRoot,
            sourceSession.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        if (!string.Equals(
                Path.GetFullPath(currentPath),
                Path.GetFullPath(expectedPath),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            return LegacyBattleFieldPayoutCommit.Failed("session-path-changed");
        }

        if (!TryReadLegacyBattleFieldPoints(sourceSession.Character.Attributes, out var battleFieldPoints))
        {
            return LegacyBattleFieldPayoutCommit.Failed("battlefield-points-invalid");
        }

        var rewardConquerPoints = battleFieldPoints / LegacyBattleFieldPointsPerConquerPoint;
        var sourceConquerPoints = sourceSession.Character.ConquerPoints;
        if (sourceConquerPoints < 0 || sourceConquerPoints > LegacyBattleFieldConquerPointsWalletCap)
        {
            return LegacyBattleFieldPayoutCommit.Failed("conquer-points-out-of-range");
        }

        if (rewardConquerPoints > LegacyBattleFieldConquerPointsWalletCap - sourceConquerPoints)
        {
            return LegacyBattleFieldPayoutCommit.Failed("conquer-points-bank-overflow-unmodeled");
        }

        var updatedConquerPoints = checked(sourceConquerPoints + rewardConquerPoints);
        var sourceCharacter = sourceSession.Character;
        var updatedAttributes = CopyCharacterAttributes(sourceCharacter.Attributes);
        updatedAttributes["BattleFieldPoints"] = "0";
        updatedAttributes["ConquerPoints"] = updatedConquerPoints.ToString(CultureInfo.InvariantCulture);
        var updatedCharacter = sourceCharacter with
        {
            ConquerPoints = updatedConquerPoints,
            Attributes = updatedAttributes,
        };
        var persistedCharacter = ReduceLegacyTeleportDestination(
                context,
                updatedCharacter,
                new LegacyTeleportDestination(
                    LegacyBattleFieldPayoutMapId,
                    LegacyBattleFieldPayoutX,
                    LegacyBattleFieldPayoutY))
            .FinalCharacter;
        var persistenceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BattleFieldPoints"] = "0",
            ["ConquerPoints"] = updatedConquerPoints.ToString(CultureInfo.InvariantCulture),
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
            return LegacyBattleFieldPayoutCommit.Failed("persistence:" + persistenceReason);
        }

        sessions.Set(context.Connection, updatedSession);
        return LegacyBattleFieldPayoutCommit.Succeeded(
            sourceSession,
            updatedSession,
            battleFieldPoints,
            rewardConquerPoints,
            persistedPath);
    }

    private static bool TryReadLegacyBattleFieldPoints(
        IReadOnlyDictionary<string, string> attributes,
        out uint battleFieldPoints)
    {
        if (!attributes.TryGetValue("BattleFieldPoints", out var raw))
        {
            battleFieldPoints = 0;
            return true;
        }

        return uint.TryParse(
            raw,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out battleFieldPoints);
    }

    private static ValueTask LegacyDisCityBattlefieldServicesSendBattleFieldPayoutUnavailableAsync(
        WorldPacketHandlerContext context,
        LegacyDisCityBattlefieldServicesSource source,
        uint npcId,
        byte optionId,
        string reason,
        CancellationToken cancellationToken)
    {
        return LegacyDisCityBattlefieldServicesSendUnavailableAsync(
            context,
            npcId,
            optionId,
            source,
            LegacyBattleFieldPayoutUnavailableText,
            reason + "; no BattleField points, wallet, map, session state, or success packets were committed.",
            LegacyDisCityBattlefieldServicesDefaultAvatarId,
            cancellationToken);
    }

    private readonly record struct LegacyBattleFieldPayoutCommit(
        bool Applied,
        WorldSession? SourceSession,
        WorldSession? UpdatedSession,
        uint BattleFieldPoints,
        uint RewardConquerPoints,
        string Path,
        string Reason)
    {
        public static LegacyBattleFieldPayoutCommit Failed(string reason)
        {
            return new LegacyBattleFieldPayoutCommit(
                false,
                null,
                null,
                0,
                0,
                string.Empty,
                reason);
        }

        public static LegacyBattleFieldPayoutCommit Succeeded(
            WorldSession sourceSession,
            WorldSession updatedSession,
            uint battleFieldPoints,
            uint rewardConquerPoints,
            string path)
        {
            return new LegacyBattleFieldPayoutCommit(
                true,
                sourceSession,
                updatedSession,
                battleFieldPoints,
                rewardConquerPoints,
                path,
                string.Empty);
        }
    }
}
