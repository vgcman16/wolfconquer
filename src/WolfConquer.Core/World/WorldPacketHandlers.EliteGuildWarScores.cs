using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyEliteGuildWarFirstRightCornerChatType = 2_108;
    private const uint LegacyEliteGuildWarContinueRightCornerChatType = 2_109;
    private const string LegacyEliteGuildWarScoresAttribute = "EliteGuildWarScores";

    private static readonly LegacyEliteGuildWarScoreStateStore LegacyEliteGuildWarStates = new();

    private static bool TryApplyLegacyEliteGuildWarPoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyEliteGuildWarScoreEntry updatedScore)
    {
        updatedSession = session;
        updatedScore = default;

        if (damageApplied == 0 ||
            sobNpc.MapId != LegacySobNpcEventRules.EliteGuildWarMapId ||
            sobNpc.Id != LegacySobNpcEventRules.EliteGuildWarPoleUid ||
            !IsLegacyEliteGuildWarProcessAlive(session.Character))
        {
            return false;
        }

        var guildId = ReadAttributeUInt64(session.Character.Attributes, "GuildID", "GuildId");
        if (guildId == 0)
        {
            return false;
        }

        var guildName = ResolveLegacyEliteGuildWarGuildName(session, guildId);
        if (guildName.Length == 0)
        {
            return false;
        }

        updatedScore = LegacyEliteGuildWarStates.UpdateScore(guildId, guildName, damageApplied);
        updatedSession = session with
        {
            Character = WithLegacyEliteGuildWarScoresAttribute(
                session.Character,
                SnapshotLegacyEliteGuildWarScores()),
        };
        return true;
    }

    private static bool TryApplyLegacyEliteGuildWarPoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacyEliteGuildWarPoleFinishRoundResult finishRound)
    {
        updatedSession = session;
        finishRound = default;

        if (!damageResult.Killed ||
            !TryApplyLegacyEliteGuildWarPoleScore(
                session,
                sobNpc,
                damageResult.DamageApplied,
                out var scoredSession,
                out _))
        {
            return false;
        }

        var scores = SnapshotLegacyEliteGuildWarScores();
        if (scores.Count == 0)
        {
            return false;
        }

        var roundOwner = scores[0];
        var scorePanelFrames = CreateLegacyEliteGuildWarScorePanelFrames(scores);
        LegacyEliteGuildWarStates.Clear();
        updatedSession = scoredSession with
        {
            Character = WithLegacyEliteGuildWarFinishRoundAttributes(
                WithLegacyEliteGuildWarScoresAttribute(
                    scoredSession.Character,
                    SnapshotLegacyEliteGuildWarScores()),
                roundOwner,
                roundOwner.GuildName),
        };
        finishRound = new LegacyEliteGuildWarPoleFinishRoundResult(
            roundOwner,
            ResetPoleName: roundOwner.GuildName,
            scorePanelFrames);
        return true;
    }

    internal static void ResetLegacyEliteGuildWarScoreStateForTests()
    {
        LegacyEliteGuildWarStates.Clear();
    }

    private static IReadOnlyList<LegacyEliteGuildWarScoreEntry> SnapshotLegacyEliteGuildWarScores()
    {
        return LegacyEliteGuildWarStates.SnapshotScores()
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.GuildId)
            .ToArray();
    }

    private static IReadOnlyList<byte[]> CreateLegacyEliteGuildWarScorePanelFrames()
    {
        var scores = SnapshotLegacyEliteGuildWarScores();
        return CreateLegacyEliteGuildWarScorePanelFrames(scores);
    }

    private static IReadOnlyList<byte[]> CreateLegacyEliteGuildWarScorePanelFrames(
        IReadOnlyList<LegacyEliteGuildWarScoreEntry> scores)
    {
        if (scores.Count == 0)
        {
            return [];
        }

        return scores
            .Take(5)
            .Select((score, index) => CreateLegacyEliteGuildWarScorePanelFrame(index, score))
            .ToArray();
    }

    private static byte[] CreateLegacyEliteGuildWarScorePanelFrame(
        int index,
        LegacyEliteGuildWarScoreEntry score)
    {
        var rank = index + 1;
        var chatType = index == 0
            ? LegacyEliteGuildWarFirstRightCornerChatType
            : LegacyEliteGuildWarContinueRightCornerChatType;
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

    private static async ValueTask SendLegacyEliteGuildWarScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        var frames = CreateLegacyEliteGuildWarScorePanelFrames();
        await SendLegacyEliteGuildWarScorePanelFramesAsync(context, frames, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyEliteGuildWarScorePanelFramesAsync(
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
            if (active.Session.Character.MapId != LegacySobNpcEventRules.EliteGuildWarMapId)
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

    private static bool IsLegacyEliteGuildWarProcessAlive(CharacterSnapshot character)
    {
        var process = ReadAttributeString(
                character.Attributes,
                "LegacyEliteGuildWarProcess",
                "EliteGuildWarProcess",
                "RuntimeEliteGuildWarProcess",
                "LegacyEliteGuildWarProces",
                "EliteGuildWarProces",
                "RuntimeEliteGuildWarProces",
                "LegacyMsgEliteGuildWarProcess",
                "MsgEliteGuildWarProcess",
                "RuntimeMsgEliteGuildWarProcess",
                "LegacyMsgEliteGuildWarProces",
                "MsgEliteGuildWarProces",
                "RuntimeMsgEliteGuildWarProces")
            .Trim();
        return process.Equals("Alive", StringComparison.OrdinalIgnoreCase) ||
            process.Equals("ProcesType.Alive", StringComparison.OrdinalIgnoreCase) ||
            process == "1";
    }

    private static string ResolveLegacyEliteGuildWarGuildName(WorldSession session, ulong guildId)
    {
        var guildName = ReadAttributeString(
                session.Character.Attributes,
                "GuildName",
                "LegacyGuildName",
                "RuntimeGuildName")
            .Trim();
        if (guildName.Length == 0 &&
            guildId <= uint.MaxValue &&
            TryGetLegacyGuildInfo(session, (uint)guildId, out var guild) &&
            !string.IsNullOrWhiteSpace(guild.Name))
        {
            guildName = guild.Name.Trim();
        }

        return guildName;
    }

    private static CharacterSnapshot WithLegacyEliteGuildWarScoresAttribute(
        CharacterSnapshot character,
        IReadOnlyList<LegacyEliteGuildWarScoreEntry> scores)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove(LegacyEliteGuildWarScoresAttribute);
        attributes.Remove("LegacyEliteGuildWarScores");
        if (scores.Count > 0)
        {
            attributes[LegacyEliteGuildWarScoresAttribute] = string.Join(
                ';',
                scores.Select(score => string.Join(
                    ',',
                    score.GuildId.ToString(CultureInfo.InvariantCulture),
                    score.Score.ToString(CultureInfo.InvariantCulture),
                    Uri.EscapeDataString(score.GuildName))));
        }

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithLegacyEliteGuildWarFinishRoundAttributes(
        CharacterSnapshot character,
        LegacyEliteGuildWarScoreEntry roundOwner,
        string resetPoleName)
    {
        var guildId = roundOwner.GuildId.ToString(CultureInfo.InvariantCulture);
        var score = roundOwner.Score.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyEliteGuildWarRoundOwnerGuildID"] = guildId,
            ["LegacyEliteGuildWarRoundOwnerGuildId"] = guildId,
            ["EliteGuildWarRoundOwnerGuildID"] = guildId,
            ["EliteGuildWarRoundOwnerGuildId"] = guildId,
            ["LegacyEliteGuildWarRoundOwnerGuildName"] = roundOwner.GuildName,
            ["EliteGuildWarRoundOwnerGuildName"] = roundOwner.GuildName,
            ["LegacyEliteGuildWarRoundOwnerScore"] = score,
            ["EliteGuildWarRoundOwnerScore"] = score,
            ["LegacyEliteGuildWarPoleOwnerName"] = resetPoleName,
            ["EliteGuildWarPoleOwnerName"] = resetPoleName,
            ["LegacyEliteGuildWarPoleReset"] = "true",
            ["EliteGuildWarPoleReset"] = "true",
        };
        return character with { Attributes = attributes };
    }

    private readonly record struct LegacyEliteGuildWarPoleFinishRoundResult(
        LegacyEliteGuildWarScoreEntry Winner,
        string ResetPoleName,
        IReadOnlyList<byte[]> ScorePanelFrames);
}

internal sealed class LegacyEliteGuildWarScoreStateStore
{
    private readonly object _sync = new();
    private readonly Dictionary<ulong, LegacyEliteGuildWarScoreEntry> _scores = [];

    public LegacyEliteGuildWarScoreEntry UpdateScore(
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
                    Score = LegacyCombatDamageCalculator.AddLegacySaturating(existing.Score, damage),
                };
                _scores[guildId] = updated;
                return updated;
            }

            var created = new LegacyEliteGuildWarScoreEntry(guildId, guildName, damage);
            _scores[guildId] = created;
            return created;
        }
    }

    public IReadOnlyList<LegacyEliteGuildWarScoreEntry> SnapshotScores()
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

}

internal readonly record struct LegacyEliteGuildWarScoreEntry(
    ulong GuildId,
    string GuildName,
    uint Score);
