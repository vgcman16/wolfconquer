using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyArcherPoleFirstRightCornerChatType = 2_108;
    private const uint LegacyArcherPoleContinueRightCornerChatType = 2_109;
    private const string LegacyArcherPoleScoresAttribute = "ArcherMasterScores";

    private static readonly LegacyArcherPoleScoreStateStore LegacyArcherPoleStates = new();

    private static bool TryApplyLegacyArcherPoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyArcherPoleScoreEntry updatedScore)
    {
        updatedSession = session;
        updatedScore = default;

        if (damageApplied == 0 ||
            !LegacySobNpcEventRules.TryGetClassPoleWarPole(
                sobNpc,
                out var scheduleName,
                out var requiredClassFamily,
                out _) ||
            scheduleName != "MsgArcherMaster" ||
            requiredClassFamily != 4 ||
            NormalizeLegacyClassPoleWarClassFamily(session.Character.Class) != requiredClassFamily ||
            !IsLegacyArcherPoleProcessAlive(session.Character))
        {
            return false;
        }

        var playerUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var playerName = session.Character.Name.Trim();
        if (playerUid == 0 || playerName.Length == 0)
        {
            return false;
        }

        updatedScore = LegacyArcherPoleStates.UpdateScore(playerUid, playerName, damageApplied);
        updatedSession = session with
        {
            Character = WithLegacyArcherPoleScoresAttribute(
                session.Character,
                SnapshotLegacyArcherPoleScores()),
        };
        return true;
    }

    private static bool TryApplyLegacyArcherPoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacyArcherPoleFinishRoundResult finishRound)
    {
        updatedSession = session;
        finishRound = default;

        if (!damageResult.Killed ||
            !TryApplyLegacyArcherPoleScore(
                session,
                sobNpc,
                damageResult.DamageApplied,
                out var scoredSession,
                out _))
        {
            return false;
        }

        var scores = SnapshotLegacyArcherPoleScores();
        if (scores.Count == 0)
        {
            return false;
        }

        var roundOwner = scores[0];
        var scorePanelFrames = CreateLegacyArcherPoleScorePanelFrames(scores);
        LegacyArcherPoleStates.Clear();
        updatedSession = scoredSession with
        {
            Character = WithLegacyArcherPoleFinishRoundAttributes(
                WithLegacyArcherPoleScoresAttribute(
                    scoredSession.Character,
                    SnapshotLegacyArcherPoleScores()),
                roundOwner,
                roundOwner.PlayerName),
        };
        finishRound = new LegacyArcherPoleFinishRoundResult(
            roundOwner,
            ResetPoleName: roundOwner.PlayerName,
            scorePanelFrames);
        return true;
    }

    internal static void ResetLegacyArcherPoleScoreStateForTests()
    {
        LegacyArcherPoleStates.Clear();
    }

    private static IReadOnlyList<LegacyArcherPoleScoreEntry> SnapshotLegacyArcherPoleScores()
    {
        return LegacyArcherPoleStates.SnapshotScores();
    }

    private static IReadOnlyList<byte[]> CreateLegacyArcherPoleScorePanelFrames()
    {
        var scores = SnapshotLegacyArcherPoleScores();
        return CreateLegacyArcherPoleScorePanelFrames(scores);
    }

    private static IReadOnlyList<byte[]> CreateLegacyArcherPoleScorePanelFrames(
        IReadOnlyList<LegacyArcherPoleScoreEntry> scores)
    {
        if (scores.Count == 0)
        {
            return [];
        }

        return scores
            .Take(5)
            .Select((score, index) => CreateLegacyArcherPoleScorePanelFrame(index, score))
            .ToArray();
    }

    private static byte[] CreateLegacyArcherPoleScorePanelFrame(
        int index,
        LegacyArcherPoleScoreEntry score)
    {
        var rank = index + 1;
        var chatType = index == 0
            ? LegacyArcherPoleFirstRightCornerChatType
            : LegacyArcherPoleContinueRightCornerChatType;
        var message =
            $"No {rank.ToString(CultureInfo.InvariantCulture)}. {score.PlayerName} ({score.Score.ToString(CultureInfo.InvariantCulture)})";
        return new LegacyMsgMessagePacket(
            LegacyMsgMessagePacket.YellowColor,
            chatType,
            MessageUid1: 0,
            MessageUid2: 0,
            ServerId: 0,
            Mesh: 0,
            UnionType: 0,
            Count: 1,
            New1: 0,
            New2: 0,
            ["SYSTEM", "ALL", string.Empty, message, string.Empty, string.Empty, string.Empty])
            .ToFrameBytes();
    }

    private static async ValueTask SendLegacyArcherPoleScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        var frames = CreateLegacyArcherPoleScorePanelFrames();
        await SendLegacyArcherPoleScorePanelFramesAsync(context, frames, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyArcherPoleScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        IReadOnlyList<byte[]> frames,
        CancellationToken cancellationToken)
    {
        if (frames.Count == 0)
        {
            return;
        }

        var activeSessions = context.Sessions?.Snapshot();
        if (activeSessions is null || activeSessions.Count == 0)
        {
            foreach (var frame in frames)
            {
                await context.Connection.SendAsync(frame, cancellationToken)
                    .ConfigureAwait(false);
            }

            return;
        }

        foreach (var active in activeSessions)
        {
            if (active.Session.Character.MapId != LegacySobNpcEventRules.ClassPoleWarMapId ||
                NormalizeLegacyClassPoleWarClassFamily(active.Session.Character.Class) != 4)
            {
                continue;
            }

            foreach (var frame in frames)
            {
                await active.Connection.SendAsync(frame, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static async ValueTask SendLegacyArcherPoleFinishRoundCenterMessageAsync(
        WorldPacketHandlerContext context,
        LegacyArcherPoleFinishRoundResult finishRound,
        CancellationToken cancellationToken)
    {
        var frame = LegacyMsgMessagePacket
            .SystemCenter($"The Round Ownered by {finishRound.Winner.PlayerName}.")
            .ToFrameBytes();
        var activeSessions = context.Sessions?.Snapshot();
        if (activeSessions is null || activeSessions.Count == 0)
        {
            await context.Connection.SendAsync(frame, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        foreach (var active in activeSessions)
        {
            await active.Connection.SendAsync(frame, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static bool IsLegacyArcherPoleProcessAlive(CharacterSnapshot character)
    {
        var process = ReadAttributeString(
                character.Attributes,
                GetLegacyClassPoleWarAttributeNames("MsgArcherMaster", "Process"))
            .Trim();
        return process.Equals("Alive", StringComparison.OrdinalIgnoreCase);
    }

    private static CharacterSnapshot WithLegacyArcherPoleScoresAttribute(
        CharacterSnapshot character,
        IReadOnlyList<LegacyArcherPoleScoreEntry> scores)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove(LegacyArcherPoleScoresAttribute);
        attributes.Remove("LegacyArcherMasterScores");
        attributes.Remove("MsgArcherMasterScores");
        attributes.Remove("LegacyMsgArcherMasterScores");
        attributes.Remove("ArcherPoleScores");
        attributes.Remove("LegacyArcherPoleScores");
        if (scores.Count > 0)
        {
            attributes[LegacyArcherPoleScoresAttribute] = string.Join(
                ';',
                scores.Select(score => string.Join(
                    ',',
                    score.PlayerUid.ToString(CultureInfo.InvariantCulture),
                    score.Score.ToString(CultureInfo.InvariantCulture),
                    Uri.EscapeDataString(score.PlayerName))));
        }

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithLegacyArcherPoleFinishRoundAttributes(
        CharacterSnapshot character,
        LegacyArcherPoleScoreEntry roundOwner,
        string resetPoleName)
    {
        var playerUid = roundOwner.PlayerUid.ToString(CultureInfo.InvariantCulture);
        var score = roundOwner.Score.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyMsgArcherMasterRoundOwnerPlayerUID"] = playerUid,
            ["LegacyMsgArcherMasterRoundOwnerPlayerUid"] = playerUid,
            ["MsgArcherMasterRoundOwnerPlayerUID"] = playerUid,
            ["MsgArcherMasterRoundOwnerPlayerUid"] = playerUid,
            ["LegacyArcherMasterRoundOwnerPlayerUID"] = playerUid,
            ["LegacyArcherMasterRoundOwnerPlayerUid"] = playerUid,
            ["ArcherMasterRoundOwnerPlayerUID"] = playerUid,
            ["ArcherMasterRoundOwnerPlayerUid"] = playerUid,
            ["LegacyArcherPoleRoundOwnerPlayerUID"] = playerUid,
            ["LegacyArcherPoleRoundOwnerPlayerUid"] = playerUid,
            ["ArcherPoleRoundOwnerPlayerUID"] = playerUid,
            ["ArcherPoleRoundOwnerPlayerUid"] = playerUid,
            ["LegacyMsgArcherMasterRoundOwnerName"] = roundOwner.PlayerName,
            ["MsgArcherMasterRoundOwnerName"] = roundOwner.PlayerName,
            ["LegacyArcherMasterRoundOwnerName"] = roundOwner.PlayerName,
            ["ArcherMasterRoundOwnerName"] = roundOwner.PlayerName,
            ["LegacyArcherPoleRoundOwnerName"] = roundOwner.PlayerName,
            ["ArcherPoleRoundOwnerName"] = roundOwner.PlayerName,
            ["LegacyMsgArcherMasterRoundOwnerScore"] = score,
            ["MsgArcherMasterRoundOwnerScore"] = score,
            ["LegacyArcherMasterRoundOwnerScore"] = score,
            ["ArcherMasterRoundOwnerScore"] = score,
            ["LegacyArcherPoleRoundOwnerScore"] = score,
            ["ArcherPoleRoundOwnerScore"] = score,
            ["LegacyMsgArcherMasterPoleOwnerName"] = resetPoleName,
            ["MsgArcherMasterPoleOwnerName"] = resetPoleName,
            ["LegacyArcherMasterPoleOwnerName"] = resetPoleName,
            ["ArcherMasterPoleOwnerName"] = resetPoleName,
            ["LegacyArcherPolePoleOwnerName"] = resetPoleName,
            ["ArcherPolePoleOwnerName"] = resetPoleName,
            ["LegacyMsgArcherMasterPoleReset"] = "true",
            ["MsgArcherMasterPoleReset"] = "true",
            ["LegacyArcherMasterPoleReset"] = "true",
            ["ArcherMasterPoleReset"] = "true",
            ["LegacyArcherPolePoleReset"] = "true",
            ["ArcherPolePoleReset"] = "true",
        };
        return character with { Attributes = attributes };
    }

    private readonly record struct LegacyArcherPoleFinishRoundResult(
        LegacyArcherPoleScoreEntry Winner,
        string ResetPoleName,
        IReadOnlyList<byte[]> ScorePanelFrames);
}

internal sealed class LegacyArcherPoleScoreStateStore
{
    private readonly object _sync = new();
    private readonly Dictionary<uint, LegacyArcherPoleScoreEntry> _scores = [];

    public LegacyArcherPoleScoreEntry UpdateScore(
        uint playerUid,
        string playerName,
        uint damage)
    {
        if (playerUid == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(playerUid), playerUid, "Player UID must be non-zero.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(playerName);

        lock (_sync)
        {
            if (_scores.TryGetValue(playerUid, out var existing))
            {
                var updated = existing with
                {
                    PlayerName = playerName,
                    Score = LegacyCombatDamageCalculator.AddLegacySaturating(existing.Score, damage),
                };
                _scores[playerUid] = updated;
                return updated;
            }

            var created = new LegacyArcherPoleScoreEntry(playerUid, playerName, damage);
            _scores[playerUid] = created;
            return created;
        }
    }

    public IReadOnlyList<LegacyArcherPoleScoreEntry> SnapshotScores()
    {
        lock (_sync)
        {
            return _scores.Values
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => entry.PlayerUid)
                .ToArray();
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _scores.Clear();
        }
    }
}

internal readonly record struct LegacyArcherPoleScoreEntry(
    uint PlayerUid,
    string PlayerName,
    uint Score);
