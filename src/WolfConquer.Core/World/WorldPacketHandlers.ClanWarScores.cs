using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyClanWarCurrentFirstRightCornerChatType = 2_108;
    private const uint LegacyClanWarCurrentContinueRightCornerChatType = 2_109;
    private const uint LegacyClanWarCurrentPoleResetHitpoints = 30_000_000;
    private const string LegacyClanWarScoresAttribute = "ClanWarScores";
    private const string LegacyClanWarCurrentInvitationPendingPrefix = "ClanWarInvitation:";
    private const string LegacyClanWarCurrentInvitationRewardText = "Conquer Points";
    private static readonly TimeSpan LegacyClanWarCurrentScorePanelInterval = TimeSpan.FromSeconds(8);

    private static readonly LegacyClanWarCurrentScoreStateStore LegacyClanWarStates = new();

    private static bool TryApplyLegacyClanWarCurrentPoleScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out LegacyClanWarCurrentScoreEntry updatedScore)
    {
        updatedSession = session;
        updatedScore = default;

        if (damageApplied == 0 ||
            !TryResolveLegacyClanWarCity(session.Character, sobNpc, out var city) ||
            !IsLegacyClanWarGlobalProcessAlive(session.Character) ||
            !IsLegacyClanWarCurrentProcessAlive(session.Character) ||
            !IsLegacyClanWarInWar(session.Character))
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

        updatedScore = LegacyClanWarStates.UpdateScore(city, clanId, clanName, damageApplied);
        updatedSession = session with
        {
            Character = WithLegacyClanWarScoresAttribute(
                session.Character,
                LegacyClanWarStates.SnapshotScores()),
        };
        return true;
    }

    private static bool TryApplyLegacyClanWarCurrentPoleFinishRound(
        WorldSession session,
        SobNpc sobNpc,
        SobNpcDamageResult damageResult,
        out WorldSession updatedSession,
        out LegacyClanWarCurrentPoleFinishRoundResult finishRound)
    {
        updatedSession = session;
        finishRound = default;

        if (!damageResult.Killed ||
            !TryApplyLegacyClanWarCurrentPoleScore(
                session,
                sobNpc,
                damageResult.DamageApplied,
                out var scoredSession,
                out var updatedScore))
        {
            return false;
        }

        var city = updatedScore.City;
        TryNormalizeLegacyClanWarCityName(city, out city);

        var scores = SnapshotLegacyClanWarCurrentScores(city);
        if (scores.Count == 0)
        {
            return false;
        }

        var scorePanelFrames = CreateLegacyClanWarCurrentScorePanelFrames(scores);
        if (scorePanelFrames.Count == 0)
        {
            return false;
        }

        var winner = scores[0];
        var mapId = TryGetLegacyClanWarCityMapId(city, out var resolvedMapId)
            ? resolvedMapId
            : sobNpc.MapId;
        ulong? dynamicId = TryReadLegacyClanWarCurrentScopeDynamicId(scoredSession.Character, out var resolvedDynamicId)
            ? resolvedDynamicId
            : null;

        LegacyClanWarStates.ClearScores(city);
        updatedSession = scoredSession with
        {
            Character = WithLegacyClanWarCurrentWinnerAttributes(
                WithLegacyClanWarScoresAttribute(
                    scoredSession.Character,
                    LegacyClanWarStates.SnapshotScores()),
                city,
                winner),
        };
        finishRound = new LegacyClanWarCurrentPoleFinishRoundResult(
            city,
            mapId,
            dynamicId,
            winner,
            ResetPoleName: winner.ClanName,
            ResetPoleHitpoints: LegacyClanWarCurrentPoleResetHitpoints,
            ResetPoleMaxHitpoints: LegacyClanWarCurrentPoleResetHitpoints,
            scorePanelFrames);
        return true;
    }

    internal static void ResetLegacyClanWarStateForTests()
    {
        LegacyClanWarStates.Clear();
    }

    internal static async ValueTask<int> SendDueLegacyClanWarCurrentScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var activeSessions = SnapshotLegacyClanWarActiveSessions(context);
        if (activeSessions.Count == 0)
        {
            return 0;
        }

        var sentFrameCount = 0;
        foreach (var scope in ResolveLegacyClanWarCurrentScorePanelScopes(activeSessions))
        {
            if (!LegacyClanWarStates.TryConsumeDueScorePanelBroadcast(
                    scope.City,
                    scope.DynamicId,
                    now,
                    LegacyClanWarCurrentScorePanelInterval))
            {
                continue;
            }

            var frames = CreateLegacyClanWarCurrentScorePanelFrames(scope.City);
            if (frames.Count == 0)
            {
                continue;
            }

            foreach (var active in activeSessions)
            {
                if (!IsLegacyClanWarCurrentScorePanelRecipient(active.Session.Character, scope))
                {
                    continue;
                }

                foreach (var frame in frames)
                {
                    await active.Connection.SendAsync(frame, cancellationToken)
                        .ConfigureAwait(false);
                    sentFrameCount++;
                }
            }
        }

        return sentFrameCount;
    }

    internal static async ValueTask<int> SendDueLegacyClanWarCurrentCompleteWarFramesAsync(
        WorldPacketHandlerContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!TryGetLegacyClanWarCompletionCity(now, out var city, out var completesGlobalWar))
        {
            return 0;
        }

        var activeSessions = SnapshotLegacyClanWarActiveSessions(context);
        if (activeSessions.Count == 0)
        {
            return 0;
        }

        var participatingSessions = activeSessions
            .Where(active => IsLegacyClanWarCompletionParticipant(active.Session.Character, city))
            .ToArray();
        if (participatingSessions.Length == 0)
        {
            return 0;
        }

        var winner = ResolveLegacyClanWarCurrentWinner(participatingSessions[0].Session.Character, city);
        var scorePanelFrames = CreateLegacyClanWarCurrentScorePanelFrames(city);
        LegacyClanWarStates.ClearScores(city);

        var completedSessions = new Dictionary<ConquerTcpConnection, WorldSession>();
        var sentFrameCount = 0;
        var rewardFrameCount = 0;
        var rewardGrantCount = 0;
        foreach (var active in activeSessions)
        {
            var isParticipant = IsLegacyClanWarCompletionParticipant(active.Session.Character, city);
            var character = isParticipant
                ? WithLegacyClanWarCurrentCompleteWarAttributes(
                    active.Session.Character,
                    city,
                    winner,
                    completesGlobalWar)
                : active.Session.Character;
            var updatedSession = active.Session with { Character = character };
            if (isParticipant)
            {
                var rewardResult = await TryGrantLegacyClanWarCurrentCompletionRewardAsync(
                        context with
                        {
                            Connection = active.Connection,
                            Session = updatedSession,
                        },
                        updatedSession,
                        city,
                        winner,
                        cancellationToken)
                    .ConfigureAwait(false);
                updatedSession = rewardResult.Session;
                rewardFrameCount += rewardResult.SentFrameCount;
                if (rewardResult.Granted)
                {
                    rewardGrantCount++;
                }
            }

            completedSessions[active.Connection] = updatedSession;
            context.Sessions?.Set(active.Connection, updatedSession);
        }

        sentFrameCount += rewardFrameCount;
        if (scorePanelFrames.Count > 0 &&
            TryGetLegacyClanWarCityMapId(city, out var mapId))
        {
            var dynamicId = TryReadLegacyClanWarCurrentScopeDynamicId(
                    participatingSessions[0].Session.Character,
                    out var resolvedDynamicId)
                ? resolvedDynamicId
                : (ulong?)null;
            var scope = new LegacyClanWarCurrentScorePanelScope(city, mapId, dynamicId);
            foreach (var active in activeSessions)
            {
                if (!completedSessions.TryGetValue(active.Connection, out var updatedSession) ||
                    !IsLegacyClanWarCurrentScorePanelRecipient(updatedSession.Character, scope))
                {
                    continue;
                }

                foreach (var frame in scorePanelFrames)
                {
                    await active.Connection.SendAsync(frame, cancellationToken)
                        .ConfigureAwait(false);
                    sentFrameCount++;
                }
            }
        }

        var claimMessage = $"{winner.ClanName}has claimed the prize for domination of {city}CityArena.";
        var claimFrame = LegacyMsgMessagePacket.SystemRedCenter(claimMessage).ToFrameBytes();
        foreach (var active in activeSessions)
        {
            await active.Connection.SendAsync(claimFrame, cancellationToken)
                .ConfigureAwait(false);
            sentFrameCount++;
        }

        var logSession = completedSessions.TryGetValue(context.Connection, out var completedSession)
            ? completedSession
            : context.Session;
        await context.Log.WriteLineAsync(
                "WorldServer completed legacy ClanWar current-war city " +
                $"{FormatSession(context with { Session = logSession })} " +
                $"city={city} winnerClanId={winner.ClanId} winnerClanName={winner.ClanName} " +
                $"rewardItemId={winner.RewardItemId} globalEnded={completesGlobalWar}; " +
                $"rewardGrants={rewardGrantCount.ToString(CultureInfo.InvariantCulture)} " +
                $"rewardFrames={rewardFrameCount.ToString(CultureInfo.InvariantCulture)} " +
                "source MsgClanWar.CityWar.CompleteWar modeled with economy-safe MsgClanWar.CityWar.GetRewards payout.")
            .ConfigureAwait(false);
        return sentFrameCount;
    }

    internal static async ValueTask<int> SendDueLegacyClanWarCurrentOpenWarFramesAsync(
        WorldPacketHandlerContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!TryGetLegacyClanWarOpenInvitation(now, out var invitation))
        {
            return 0;
        }

        var activeSessions = SnapshotLegacyClanWarActiveSessions(context);
        if (activeSessions.Count == 0)
        {
            return 0;
        }

        var legacyClanWarSessions = invitation.StartsGlobalWar
            ? activeSessions
            : activeSessions
                .Where(active => IsLegacyClanWarGlobalProcessAlive(active.Session.Character))
                .ToArray();
        if (legacyClanWarSessions.Count == 0 ||
            IsLegacyClanWarCurrentCityOpen(activeSessions, invitation.City) ||
            (invitation.StartsGlobalWar &&
                activeSessions.Any(active => IsLegacyClanWarGlobalProcessAlive(active.Session.Character))))
        {
            return 0;
        }

        var legacyClanWarConnections = legacyClanWarSessions
            .Select(active => active.Connection)
            .ToHashSet();
        var prompt = new LegacyRuntimeMessageBoxPrompt(
            LegacyClanWarCurrentInvitationPendingPrefix + invitation.City,
            invitation.MessageText,
            invitation.MapId,
            invitation.X,
            invitation.Y,
            invitation.DynamicId,
            invitation.Seconds);
        var openedSessions = new Dictionary<ConquerTcpConnection, WorldSession>();
        foreach (var active in activeSessions)
        {
            var character = WithRuntimeMessageBoxPrompt(active.Session.Character, prompt);
            if (legacyClanWarConnections.Contains(active.Connection))
            {
                character = WithLegacyClanWarCurrentOpenWarAttributes(character, invitation.City);
            }

            var updatedSession = active.Session with { Character = character };
            openedSessions[active.Connection] = updatedSession;
            context.Sessions?.Set(active.Connection, updatedSession);
        }

        var centerFrame = LegacyMsgMessagePacket.SystemCenter(prompt.Text).ToFrameBytes();
        var messageBoxFrame = LegacyNpcReplyPacket.ForMessageBox(prompt.Text).ToFrameBytes();
        var sentFrameCount = 0;
        foreach (var active in activeSessions)
        {
            await active.Connection.SendAsync(centerFrame, cancellationToken)
                .ConfigureAwait(false);
            sentFrameCount++;
            await active.Connection.SendAsync(messageBoxFrame, cancellationToken)
                .ConfigureAwait(false);
            sentFrameCount++;
        }

        var logSession = openedSessions.TryGetValue(context.Connection, out var openedSession)
            ? openedSession
            : context.Session;
        await context.Log.WriteLineAsync(
                "WorldServer opened legacy ClanWar current-war city " +
                $"{FormatSession(context with { Session = logSession })} " +
                $"city={invitation.City} title=\"{invitation.Title}\" " +
                $"reward=\"{LegacyClanWarCurrentInvitationRewardText}\" " +
                $"map={invitation.MapId.ToString(CultureInfo.InvariantCulture)} " +
                $"x={invitation.X.ToString(CultureInfo.InvariantCulture)} " +
                $"y={invitation.Y.ToString(CultureInfo.InvariantCulture)} " +
                $"dynamicId={invitation.DynamicId.ToString(CultureInfo.InvariantCulture)} " +
                $"seconds={invitation.Seconds.ToString(CultureInfo.InvariantCulture)}; " +
                $"startsGlobalWar={invitation.StartsGlobalWar.ToString(CultureInfo.InvariantCulture)}; " +
                "source MsgClanWar.Open/CityWar.Open modeled.")
            .ConfigureAwait(false);
        return sentFrameCount;
    }

    private static async ValueTask<int> SendLegacyClanWarCurrentFinishRoundScorePanelFramesAsync(
        WorldPacketHandlerContext context,
        LegacyClanWarCurrentPoleFinishRoundResult finishRound,
        CancellationToken cancellationToken)
    {
        var activeSessions = SnapshotLegacyClanWarActiveSessions(context);
        if (activeSessions.Count == 0 || finishRound.ScorePanelFrames.Count == 0)
        {
            return 0;
        }

        var scope = new LegacyClanWarCurrentScorePanelScope(
            finishRound.City,
            finishRound.MapId,
            finishRound.DynamicId);
        var sentFrameCount = 0;
        foreach (var active in activeSessions)
        {
            if (!IsLegacyClanWarCurrentScorePanelRecipient(active.Session.Character, scope))
            {
                continue;
            }

            foreach (var frame in finishRound.ScorePanelFrames)
            {
                await active.Connection.SendAsync(frame, cancellationToken)
                    .ConfigureAwait(false);
                sentFrameCount++;
            }
        }

        return sentFrameCount;
    }

    private static IReadOnlyList<WorldActiveSession> SnapshotLegacyClanWarActiveSessions(
        WorldPacketHandlerContext context)
    {
        var activeSessions = context.Sessions?.Snapshot();
        if (activeSessions is { Count: > 0 })
        {
            return activeSessions;
        }

        return context.Session is null
            ? []
            : [new WorldActiveSession(context.Connection, context.Session)];
    }

    private static IReadOnlyList<LegacyClanWarCurrentScorePanelScope> ResolveLegacyClanWarCurrentScorePanelScopes(
        IReadOnlyList<WorldActiveSession> activeSessions)
    {
        var scopes = new Dictionary<LegacyClanWarCurrentScorePanelScopeKey, LegacyClanWarCurrentScorePanelScope>();
        foreach (var active in activeSessions)
        {
            var character = active.Session.Character;
            if (!IsLegacyClanWarGlobalProcessAlive(character) ||
                !IsLegacyClanWarCurrentProcessAlive(character) ||
                !IsLegacyClanWarInWar(character) ||
                !TryResolveLegacyClanWarCurrentScorePanelScope(character, out var scope))
            {
                continue;
            }

            scopes.TryAdd(new LegacyClanWarCurrentScorePanelScopeKey(scope.City, scope.DynamicId), scope);
        }

        return scopes.Values
            .OrderBy(scope => scope.City, StringComparer.Ordinal)
            .ThenBy(scope => scope.DynamicId ?? ulong.MaxValue)
            .ToArray();
    }

    private static bool TryGetLegacyClanWarCompletionCity(
        DateTimeOffset now,
        out string city,
        out bool completesGlobalWar)
    {
        completesGlobalWar = false;
        switch (now.Minute)
        {
            case 10:
                city = "Twin";
                return true;
            case 21:
                city = "Phoenix";
                return true;
            case 33:
                city = "Ape";
                return true;
            case 45:
                city = "Desert";
                return true;
            case 57:
                city = "Bird";
                completesGlobalWar = true;
                return true;
            default:
                city = string.Empty;
                return false;
        }
    }

    private static bool TryGetLegacyClanWarOpenInvitation(
        DateTimeOffset now,
        out LegacyClanWarCurrentOpenInvitation invitation)
    {
        if (now.Hour == 23 && now.Minute == 1 && now.Second < 2)
        {
            invitation = new LegacyClanWarCurrentOpenInvitation("Twin", 327, 138, 1_002, 0, 60, true);
            return true;
        }

        switch (now.Minute)
        {
            case 11:
                invitation = new LegacyClanWarCurrentOpenInvitation("Phoenix", 210, 260, 1_011, 0, 60, false);
                return true;
            case 22:
                invitation = new LegacyClanWarCurrentOpenInvitation("Ape", 568, 583, 1_020, 0, 60, false);
                return true;
            case 34:
                invitation = new LegacyClanWarCurrentOpenInvitation("Desert", 496, 673, 1_000, 0, 60, false);
                return true;
            case 46:
                invitation = new LegacyClanWarCurrentOpenInvitation("Bird", 707, 571, 1_015, 0, 60, false);
                return true;
            default:
                invitation = default;
                return false;
        }
    }

    private static bool IsLegacyClanWarCurrentCityOpen(
        IReadOnlyList<WorldActiveSession> activeSessions,
        string city)
    {
        foreach (var active in activeSessions)
        {
            var character = active.Session.Character;
            if (IsLegacyClanWarGlobalProcessAlive(character) &&
                IsLegacyClanWarCurrentProcessAlive(character) &&
                TryResolveLegacyClanWarCurrentScorePanelScope(character, out var scope) &&
                IsLegacyClanWarCityMatch(scope.City, city))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLegacyClanWarCompletionParticipant(CharacterSnapshot character, string city)
    {
        return IsLegacyClanWarGlobalProcessAlive(character) &&
            IsLegacyClanWarCurrentProcessAlive(character) &&
            TryResolveLegacyClanWarCurrentScorePanelScope(character, out var scope) &&
            IsLegacyClanWarCityMatch(scope.City, city);
    }

    private static bool TryResolveLegacyClanWarCurrentScorePanelScope(
        CharacterSnapshot character,
        out LegacyClanWarCurrentScorePanelScope scope)
    {
        scope = default;

        if (TryGetLegacyClanWarCityNameForMap(character.MapId, out var city) &&
            TryGetLegacyClanWarCityMapId(city, out var mapId))
        {
            scope = new LegacyClanWarCurrentScorePanelScope(
                city,
                mapId,
                TryReadLegacyClanWarCurrentScopeDynamicId(character, out var dynamicId) ? dynamicId : null);
            return true;
        }

        city = ReadAttributeString(
                character.Attributes,
                "LegacyClanWarCurrentCity",
                "ClanWarCurrentCity",
                "RuntimeClanWarCurrentCity",
                "LegacyClanWarCity",
                "ClanWarCity",
                "RuntimeClanWarCity")
            .Trim();
        if (!TryNormalizeLegacyClanWarCityName(city, out city) ||
            !TryGetLegacyClanWarCityMapId(city, out mapId))
        {
            return false;
        }

        scope = new LegacyClanWarCurrentScorePanelScope(
            city,
            mapId,
            TryReadLegacyClanWarCurrentScopeDynamicId(character, out var resolvedDynamicId) ? resolvedDynamicId : null);
        return true;
    }

    private static IReadOnlyList<byte[]> CreateLegacyClanWarCurrentScorePanelFrames(string city)
    {
        var scores = SnapshotLegacyClanWarCurrentScores(city);
        if (scores.Count == 0)
        {
            return [];
        }

        return CreateLegacyClanWarCurrentScorePanelFrames(scores);
    }

    private static IReadOnlyList<byte[]> CreateLegacyClanWarCurrentScorePanelFrames(
        IReadOnlyList<LegacyClanWarCurrentScoreEntry> scores)
    {
        return scores
            .Take(5)
            .Select((score, index) => CreateLegacyClanWarCurrentScorePanelFrame(index, score))
            .ToArray();
    }

    private static IReadOnlyList<LegacyClanWarCurrentScoreEntry> SnapshotLegacyClanWarCurrentScores(string city)
    {
        return LegacyClanWarStates.SnapshotScores(city)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.ClanId)
            .ToArray();
    }

    private static byte[] CreateLegacyClanWarCurrentScorePanelFrame(
        int index,
        LegacyClanWarCurrentScoreEntry score)
    {
        var rank = index + 1;
        var chatType = index == 0
            ? LegacyClanWarCurrentFirstRightCornerChatType
            : LegacyClanWarCurrentContinueRightCornerChatType;
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

    private static bool IsLegacyClanWarCurrentScorePanelRecipient(
        CharacterSnapshot character,
        LegacyClanWarCurrentScorePanelScope scope)
    {
        if (character.MapId != scope.MapId)
        {
            return false;
        }

        return !scope.DynamicId.HasValue ||
            (TryReadLegacyClanWarCharacterDynamicId(character, out var dynamicId) &&
                dynamicId == scope.DynamicId.Value);
    }

    private static bool TryResolveLegacyClanWarCity(
        CharacterSnapshot character,
        SobNpc sobNpc,
        out string city)
    {
        city = string.Empty;
        if (sobNpc.Id != LegacySobNpcEventRules.ClanWarPoleUid)
        {
            return false;
        }

        if (LegacySobNpcEventRules.TryGetClanWarCityName(sobNpc.MapId, out city))
        {
            return true;
        }

        city = ReadAttributeString(
                character.Attributes,
                "LegacyClanWarCity",
                "ClanWarCity",
                "RuntimeClanWarCity")
            .Trim();
        return city.Length != 0;
    }

    private static bool IsLegacyClanWarGlobalProcessAlive(CharacterSnapshot character)
    {
        var process = ReadAttributeString(
                character.Attributes,
                "LegacyClanWarProcess",
                "ClanWarProcess",
                "RuntimeClanWarProcess",
                "LegacyClanWarProces",
                "ClanWarProces",
                "RuntimeClanWarProces")
            .Trim();
        return IsLegacyClanWarAliveProcess(process);
    }

    private static bool IsLegacyClanWarCurrentProcessAlive(CharacterSnapshot character)
    {
        var process = ReadAttributeString(
                character.Attributes,
                "LegacyClanWarCurrentProcess",
                "ClanWarCurrentProcess",
                "RuntimeClanWarCurrentProcess",
                "LegacyClanWarCurrentProces",
                "ClanWarCurrentProces",
                "RuntimeClanWarCurrentProces",
                "LegacyClanWarCityProcess",
                "ClanWarCityProcess",
                "RuntimeClanWarCityProcess",
                "LegacyClanWarCityProces",
                "ClanWarCityProces",
                "RuntimeClanWarCityProces")
            .Trim();
        return IsLegacyClanWarAliveProcess(process);
    }

    private static bool IsLegacyClanWarInWar(CharacterSnapshot character)
    {
        var inWar = ReadAttributeString(
                character.Attributes,
                "LegacyClanWarInWar",
                "ClanWarInWar",
                "RuntimeClanWarInWar")
            .Trim();
        return inWar.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            inWar.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
            inWar.Equals("Alive", StringComparison.OrdinalIgnoreCase) ||
            inWar == "1";
    }

    private static bool IsLegacyClanWarAliveProcess(string process)
    {
        return process.Equals("Alive", StringComparison.OrdinalIgnoreCase) ||
            process.Equals("ProcesType.Alive", StringComparison.OrdinalIgnoreCase) ||
            process == "1";
    }

    private static bool TryReadLegacyClanWarCurrentScopeDynamicId(
        CharacterSnapshot character,
        out ulong dynamicId)
    {
        return TryReadUInt64(
                character.Attributes,
                out dynamicId,
                "LegacyClanWarCurrentDynamicID",
                "ClanWarCurrentDynamicID",
                "RuntimeClanWarCurrentDynamicID",
                "LegacyClanWarCurrentDynamicId",
                "ClanWarCurrentDynamicId",
                "RuntimeClanWarCurrentDynamicId",
                "LegacyClanWarCurrentDinamycID",
                "ClanWarCurrentDinamycID",
                "RuntimeClanWarCurrentDinamycID",
                "LegacyClanWarDynamicID",
                "ClanWarDynamicID",
                "RuntimeClanWarDynamicID",
                "LegacyClanWarDynamicId",
                "ClanWarDynamicId",
                "RuntimeClanWarDynamicId",
                "LegacyClanWarDinamycID",
                "ClanWarDinamycID",
                "RuntimeClanWarDinamycID") ||
            TryReadLegacyClanWarCharacterDynamicId(character, out dynamicId);
    }

    private static bool TryReadLegacyClanWarCharacterDynamicId(
        CharacterSnapshot character,
        out ulong dynamicId)
    {
        return TryReadUInt64(character.Attributes, out dynamicId, "DynamicID", "DynamicId");
    }

    private static bool TryGetLegacyClanWarCityNameForMap(long mapId, out string city)
    {
        if (mapId is < int.MinValue or > int.MaxValue)
        {
            city = string.Empty;
            return false;
        }

        return LegacySobNpcEventRules.TryGetClanWarCityName((int)mapId, out city);
    }

    private static bool TryNormalizeLegacyClanWarCityName(string city, out string normalized)
    {
        normalized = city.Trim();
        var compact = normalized
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        switch (compact.ToLowerInvariant())
        {
            case "twin":
            case "twincity":
            case "tc":
                normalized = "Twin";
                return true;
            case "phoenix":
            case "phoenixcastle":
            case "pc":
                normalized = "Phoenix";
                return true;
            case "ape":
            case "apecity":
            case "ac":
                normalized = "Ape";
                return true;
            case "desert":
            case "desertcity":
            case "dc":
                normalized = "Desert";
                return true;
            case "bird":
            case "birdisland":
            case "bi":
                normalized = "Bird";
                return true;
            default:
                return normalized.Length != 0;
        }
    }

    private static bool TryGetLegacyClanWarCityMapId(string city, out int mapId)
    {
        if (!TryNormalizeLegacyClanWarCityName(city, out var normalized))
        {
            mapId = 0;
            return false;
        }

        mapId = normalized switch
        {
            "Twin" => LegacySobNpcEventRules.TwinCityClanWarMapId,
            "Phoenix" => LegacySobNpcEventRules.PhoenixCastleClanWarMapId,
            "Ape" => LegacySobNpcEventRules.ApeCityClanWarMapId,
            "Desert" => LegacySobNpcEventRules.DesertCityClanWarMapId,
            "Bird" => LegacySobNpcEventRules.BirdIslandClanWarMapId,
            _ => 0,
        };
        return mapId != 0;
    }

    internal static bool IsLegacyClanWarCityMatch(string left, string right)
    {
        return TryNormalizeLegacyClanWarCityName(left, out var normalizedLeft) &&
            TryNormalizeLegacyClanWarCityName(right, out var normalizedRight) &&
            normalizedLeft.Equals(normalizedRight, StringComparison.Ordinal);
    }

    private static CharacterSnapshot WithLegacyClanWarScoresAttribute(
        CharacterSnapshot character,
        IReadOnlyList<LegacyClanWarCurrentScoreEntry> scores)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove(LegacyClanWarScoresAttribute);
        attributes.Remove("LegacyClanWarScores");
        if (scores.Count > 0)
        {
            attributes[LegacyClanWarScoresAttribute] = string.Join(
                ';',
                scores.Select(score => string.Join(
                    ',',
                    score.City,
                    score.ClanId.ToString(CultureInfo.InvariantCulture),
                    score.Score.ToString(CultureInfo.InvariantCulture),
                    Uri.EscapeDataString(score.ClanName))));
        }

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithLegacyClanWarCurrentWinnerAttributes(
        CharacterSnapshot character,
        string city,
        LegacyClanWarCurrentScoreEntry winner)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyClanWarCurrentWinnerCity"] = city,
            ["ClanWarCurrentWinnerCity"] = city,
            ["LegacyClanWarCurrentWinnerClanID"] = winner.ClanId.ToString(CultureInfo.InvariantCulture),
            ["ClanWarCurrentWinnerClanID"] = winner.ClanId.ToString(CultureInfo.InvariantCulture),
            ["LegacyClanWarCurrentWinnerClanName"] = winner.ClanName,
            ["ClanWarCurrentWinnerClanName"] = winner.ClanName,
            ["LegacyClanWarCurrentWinnerScore"] = winner.Score.ToString(CultureInfo.InvariantCulture),
            ["ClanWarCurrentWinnerScore"] = winner.Score.ToString(CultureInfo.InvariantCulture),
            ["LegacyClanWarWinnerClanID"] = winner.ClanId.ToString(CultureInfo.InvariantCulture),
            ["ClanWarWinnerClanID"] = winner.ClanId.ToString(CultureInfo.InvariantCulture),
            ["LegacyClanWarWinnerClanName"] = winner.ClanName,
            ["ClanWarWinnerClanName"] = winner.ClanName,
        };
        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithLegacyClanWarCurrentCompleteWarAttributes(
        CharacterSnapshot character,
        string city,
        LegacyClanWarCurrentWinner winner,
        bool completesGlobalWar)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove(LegacyClanWarScoresAttribute);
        attributes.Remove("LegacyClanWarScores");
        attributes["LegacyClanWarLastCompletedCity"] = city;
        attributes["ClanWarLastCompletedCity"] = city;
        attributes["LegacyClanWarCurrentProcess"] = "Dead";
        attributes["ClanWarCurrentProcess"] = "Dead";
        attributes["LegacyClanWarCurrentProces"] = "Dead";
        attributes["ClanWarCurrentProces"] = "Dead";
        attributes["LegacyClanWarCityProcess"] = "Dead";
        attributes["ClanWarCityProcess"] = "Dead";
        attributes["LegacyClanWarCityProces"] = "Dead";
        attributes["ClanWarCityProces"] = "Dead";
        attributes["LegacyClanWarCurrentWinnerCity"] = city;
        attributes["ClanWarCurrentWinnerCity"] = city;
        attributes["LegacyClanWarCurrentWinnerClanID"] = winner.ClanId.ToString(CultureInfo.InvariantCulture);
        attributes["ClanWarCurrentWinnerClanID"] = winner.ClanId.ToString(CultureInfo.InvariantCulture);
        attributes["LegacyClanWarCurrentWinnerClanName"] = winner.ClanName;
        attributes["ClanWarCurrentWinnerClanName"] = winner.ClanName;
        attributes["LegacyClanWarWinnerClanID"] = winner.ClanId.ToString(CultureInfo.InvariantCulture);
        attributes["ClanWarWinnerClanID"] = winner.ClanId.ToString(CultureInfo.InvariantCulture);
        attributes["LegacyClanWarWinnerClanName"] = winner.ClanName;
        attributes["ClanWarWinnerClanName"] = winner.ClanName;
        attributes["LegacyClanWarWinnerOccupationDays"] = "1";
        attributes["ClanWarWinnerOccupationDays"] = "1";
        attributes["LegacyClanWarWinnerRewardItemID"] = winner.RewardItemId.ToString(CultureInfo.InvariantCulture);
        attributes["ClanWarWinnerRewardItemID"] = winner.RewardItemId.ToString(CultureInfo.InvariantCulture);
        if (completesGlobalWar)
        {
            attributes["LegacyClanWarProcess"] = "Dead";
            attributes["ClanWarProcess"] = "Dead";
            attributes["LegacyClanWarProces"] = "Dead";
            attributes["ClanWarProces"] = "Dead";
        }

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithLegacyClanWarCurrentOpenWarAttributes(
        CharacterSnapshot character,
        string city)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyClanWarProcess"] = "Alive",
            ["ClanWarProcess"] = "Alive",
            ["RuntimeClanWarProcess"] = "Alive",
            ["LegacyClanWarProces"] = "Alive",
            ["ClanWarProces"] = "Alive",
            ["RuntimeClanWarProces"] = "Alive",
            ["LegacyClanWarCurrentCity"] = city,
            ["ClanWarCurrentCity"] = city,
            ["RuntimeClanWarCurrentCity"] = city,
            ["LegacyClanWarCity"] = city,
            ["ClanWarCity"] = city,
            ["RuntimeClanWarCity"] = city,
            ["LegacyClanWarCurrentProcess"] = "Alive",
            ["ClanWarCurrentProcess"] = "Alive",
            ["RuntimeClanWarCurrentProcess"] = "Alive",
            ["LegacyClanWarCurrentProces"] = "Alive",
            ["ClanWarCurrentProces"] = "Alive",
            ["RuntimeClanWarCurrentProces"] = "Alive",
            ["LegacyClanWarCityProcess"] = "Alive",
            ["ClanWarCityProcess"] = "Alive",
            ["RuntimeClanWarCityProcess"] = "Alive",
            ["LegacyClanWarCityProces"] = "Alive",
            ["ClanWarCityProces"] = "Alive",
            ["RuntimeClanWarCityProces"] = "Alive",
        };
        return character with { Attributes = attributes };
    }

    private static LegacyClanWarCurrentWinner ResolveLegacyClanWarCurrentWinner(
        CharacterSnapshot character,
        string city)
    {
        var clanName = ReadAttributeString(
                character.Attributes,
                "LegacyClanWarCurrentWinnerClanName",
                "ClanWarCurrentWinnerClanName",
                "LegacyClanWarWinnerClanName",
                "ClanWarWinnerClanName",
                "LegacyClanWarWinnerName",
                "ClanWarWinnerName")
            .Trim();
        if (clanName.Length == 0)
        {
            clanName = "None";
        }

        var clanId = ReadAttributeUInt64(
            character.Attributes,
            "LegacyClanWarCurrentWinnerClanID",
            "LegacyClanWarCurrentWinnerClanId",
            "ClanWarCurrentWinnerClanID",
            "ClanWarCurrentWinnerClanId",
            "LegacyClanWarWinnerClanID",
            "LegacyClanWarWinnerClanId",
            "LegacyClanWarWinnerClainID",
            "ClanWarWinnerClanID",
            "ClanWarWinnerClanId",
            "ClanWarWinnerClainID");
        return new LegacyClanWarCurrentWinner(
            clanId,
            clanName,
            GetLegacyClanWarRewardItemId(city));
    }

    private static uint GetLegacyClanWarRewardItemId(string city)
    {
        return TryNormalizeLegacyClanWarCityName(city, out var normalized)
            ? normalized switch
            {
                "Twin" => 722_454U,
                "Desert" => 722_459U,
                "Bird" => 722_464U,
                "Ape" => 722_469U,
                "Phoenix" => 722_474U,
                _ => 0U,
            }
            : 0U;
    }

    private readonly record struct LegacyClanWarCurrentWinner(
        ulong ClanId,
        string ClanName,
        uint RewardItemId);

    private readonly record struct LegacyClanWarCurrentScorePanelScope(
        string City,
        int MapId,
        ulong? DynamicId);

    private readonly record struct LegacyClanWarCurrentScorePanelScopeKey(
        string City,
        ulong? DynamicId);

    private readonly record struct LegacyClanWarCurrentOpenInvitation(
        string City,
        ushort X,
        ushort Y,
        ushort MapId,
        ushort DynamicId,
        int Seconds,
        bool StartsGlobalWar)
    {
        public string Title => City + "City Clan War";

        public string MessageText =>
            " " + Title + " is about to begin! Will you join it? Prize[" +
            LegacyClanWarCurrentInvitationRewardText +
            "]";
    }

    private readonly record struct LegacyClanWarCurrentPoleFinishRoundResult(
        string City,
        int MapId,
        ulong? DynamicId,
        LegacyClanWarCurrentScoreEntry Winner,
        string ResetPoleName,
        uint ResetPoleHitpoints,
        uint ResetPoleMaxHitpoints,
        IReadOnlyList<byte[]> ScorePanelFrames);
}

