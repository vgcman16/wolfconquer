using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public enum LegacyFreezeWarProcess
{
    Dead,
    Idle,
    Alive,
}

public enum LegacyFreezeWarTeamType
{
    None = 0,
    Blue = 1,
    Red = 2,
}

public sealed class LegacyFreezeWarRuntimeStateStore
{
    public const long FreezeWarMapId = 1_505;
    public const int FreezeStatusFlag = 0x3B;
    public const long BlueGarmentId = 185_825;
    public const long RedGarmentId = 185_625;
    public const long FinishMapId = 1_002;
    public const long FinishX = 410;
    public const long FinishY = 354;
    public const long WinnerConquerPointsReward = 120_000;

    private const ushort InvitationMapId = 1_002;
    private const ushort InvitationX = 364;
    private const ushort InvitationY = 341;
    private const ushort InvitationDynamicId = 0;
    private const int InvitationSeconds = 60;
    private const string InvitationName = "FreezeWar";
    private const string InvitationPrize = "ConquerPoints";

    private readonly object _gate = new();
    private readonly Dictionary<long, LegacyFreezeWarTeamType> _teamsByUid = new();
    private LegacyFreezeWarProcess _process;
    private uint _dynamicId;
    private string? _lastOpenSlotKey;
    private DateTimeOffset _startTimerUtc;
    private DateTimeOffset _infoTimerUtc;
    private DateTimeOffset _scoreStampUtc;
    private DateTimeOffset _roundResetUtc;
    private uint _seconds;
    private int _blueScore;
    private int _redScore;
    private bool _roundResetPending;

    public LegacyFreezeWarProcess Process
    {
        get
        {
            lock (_gate)
            {
                return _process;
            }
        }
    }

    public uint DynamicId
    {
        get
        {
            lock (_gate)
            {
                return _dynamicId;
            }
        }
    }

    public LegacyRuntimeUpdate? Open(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_process != LegacyFreezeWarProcess.Dead)
            {
                return null;
            }

