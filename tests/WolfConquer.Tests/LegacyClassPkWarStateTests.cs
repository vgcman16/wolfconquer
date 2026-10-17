using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyClassPkWarStateTests
{
    [Fact]
    public void SourceMetadataConstantsMatchMsgClassPkWar()
    {
        Assert.Equal((ushort)1_764, LegacyClassPkWarState.MapId);
        Assert.Equal(500_000U, LegacyClassPkWarState.RewardConquerPoints);
        Assert.Equal("\\ClassPkWar.ini", LegacyClassPkWarState.FileName);
        Assert.Equal(TimeSpan.FromMinutes(30), LegacyClassPkWarState.WarDuration);
    }

    [Theory]
    [InlineData(1_000, 0)]
    [InlineData(1_057, 0)]
    [InlineData(2_000, 1)]
    [InlineData(4_000, 2)]
    [InlineData(5_000, 3)]
    [InlineData(6_000, 4)]
    [InlineData(7_000, 5)]
    [InlineData(14_002, 6)]
    [InlineData(13_002, 7)]
    [InlineData(8_000, 8)]
    [InlineData(16_000, 9)]
    [InlineData(9_000, 10)]
    [InlineData(3_000, 11)]
    public void ClassifiesTournamentTypeFromSourceClassFamilies(
        long classCode,
        byte expectedValue)
    {
        var expectedType = (LegacyClassPkWarTournamentType)expectedValue;

        Assert.Equal(expectedType, LegacyClassPkWarState.GetTournamentType(classCode));
        Assert.Equal(expectedValue, (byte)expectedType);
        Assert.Equal(expectedType, LegacyClassPkWarState.GetTournamentType(CreateSnapshot(classCode: classCode)));
    }

    [Theory]
    [InlineData(1, 4)]
    [InlineData(99, 4)]
    [InlineData(100, 4)]
    [InlineData(119, 4)]
    [InlineData(120, 4)]
    [InlineData(129, 4)]
    [InlineData(130, 3)]
    [InlineData(140, 3)]
    public void PreservesSourceOddLevelClassification(long level, byte expectedValue)
    {
        var expected = (LegacyClassPkWarTournamentLevel)expectedValue;

        Assert.Equal(expected, LegacyClassPkWarState.GetTournamentLevel(level));
        Assert.Equal(expected, LegacyClassPkWarState.GetTournamentLevel(CreateSnapshot(level: level)));
    }

    [Fact]
    public void BracketCombinesSnapshotClassAndLevel()
    {
        var bracket = LegacyClassPkWarState.GetBracket(CreateSnapshot(classCode: 9_051, level: 130));

        Assert.Equal(LegacyClassPkWarTournamentType.Thunderstriker, bracket.TournamentType);
        Assert.Equal(LegacyClassPkWarTournamentLevel.Level_130_140, bracket.TournamentLevel);
    }

    [Fact]
    public void StartCreatesAliveThirtyMinuteStateWithProvidedDynamicId()
    {
        var now = new DateTimeOffset(2026, 6, 30, 22, 0, 0, TimeSpan.Zero);

        var state = LegacyClassPkWarState.Start(now, dynamicId: 123_456);

        Assert.Equal(LegacyClassPkWarProcess.Alive, state.Process);
        Assert.Equal(now.AddMinutes(30), state.FinishTimer);
        Assert.Equal(123_456U, state.DynamicId);
    }

    [Theory]
    [InlineData("Alive")]
    [InlineData("alive")]
    [InlineData("ProcesType.Alive")]
    [InlineData("1")]
    public void CheckJoinAllowsRecognizedAliveProcessBeforeFinishTimer(string process)
    {
        var now = new DateTimeOffset(2026, 6, 30, 22, 10, 0, TimeSpan.Zero);
        var finishTimer = now.AddMinutes(5);

        var result = LegacyClassPkWarState.CheckJoin(process, finishTimer, now, dynamicId: 77);

        Assert.True(result.CanJoin);
        Assert.Equal(LegacyClassPkWarProcess.Alive, result.State.Process);
        Assert.Equal(finishTimer, result.State.FinishTimer);
        Assert.Equal(77U, result.State.DynamicId);
    }

    [Theory]
    [InlineData("Dead")]
    [InlineData("dead")]
    [InlineData("ProcesType.Dead")]
    [InlineData("0")]
    [InlineData("")]
    [InlineData(null)]
    public void CheckJoinDeniesRecognizedDeadProcess(string? process)
    {
        var now = new DateTimeOffset(2026, 6, 30, 22, 10, 0, TimeSpan.Zero);
        var finishTimer = now.AddMinutes(5);

        var result = LegacyClassPkWarState.CheckJoin(process, finishTimer, now, dynamicId: 77);

        Assert.False(result.CanJoin);
        Assert.Equal(LegacyClassPkWarProcess.Dead, result.State.Process);
        Assert.Equal(finishTimer, result.State.FinishTimer);
        Assert.Equal(77U, result.State.DynamicId);
    }

    [Fact]
    public void CheckJoinExpiresAliveProcessByKeepingAliveAndDenyingJoin()
    {
        var now = new DateTimeOffset(2026, 6, 30, 22, 31, 0, TimeSpan.Zero);
        var finishTimer = now.AddMinutes(-1);

        var result = LegacyClassPkWarState.CheckJoin("Alive", finishTimer, now);

        Assert.False(result.CanJoin);
        Assert.Equal(LegacyClassPkWarProcess.Alive, result.State.Process);
        Assert.Equal(finishTimer, result.State.FinishTimer);
    }

    private static CharacterSnapshot CreateSnapshot(long classCode = 1_000, long level = 130)
    {
        return new CharacterSnapshot(
            Uid: 1,
            Name: "ClassPkWarTest",
            Body: 1003,
            Face: 0,
            Hair: 0,
            Class: classCode,
            FirstClass: 0,
            SecondClass: 0,
            Reborn: 0,
            Level: level,
            Experience: 0,
            MapId: 1_002,
            X: 430,
            Y: 378,
            PreviousMapId: 1_002,
            PreviousMapX: 430,
            PreviousMapY: 378,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: 0,
            AttributePoints: 0,
            MinHitPoints: 1,
            MinMana: 0,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 0,
            SourceFile: "fixture",
            Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }
}