internal sealed class LegacyClanWarCurrentScoreStateStore
{
    private readonly object _sync = new();
    private readonly Dictionary<LegacyClanWarScoreKey, LegacyClanWarCurrentScoreEntry> _scores = [];
    private readonly Dictionary<LegacyClanWarScorePanelKey, DateTimeOffset> _nextScorePanelBroadcasts = [];

    public LegacyClanWarCurrentScoreEntry UpdateScore(
        string city,
        ulong clanId,
        string clanName,
        uint damage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        if (clanId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(clanId), clanId, "Clan id must be non-zero.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(clanName);

        lock (_sync)
        {
            var key = new LegacyClanWarScoreKey(city, clanId);
            if (_scores.TryGetValue(key, out var existing))
            {
                var updated = existing with
                {
                    ClanName = clanName,
                    Score = LegacyCombatDamageCalculator.AddLegacySaturating(existing.Score, damage),
                };
                _scores[key] = updated;
                return updated;
            }

            var created = new LegacyClanWarCurrentScoreEntry(city, clanId, clanName, damage);
            _scores[key] = created;
            return created;
        }
    }

    public IReadOnlyList<LegacyClanWarCurrentScoreEntry> SnapshotScores()
    {
        lock (_sync)
        {
            return _scores.Values
                .OrderBy(entry => entry.City, StringComparer.Ordinal)
                .ThenBy(entry => entry.ClanId)
                .ToArray();
        }
    }

    public IReadOnlyList<LegacyClanWarCurrentScoreEntry> SnapshotScores(string city)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(city);

        lock (_sync)
        {
            return _scores.Values
                .Where(entry => WorldPacketHandlers.IsLegacyClanWarCityMatch(entry.City, city))
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => entry.ClanId)
                .ToArray();
        }
    }

    public bool TryConsumeDueScorePanelBroadcast(
        string city,
        ulong? dynamicId,
        DateTimeOffset now,
        TimeSpan interval)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "Interval must be positive.");
        }

        lock (_sync)
        {
            var key = new LegacyClanWarScorePanelKey(city, dynamicId);
            if (_nextScorePanelBroadcasts.TryGetValue(key, out var nextBroadcast) &&
                nextBroadcast > now)
            {
                return false;
            }

            _nextScorePanelBroadcasts[key] = now.Add(interval);
            return true;
        }
    }

    public void ClearScores(string city)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(city);

        lock (_sync)
        {
            foreach (var key in _scores.Keys
                .Where(key => WorldPacketHandlers.IsLegacyClanWarCityMatch(key.City, city))
                .ToArray())
            {
                _scores.Remove(key);
            }

            foreach (var key in _nextScorePanelBroadcasts.Keys
                .Where(key => WorldPacketHandlers.IsLegacyClanWarCityMatch(key.City, city))
                .ToArray())
            {
                _nextScorePanelBroadcasts.Remove(key);
            }
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _scores.Clear();
            _nextScorePanelBroadcasts.Clear();
        }
    }

    private readonly record struct LegacyClanWarScoreKey(
        string City,
        ulong ClanId);

    private readonly record struct LegacyClanWarScorePanelKey(
        string City,
        ulong? DynamicId);
}

internal readonly record struct LegacyClanWarCurrentScoreEntry(
    string City,
    ulong ClanId,
    string ClanName,
    uint Score);
