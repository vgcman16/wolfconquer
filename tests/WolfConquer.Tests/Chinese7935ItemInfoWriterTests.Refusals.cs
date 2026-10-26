using WolfConquer.Core.World;
using Scalar = WolfConquer.Core.World.Chinese7935ItemInfoScalar;

namespace WolfConquer.Tests;

public sealed partial class Chinese7935ItemInfoWriterTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(27)]
    [InlineData(31)]
    [InlineData(36)]
    [InlineData(40)]
    [InlineData(44)]
    public void UInt32FactoryRefusesWrongOrUnsupportedFields(int field)
    {
        Assert.Throws<ArgumentException>(() => Scalar.UInt32(field, 0));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(27)]
    [InlineData(41)]
    public void Int32FactoryRefusesMismatchedFields(int field)
    {
        Assert.Throws<ArgumentException>(() => Scalar.Int32(field, -1));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(28)]
    [InlineData(40)]
    public void UInt64FactoryRefusesMismatchedFields(int field)
    {
        Assert.Throws<ArgumentException>(() => Scalar.UInt64(field, ulong.MaxValue));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void OptionalInputCannotOverrideARequiredFieldEvenWithMatchingValue(int field)
    {
        Assert.Throws<ArgumentException>(() => Chinese7935ItemInfoView.Create(0, 0, 0, [Scalar.UInt32(field, 0)]));
    }

    [Fact]
    public void DefaultScalarAndDuplicateOptionalFieldsRefuseRatherThanChooseLast()
    {
        Assert.Throws<ArgumentException>(() => Chinese7935ItemInfoView.Create(0, 0, 0, [default]));
        Assert.Throws<ArgumentException>(() => Chinese7935ItemInfoView.Create(0, 0, 0,
            [Scalar.Int32(40, 1), Scalar.Int32(40, 2)]));
        Assert.Throws<ArgumentException>(() => Chinese7935ItemInfoView.Create(0, 0, 0,
            [Scalar.Int32(40, 1), Scalar.Int32(40, 1)]));
    }

    [Fact]
    public void InvalidConstructionLeavesCallerDataAndPreviouslyConstructedMessageUnchanged()
    {
        var accepted = CreateGolden();
        var before = accepted.ToPayloadBytes();
        Scalar[] rejectedFields = [Scalar.Int32(40, 1), Scalar.Int32(40, 2)];
        byte[] data = [255, 0];
        int[] values = [2, -1];
        var beforeFields = rejectedFields.ToArray();
        Assert.Throws<ArgumentException>(() => Chinese7935ItemInfoView.Create(5, 6, 7, rejectedFields, data, data, values));
        Assert.Equal(beforeFields, rejectedFields);
        Assert.Equal(new byte[] { 255, 0 }, data);
        Assert.Equal(new[] { 2, -1 }, values);
        Assert.Equal(before, accepted.ToPayloadBytes());
    }
}
