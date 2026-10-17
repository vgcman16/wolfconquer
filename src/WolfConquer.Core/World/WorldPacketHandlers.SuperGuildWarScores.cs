using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacySuperGuildWarFirstRightCornerChatType = 2_108;
    private const uint LegacySuperGuildWarContinueRightCornerChatType = 2_109;
    private const string LegacySuperGuildWarScoresAttribute = "SuperGuildWarScores";

    private static bool TryApplyLegacySuperGuildWarPoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacySuperGuildWarScoreEntry updatedScore)
    {
        updatedSession = session;
        updatedScore = default;

        if (damageApplied == 0 ||
            !LegacySobNpcEventRules.IsSuperGuildWarPole(sobNpc) ||
            !IsLegacySuperGuildWarProcessAlive(session.Character))
        {
            return false;
        }

        var guildId = ReadAttributeUInt64(session.Character.Attributes, "GuildID", "GuildId");
        if (guildId == 0)
        {
            return false;
        }

        var guildName = ResolveLegacySuperGuildWarGuildName(session, guildId);
        if (guildName.Length == 0)
        {
            return false;
        }

        updatedScore = LegacySuperGuildWarStates.UpdateScore(guildId, guildName, damageApplied);
        updatedSession = session with
        {
            Character = WithLegacySuperGuildWarScoresAttribute(
                session.Character,
                SnapshotLegacySuperGuildWarScores()),
        };
        return true;
    }

    private static bool TryApplyLegacySuperGuildWarPoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacySuperGuildWarPoleFinishRoundResult finishRound)
    {
        updatedSession = session;
        finishRound = default;

        if (!damageResult.Killed ||
            !TryApplyLegacySuperGuildWarPoleScore(
                session,
                sobNpc,
                damageResult.DamageApplied,
                out var scoredSession,
                out _))
        {
            return false;
        }

        var scores = SnapshotLegacySuperGuildWarScores();
        if (scores.Count == 0)
        {
            return false;
        }

        var winner = scores[0];
        var scorePanelFrames = CreateLegacySuperGuildWarScorePanelFrames(scores);
        LegacySuperGuildWarStates.ClearScores();
        updatedSession = scoredSession with
        {
            Character = WithLegacySuperGuildWarFinishRoundAttributes(
                WithLegacySuperGuildWarScoresAttribute(
                    scoredSession.Character,
                    SnapshotLegacySuperGuildWarScores()),
                winner,
                winner.GuildName),
        };
        finishRound = new LegacySuperGuildWarPoleFinishRoundResult(
            winner,
            ResetPoleName: winner.GuildName,
            scorePanelFrames);
        return true;
    }

    private static IReadOnlyList<LegacySuperGuildWarScoreEntry> SnapshotLegacySuperGuildWarScores()
    {
        return LegacySuperGuildWarStates.SnapshotScores()
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.GuildId)
            .ToArray();
    }

    private static IReadOnlyList<byte[]> CreateLegacySuperGuildWarScorePanelFrames()
    {
        var scores = SnapshotLegacySuperGuildWarScores();
        return CreateLegacySuperGuildWarScorePanelFrames(scores);
    }

    private static IReadOnlyList<byte[]> CreateLegacySuperGuildWarScorePanelFrames(
        IReadOnlyList<LegacySuperGuildWarScoreEntry> scores)
    {
        if (scores.Count == 0)
        {
            return [];
        }

        return scores
            .Take(5)
            .Select((score, index) => CreateLegacySuperGuildWarScorePanelFrame(index, score))
            .ToArray();
    }

    private static byte[] CreateLegacySuperGuildWarScorePanelFrame(
        int index,
        LegacySuperGuildWarScoreEntry score)
    {
        var rank = index + 1;
        var chatType = index == 0
            ? LegacySuperGuildWarFirstRightCornerChatType
            : LegacySuperGuildWarContinueRightCornerChatType;
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

    private static async ValueTask SendLegacySuperGuildWarScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        var frames = CreateLegacySuperGuildWarScorePanelFrames();
        await SendLegacySuperGuildWarScorePanelFramesAsync(context, frames, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacySuperGuildWarScorePanelFramesAsync(
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
            if (active.Session.Character.MapId != LegacySobNpcEventRules.SuperGuildWarMapId)
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

    private static async ValueTask SendLegacySuperGuildWarFinishRoundMessagesAsync(
        WorldPacketHandlerContext context,
        LegacySuperGuildWarPoleFinishRoundResult finishRound,
        CancellationToken cancellationToken)
    {
        var score = finishRound.Winner.Score.ToString(CultureInfo.InvariantCulture);
        var message =
            $"Congratulations to {finishRound.Winner.GuildName}, they've won the Super Guild War with a score of {score}";
        var systemFrame = LegacyMsgMessagePacket
            .SystemWhiteTalk(message)
            .ToFrameBytes();
        var centerFrame = LegacyMsgMessagePacket
            .SystemRedCenter(message)
            .ToFrameBytes();
        var activeSessions = context.Sessions?.Snapshot();
        if (activeSessions is null || activeSessions.Count == 0)
        {
            await context.Connection.SendAsync(systemFrame, cancellationToken)
                .ConfigureAwait(false);
            await context.Connection.SendAsync(centerFrame, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        foreach (var active in activeSessions)
        {
            await active.Connection.SendAsync(systemFrame, cancellationToken)
                .ConfigureAwait(false);
            await active.Connection.SendAsync(centerFrame, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static bool IsLegacySuperGuildWarProcessAlive(CharacterSnapshot character)
    {
        var process = ReadAttributeString(
                character.Attributes,
                GetLegacySuperGuildWarAttributeNames("Process", "Proces"))
            .Trim();
        return process.Equals("Alive", StringComparison.OrdinalIgnoreCase) ||
            process.Equals("ProcesType.Alive", StringComparison.OrdinalIgnoreCase) ||
            process == "1";
    }

    private static string ResolveLegacySuperGuildWarGuildName(WorldSession session, ulong guildId)
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

    private static CharacterSnapshot WithLegacySuperGuildWarScoresAttribute(
        CharacterSnapshot character,
        IReadOnlyList<LegacySuperGuildWarScoreEntry> scores)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove(LegacySuperGuildWarScoresAttribute);
        attributes.Remove("LegacySuperGuildWarScores");
        if (scores.Count > 0)
        {
            attributes[LegacySuperGuildWarScoresAttribute] = string.Join(
                ';',
                scores.Select(score => string.Join(
                    ',',
                    score.GuildId.ToString(CultureInfo.InvariantCulture),
                    score.Score.ToString(CultureInfo.InvariantCulture),
                    Uri.EscapeDataString(score.GuildName))));
        }

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithLegacySuperGuildWarFinishRoundAttributes(
        CharacterSnapshot character,
        LegacySuperGuildWarScoreEntry winner,
        string resetPoleName)
    {
        var guildId = winner.GuildId.ToString(CultureInfo.InvariantCulture);
        var score = winner.Score.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        foreach (var name in GetLegacySuperGuildWarAttributeNames("WinnerGuildID", "WinnerGuildId"))
        {
            attributes[name] = guildId;
        }

        foreach (var name in GetLegacySuperGuildWarAttributeNames("RoundOwnerGuildID", "RoundOwnerGuildId"))
        {
            attributes[name] = guildId;
        }

        foreach (var name in GetLegacySuperGuildWarAttributeNames("WinnerGuildName", "RoundOwnerGuildName", "PoleOwnerName"))
        {
            attributes[name] = resetPoleName;
        }

        foreach (var name in GetLegacySuperGuildWarAttributeNames("WinnerScore", "RoundOwnerScore"))
        {
            attributes[name] = score;
        }

        foreach (var name in GetLegacySuperGuildWarAttributeNames("Process", "Proces"))
        {
            attributes[name] = "Idle";
        }

        foreach (var name in GetLegacySuperGuildWarAttributeNames("PoleReset"))
        {
            attributes[name] = "true";
        }

        foreach (var name in GetLegacySuperGuildWarAttributeNames("StampRoundSeconds"))
        {
            attributes[name] = "3";
        }

        return character with { Attributes = attributes };
    }

    private readonly record struct LegacySuperGuildWarPoleFinishRoundResult(
        LegacySuperGuildWarScoreEntry Winner,
        string ResetPoleName,
        IReadOnlyList<byte[]> ScorePanelFrames);
}