            _process = LegacyFreezeWarProcess.Idle;
            _dynamicId = CreateDynamicId(now);
            _startTimerUtc = now.ToUniversalTime();
            _infoTimerUtc = _startTimerUtc;
            _scoreStampUtc = _startTimerUtc;
            _roundResetUtc = default;
            _seconds = 60;
            _blueScore = 0;
            _redScore = 0;
            _roundResetPending = false;
            _teamsByUid.Clear();
        }

        return CreateOpenInvitationUpdate();
    }

    public LegacyRuntimeUpdate? CreateDueOpenUpdate(DateTimeOffset now, TimeZoneInfo localTimeZone)
    {
        ArgumentNullException.ThrowIfNull(localTimeZone);

        var localNow = TimeZoneInfo.ConvertTime(now, localTimeZone);
        if (localNow.Minute != 55 || localNow.Second >= 1)
        {
            return null;
        }

        var slotKey = localNow.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
        lock (_gate)
        {
            if (string.Equals(_lastOpenSlotKey, slotKey, StringComparison.Ordinal))
            {
                return null;
            }

            _lastOpenSlotKey = slotKey;
        }

        return Open(now);
    }

    public LegacyFreezeWarRuntimeUpdate? CreateDueLifecycleUpdate(
        DateTimeOffset now,
        IReadOnlyList<LegacyFreezeWarRuntimeParticipantSnapshot>? participantSnapshots = null)
    {
        var nowUtc = now.ToUniversalTime();
        lock (_gate)
        {
            if (_process == LegacyFreezeWarProcess.Alive)
            {
                var participants = CreateCurrentParticipants();
                var activeParticipants = CreateActiveParticipantSnapshots(participantSnapshots);

                if (_roundResetPending)
                {
                    if (nowUtc <= _roundResetUtc)
                    {
                        return null;
                    }

                    _roundResetPending = false;
                    _scoreStampUtc = nowUtc;
                    return LegacyFreezeWarRuntimeUpdate.RoundReset(participants);
                }

                var blueCount = CountActiveTeam(activeParticipants, LegacyFreezeWarTeamType.Blue);
                var redCount = CountActiveTeam(activeParticipants, LegacyFreezeWarTeamType.Red);
                if (blueCount > 0 &&
                    redCount > 0 &&
                    TryGetFullyFrozenLoser(activeParticipants, out var frozenTeam))
                {
                    var winnerTeam = frozenTeam == LegacyFreezeWarTeamType.Blue
                        ? LegacyFreezeWarTeamType.Red
                        : LegacyFreezeWarTeamType.Blue;
                    IncrementScore(winnerTeam);
                    if (GetScore(winnerTeam) >= 5)
                    {
                        return EndTournament(winnerTeam, participants);
                    }

                    _roundResetPending = true;
                    _roundResetUtc = nowUtc.AddSeconds(5);
                    return LegacyFreezeWarRuntimeUpdate.RoundWon(
                        winnerTeam,
                        participants,
                        _blueScore,
                        _redScore);
                }

                if (blueCount == 0 && redCount > 0)
                {
                    return EndTournament(LegacyFreezeWarTeamType.Red, participants);
                }

                if (redCount == 0 && blueCount > 0)
                {
                    return EndTournament(LegacyFreezeWarTeamType.Blue, participants);
                }

                if (nowUtc > _startTimerUtc.AddMinutes(10))
                {
                    var winnerTeam = _redScore >= _blueScore
                        ? LegacyFreezeWarTeamType.Red
                        : LegacyFreezeWarTeamType.Blue;
                    return EndTournament(winnerTeam, participants);
                }

                if (nowUtc > _scoreStampUtc)
                {
                    _scoreStampUtc = nowUtc.AddSeconds(4);
                    return LegacyFreezeWarRuntimeUpdate.ScoreHud(
                        participants,
                        CreateFinishTimer(nowUtc),
                        _blueScore,
                        _redScore);
                }

                return null;
            }

            if (_process != LegacyFreezeWarProcess.Idle)
            {
                return null;
            }

            if (nowUtc > _startTimerUtc.AddMinutes(1))
            {
                _process = LegacyFreezeWarProcess.Alive;
                _startTimerUtc = nowUtc;
                _infoTimerUtc = nowUtc;
                _scoreStampUtc = nowUtc;
                _roundResetUtc = default;
                _seconds = 0;
                _roundResetPending = false;
                return LegacyFreezeWarRuntimeUpdate.Started(_teamsByUid.Keys.ToArray());
            }

            if (nowUtc > _infoTimerUtc.AddSeconds(10))
            {
                _seconds = _seconds > 10 ? _seconds - 10 : 0;
                _infoTimerUtc = nowUtc;
                return LegacyFreezeWarRuntimeUpdate.Countdown(_seconds);
            }
        }

        return null;
    }

    private LegacyFreezeWarRuntimeUpdate EndTournament(
        LegacyFreezeWarTeamType winnerTeam,
        IReadOnlyList<LegacyFreezeWarRuntimeParticipant> participants)
    {
        _process = LegacyFreezeWarProcess.Dead;
        _dynamicId = 0;
        _teamsByUid.Clear();
        _roundResetPending = false;
        return LegacyFreezeWarRuntimeUpdate.Ended(winnerTeam, participants);
    }

    private LegacyFreezeWarRuntimeParticipant[] CreateCurrentParticipants()
    {
        return _teamsByUid
            .Select(pair => new LegacyFreezeWarRuntimeParticipant(pair.Key, pair.Value))
            .ToArray();
    }

    private LegacyFreezeWarRuntimeParticipantSnapshot[] CreateActiveParticipantSnapshots(
        IReadOnlyList<LegacyFreezeWarRuntimeParticipantSnapshot>? participantSnapshots)
    {
        if (participantSnapshots is null || participantSnapshots.Count == 0 || _dynamicId == 0)
        {
            return [];
        }

        return participantSnapshots
            .Where(snapshot =>
                snapshot.MapId == FreezeWarMapId &&
                snapshot.DynamicId == _dynamicId &&
                _teamsByUid.TryGetValue(snapshot.Uid, out var team) &&
                team == snapshot.Team)
            .ToArray();
    }

    private static int CountActiveTeam(
        IReadOnlyList<LegacyFreezeWarRuntimeParticipantSnapshot> snapshots,
        LegacyFreezeWarTeamType team)
    {
        return snapshots.Count(snapshot => snapshot.Team == team);
    }

    private static bool TryGetFullyFrozenLoser(
        IReadOnlyList<LegacyFreezeWarRuntimeParticipantSnapshot> snapshots,
        out LegacyFreezeWarTeamType frozenTeam)
    {
        var blue = snapshots.Where(snapshot => snapshot.Team == LegacyFreezeWarTeamType.Blue).ToArray();
        if (blue.Length > 0 && blue.All(snapshot => snapshot.IsFrozen))
        {
            frozenTeam = LegacyFreezeWarTeamType.Blue;
            return true;
        }

        var red = snapshots.Where(snapshot => snapshot.Team == LegacyFreezeWarTeamType.Red).ToArray();
        if (red.Length > 0 && red.All(snapshot => snapshot.IsFrozen))
        {
            frozenTeam = LegacyFreezeWarTeamType.Red;
            return true;
        }

        frozenTeam = LegacyFreezeWarTeamType.None;
        return false;
    }

    private void IncrementScore(LegacyFreezeWarTeamType team)
    {
        if (team == LegacyFreezeWarTeamType.Blue)
        {
            _blueScore++;
        }
        else if (team == LegacyFreezeWarTeamType.Red)
        {
            _redScore++;
        }
    }

    private int GetScore(LegacyFreezeWarTeamType team)
    {
        return team == LegacyFreezeWarTeamType.Blue ? _blueScore : _redScore;
    }

    private string CreateFinishTimer(DateTimeOffset nowUtc)
    {
        var remaining = _startTimerUtc.AddMinutes(10) - nowUtc;
        var minutes = Math.Max(0, remaining.Minutes);
        var seconds = Math.Max(0, remaining.Seconds);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{minutes.ToString(CultureInfo.InvariantCulture)} : {seconds.ToString(CultureInfo.InvariantCulture)}");
    }

    public bool TryJoin(
        WorldSession session,
        out WorldSession updatedSession,
        out LegacyFreezeWarTeamType team)
    {
        ArgumentNullException.ThrowIfNull(session);

        uint dynamicId;
        lock (_gate)
        {
            if (_process != LegacyFreezeWarProcess.Idle || _dynamicId == 0)
            {
                updatedSession = default!;
                team = LegacyFreezeWarTeamType.None;
                return false;
            }

            dynamicId = _dynamicId;
            if (!_teamsByUid.TryGetValue(session.Character.Uid, out team))
            {
                var blueCount = _teamsByUid.Values.Count(value => value == LegacyFreezeWarTeamType.Blue);
                var redCount = _teamsByUid.Values.Count(value => value == LegacyFreezeWarTeamType.Red);
                team = blueCount < redCount
                    ? LegacyFreezeWarTeamType.Blue
                    : LegacyFreezeWarTeamType.Red;
                _teamsByUid[session.Character.Uid] = team;
            }
        }

        updatedSession = session with
        {
            Character = CreateJoinedCharacter(session.Character, team, dynamicId),
        };
        return true;
    }

    private static CharacterSnapshot CreateJoinedCharacter(
        CharacterSnapshot character,
        LegacyFreezeWarTeamType team,
        uint dynamicId)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["DynamicID"] = dynamicId.ToString(CultureInfo.InvariantCulture),
            ["LegacyTournamentInTournament"] = "1",
            ["LegacyTournamentType"] = InvitationName,
            ["LegacyTournamentProcess"] = LegacyFreezeWarProcess.Idle.ToString(),
            ["FreezeTeamType"] = ((int)team).ToString(CultureInfo.InvariantCulture),
            ["LegacyFreezeWarTeam"] = team.ToString(),
        };
        var garmentId = team == LegacyFreezeWarTeamType.Blue ? BlueGarmentId : RedGarmentId;
        attributes["SpecialGarment"] = garmentId.ToString(CultureInfo.InvariantCulture);
        attributes["GarmentId"] = garmentId.ToString(CultureInfo.InvariantCulture);
        AddLegacyStatusFlag(attributes, FreezeStatusFlag);

        var (x, y) = CreateTeamCoordinates(team);
        return LegacyStatusFlagDeathRegistration.CaptureAddFlag(character, character with
        {
            MapId = FreezeWarMapId,
            X = x,
            Y = y,
            PreviousMapId = character.MapId,
            PreviousMapX = character.X,
            PreviousMapY = character.Y,
            Attributes = attributes,
        }, FreezeStatusFlag, removeOnDead: true);
    }

    private static (long X, long Y) CreateTeamCoordinates(LegacyFreezeWarTeamType team)
    {
        return team == LegacyFreezeWarTeamType.Blue
            ? (153, 105)
            : (165, 245);
    }

    private static LegacyRuntimeUpdate CreateOpenInvitationUpdate()
    {
        var message = $" {InvitationName} is about to begin! Will you join it? Prize[{InvitationPrize}]";
        var prompt = new LegacyRuntimeMessageBoxPrompt(
            LegacyRuntimeMessageBoxPrompt.FreezeWarInvitationKey,
            message,
            InvitationMapId,
            InvitationX,
            InvitationY,
            InvitationDynamicId,
            InvitationSeconds);
        return new LegacyRuntimeUpdate(LegacyMsgMessagePacket.SystemCenter(message).ToFrameBytes(), prompt);
    }

    private static uint CreateDynamicId(DateTimeOffset now)
    {
        var seed = unchecked((uint)now.UtcTicks);
        seed ^= unchecked((uint)(now.UtcTicks >> 32));
        seed ^= (uint)Environment.TickCount;
        return seed == 0 ? 1 : seed;
    }

    private static void AddLegacyStatusFlag(Dictionary<string, string> attributes, int flag)
    {
        LegacyStatusFlagDeathRegistration.PrepareAdd(attributes, flag);
        if (flag < 0)
        {
            return;
        }

        var key = attributes.ContainsKey("BitVector") && !attributes.ContainsKey("StatusFlags")
            ? "BitVector"
            : "StatusFlags";
        var words = ReadLegacyStatusWords32(attributes);
        var wordIndex = flag / 32;
        var wordCount = Math.Max(Math.Max(10, words.Count), wordIndex + 1);
        while (words.Count < wordCount)
        {
            words.Add(0);
        }

        words[wordIndex] |= 1UL << (flag % 32);
        attributes[key] = string.Join('/', words.Select(word => word.ToString(CultureInfo.InvariantCulture))) + "/";
    }

    public static CharacterSnapshot MarkAliveAndUnfreeze(CharacterSnapshot character)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyTournamentProcess"] = LegacyFreezeWarProcess.Alive.ToString(),
        };
        RemoveLegacyStatusFlag(attributes, FreezeStatusFlag);
        return character with { Attributes = attributes };
    }

    public static CharacterSnapshot MarkRoundFrozen(CharacterSnapshot character)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove("LegacyFreezeWarExpiresAtUtc");
        AddLegacyStatusFlag(attributes, FreezeStatusFlag);
        return LegacyStatusFlagDeathRegistration.CaptureAddFlag(character,
            character with { Attributes = attributes }, FreezeStatusFlag, removeOnDead: false);
    }

    public static CharacterSnapshot MarkRoundReset(CharacterSnapshot character, uint dynamicId)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["DynamicID"] = dynamicId.ToString(CultureInfo.InvariantCulture),
            ["LegacyTournamentProcess"] = LegacyFreezeWarProcess.Alive.ToString(),
        };
        attributes.Remove("LegacyFreezeWarExpiresAtUtc");
        RemoveLegacyStatusFlag(attributes, FreezeStatusFlag);

        var team = ReadFreezeWarTeam(character);
        var (x, y) = CreateTeamCoordinates(team);
        return character with
        {
            MapId = FreezeWarMapId,
            X = x,
            Y = y,
            PreviousMapId = character.MapId,
            PreviousMapX = character.X,
            PreviousMapY = character.Y,
            Attributes = attributes,
        };
    }

    public static CharacterSnapshot MarkFinishedAndReturnToTwinCity(
        CharacterSnapshot character,
        bool awardWinner)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["DynamicID"] = "0",
        };
        attributes.Remove("LegacyTournamentInTournament");
        attributes.Remove("LegacyTournamentType");
        attributes.Remove("LegacyTournamentProcess");
        attributes.Remove("FreezeTeamType");
        attributes.Remove("LegacyFreezeWarTeam");
        attributes.Remove("SpecialGarment");
        attributes.Remove("GarmentId");
        RemoveLegacyStatusFlag(attributes, FreezeStatusFlag);

        return character with
        {
            MapId = FinishMapId,
            X = FinishX,
            Y = FinishY,
            PreviousMapId = character.MapId,
            PreviousMapX = character.X,
            PreviousMapY = character.Y,
            ConquerPoints = awardWinner
                ? checked(character.ConquerPoints + WinnerConquerPointsReward)
                : character.ConquerPoints,
            Attributes = attributes,
        };
    }

    private static bool RemoveLegacyStatusFlag(Dictionary<string, string> attributes, int flag)
    {
        var key = attributes.ContainsKey("StatusFlags") ? "StatusFlags" : "BitVector";
        if (!attributes.TryGetValue(key, out var raw))
        {
            return false;
        }

        var wordIndex = flag / 32;
        var bitIndex = flag % 32;
        var words = ReadLegacyStatusWords32(attributes);
        if (wordIndex >= words.Count)
        {
            return false;
        }

        var mask = 1UL << bitIndex;
        if ((words[wordIndex] & mask) == 0)
        {
            return false;
        }

        words[wordIndex] &= ~mask;
        attributes[key] = string.Join('/', words.Select(word => word.ToString(CultureInfo.InvariantCulture))) + "/";
        return true;
    }

    public static bool HasFreezeStatusFlag(CharacterSnapshot character)
    {
        var wordIndex = FreezeStatusFlag / 32;
        var bitIndex = FreezeStatusFlag % 32;
        var words = ReadLegacyStatusWords32(character.Attributes);
        return wordIndex < words.Count && (words[wordIndex] & (1UL << bitIndex)) != 0;
    }

    public static LegacyFreezeWarTeamType ReadFreezeWarTeam(CharacterSnapshot character)
    {
        if (character.Attributes.TryGetValue("LegacyFreezeWarTeam", out var rawTeam) &&
            Enum.TryParse<LegacyFreezeWarTeamType>(rawTeam, ignoreCase: true, out var parsedTeam))
        {
            return parsedTeam;
        }

        if (character.Attributes.TryGetValue("FreezeTeamType", out var rawNumeric) &&
            int.TryParse(rawNumeric, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric) &&
            Enum.IsDefined(typeof(LegacyFreezeWarTeamType), numeric))
        {
            return (LegacyFreezeWarTeamType)numeric;
        }

        return LegacyFreezeWarTeamType.None;
    }

    public static uint ReadDynamicId(CharacterSnapshot character)
    {
        return character.Attributes.TryGetValue("DynamicID", out var raw) &&
            uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dynamicId)
            ? dynamicId
            : 0;
    }

    private static List<ulong> ReadLegacyStatusWords32(IReadOnlyDictionary<string, string> attributes)
    {
        if ((!attributes.TryGetValue("StatusFlags", out var raw) &&
            !attributes.TryGetValue("BitVector", out raw)) ||
            string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        if (raw.Contains('/', StringComparison.Ordinal))
        {
            return raw
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => ulong.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                    ? value
                    : 0)
                .ToList();
        }

        var words = new List<ulong>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!ulong.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                value = 0;
            }

            words.Add(value & uint.MaxValue);
            words.Add(value >> 32);
        }

        return words;
    }
}

