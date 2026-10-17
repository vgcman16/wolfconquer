using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyChampionsOfWarrFirstRightCornerChatType = 2_108;
    private const uint LegacyChampionsOfWarrContinueRightCornerChatType = 2_109;
    private const string LegacyChampionsOfWarrScoresAttribute = "ChampionsOfWarrScores";

    private static readonly LegacyChampionsOfWarrScoreStateStore LegacyChampionsOfWarrStates = new();

    private static bool TryApplyLegacyChampionsOfWarrPoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyChampionsOfWarrScoreEntry updatedScore)
    {
        updatedSession = session;
        updatedScore = default;

        if (damageApplied == 0 ||
            sobNpc.MapId != LegacySobNpcEventRules.ChampionsOfWarrMapId ||
            sobNpc.Id != LegacySobNpcEventRules.ChampionsOfWarrPoleUid ||
            !IsLegacyChampionsOfWarrProcessAlive(session.Character))
        {
            return false;
        }

        var guildId = ReadAttributeUInt64(session.Character.Attributes, "GuildID", "GuildId");
        if (guildId == 0)
        {
            return false;
        }

        var guildName = ResolveLegacyChampionsOfWarrGuildName(session, guildId);
        if (guildName.Length == 0)
        {
            return false;
        }

        updatedScore = LegacyChampionsOfWarrStates.UpdateScore(guildId, guildName, damageApplied);
        updatedSession = session with
        {
            Character = WithLegacyChampionsOfWarrScoresAttribute(
                session.Character,
                SnapshotLegacyChampionsOfWarrScores()),
        };
        return true;
    }

    private static bool TryApplyLegacyChampionsOfWarrPoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacyChampionsOfWarrPoleFinishRoundResult finishRound)
    {
        updatedSession = session;
        finishRound = default;

        if (!damageResult.Killed ||
            !TryApplyLegacyChampionsOfWarrPoleScore(
                session,
                sobNpc,
                damageResult.DamageApplied,
                out var scoredSession,
                out _))
        {
            return false;
        }

        var scores = SnapshotLegacyChampionsOfWarrScores();
        if (scores.Count == 0)
        {
            return false;
        }

        var roundOwner = scores[0];
        var scorePanelFrames = CreateLegacyChampionsOfWarrScorePanelFrames(scores);
        LegacyChampionsOfWarrStates.Clear();
        updatedSession = scoredSession with
        {
            Character = WithLegacyChampionsOfWarrFinishRoundAttributes(
                WithLegacyChampionsOfWarrScoresAttribute(
                    scoredSession.Character,
                    SnapshotLegacyChampionsOfWarrScores()),
                roundOwner,
                roundOwner.GuildName),
        };
        finishRound = new LegacyChampionsOfWarrPoleFinishRoundResult(
            roundOwner,
            ResetPoleName: roundOwner.GuildName,
            scorePanelFrames);
        return true;
    }

    internal static void ResetLegacyChampionsOfWarrScoreStateForTests()
    {
        LegacyChampionsOfWarrStates.Clear();
    }

    private static IReadOnlyList<LegacyChampionsOfWarrScoreEntry> SnapshotLegacyChampionsOfWarrScores()
    {
        return LegacyChampionsOfWarrStates.SnapshotScores();
    }

    private static IReadOnlyList<byte[]> CreateLegacyChampionsOfWarrScorePanelFrames()
    {
        var scores = SnapshotLegacyChampionsOfWarrScores();
        return CreateLegacyChampionsOfWarrScorePanelFrames(scores);
    }

    private static IReadOnlyList<byte[]> CreateLegacyChampionsOfWarrScorePanelFrames(
        IReadOnlyList<LegacyChampionsOfWarrScoreEntry> scores)
    {
        if (scores.Count == 0)
        {
            return [];
        }

        return scores
            .Take(5)
            .Select((score, index) => CreateLegacyChampionsOfWarrScorePanelFrame(index, score))
            .ToArray();
    }

    private static byte[] CreateLegacyChampionsOfWarrScorePanelFrame(
        int index,
        LegacyChampionsOfWarrScoreEntry score)
    {
        var rank = index + 1;
        var chatType = index == 0
            ? LegacyChampionsOfWarrFirstRightCornerChatType
            : LegacyChampionsOfWarrContinueRightCornerChatType;
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

    private static async ValueTask SendLegacyChampionsOfWarrScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        var frames = CreateLegacyChampionsOfWarrScorePanelFrames();
        await SendLegacyChampionsOfWarrScorePanelFramesAsync(context, frames, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyChampionsOfWarrScorePanelFramesAsync(
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
            if (active.Session.Character.MapId != LegacySobNpcEventRules.ChampionsOfWarrMapId)
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

    private static async ValueTask SendLegacyChampionsOfWarrFinishRoundCenterMessageAsync(
        WorldPacketHandlerContext context,
        LegacyChampionsOfWarrPoleFinishRoundResult finishRound,
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

    private static bool IsLegacyChampionsOfWarrProcessAlive(CharacterSnapshot character)
    {
        var process = ReadAttributeString(
                character.Attributes,
                "LegacyChampionsOfWarrProcess",
                "ChampionsOfWarrProcess",
                "RuntimeChampionsOfWarrProcess",
                "LegacyChampionsOfWarrProces",
                "ChampionsOfWarrProces",
                "RuntimeChampionsOfWarrProces",
                "LegacyMsgChampionsOfWarrProcess",
                "MsgChampionsOfWarrProcess",
                "RuntimeMsgChampionsOfWarrProcess",
                "LegacyMsgChampionsOfWarrProces",
                "MsgChampionsOfWarrProces",
                "RuntimeMsgChampionsOfWarrProces")
            .Trim();
        return process.Equals("Alive", StringComparison.OrdinalIgnoreCase) ||
            process.Equals("ProcesType.Alive", StringComparison.OrdinalIgnoreCase) ||
            process == "1";
    }

    private static string ResolveLegacyChampionsOfWarrGuildName(WorldSession session, ulong guildId)
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

    private static CharacterSnapshot WithLegacyChampionsOfWarrScoresAttribute(
        CharacterSnapshot character,
        IReadOnlyList<LegacyChampionsOfWarrScoreEntry> scores)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove(LegacyChampionsOfWarrScoresAttribute);
        attributes.Remove("LegacyChampionsOfWarrScores");
        if (scores.Count > 0)
        {
            attributes[LegacyChampionsOfWarrScoresAttribute] = string.Join(
                ';',
                scores.Select(score => string.Join(
                    ',',
                    score.GuildId.ToString(CultureInfo.InvariantCulture),
                    score.Score.ToString(CultureInfo.InvariantCulture),
                    Uri.EscapeDataString(score.GuildName))));
        }

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithLegacyChampionsOfWarrFinishRoundAttributes(
        CharacterSnapshot character,
        LegacyChampionsOfWarrScoreEntry roundOwner,
        string resetPoleName)
    {
        var guildId = roundOwner.GuildId.ToString(CultureInfo.InvariantCulture);
        var score = roundOwner.Score.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyChampionsOfWarrRoundOwnerGuildID"] = guildId,
            ["LegacyChampionsOfWarrRoundOwnerGuildId"] = guildId,
            ["ChampionsOfWarrRoundOwnerGuildID"] = guildId,
            ["ChampionsOfWarrRoundOwnerGuildId"] = guildId,
            ["LegacyChampionsOfWarrRoundOwnerGuildName"] = roundOwner.GuildName,
            ["ChampionsOfWarrRoundOwnerGuildName"] = roundOwner.GuildName,
            ["LegacyChampionsOfWarrRoundOwnerScore"] = score,
            ["ChampionsOfWarrRoundOwnerScore"] = score,
            ["LegacyChampionsOfWarrPoleOwnerName"] = resetPoleName,
            ["ChampionsOfWarrPoleOwnerName"] = resetPoleName,
            ["LegacyChampionsOfWarrPoleReset"] = "true",
            ["ChampionsOfWarrPoleReset"] = "true",
        };
        return character with { Attributes = attributes };
    }

    private readonly record struct LegacyChampionsOfWarrPoleFinishRoundResult(
        LegacyChampionsOfWarrScoreEntry Winner,
        string ResetPoleName,
        IReadOnlyList<byte[]> ScorePanelFrames);
}

internal sealed class LegacyChampionsOfWarrScoreStateStore
{
    private readonly object _sync = new();
    private readonly Dictionary<ulong, LegacyChampionsOfWarrScoreEntry> _scores = [];

    public LegacyChampionsOfWarrScoreEntry UpdateScore(
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

            var created = new LegacyChampionsOfWarrScoreEntry(guildId, guildName, damage);
            _scores[guildId] = created;
            return created;
        }
    }

    public IReadOnlyList<LegacyChampionsOfWarrScoreEntry> SnapshotScores()
    {
        lock (_sync)
        {
            return _scores.Values
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => entry.GuildId)
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

internal readonly record struct LegacyChampionsOfWarrScoreEntry(
    ulong GuildId,
    string GuildName,
    uint Score);
