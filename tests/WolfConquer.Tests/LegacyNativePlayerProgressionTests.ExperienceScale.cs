namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerProgressionTests
{
    [Theory]
    [InlineData(3, 7000UL)]
    [InlineData(6, 2000UL)]
    [InlineData(11, 1000UL)]
    [InlineData(21, 500UL)]
    public Task GainExpressionRetainsSourceShapedProductRounding(int difference, ulong expected) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Level(100);
            Assert.Equal(expected, scope.Client.GainExperience(10000, (ushort)(100 - difference)));
        });

    [Theory]
    [InlineData(-16, 1800UL)]
    [InlineData(-15, 1500UL)]
    [InlineData(-9, 1500UL)]
    [InlineData(-8, 1300UL)]
    [InlineData(-6, 1300UL)]
    [InlineData(-5, 1000UL)]
    [InlineData(0, 1000UL)]
    [InlineData(2, 1000UL)]
    [InlineData(3, 700UL)]
    [InlineData(5, 700UL)]
    [InlineData(6, 200UL)]
    [InlineData(10, 200UL)]
    [InlineData(11, 100UL)]
    [InlineData(20, 100UL)]
    [InlineData(21, 50UL)]
    public Task GainExperienceUsesEveryNativeDifferenceBoundaryWithoutMutation(int difference, ulong expected) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Level(100);
            scope.Player.Experience = 1234;
            scope.Client.FullLoading = true;

            Assert.Equal(expected, scope.Client.GainExperience(1000, (ushort)(100 - difference)));

            Assert.Equal((ushort)100, scope.Player.Level);
            Assert.Equal(1234UL, scope.Player.Experience);
            Assert.True(scope.Client.FullLoading);
            Assert.Empty(scope.Rows);
            Assert.Empty(scope.Queue);
            Assert.Equal(237, scope.Packet.Position);
            Assert.Equal(321, scope.Packet.Size);
        });

    [Theory]
    [InlineData(-16, 19UL)]
    [InlineData(-9, 16UL)]
    [InlineData(-6, 14UL)]
    [InlineData(0, 10UL)]
    [InlineData(3, 7UL)]
    [InlineData(6, 2UL)]
    [InlineData(11, 1UL)]
    [InlineData(21, 0UL)]
    public Task GainExperienceTruncatesAfterMultiplierRatherThanBefore(int difference, ulong expected) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Level(100);
            Assert.Equal(expected, scope.Client.GainExperience(10.9, (ushort)(100 - difference)));
            Assert.Empty(scope.Queue);
        });

    [Theory]
    [InlineData(0, 65535, 1800UL)]
    [InlineData(65535, 0, 50UL)]
    [InlineData(140, 140, 1000UL)]
    [InlineData(150, 150, 1000UL)]
    public Task GainExperienceUsesFullUshortDifferenceAndHasNoProgressionLevelCap(
        int level, int target, ulong expected) => NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Level((ushort)level);
            Assert.Equal(expected, scope.Client.GainExperience(1000, (ushort)target));
            Assert.Equal((ushort)level, scope.Player.Level);
        });

    [Theory]
    [InlineData(-1.0, ulong.MaxValue)]
    [InlineData(-1.75, ulong.MaxValue)]
    [InlineData(-0.75, 0UL)]
    [InlineData(double.NaN, 0UL)]
    [InlineData(double.PositiveInfinity, 0UL)]
    [InlineData(double.NegativeInfinity, 0x8000000000000000UL)]
    [InlineData(18446744073709551616.0, 0UL)]
    public Task GainExperiencePreservesFrameworkUnsignedCastFaultDomain(double experience, ulong expected) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Level(100);
            Assert.Equal(expected, scope.Client.GainExperience(experience, 100));
            Assert.Empty(scope.Queue);
        });
}
