using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerProgressionTests
{
    [Fact]
    public Task CalculationUsesOriginalExperienceFractionAndDoesNotMutatePlayerOrRows() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Level(20);
            scope.Player.Experience = 250;
            var row = scope.Row(20, 1000, 100);

            Assert.Equal(500UL, scope.Client.CalcExpBall(25, out ushort next));

            Assert.Equal((ushort)20, next);
            Assert.Equal((ushort)20, scope.Player.Level);
            Assert.Equal(250UL, scope.Player.Experience);
            Assert.Same(row, Assert.Single(scope.Rows).Value);
            Assert.Equal(1000UL, row.Experience);
            Assert.Equal(100, row.UpLevTime);
            Assert.Empty(scope.Queue);
            Assert.Equal(237, scope.Packet.Position);
            Assert.Equal(321, scope.Packet.Size);
        });

    [Fact]
    public Task ProjectedLevelUsesItsTimeButFinalResultUsesOriginalLevelExperience() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Level(20);
            scope.Player.Experience = 250;
            var original = scope.Row(20, 1000, 100);
            var projected = scope.Row(21, 9000, 200);

            Assert.Equal(125UL, scope.Client.CalcExpBall(100, out ushort next));

            Assert.Equal((ushort)21, next);
            Assert.Equal((ushort)20, scope.Player.Level);
            Assert.Equal(250UL, scope.Player.Experience);
            Assert.Same(original, scope.Rows[20]);
            Assert.Same(projected, scope.Rows[21]);
            Assert.Empty(scope.Queue);
        });

    [Fact]
    public Task ProjectionTraversesEveryWholeThresholdWithVaryingNativeTimes() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Level(20);
            scope.Row(20, 1000, 100);
            scope.Row(21, 7000, 200);
            scope.Row(22, 11000, 400);

            Assert.Equal(125UL, scope.Client.CalcExpBall(350, out ushort next));

            Assert.Equal((ushort)22, next);
            Assert.Equal((ushort)20, scope.Player.Level);
            Assert.Equal(0UL, scope.Player.Experience);
        });

    [Fact]
    public Task EqualityCrossesThresholdRatherThanLeavingCurrentLevelAtFullFraction() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Level(20);
            scope.Row(20, 1000, 100);
            scope.Row(21, 9000, 200);
            Assert.Equal(0UL, scope.Client.CalcExpBall(100, out ushort next));
            Assert.Equal((ushort)21, next);
        });

    [Fact]
    public Task ProjectionStopsAt140EvenWithRemainingAmountAndReadsThatRowTime() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Level(139);
            scope.Row(139, 1000, 100);
            scope.Row(140, 9000, 200);
            Assert.Equal(750UL, scope.Client.CalcExpBall(250, out ushort next));
            Assert.Equal((ushort)140, next);
            Assert.Equal((ushort)139, scope.Player.Level);
        });

    [Theory]
    [InlineData(140)]
    [InlineData(150)]
    [InlineData(65535)]
    public Task CappedPlayerReturnsZeroAndZeroNextBeforeReadingMissingTable(int level) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Level((ushort)level);
            LegacyNativePoolLevelInfo.LevelInfo = null!;
            Assert.Equal(0UL, scope.Client.CalcExpBall(600, out ushort next));
            Assert.Equal((ushort)0, next);
        });

    [Fact]
    public Task SignedExperienceAndTimeProductRetainsUncheckedInt64WrapBeforeDoubleDivision() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Level(20);
            scope.Player.Experience = 0x4000000000000000UL;
            scope.Row(20, 1000, 4);
            Assert.Equal(0UL, scope.Client.CalcExpBall(0, out ushort next));
            Assert.Equal((ushort)20, next);
            Assert.Equal(0x4000000000000000UL, scope.Player.Experience);
        });

    [Fact]
    public Task NegativeAmountRetainsNativeUnsignedWrapRatherThanClampingToZero() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Level(20);
            scope.Row(20, 1000, 100);
            Assert.Equal(18446744073709551116UL, scope.Client.CalcExpBall(-50, out ushort next));
            Assert.Equal((ushort)20, next);
        });

    [Theory]
    [InlineData(double.NaN, 0UL, 140)]
    [InlineData(double.PositiveInfinity, 0UL, 140)]
    [InlineData(double.NegativeInfinity, 0x8000000000000000UL, 20)]
    public Task NonfiniteAmountRetainsNativeComparisonLoopAndConversion(
        double amount, ulong expected, int projected) => NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Level(20);
            for (int level = 20; level <= 140; level++)
                scope.Row((byte)level, 1000, 100);
            Assert.Equal(expected, scope.Client.CalcExpBall(amount, out ushort next));
            Assert.Equal((ushort)projected, next);
            Assert.Equal((ushort)20, scope.Player.Level);
            Assert.Empty(scope.Queue);
        });
}
