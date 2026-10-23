using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerProgressionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public Task MissingCurrentTableOrRowFaultsBeforeAssigningOutAndDoesNotRepairOwner(int mode) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Level(20);
            if (mode == 0) LegacyNativePoolLevelInfo.LevelInfo.Clear();
            ushort next = 777;

            Assert.Throws<KeyNotFoundException>(() => scope.Client.CalcExpBall(1, out next));

            Assert.Equal((ushort)777, next);
            Assert.Empty(scope.Rows);
            Assert.Empty(scope.Queue);
            Assert.Equal((ushort)20, scope.Player.Level);
        });

    [Fact]
    public Task ExplicitNullCurrentRowReturnsZeroUnlikeMissingKey() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Level(20);
            scope.Rows[20] = null!;
            Assert.Equal(0UL, scope.Client.CalcExpBall(1, out ushort next));
            Assert.Equal((ushort)0, next);
            Assert.Null(scope.Rows[20]);
        });

    [Fact]
    public Task MissingProjectedRowFaultRetainsPreviousOutAndDoesNotApplyPartialLevel() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Level(20);
            scope.Row(20, 1000, 100);
            ushort next = 777;

            Assert.Throws<KeyNotFoundException>(() => scope.Client.CalcExpBall(150, out next));

            Assert.Equal((ushort)777, next);
            Assert.Equal((ushort)20, scope.Player.Level);
            Assert.Equal(0UL, scope.Player.Experience);
            Assert.Empty(scope.Queue);
        });

    [Fact]
    public Task ExplicitNullProjectedRowBreaksAndKeepsPreviousTimeForFinalOriginalRow() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Level(20);
            scope.Row(20, 1000, 100);
            scope.Rows[21] = null!;
            Assert.Equal(500UL, scope.Client.CalcExpBall(150, out ushort next));
            Assert.Equal((ushort)21, next);
            Assert.Null(scope.Rows[21]);
            Assert.Equal((ushort)20, scope.Player.Level);
        });

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public Task NonpositiveCurrentTimeReturnsZeroAfterNativeComparisonWithoutDivision(int time) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Level(20);
            scope.Row(20, 1000, time);
            Assert.Equal(0UL, scope.Client.CalcExpBall(double.NegativeInfinity, out ushort next));
            Assert.Equal((ushort)20, next);
            Assert.Single(scope.Rows);
        });

    [Fact]
    public Task ZeroTimeAtCapStillAssignsProjected140BeforeReturningZero() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Level(139);
            scope.Row(139, 1000, 0);
            scope.Row(140, 9000, 0);
            Assert.Equal(0UL, scope.Client.CalcExpBall(50, out ushort next));
            Assert.Equal((ushort)140, next);
        });

    [Fact]
    public Task ZeroExperienceDenominatorProducesNaNAndTraversesNativeLoopTo140() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Level(20);
            for (int level = 20; level <= 140; level++)
                scope.Row((byte)level, 0, 100);
            Assert.Equal(0UL, scope.Client.CalcExpBall(0, out ushort next));
            Assert.Equal((ushort)140, next);
            Assert.Equal((ushort)20, scope.Player.Level);
        });

    [Fact]
    public Task WrongSortIsNotAReplacementUserTable() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Level(20);
            scope.Row(20, 1000, 100);
            LegacyNativePoolLevelInfo.LevelInfo = new()
            {
                [LegacyNativeDbLevExp.Sort.Warlock] = scope.Rows
            };
            Assert.Throws<KeyNotFoundException>(() => scope.Client.CalcExpBall(1, out _));
            Assert.Single(scope.Rows);
            Assert.Empty(scope.Queue);
        });
}
