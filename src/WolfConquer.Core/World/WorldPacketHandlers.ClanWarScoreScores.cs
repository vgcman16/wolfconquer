using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyClanWarScoreFirstRightCornerChatType = 2_108;
    private const uint LegacyClanWarScoreContinueRightCornerChatType = 2_109;
    private const string LegacyClanWarScoreScoresAttribute = "ClanWarScoreScores";

    private static readonly LegacyClanWarScoreStateStore LegacyClanWarScoreStates = new();

    private static bool TryApplyLegacyClanWarScorePoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyClanWarScoreEntry updatedScore)
    {
        updatedSession = session;
        updatedScore = default;

        if (damageApplied == 0 ||
            !LegacySobNpcEventRules.IsClanWarScorePole(sobNpc) ||
            !IsLegacyClanWarScoreProcessAlive(session.Character))
        {
            return false;
        }

        var clanId = ReadAttributeUInt64(session.Character.Attributes, "ClanID", "ClanUID");
        if (clanId == 0)
        {
            return false;
        }

        var clanName = ReadAttributeString(
                session.Character.Attributes,
                "ClanName",
                "LegacyClanName",
                "RuntimeClanName")
            .Trim();
        if (clanName.Length == 0)
        {
            return false;
        }

        updatedScore = LegacyClanWarScoreStates.UpdateScore(clanId, clanName, damageApplied);
        updatedSession = session with
        {
            Character = WithLegacyClanWarScoreScoresAttribute(
                session.Character,
                SnapshotLegacyClanWarScoreScores()),
        };
        return true;
    }

    private static bool TryApplyLegacyClanWarScorePoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacyClanWarScorePoleFinishRoundResult finishRound)
    {
        updatedSession = session;
        finishRound = default;

        if (!damageResult.Killed ||
            !TryApplyLegacyClanWarScorePoleScore(
                session,
                sobNpc,
                damageResult.DamageApplied,
                out var scoredSession,
                out _))
        {
            return false;
        }

        var scores = SnapshotLegacyClanWarScoreScores();
        if (scores.Count == 0)
        {
            return false;
        }

        var roundOwner = scores[0];
        var scorePanelFrames = CreateLegacyClanWarScoreScorePanelFrames(scores);
        LegacyClanWarScoreStates.Clear();
        updatedSession = scoredSession with
        {
            Character = WithLegacyClanWarScoreFinishRoundAttributes(
                WithLegacyClanWarScoreScoresAttribute(
                    scoredSession.Character,
                    SnapshotLegacyClanWarScoreScores()),
                roundOwner,
                roundOwner.ClanName),
        };
        finishRound = new LegacyClanWarScorePoleFinishRoundResult(
            roundOwner,
            ResetPoleName: roundOwner.ClanName,
            scorePanelFrames);
        return true;
    }

    internal static void ResetLegacyClanWarScoreStateForTests()
    {
        LegacyClanWarScoreStates.Clear();
    }

    private static IReadOnlyList<LegacyClanWarScoreEntry> SnapshotLegacyClanWarScoreScores()
    {
        return LegacyClanWarScoreStates.SnapshotScores()
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.ClanId)
            .ToArray();
    }

    private static IReadOnlyList<byte[]> CreateLegacyClanWarScoreScorePanelFrames()
    {
        var scores = SnapshotLegacyClanWarScoreScores();
        return CreateLegacyClanWarScoreScorePanelFrames(scores);
    }

    private static IReadOnlyList<byte[]> CreateLegacyClanWarScoreScorePanelFrames(
        IReadOnlyList<LegacyClanWarScoreEntry> scores)
    {
        if (scores.Count == 0)
        {
            return [];
        }

        return scores
            .Take(5)
            .Select((score, index) => CreateLegacyClanWarScoreScorePanelFrame(index, score))
            .ToArray();
    }

    private static byte[] CreateLegacyClanWarScoreScorePanelFrame(
        int index,
        LegacyClanWarScoreEntry score)
    {
        var rank = index + 1;
        var chatType = index == 0
            ? LegacyClanWarScoreFirstRightCornerChatType
            : LegacyClanWarScoreContinueRightCornerChatType;
        var message =
            $"No {rank.ToString(CultureInfo.InvariantCulture)}. {score.ClanName} ({score.Score.ToString(CultureInfo.InvariantCulture)})";
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

    private static async ValueTask SendLegacyClanWarScoreScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        var frames = CreateLegacyClanWarScoreScorePanelFrames();
        await SendLegacyClanWarScoreScorePanelFramesAsync(context, frames, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyClanWarScoreScorePanelFramesAsync(
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
            if (active.Session.Character.MapId != LegacySobNpcEventRules.ClanWarScoreMapId)
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

    private static async ValueTask SendLegacyClanWarScoreFinishRoundCenterMessageAsync(
        WorldPacketHandlerContext context,
        LegacyClanWarScorePoleFinishRoundResult finishRound,
        CancellationToken cancellationToken)
    {
        var frame = LegacyMsgMessagePacket
            .SystemRedCenter($"The Round Ownered by Clan {finishRound.Winner.ClanName}.")
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

    private static bool IsLegacyClanWarScoreProcessAlive(CharacterSnapshot character)
    {
        var process = ReadAttributeString(
                character.Attributes,
                GetLegacyClanWarScoreProcessAttributeNames())
            .Trim();
        return process.Equals("Alive", StringComparison.OrdinalIgnoreCase) ||
            process.Equals("ProcesType.Alive", StringComparison.OrdinalIgnoreCase) ||
            process == "1";
    }

    private static string[] GetLegacyClanWarScoreProcessAttributeNames()
    {
        return
        [
            "LegacyMsgClanWarScoreProcess",
            "MsgClanWarScoreProcess",
            "RuntimeMsgClanWarScoreProcess",
            "LegacyClanWarScoreProcess",
            "ClanWarScoreProcess",
            "RuntimeClanWarScoreProcess",
            "LegacyMsgClanWarScoreProces",
            "MsgClanWarScoreProces",
            "RuntimeMsgClanWarScoreProces",
            "LegacyClanWarScoreProces",
            "ClanWarScoreProces",
            "RuntimeClanWarScoreProces",
            "LegacyMsgClanWProcess",
            "MsgClanWProcess",
            "RuntimeMsgClanWProcess",
            "LegacyMsgClanWProces",
            "MsgClanWProces",
            "RuntimeMsgClanWProces",
        ];
    }

    private static CharacterSnapshot WithLegacyClanWarScoreScoresAttribute(
        CharacterSnapshot character,
        IReadOnlyList<LegacyClanWarScoreEntry> scores)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove(LegacyClanWarScoreScoresAttribute);
        attributes.Remove("LegacyClanWarScoreScores");
        if (scores.Count > 0)
        {
            attributes[LegacyClanWarScoreScoresAttribute] = string.Join(
                ';',
                scores.Select(score => string.Join(
                    ',',
                    score.ClanId.ToString(CultureInfo.InvariantCulture),
                    score.Score.ToString(CultureInfo.InvariantCulture),
                    Uri.EscapeDataString(score.ClanName))));
        }

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithLegacyClanWarScoreFinishRoundAttributes(
        CharacterSnapshot character,
        LegacyClanWarScoreEntry roundOwner,
        string resetPoleName)
    {
        var clanId = roundOwner.ClanId.ToString(CultureInfo.InvariantCulture);
        var score = roundOwner.Score.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyMsgClanWRoundOwnerClainID"] = clanId,
            ["MsgClanWRoundOwnerClainID"] = clanId,
            ["LegacyMsgClanWRoundOwnerClanID"] = clanId,
            ["MsgClanWRoundOwnerClanID"] = clanId,
            ["LegacyMsgClanWRoundOwnerName"] = roundOwner.ClanName,
            ["MsgClanWRoundOwnerName"] = roundOwner.ClanName,
            ["LegacyMsgClanWRoundOwnerScore"] = score,
            ["MsgClanWRoundOwnerScore"] = score,
            ["LegacyClanWarScoreRoundOwnerClainID"] = clanId,
            ["ClanWarScoreRoundOwnerClainID"] = clanId,
            ["LegacyClanWarScoreRoundOwnerClanID"] = clanId,
            ["ClanWarScoreRoundOwnerClanID"] = clanId,
            ["LegacyClanWarScoreRoundOwnerName"] = roundOwner.ClanName,
            ["ClanWarScoreRoundOwnerName"] = roundOwner.ClanName,
            ["LegacyClanWarScoreRoundOwnerScore"] = score,
            ["ClanWarScoreRoundOwnerScore"] = score,
            ["LegacyMsgClanWPoleOwnerName"] = resetPoleName,
            ["MsgClanWPoleOwnerName"] = resetPoleName,
            ["LegacyClanWarScorePoleOwnerName"] = resetPoleName,
            ["ClanWarScorePoleOwnerName"] = resetPoleName,
            ["LegacyMsgClanWPoleReset"] = "true",
            ["MsgClanWPoleReset"] = "true",
            ["LegacyClanWarScorePoleReset"] = "true",
            ["ClanWarScorePoleReset"] = "true",
        };
        return character with { Attributes = attributes };
    }

    private readonly record struct LegacyClanWarScorePoleFinishRoundResult(
        LegacyClanWarScoreEntry Winner,
        string ResetPoleName,
        IReadOnlyList<byte[]> ScorePanelFrames);
}

internal sealed class LegacyClanWarScoreStateStore
{
    private readonly object _sync = new();
    private readonly Dictionary<ulong, LegacyClanWarScoreEntry> _scores = [];

    public LegacyClanWarScoreEntry UpdateScore(
        ulong clanId,
        string clanName,
        uint damage)
    {
        if (clanId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(clanId), clanId, "Clan id must be non-zero.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(clanName);

        lock (_sync)
        {
            if (_scores.TryGetValue(clanId, out var existing))
            {
                return existing;
            }

            var created = new LegacyClanWarScoreEntry(clanId, clanName, damage);
            _scores[clanId] = created;
            return created;
        }
    }

    public IReadOnlyList<LegacyClanWarScoreEntry> SnapshotScores()
    {
        lock (_sync)
        {
            return _scores.Values
                .OrderBy(entry => entry.ClanId)
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

internal readonly record struct LegacyClanWarScoreEntry(
    ulong ClanId,
    string ClanName,
    uint Score);
