using WolfConquer.Core.World;
using Xunit;
using Contribution = WolfConquer.Core.World.Chinese7935EchoType6Thresholds.Contribution;
using Row = WolfConquer.Core.World.Chinese7935EchoType6Thresholds.Row;

namespace WolfConquer.Tests;

public sealed partial class Chinese7935EchoType6ThresholdsTests
{
    [Fact]
    public void ContributionOverflowRefusesWithoutChangingInputs()
    {
        Row[] rows = [new(4, 10, 6, 17)];
        Contribution[] contributions = [new(4, uint.MaxValue), new(4, 1)];
        var beforeRows = rows.ToArray();
        var beforeContributions = contributions.ToArray();
        Assert.Throws<OverflowException>(() => Chinese7935EchoType6Thresholds.Calculate(rows, contributions));
        Assert.Equal(beforeRows, rows);
        Assert.Equal(beforeContributions, contributions);
    }

    [Fact]
    public void TotalBonusOverflowRefusesRatherThanWrappingToZero()
    {
        Assert.Throws<OverflowException>(() => Chinese7935EchoType6Thresholds.Calculate(
            [new Row(4, 1, 6, uint.MaxValue), new Row(8, 1, 6, 1)],
            [new Contribution(4, 1), new Contribution(8, 1)]));
    }

    [Theory]
    [InlineData(10u, 17u, 17u)]
    [InlineData(10u, 19u, 19u)]
    [InlineData(9u, 17u, 0u)]
    [InlineData(9u, 19u, 0u)]
    public void RetainedEqualThresholdRowsHaveADefinedUnsignedMaximumInEitherOrder(
        uint current, uint secondBonus, uint expected)
    {
        Row[] rows = [new(4, 10, 6, 17), new(4, 10, 6, secondBonus)];
        Contribution[] contributions = [new(4, current)];
        Assert.Equal(expected, Chinese7935EchoType6Thresholds.Calculate(rows, contributions));
        Array.Reverse(rows);
        Assert.Equal(expected, Chinese7935EchoType6Thresholds.Calculate(rows, contributions));
    }

    [Fact]
    public void RowBoundIsAppliedBeforeCalculation()
    {
        var rows = new Row[Chinese7935EchoType6Thresholds.MaximumRows + 1];
        Assert.Throws<ArgumentOutOfRangeException>(() => Chinese7935EchoType6Thresholds.Calculate(rows, []));
    }

    [Fact]
    public void ContributionBoundIsAppliedBeforeCalculation()
    {
        var contributions = new Contribution[Chinese7935EchoType6Thresholds.MaximumContributions + 1];
        Assert.Throws<ArgumentOutOfRangeException>(() => Chinese7935EchoType6Thresholds.Calculate([], contributions));
    }

    [Fact]
    public void ExactOfflineBoundsRemainAccepted()
    {
        Assert.Equal(0u, Chinese7935EchoType6Thresholds.Calculate(
            new Row[Chinese7935EchoType6Thresholds.MaximumRows],
            new Contribution[Chinese7935EchoType6Thresholds.MaximumContributions]));
    }

    [Fact]
    public void SuccessfulCalculationDoesNotChangeCallerArrays()
    {
        Row[] rows = [new(4, 10, 6, 17), new(8, 20, 6, 11)];
        Contribution[] contributions = [new(8, 20), new(4, 10)];
        var beforeRows = rows.ToArray();
        var beforeContributions = contributions.ToArray();
        Assert.Equal(28u, Chinese7935EchoType6Thresholds.Calculate(rows, contributions));
        Assert.Equal(beforeRows, rows);
        Assert.Equal(beforeContributions, contributions);
    }
}