public enum LegacyFreezeWarRuntimeUpdateKind
{
    Countdown,
    Started,
    ScoreHud,
    RoundWon,
    RoundReset,
    Ended,
}

public sealed record LegacyFreezeWarRuntimeParticipant(long Uid, LegacyFreezeWarTeamType Team);

public sealed record LegacyFreezeWarRuntimeParticipantSnapshot(
    long Uid,
    LegacyFreezeWarTeamType Team,
    long MapId,
    uint DynamicId,
    bool IsFrozen);

public sealed record LegacyFreezeWarRuntimeUpdate(
    LegacyFreezeWarRuntimeUpdateKind Kind,
    IReadOnlyList<byte[]> FrameBytes,
    IReadOnlyList<long> ParticipantUids,
    LegacyFreezeWarTeamType WinnerTeam,
    IReadOnlyList<LegacyFreezeWarRuntimeParticipant> Participants,
    int BlueScore = 0,
    int RedScore = 0)
{
    public static LegacyFreezeWarRuntimeUpdate Countdown(uint seconds)
    {
        return new LegacyFreezeWarRuntimeUpdate(
            LegacyFreezeWarRuntimeUpdateKind.Countdown,
            [LegacyMsgMessagePacket
                .SystemRedCenter($"[FreezeWar] Fight starts in {seconds.ToString(CultureInfo.InvariantCulture)} Secounds.")
                .ToFrameBytes()],
            [],
            LegacyFreezeWarTeamType.None,
            []);
    }

    public static LegacyFreezeWarRuntimeUpdate Started(IReadOnlyList<long> participantUids)
    {
        return new LegacyFreezeWarRuntimeUpdate(
            LegacyFreezeWarRuntimeUpdateKind.Started,
            [LegacyMsgMessagePacket
                .SystemRedCenter("FreezeWar has started! signup are now closed.")
                .ToFrameBytes()],
            participantUids,
            LegacyFreezeWarTeamType.None,
            []);
    }

    public static LegacyFreezeWarRuntimeUpdate ScoreHud(
        IReadOnlyList<LegacyFreezeWarRuntimeParticipant> participants,
        string timeLeft,
        int blueScore,
        int redScore)
    {
        var frames = new List<byte[]>
        {
            CreateRightCornerMessage(
                    "FreezeWar: Time Left: " + timeLeft,
                    firstLine: true)
                .ToFrameBytes(),
        };

        if (blueScore >= redScore)
        {
            frames.Add(CreateRightCornerMessage(
                    $"BlueTeam Score: {blueScore.ToString(CultureInfo.InvariantCulture)}",
                    firstLine: false)
                .ToFrameBytes());
            frames.Add(CreateRightCornerMessage(
                    $"RedTeam Score: {redScore.ToString(CultureInfo.InvariantCulture)}",
                    firstLine: false)
                .ToFrameBytes());
        }
        else
        {
            frames.Add(CreateRightCornerMessage(
                    $"RedTeam Score: {redScore.ToString(CultureInfo.InvariantCulture)}",
                    firstLine: false)
                .ToFrameBytes());
            frames.Add(CreateRightCornerMessage(
                    $"BlueTeam Score: {blueScore.ToString(CultureInfo.InvariantCulture)}",
                    firstLine: false)
                .ToFrameBytes());
        }

        return new LegacyFreezeWarRuntimeUpdate(
            LegacyFreezeWarRuntimeUpdateKind.ScoreHud,
            frames,
            participants.Select(participant => participant.Uid).ToArray(),
            LegacyFreezeWarTeamType.None,
            participants,
            blueScore,
            redScore);
    }

    public static LegacyFreezeWarRuntimeUpdate RoundWon(
        LegacyFreezeWarTeamType winnerTeam,
        IReadOnlyList<LegacyFreezeWarRuntimeParticipant> participants,
        int blueScore,
        int redScore)
    {
        return new LegacyFreezeWarRuntimeUpdate(
            LegacyFreezeWarRuntimeUpdateKind.RoundWon,
            [
                LegacyMsgMessagePacket
                    .SystemTalk($"{winnerTeam} has won this round. The next round has started.")
                    .ToFrameBytes(),
                LegacyMsgMessagePacket
                    .SystemTalk("The next round will start in 5 seconds.")
                    .ToFrameBytes(),
            ],
            participants.Select(participant => participant.Uid).ToArray(),
            winnerTeam,
            participants,
            blueScore,
            redScore);
    }

    public static LegacyFreezeWarRuntimeUpdate RoundReset(
        IReadOnlyList<LegacyFreezeWarRuntimeParticipant> participants)
    {
        return new LegacyFreezeWarRuntimeUpdate(
            LegacyFreezeWarRuntimeUpdateKind.RoundReset,
            [],
            participants.Select(participant => participant.Uid).ToArray(),
            LegacyFreezeWarTeamType.None,
            participants);
    }

    public static LegacyFreezeWarRuntimeUpdate Ended(
        LegacyFreezeWarTeamType winnerTeam,
        IReadOnlyList<LegacyFreezeWarRuntimeParticipant> participants)
    {
        return new LegacyFreezeWarRuntimeUpdate(
            LegacyFreezeWarRuntimeUpdateKind.Ended,
            [LegacyMsgMessagePacket
                .SystemRedCenter($"FreezeWar has ended.{winnerTeam} All Players of FreezeWar has teleported to TwinCity.")
                .ToFrameBytes()],
            participants.Select(participant => participant.Uid).ToArray(),
            winnerTeam,
            participants);
    }

    private static LegacyMsgMessagePacket CreateRightCornerMessage(string message, bool firstLine)
    {
        const uint firstRightCornerChatType = 2_108;
        const uint continueRightCornerChatType = 2_109;

        return new LegacyMsgMessagePacket(
            LegacyMsgMessagePacket.YellowColor,
            firstLine ? firstRightCornerChatType : continueRightCornerChatType,
            MessageUid1: 0,
            MessageUid2: 0,
            ServerId: 0,
            Mesh: 0,
            UnionType: 0,
            Count: 1,
            New1: 0,
            New2: 0,
            ["SYSTEM", "ALL", string.Empty, message, string.Empty, string.Empty, string.Empty]);
    }
}
