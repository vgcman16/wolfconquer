using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyGuildWarScoreFirstRightCornerChatType = 2_108;
    private const uint LegacyGuildWarScoreContinueRightCornerChatType = 2_109;
    private const string LegacyGuildWarScoreScoresAttribute = "GuildWarScoreScores";

    private static readonly LegacyGuildWarScoreStateStore LegacyGuildWarScoreStates = new();

    private static bool TryApplyLegacyGuildWarScorePoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyGuildWarScoreEntry updatedScore)
    {
        updatedSession = session;
        updatedScore = default;

        if (damageApplied == 0 ||
            !LegacySobNpcEventRules.IsGuildWarScorePole(sobNpc) ||
            !IsLegacyGuildWarScoreProcessAlive(session.Character))
        {
            return false;
        }

        var guildId = ReadAttributeUInt64(session.Character.Attributes, "GuildID", "GuildId");
        if (guildId == 0)
        {
            return false;
        }

        var guildName = ResolveLegacyGuildWarScoreGuildName(session, guildId);
        if (guildName.Length == 0)
        {
            return false;
        }

        updatedScore = LegacyGuildWarScoreStates.UpdateScore(guildId, guildName, damageApplied);
        updatedSession = session with
        {
            Character = WithLegacyGuildWarScoreScoresAttribute(
                session.Character,
                SnapshotLegacyGuildWarScoreScores()),
        };
        return true;
    }

    private static bool TryApplyLegacyGuildWarScorePoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacyGuildWarScorePoleFinishRoundResult finishRound)
    {
        updatedSession = session;
        finishRound = default;

        if (!damageResult.Killed ||
            !TryApplyLegacyGuildWarScorePoleScore(
                session,
                sobNpc,
                damageResult.DamageApplied,
                out var scoredSession,
                out _))
        {
            return false;
        }

        var scores = SnapshotLegacyGuildWarScoreScores();
        if (scores.Count == 0)
        {
            return false;
        }

        var roundOwner = scores[0];
        var scorePanelFrames = CreateLegacyGuildWarScoreScorePanelFrames(scores);
        updatedSession = scoredSession with
        {
            Character = WithLegacyGuildWarScoreFinishRoundAttributes(
                scoredSession.Character,
                roundOwner,
                roundOwner.GuildName),
        };
        finishRound = new LegacyGuildWarScorePoleFinishRoundResult(
            roundOwner,
            ResetPoleName: roundOwner.GuildName,
            scorePanelFrames);
        return true;
    }

    internal static void ResetLegacyGuildWarScoreStateForTests()
    {
        LegacyGuildWarScoreStates.Clear();
    }

    private static IReadOnlyList<LegacyGuildWarScoreEntry> SnapshotLegacyGuildWarScoreScores()
    {
        return LegacyGuildWarScoreStates.SnapshotScores()
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.GuildId)
            .ToArray();
    }

    private static IReadOnlyList<byte[]> CreateLegacyGuildWarScoreScorePanelFrames()
    {
        var scores = SnapshotLegacyGuildWarScoreScores();
        return CreateLegacyGuildWarScoreScorePanelFrames(scores);
    }

    private static IReadOnlyList<byte[]> CreateLegacyGuildWarScoreScorePanelFrames(
        IReadOnlyList<LegacyGuildWarScoreEntry> scores)
    {
        if (scores.Count == 0)
        {
            return [];
        }

        return scores
            .Take(5)
            .Select((score, index) => CreateLegacyGuildWarScoreScorePanelFrame(index, score))
            .ToArray();
    }

    private static byte[] CreateLegacyGuildWarScoreScorePanelFrame(
        int index,
        LegacyGuildWarScoreEntry score)
    {
        var rank = index + 1;
        var chatType = index == 0
            ? LegacyGuildWarScoreFirstRightCornerChatType
            : LegacyGuildWarScoreContinueRightCornerChatType;
        var message =
            $"No {rank.ToString(CultureInfo.InvariantCulture)}. {score.GuildName} ({score.Score.ToString(CultureInfo.InvariantCulture)})";
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

    private static async ValueTask SendLegacyGuildWarScoreScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        var frames = CreateLegacyGuildWarScoreScorePanelFrames();
        await SendLegacyGuildWarScoreScorePanelFramesAsync(context, frames, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyGuildWarScoreScorePanelFramesAsync(
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
            if (active.Session.Character.MapId != LegacySobNpcEventRules.GuildWarScoreMapId)
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

    private static async ValueTask SendLegacyGuildWarScoreFinishRoundCenterMessageAsync(
        WorldPacketHandlerContext context,
        LegacyGuildWarScorePoleFinishRoundResult finishRound,
        CancellationToken cancellationToken)
    {
        var frame = LegacyMsgMessagePacket
            .SystemCenter($"The Round Ownered by guild {finishRound.Winner.GuildName}.")
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

    private static bool IsLegacyGuildWarScoreProcessAlive(CharacterSnapshot character)
    {
        var process = ReadAttributeString(
                character.Attributes,
                GetLegacyGuildWarScoreProcessAttributeNames())
            .Trim();
        return process.Equals("Alive", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] GetLegacyGuildWarScoreProcessAttributeNames()
    {
        return
        [
            "LegacyMsgGuildWarScoreProcess",
            "MsgGuildWarScoreProcess",
            "RuntimeMsgGuildWarScoreProcess",
            "LegacyGuildWarScoreProcess",
            "GuildWarScoreProcess",
            "RuntimeGuildWarScoreProcess",
            "LegacyMsgGuildWarScoreProces",
            "MsgGuildWarScoreProces",
            "RuntimeMsgGuildWarScoreProces",
            "LegacyGuildWarScoreProces",
            "GuildWarScoreProces",
            "RuntimeGuildWarScoreProces",
            "LegacyMsgTopWarScoreProces",
            "MsgTopWarScoreProces",
            "RuntimeMsgTopWarScoreProces",
            "LegacyTopWarScoreProces",
            "TopWarScoreProces",
            "RuntimeTopWarScoreProces",
        ];
    }

    private static string ResolveLegacyGuildWarScoreGuildName(WorldSession session, ulong guildId)
    {
        var guildName = ReadAttributeString(session.Character.Attributes, "GuildName", "LegacyGuildName").Trim();
        if (guildName.Length == 0 &&
            guildId <= uint.MaxValue &&
            TryGetLegacyGuildInfo(session, (uint)guildId, out var guild) &&
            !string.IsNullOrWhiteSpace(guild.Name))
        {
            guildName = guild.Name.Trim();
        }

        return guildName;
    }

    private static CharacterSnapshot WithLegacyGuildWarScoreScoresAttribute(
        CharacterSnapshot character,
        IReadOnlyList<LegacyGuildWarScoreEntry> scores)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove(LegacyGuildWarScoreScoresAttribute);
        attributes.Remove("LegacyGuildWarScoreScores");
        if (scores.Count > 0)
        {
            attributes[LegacyGuildWarScoreScoresAttribute] = string.Join(
                ';',
                scores.Select(score => string.Join(
                    ',',
                    score.GuildId.ToString(CultureInfo.InvariantCulture),
                    score.Score.ToString(CultureInfo.InvariantCulture),
                    Uri.EscapeDataString(score.GuildName))));
        }

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithLegacyGuildWarScoreFinishRoundAttributes(
        CharacterSnapshot character,
        LegacyGuildWarScoreEntry roundOwner,
        string resetPoleName)
    {
        var guildId = roundOwner.GuildId.ToString(CultureInfo.InvariantCulture);
        var score = roundOwner.Score.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyGuildWarScoreRoundOwnerGuildID"] = guildId,
            ["GuildWarScoreRoundOwnerGuildID"] = guildId,
            ["LegacyGuildWarScoreRoundOwnerGuildName"] = roundOwner.GuildName,
            ["GuildWarScoreRoundOwnerGuildName"] = roundOwner.GuildName,
            ["LegacyGuildWarScoreRoundOwnerScore"] = score,
            ["GuildWarScoreRoundOwnerScore"] = score,
            ["LegacyTopWarScoreRoundOwnerGuildID"] = guildId,
            ["TopWarScoreRoundOwnerGuildID"] = guildId,
            ["LegacyTopWarScoreRoundOwnerGuildName"] = roundOwner.GuildName,
            ["TopWarScoreRoundOwnerGuildName"] = roundOwner.GuildName,
            ["LegacyTopWarScoreRoundOwnerScore"] = score,
            ["TopWarScoreRoundOwnerScore"] = score,
            ["LegacyGuildWarScorePoleOwnerName"] = resetPoleName,
            ["GuildWarScorePoleOwnerName"] = resetPoleName,
            ["LegacyTopWarScorePoleOwnerName"] = resetPoleName,
            ["TopWarScorePoleOwnerName"] = resetPoleName,
            ["LegacyGuildWarScorePoleReset"] = "true",
            ["GuildWarScorePoleReset"] = "true",
            ["LegacyTopWarScorePoleReset"] = "true",
            ["TopWarScorePoleReset"] = "true",
        };
        return character with { Attributes = attributes };
    }

    private readonly record struct LegacyGuildWarScorePoleFinishRoundResult(
        LegacyGuildWarScoreEntry Winner,
        string ResetPoleName,
        IReadOnlyList<byte[]> ScorePanelFrames);
}

internal sealed class LegacyGuildWarScoreStateStore
{
    private readonly object _sync = new();
    private readonly Dictionary<ulong, LegacyGuildWarScoreEntry> _scores = [];

    public LegacyGuildWarScoreEntry UpdateScore(
        ulong guildId,
        string guildName,
        uint damage)
    {
        if (guildId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(guildId), guildId, "Guild id must be non-zero.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(guildName);

        lock (_sync)
        {
            if (_scores.TryGetValue(guildId, out var existing))
            {
                var updated = existing with
                {
                    GuildName = guildName,
                    Score = SaturatingAdd(existing.Score, damage),
                };
                _scores[guildId] = updated;
                return updated;
            }

            var created = new LegacyGuildWarScoreEntry(guildId, guildName, damage);
            _scores[guildId] = created;
            return created;
        }
    }

    public IReadOnlyList<LegacyGuildWarScoreEntry> SnapshotScores()
    {
        lock (_sync)
        {
            return _scores.Values
                .OrderBy(entry => entry.GuildId)
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

    private static uint SaturatingAdd(uint left, uint right)
    {
        return left > uint.MaxValue - right ? uint.MaxValue : left + right;
    }
}

internal readonly record struct LegacyGuildWarScoreEntry(
    ulong GuildId,
    string GuildName,
    uint Score);
