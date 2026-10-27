using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePerfectionEffectTests
{
    public static IEnumerable<object[]> ThresholdLevels() => Bands.SelectMany(bands => bands)
        .SelectMany(band => new[] { band.Level - 1, band.Level })
        .Concat([int.MinValue, -1, 0, int.MaxValue]).Distinct().Order().Select(level => new object[] { level });

    [Theory]
    [MemberData(nameof(ThresholdLevels))]
    public void WholeUpdateResetsAllFieldsAndPreservesEveryStrictNativeThreshold(int level)
    {
        using var fixture = new Fixture();
        var owner = fixture.Client.PerfectionStatus;
        Poison(owner);

        owner.Update(level, fixture.Client);

        Assert.Equal(Expected(level), Values(owner));
        Assert.Same(owner, fixture.Client.PerfectionStatus);
        Assert.Empty(fixture.Storage.Objects);
        Assert.Equal(0u, fixture.Client.MyPrestigePoints);
        Assert.All(fixture.Client.PrestigePoints, value => Assert.Equal(0u, value));
    }

    [Fact]
    public void ClientBirthHasDistinctEagerStatusAndFortyThreePointSlotsButNoInventedRankEntry()
    {
        using var fixture = new Fixture();
        var other = new LegacyNativeGameClientRuntime();
        Assert.Equal(new double[16], Values(fixture.Client.PerfectionStatus));
        Assert.NotSame(fixture.Client.PerfectionStatus, other.PerfectionStatus);
        Assert.NotSame(fixture.Client.PrestigePoints, other.PrestigePoints);
        Assert.Equal(43, fixture.Client.PrestigePoints.Length);
        Assert.Null(fixture.Client.PrestrigeEntry);
        Assert.Equal(0u, fixture.Client.MyPrestigePoints);
        Assert.Null(other.Rune);
        fixture.Client.PerfectionStatus.ToxinEraser = 19;
        fixture.Client.PrestigePoints[0] = 77;
        Assert.Equal(0d, other.PerfectionStatus.ToxinEraser);
        Assert.Equal(0u, other.PrestigePoints[0]);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(325, false)]
    [InlineData(325, true)]
    [InlineData(601, false)]
    [InlineData(601, true)]
    public void NullClientOrMissingRuneRefusesBeforeChangingAnyExistingPerfectionField(int level, bool missingRune)
    {
        using var fixture = new Fixture();
        var state = fixture.Client.PerfectionStatus;
        Poison(state);
        var prior = Values(state);
        Exception? failure;
        if (missingRune)
        {
            fixture.Client.Rune = null;
            failure = Record.Exception(() => state.Update(level, fixture.Client));
        }
        else
        {
            failure = Record.Exception(() => state.Update(level, null!));
        }
        Assert.Equal(prior, Values(state));
        Assert.Same(state, fixture.Client.PerfectionStatus);
        if (missingRune)
        {
            Assert.Null(fixture.Client.Rune);
            Assert.IsType<InvalidOperationException>(failure);
        }
        else
        {
            Assert.Same(fixture.Storage, fixture.Client.Rune);
            Assert.IsType<ArgumentNullException>(failure);
        }
    }
}
