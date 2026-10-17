using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyTrojanPoleFirstRightCornerChatType = 2_108;
    private const uint LegacyTrojanPoleContinueRightCornerChatType = 2_109;
    private const string LegacyTrojanPoleScoresAttribute = "MsgTrojanMasterScores";

    private static readonly LegacyTrojanPoleScoreStateStore LegacyTrojanPoleStates = new();

    private static bool TryApplyLegacyTrojanPoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyTrojanPoleScoreEntry updatedScore)
    {
        updatedSession = session;
        updatedScore = default;

        if (damageApplied == 0 ||
            !IsLegacyTrojanPole(sobNpc) ||
            NormalizeLegacyClassPoleWarClassFamily(session.Character.Class) != 1 ||
            !IsLegacyTrojanPoleProcessAlive(session.Character))
        {
            return false;
        }

        var playerUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var playerName = session.Character.Name.Trim();
        if (playerUid == 0 || playerName.Length == 0)
        {
            return false;
        }

        updatedScore = LegacyTrojanPoleStates.UpdateScore(playerUid, playerName, damageApplied);
        updatedSession = session with
        {
            Character = WithLegacyTrojanPoleScoresAttribute(
                session.Character,
                SnapshotLegacyTrojanPoleScores()),
        };
        return true;
    }

    private static bool TryApplyLegacyTrojanPoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacyTrojanPoleFinishRoundResult finishRound)
    {
        updatedSession = session;
        finishRound = default;

        if (!damageResult.Killed ||
            !TryApplyLegacyTrojanPoleScore(
                session,
                sobNpc,
                damageResult.DamageApplied,
                out var scoredSession,
                out _))
        {
            return false;
        }

        var scores = SnapshotLegacyTrojanPoleScores();
        if (scores.Count == 0)
        {
            return false;
        }

        var roundOwner = scores[0];
        var scorePanelFrames = CreateLegacyTrojanPoleScorePanelFrames(scores);
        LegacyTrojanPoleStates.Clear();
        updatedSession = scoredSession with
        {
            Character = WithLegacyTrojanPoleFinishRoundAttributes(
                WithLegacyTrojanPoleScoresAttribute(
                    scoredSession.Character,
                    SnapshotLegacyTrojanPoleScores()),
                roundOwner,
                roundOwner.PlayerName),
        };
        finishRound = new LegacyTrojanPoleFinishRoundResult(
            roundOwner,
            ResetPoleName: roundOwner.PlayerName,
            scorePanelFrames);
        return true;
    }

    internal static void ResetLegacyTrojanPoleScoreStateForTests()
    {
        LegacyTrojanPoleStates.Clear();
    }

    private static IReadOnlyList<LegacyTrojanPoleScoreEntry> SnapshotLegacyTrojanPoleScores()
    {
        return LegacyTrojanPoleStates.SnapshotScores()
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.PlayerUid)
            .ToArray();
    }

    private static IReadOnlyList<byte[]> CreateLegacyTrojanPoleScorePanelFrames()
    {
        var scores = SnapshotLegacyTrojanPoleScores();
        return CreateLegacyTrojanPoleScorePanelFrames(scores);
    }

    private static IReadOnlyList<byte[]> CreateLegacyTrojanPoleScorePanelFrames(
        IReadOnlyList<LegacyTrojanPoleScoreEntry> scores)
    {
        if (scores.Count == 0)
        {
            return [];
        }

        return scores
            .Take(5)
            .Select((score, index) => CreateLegacyTrojanPoleScorePanelFrame(index, score))
            .ToArray();
    }

    private static byte[] CreateLegacyTrojanPoleScorePanelFrame(
        int index,
        LegacyTrojanPoleScoreEntry score)
    {
        var rank = index + 1;
        var chatType = index == 0
            ? LegacyTrojanPoleFirstRightCornerChatType
            : LegacyTrojanPoleContinueRightCornerChatType;
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

    private static async ValueTask SendLegacyTrojanPoleScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        var frames = CreateLegacyTrojanPoleScorePanelFrames();
        await SendLegacyTrojanPoleScorePanelFramesAsync(context, frames, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyTrojanPoleScorePanelFramesAsync(
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
            var character = active.Session.Character;
            if (character.MapId != LegacySobNpcEventRules.ClassPoleWarMapId ||
                NormalizeLegacyClassPoleWarClassFamily(character.Class) != 1)
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

    private static async ValueTask SendLegacyTrojanPoleFinishRoundCenterMessageAsync(
        WorldPacketHandlerContext context,
        LegacyTrojanPoleFinishRoundResult finishRound,
        CancellationToken cancellationToken)
    {
        var frame = LegacyMsgMessagePacket
            .SystemRedCenter($"The Round Ownered by {finishRound.Winner.PlayerName}.")
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

    private static bool IsLegacyTrojanPole(SobNpc sobNpc)
    {
        return LegacySobNpcEventRules.TryGetClassPoleWarPole(
                sobNpc,
                out var scheduleName,
                out var requiredClassFamily,
                out _) &&
            scheduleName.Equals("MsgTrojanMaster", StringComparison.Ordinal) &&
            requiredClassFamily == 1;
    }

    private static bool IsLegacyTrojanPoleProcessAlive(CharacterSnapshot character)
    {
        var process = ReadAttributeString(
                character.Attributes,
                GetLegacyTrojanPoleProcessAttributeNames())
            .Trim();
        return process.Equals("Alive", StringComparison.OrdinalIgnoreCase) ||
            process.Equals("ProcesType.Alive", StringComparison.OrdinalIgnoreCase) ||
            process == "1";
    }

    private static string[] GetLegacyTrojanPoleProcessAttributeNames()
    {
        return
        [
            "LegacyMsgTrojanMasterProcess",
            "MsgTrojanMasterProcess",
            "RuntimeMsgTrojanMasterProcess",
            "LegacyTrojanMasterProcess",
            "TrojanMasterProcess",
            "RuntimeTrojanMasterProcess",
            "LegacyMsgTrojanMasterProces",
            "MsgTrojanMasterProces",
            "RuntimeMsgTrojanMasterProces",
            "LegacyTrojanMasterProces",
            "TrojanMasterProces",
            "RuntimeTrojanMasterProces",
        ];
    }

    private static CharacterSnapshot WithLegacyTrojanPoleScoresAttribute(
        CharacterSnapshot character,
        IReadOnlyList<LegacyTrojanPoleScoreEntry> scores)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove(LegacyTrojanPoleScoresAttribute);
        attributes.Remove("LegacyMsgTrojanMasterScores");
        attributes.Remove("LegacyTrojanMasterScores");
        attributes.Remove("TrojanMasterScores");
        if (scores.Count > 0)
        {
            attributes[LegacyTrojanPoleScoresAttribute] = string.Join(
                ';',
                scores.Select(score => string.Join(
                    ',',
                    score.PlayerUid.ToString(CultureInfo.InvariantCulture),
                    score.Score.ToString(CultureInfo.InvariantCulture),
                    Uri.EscapeDataString(score.PlayerName))));
        }

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithLegacyTrojanPoleFinishRoundAttributes(
        CharacterSnapshot character,
        LegacyTrojanPoleScoreEntry roundOwner,
        string resetPoleName)
    {
        var playerUid = roundOwner.PlayerUid.ToString(CultureInfo.InvariantCulture);
        var score = roundOwner.Score.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyMsgTrojanMasterRoundOwnerPlayerUID"] = playerUid,
            ["LegacyMsgTrojanMasterRoundOwnerPlayerUid"] = playerUid,
            ["MsgTrojanMasterRoundOwnerPlayerUID"] = playerUid,
            ["MsgTrojanMasterRoundOwnerPlayerUid"] = playerUid,
            ["LegacyTrojanMasterRoundOwnerPlayerUID"] = playerUid,
            ["LegacyTrojanMasterRoundOwnerPlayerUid"] = playerUid,
            ["TrojanMasterRoundOwnerPlayerUID"] = playerUid,
            ["TrojanMasterRoundOwnerPlayerUid"] = playerUid,
            ["LegacyMsgTrojanMasterRoundOwnerName"] = roundOwner.PlayerName,
            ["MsgTrojanMasterRoundOwnerName"] = roundOwner.PlayerName,
            ["LegacyTrojanMasterRoundOwnerName"] = roundOwner.PlayerName,
            ["TrojanMasterRoundOwnerName"] = roundOwner.PlayerName,
            ["LegacyMsgTrojanMasterRoundOwnerScore"] = score,
            ["MsgTrojanMasterRoundOwnerScore"] = score,
            ["LegacyTrojanMasterRoundOwnerScore"] = score,
            ["TrojanMasterRoundOwnerScore"] = score,
            ["LegacyMsgTrojanMasterPoleOwnerName"] = resetPoleName,
            ["MsgTrojanMasterPoleOwnerName"] = resetPoleName,
            ["LegacyTrojanMasterPoleOwnerName"] = resetPoleName,
            ["TrojanMasterPoleOwnerName"] = resetPoleName,
            ["LegacyMsgTrojanMasterPoleReset"] = "true",
            ["MsgTrojanMasterPoleReset"] = "true",
            ["LegacyTrojanMasterPoleReset"] = "true",
            ["TrojanMasterPoleReset"] = "true",
        };
        return character with { Attributes = attributes };
    }

    private readonly record struct LegacyTrojanPoleFinishRoundResult(
        LegacyTrojanPoleScoreEntry Winner,
        string ResetPoleName,
        IReadOnlyList<byte[]> ScorePanelFrames);
}

internal sealed class LegacyTrojanPoleScoreStateStore
{
    private readonly object _sync = new();
    private readonly Dictionary<uint, LegacyTrojanPoleScoreEntry> _scores = [];

    public LegacyTrojanPoleScoreEntry UpdateScore(
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

            var created = new LegacyTrojanPoleScoreEntry(playerUid, playerName, damage);
            _scores[playerUid] = created;
            return created;
        }
    }

    public IReadOnlyList<LegacyTrojanPoleScoreEntry> SnapshotScores()
    {
        lock (_sync)
        {
            return _scores.Values
                .OrderBy(entry => entry.PlayerUid)
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

internal readonly record struct LegacyTrojanPoleScoreEntry(
    uint PlayerUid,
    string PlayerName,
    uint Score);
