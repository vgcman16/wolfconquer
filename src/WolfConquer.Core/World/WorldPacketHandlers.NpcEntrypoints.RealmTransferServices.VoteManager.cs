using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyVoteManagerSourceLines = "25769-25831";
    private static string LegacyVoteManagerUrl => WolfConquer.Core.Runtime.ConquerRuntimeDefaults.VoteUrl;
    private const string LegacyVoteManagerPendingMessage = "Please wait while the system checks your vote.";
    private const string LegacyVoteManagerClaimMessage = "You`ve received VIP4 (11 Hours) And 1 points.";

    private static async ValueTask StartLegacyRealmTransferServicesVoteAsync(
        WorldPacketHandlerContext context,
        string source,
        string sourceLines,
        CancellationToken cancellationToken)
    {
        if (context.VoteStates is not { } voteStates)
        {
            await LogLegacyVoteManagerFenceAsync(context, source, "vote-store-missing").ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions ||
            !sessions.TryGet(context.Connection, out var currentSession) ||
            currentSession is null)
        {
            await LogLegacyVoteManagerFenceAsync(context, source, "session-missing").ConfigureAwait(false);
            return;
        }

        if (!TryResolveLegacyVoteIdentity(context, currentSession, out var uid, out var ipAddress, out var identityReason))
        {
            await LogLegacyVoteManagerFenceAsync(context, source, identityReason).ConfigureAwait(false);
            return;
        }

        var timeProvider = context.TimeProvider ?? TimeProvider.System;
        var nowUtc = timeProvider.GetUtcNow();
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, timeProvider.LocalTimeZone).DateTime;
        bool canVote;
        try
        {
            canVote = voteStates.CanVote(uid, ipAddress, localNow);
        }
        catch (Exception exception)
        {
            await LogLegacyVoteManagerFenceAsync(
                    context,
                    source,
                    "cooldown-check-failed:" + exception.GetType().Name)
                .ConfigureAwait(false);
            return;
        }

        if (!canVote)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    ["You have already claimed the vote reward. Each IP address can vote only once every 12 hours."],
                    [new("I understand.", LegacyNpcClosedOption)],
                    LegacyRealmTransferServicesVoteRewardAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemWebsite(LegacyVoteManagerUrl).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var pendingSession = currentSession with
        {
            LegacyVoteClaimDueAtUtc = nowUtc.AddSeconds(90),
        };
        sessions.Set(context.Connection, pendingSession);

        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTopLeft(LegacyVoteManagerPendingMessage).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer started LegacyNpc VoteManager vote claim " +
                $"{FormatSession(context with { Session = pendingSession })} source={source} " +
                $"sourceLines={sourceLines} dueAtUtc={pendingSession.LegacyVoteClaimDueAtUtc:O} " +
                "packetOrder=Website,PendingDeadline,TopLeftSystem.")
            .ConfigureAwait(false);
    }

    internal static async ValueTask ProcessDueLegacyVoteClaimAsync(
        WorldPacketHandlerContext context,
        DateTimeOffset nowUtc,
        DateTime localNow,
        CancellationToken cancellationToken)
    {
        if (context.Session is null || context.Sessions is not { } sessions)
        {
            return;
        }

        if (!sessions.TryGet(context.Connection, out var currentSession) ||
            currentSession is null ||
            currentSession.LegacyVoteClaimDueAtUtc is not { } dueAtUtc ||
            nowUtc <= dueAtUtc)
        {
            return;
        }

        const string source = "NpcHandler.VoteManager";
        if (context.VoteStates is not { } voteStates)
        {
            await LogLegacyVoteManagerFenceAsync(context, source, "vote-store-missing").ConfigureAwait(false);
            return;
        }

        if (!TryResolveLegacyVoteIdentity(context, currentSession, out var uid, out var ipAddress, out var identityReason))
        {
            await LogLegacyVoteManagerFenceAsync(context, source, identityReason).ConfigureAwait(false);
            return;
        }

        if (!byte.TryParse(
                ReadAttributeString(currentSession.Character.Attributes, "VotePoints"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var votePoints))
        {
            await LogLegacyVoteManagerFenceAsync(context, source, "vote-points-invalid").ConfigureAwait(false);
            return;
        }

        var currentExpiry = ReadLegacyRewardPointsVipExpiry(currentSession.Character);
        DateTime updatedExpiry;
        try
        {
            updatedExpiry = (currentExpiry > localNow ? currentExpiry : localNow).AddHours(11);
        }
        catch (ArgumentOutOfRangeException)
        {
            await LogLegacyVoteManagerFenceAsync(context, source, "vip-expiry-out-of-range").ConfigureAwait(false);
            return;
        }

        var updatedVotePoints = unchecked((byte)(votePoints + 1));
        var vipTime = updatedExpiry.ToBinary().ToString(CultureInfo.InvariantCulture);
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithCharacterAttribute(currentSession.Character, "VotePoints", updatedVotePoints),
                "VipLevel",
                4),
            "VipTime",
            vipTime);
        var characterValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["VotePoints"] = updatedVotePoints.ToString(CultureInfo.InvariantCulture),
            ["VipLevel"] = "4",
            ["VipTime"] = vipTime,
        };

        var persistenceReason = "callback-not-run";
        cancellationToken.ThrowIfCancellationRequested();
        LegacyVoteClaimResult claimResult;
        try
        {
            claimResult = voteStates.TryCommitDueClaim(
                uid,
                ipAddress,
                localNow,
                plan => TryPersistLegacyVoteClaim(
                    currentSession,
                    plan,
                    characterValues,
                    out persistenceReason));
        }
        catch (Exception exception)
        {
            await LogLegacyVoteManagerFenceAsync(
                    context,
                    source,
                    "claim-commit-failed:" + exception.GetType().Name)
                .ConfigureAwait(false);
            return;
        }

        if (!claimResult.Success)
        {
            await LogLegacyVoteManagerFenceAsync(
                    context,
                    source,
                    "claim-persistence-failed:" +
                    (persistenceReason == "callback-not-run"
                        ? claimResult.FailureReason ?? "unknown"
                        : persistenceReason))
                .ConfigureAwait(false);
            return;
        }

        var updatedSession = currentSession with
        {
            Character = updatedCharacter,
            LegacyVoteClaimDueAtUtc = null,
        };
        sessions.Set(context.Connection, updatedSession);

        var vipLevelFrame = LegacyMsgUpdatePacket
            .CreateLegacyVipLevelStartupUpdate(updatedCharacter)
            .ToFrameBytes();
        await context.Connection.SendAsync(vipLevelFrame, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(vipLevelFrame, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(
                new LegacyVipStatusPacket(LegacyVipStatusFlags.VipLevelOne).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox(LegacyVoteManagerClaimMessage).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer committed LegacyNpc VoteManager due claim " +
                $"{FormatSession(context with { Session = updatedSession })} source={source} " +
                $"sourceLines={LegacyVoteManagerSourceLines} votePoints={votePoints}->{updatedVotePoints} " +
                $"vipLevel=4 vipTime={vipTime} packetOrder=VipLevel,VipLevel,VipStatusVipLevelOne,NpcMessageBox.")
            .ConfigureAwait(false);
    }

    private static bool TryPersistLegacyVoteClaim(
        WorldSession session,
        LegacyVotePersistencePlan plan,
        IReadOnlyDictionary<string, string> characterValues,
        out string reason)
    {
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(session.UsersRoot))
        {
            reason = "users-root-missing";
            return false;
        }

        var characterPath = Path.Combine(
            session.UsersRoot,
            session.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        if (!File.Exists(characterPath))
        {
            reason = "snapshot-missing";
            return false;
        }

        string votesPath;
        try
        {
            votesPath = Path.GetFullPath(plan.VotesFilePath);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            reason = "votes-path-invalid:" + exception.GetType().Name;
            return false;
        }

        if (string.Equals(Path.GetFullPath(characterPath), votesPath, StringComparison.OrdinalIgnoreCase))
        {
            reason = "transaction-path-collision";
            return false;
        }

        var votesDirectory = Path.GetDirectoryName(votesPath);
        if (string.IsNullOrWhiteSpace(votesDirectory) || !Directory.Exists(votesDirectory))
        {
            reason = "votes-directory-missing";
            return false;
        }

        string[] characterLines;
        try
        {
            characterLines = File.ReadAllLines(characterPath, LegacyCharacterSnapshotEncoding);
        }
        catch (IOException)
        {
            reason = "snapshot-read-failed";
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            reason = "snapshot-read-denied";
            return false;
        }

        if (!TryValidateLegacyCharacterUid(characterLines, session.Character.Uid, out reason) ||
            !TryApplyLegacyCharacterAttributeLines(characterLines, characterValues, out var updatedCharacterLines))
        {
            if (reason.Length == 0)
            {
                reason = "character-section-missing";
            }

            return false;
        }

        var transactionId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var characterTempPath = characterPath + ".vote-claim-" + transactionId + ".tmp";
        var characterBackupPath = characterPath + ".vote-claim-" + transactionId + ".bak";
        var votesTempPath = votesPath + ".vote-claim-" + transactionId + ".tmp";
        var votesBackupPath = votesPath + ".vote-claim-" + transactionId + ".bak";
        var votesExisted = File.Exists(votesPath);
        var characterCommitted = false;
        var votesCommitted = false;

        try
        {
            File.WriteAllLines(characterTempPath, updatedCharacterLines, LegacyCharacterSnapshotEncoding);
            File.WriteAllBytes(votesTempPath, plan.VotesFileBytes.ToArray());

            File.Replace(
                characterTempPath,
                characterPath,
                characterBackupPath,
                ignoreMetadataErrors: true);
            characterCommitted = true;

            if (votesExisted)
            {
                File.Replace(votesTempPath, votesPath, votesBackupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(votesTempPath, votesPath);
            }

            votesCommitted = true;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException or
            OverflowException)
        {
            var votesRestored = !votesCommitted ||
                TryRestoreLegacyRewardPointsFile(votesBackupPath, votesPath, votesExisted);
            var characterRestored = !characterCommitted ||
                TryRestoreLegacyRewardPointsFile(characterBackupPath, characterPath, existed: true);
            reason = votesRestored && characterRestored
                ? "transaction-write-failed:" + exception.GetType().Name
                : "transaction-rollback-failed:" + exception.GetType().Name;
            return false;
        }
        finally
        {
            TryDeleteLegacyRewardPointsFile(characterTempPath);
            TryDeleteLegacyRewardPointsFile(votesTempPath);
        }

        TryDeleteLegacyRewardPointsFile(characterBackupPath);
        TryDeleteLegacyRewardPointsFile(votesBackupPath);
        return true;
    }

    private static bool TryResolveLegacyVoteIdentity(
        WorldPacketHandlerContext context,
        WorldSession session,
        out uint uid,
        out string ipAddress,
        out string reason)
    {
        uid = 0;
        ipAddress = string.Empty;
        reason = string.Empty;

        if (session.Character.Uid is < 0 or > uint.MaxValue)
        {
            reason = "character-uid-invalid";
            return false;
        }

        if (context.Connection.RemoteEndPoint is not IPEndPoint remoteEndPoint)
        {
            reason = "remote-ip-missing";
            return false;
        }

        uid = (uint)session.Character.Uid;
        ipAddress = remoteEndPoint.Address.ToString();
        return true;
    }

    private static ValueTask LogLegacyVoteManagerFenceAsync(
        WorldPacketHandlerContext context,
        string source,
        string reason)
    {
        return new ValueTask(
            context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc VoteManager fenced " +
                $"{FormatSession(context)} source={source} sourceLines={LegacyVoteManagerSourceLines} " +
                $"reason={reason}; no character, vote store, session, or packet state was exposed."));
    }
}
