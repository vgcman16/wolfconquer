using WolfConquer.Core.World;
using Xunit;
using Contribution = WolfConquer.Core.World.Chinese7935EchoType6Thresholds.Contribution;
using Row = WolfConquer.Core.World.Chinese7935EchoType6Thresholds.Row;

namespace WolfConquer.Tests;

public sealed partial class Chinese7935EchoType6ThresholdsTests
{
    [Fact]
    public void EmptyInputsProduceZero()
    {
        Assert.Equal(0u, Chinese7935EchoType6Thresholds.Calculate([], []));
    }

    [Fact]
    public void MissingAttributeDoesNotQualifyEvenForZeroThreshold()
    {
        Assert.Equal(0u, Chinese7935EchoType6Thresholds.Calculate([new Row(11, 0, 6, 71)], []));
    }

    [Fact]
    public void PresentZeroAttributeQualifiesForZeroThreshold()
    {
        Assert.Equal(71u, Chinese7935EchoType6Thresholds.Calculate(
            [new Row(11, 0, 6, 71)], [new Contribution(11, 0)]));
    }

    [Theory]
    [InlineData(39u, 0u)]
    [InlineData(40u, 17u)]
    [InlineData(41u, 17u)]
    public void ThresholdComparisonIncludesExactBoundary(uint current, uint expected)
    {
        Assert.Equal(expected, Chinese7935EchoType6Thresholds.Calculate(
            [new Row(4, 40, 6, 17)], [new Contribution(4, current)]));
    }

    [Fact]
    public void ContributionsAccumulateByMatchingAttributeType()
    {
        Assert.Equal(17u, Chinese7935EchoType6Thresholds.Calculate(
            [new Row(4, 40, 6, 17)],
            [new Contribution(4, 19), new Contribution(8, 90), new Contribution(4, 21)]));
    }

    [Fact]
    public void QualifiedRowsContributeMaximumPerTypeRatherThanSumOrLastRow()
    {
        Row[] rows = [new(4, 10, 6, 29), new(4, 30, 6, 11), new(4, 20, 6, 7)];
        Assert.Equal(29u, Chinese7935EchoType6Thresholds.Calculate(rows, [new Contribution(4, 40)]));
    }

    [Fact]
    public void DifferentPrimaryAttributeMaximaAreAdded()
    {
        Row[] rows = [new(4, 10, 6, 29), new(4, 30, 6, 11), new(8, 20, 6, 13), new(8, 60, 6, 99)];
        Assert.Equal(42u, Chinese7935EchoType6Thresholds.Calculate(rows,
            [new Contribution(4, 40), new Contribution(8, 30)]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(12)]
    public void OtherExtraAttributeTypesDoNotContribute(int extraType)
    {
        Assert.Equal(17u, Chinese7935EchoType6Thresholds.Calculate(
            [new Row(4, 40, 6, 17), new Row(4, 1, extraType, uint.MaxValue)],
            [new Contribution(4, 40)]));
    }

    [Fact]
    public void UnsatisfiedHigherThresholdIsExcludedEvenWhenBonusIsLarger()
    {
        Assert.Equal(17u, Chinese7935EchoType6Thresholds.Calculate(
            [new Row(4, 40, 6, 17), new Row(4, 41, 6, uint.MaxValue)], [new Contribution(4, 40)]));
    }

    [Fact]
    public void UnsignedCurrentAndThresholdAreNotComparedAsSignedIntegers()
    {
        Assert.Equal(17u, Chinese7935EchoType6Thresholds.Calculate(
            [new Row(4, 0x8000_0000, 6, 17)], [new Contribution(4, 0x8000_0001)]));
        Assert.Equal(0u, Chinese7935EchoType6Thresholds.Calculate(
            [new Row(4, 0x8000_0000, 6, 17)], [new Contribution(4, 0x7FFF_FFFF)]));
    }

    [Fact]
    public void UnsignedBonusMaximumIsNotSelectedAsSigned()
    {
        Assert.Equal(uint.MaxValue, Chinese7935EchoType6Thresholds.Calculate(
            [new Row(4, 1, 6, uint.MaxValue), new Row(4, 2, 6, 17)], [new Contribution(4, 10)]));
    }

    [Fact]
    public void AttributeKeysRemainIndependentSignedDwords()
    {
        Assert.Equal(7u, Chinese7935EchoType6Thresholds.Calculate(
            [new Row(-1, 3, 6, 7), new Row(1, 3, 6, 19)], [new Contribution(-1, 3)]));
    }

    [Fact]
    public void ResultDoesNotDependOnCatalogOrContributionOrder()
    {
        Row[] rows = [new(4, 5, 6, 9), new(8, 7, 6, 13), new(4, 15, 6, 11)];
        Contribution[] contributions = [new(8, 7), new(4, 10), new(4, 5)];
        Assert.Equal(24u, Chinese7935EchoType6Thresholds.Calculate(rows, contributions));
        Array.Reverse(rows);
        Array.Reverse(contributions);
        Assert.Equal(24u, Chinese7935EchoType6Thresholds.Calculate(rows, contributions));
    }
}
