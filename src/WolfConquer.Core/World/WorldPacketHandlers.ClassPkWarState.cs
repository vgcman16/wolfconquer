using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static class LegacyClassPkWarState
{
    public const ushort MapId = 1_764;
    public const uint RewardConquerPoints = 500_000;
    public const ushort RewardPowerExpBallStack = 4;
    public const string FileName = "\\ClassPkWar.ini";

    public static readonly TimeSpan WarDuration = TimeSpan.FromMinutes(30);

    public static LegacyClassPkWarTournamentType GetTournamentType(CharacterSnapshot character)
    {
        ArgumentNullException.ThrowIfNull(character);

        return GetTournamentType(character.Class);
    }

    public static LegacyClassPkWarTournamentType GetTournamentType(long classCode)
    {
        return classCode switch
        {
            >= 1_000 and <= 1_057 => LegacyClassPkWarTournamentType.Trojan,
            >= 2_000 and <= 2_057 => LegacyClassPkWarTournamentType.Warrior,
            >= 4_000 and <= 4_057 => LegacyClassPkWarTournamentType.Archer,
            >= 5_000 and <= 5_057 => LegacyClassPkWarTournamentType.Ninja,
            >= 6_000 and <= 6_057 => LegacyClassPkWarTournamentType.Monk,
            >= 7_000 and <= 7_057 => LegacyClassPkWarTournamentType.Pirate,
            >= 13_002 and <= 13_057 => LegacyClassPkWarTournamentType.Water,
            >= 14_002 and <= 14_057 => LegacyClassPkWarTournamentType.Fire,
            >= 8_000 and <= 8_057 => LegacyClassPkWarTournamentType.LongLee,
            >= 16_000 and <= 16_057 => LegacyClassPkWarTournamentType.WindWalker,
            >= 9_000 and <= 9_057 => LegacyClassPkWarTournamentType.Thunderstriker,
            _ => LegacyClassPkWarTournamentType.Count,
        };
    }

    public static LegacyClassPkWarTournamentLevel GetTournamentLevel(CharacterSnapshot character)
    {
        ArgumentNullException.ThrowIfNull(character);

        return GetTournamentLevel(character.Level);
    }

    public static LegacyClassPkWarTournamentLevel GetTournamentLevel(long level)
    {
        return level >= 130
            ? LegacyClassPkWarTournamentLevel.Level_130_140
            : LegacyClassPkWarTournamentLevel.Count;
    }

    public static LegacyClassPkWarBracket GetBracket(CharacterSnapshot character)
    {
        ArgumentNullException.ThrowIfNull(character);

        return new LegacyClassPkWarBracket(
            GetTournamentType(character),
            GetTournamentLevel(character));
    }

    public static LegacyClassPkWarWarState Start(DateTimeOffset now, uint dynamicId)
    {
        return new LegacyClassPkWarWarState(
            LegacyClassPkWarProcess.Alive,
            now.Add(WarDuration),
            dynamicId);
    }

    public static LegacyClassPkWarWarState Start(DateTime now, uint dynamicId)
    {
        return Start(new DateTimeOffset(now), dynamicId);
    }

    public static LegacyClassPkWarCheckJoinResult CheckJoin(
        string? process,
        DateTimeOffset finishTimer,
        DateTimeOffset now,
        uint dynamicId = 0)
    {
        var current = NormalizeProcess(process);
        if (current == LegacyClassPkWarProcess.Dead)
        {
            return new LegacyClassPkWarCheckJoinResult(
                CanJoin: false,
                new LegacyClassPkWarWarState(LegacyClassPkWarProcess.Dead, finishTimer, dynamicId));
        }

        if (now < finishTimer)
        {
            return new LegacyClassPkWarCheckJoinResult(
                CanJoin: true,
                new LegacyClassPkWarWarState(LegacyClassPkWarProcess.Alive, finishTimer, dynamicId));
        }

        return new LegacyClassPkWarCheckJoinResult(
            CanJoin: false,
            new LegacyClassPkWarWarState(LegacyClassPkWarProcess.Alive, finishTimer, dynamicId));
    }

    public static LegacyClassPkWarCheckJoinResult CheckJoin(
        string? process,
        DateTime finishTimer,
        DateTime now,
        uint dynamicId = 0)
    {
        return CheckJoin(process, new DateTimeOffset(finishTimer), new DateTimeOffset(now), dynamicId);
    }

    public static bool IsAliveProcess(string? process)
    {
        return NormalizeProcess(process) == LegacyClassPkWarProcess.Alive;
    }

    public static bool IsDeadProcess(string? process)
    {
        return NormalizeProcess(process) == LegacyClassPkWarProcess.Dead;
    }

    public static LegacyClassPkWarProcess NormalizeProcess(string? process)
    {
        var trimmed = process?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return LegacyClassPkWarProcess.Dead;
        }

        if (trimmed.Equals("Alive", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("ProcesType.Alive", StringComparison.OrdinalIgnoreCase) ||
            trimmed == "1")
        {
            return LegacyClassPkWarProcess.Alive;
        }

        return LegacyClassPkWarProcess.Dead;
    }
}

internal enum LegacyClassPkWarTournamentType : byte
{
    Trojan = 0,
    Warrior = 1,
    Archer = 2,
    Ninja = 3,
    Monk = 4,
    Pirate = 5,
    Fire = 6,
    Water = 7,
    LongLee = 8,
    WindWalker = 9,
    Thunderstriker = 10,
    Count = 11,
}

internal enum LegacyClassPkWarTournamentLevel : byte
{
    Level_1_99 = 0,
    Level_100_119 = 1,
    Level_120_130 = 2,
    Level_130_140 = 3,
    Count = 4,
}

internal enum LegacyClassPkWarProcess : byte
{
    Dead = 0,
    Alive = 1,
}

internal readonly record struct LegacyClassPkWarBracket(
    LegacyClassPkWarTournamentType TournamentType,
    LegacyClassPkWarTournamentLevel TournamentLevel);

internal readonly record struct LegacyClassPkWarWarState(
    LegacyClassPkWarProcess Process,
    DateTimeOffset FinishTimer,
    uint DynamicId);

internal readonly record struct LegacyClassPkWarCheckJoinResult(
    bool CanJoin,
    LegacyClassPkWarWarState State);
