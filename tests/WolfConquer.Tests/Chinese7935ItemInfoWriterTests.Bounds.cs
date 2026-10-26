using WolfConquer.Core.World;
using Scalar = WolfConquer.Core.World.Chinese7935ItemInfoScalar;

namespace WolfConquer.Tests;

public sealed partial class Chinese7935ItemInfoWriterTests
{
    [Fact]
    public void KnownScalarAndRepeatedConstructionBoundsRejectBeforeOutput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Chinese7935ItemInfoView.Create(0, 0, 0,
            new Scalar[Chinese7935ItemInfoView.MaximumOptionalScalarFields + 1]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Chinese7935ItemInfoView.Create(0, 0, 0,
            values31: new int[Chinese7935ItemInfoView.MaximumRepeated31Values + 1]));
        var accepted = Chinese7935ItemInfoView.Create(0, 0, 0,
            values31: new int[Chinese7935ItemInfoView.MaximumRepeated31Values]);
        Assert.Equal(6 + 3 * Chinese7935ItemInfoView.MaximumRepeated31Values, accepted.ToPayloadBytes().Length);
    }

    [Fact]
    public void CanonicalPayloadExactLimitWritesAndOneMoreRefusesConstruction()
    {
        byte[] exact = new byte[65520];
        var accepted = Chinese7935ItemInfoView.Create(0, 0, 0, data27: exact);
        var output = accepted.ToPayloadBytes();
        Assert.Equal(Chinese7935ItemInfoView.MaximumPayloadBytes, output.Length);
        Assert.Equal(Convert.FromHexString(RequiredZero + "DA01F0FF03"), output[..11]);
        Chinese7935ItemInfoView? rejected = null;
        Assert.Throws<ArgumentException>(() => rejected = Chinese7935ItemInfoView.Create(0, 0, 0, data27: new byte[65521]));
        Assert.Null(rejected);
        Assert.Throws<ArgumentException>(() => Chinese7935ItemInfoView.Create(0, 0, 0,
            data27: new byte[Chinese7935ItemInfoView.MaximumPayloadBytes + 1]));
        Assert.Equal(new byte[65520], exact);
    }

    [Fact]
    public void CombinedRawFieldsAreBoundedTogetherRatherThanIndividuallyOnly()
    {
        Assert.Throws<ArgumentException>(() => Chinese7935ItemInfoView.Create(0, 0, 0,
            data27: new byte[40000], data28: new byte[40000]));
    }

    [Fact]
    public void ValidPackedInputWhoseNativeCanonicalOutputIsTooLargeRefusesWithoutOutput()
    {
        // Independently framed: required6 + bytes27 header5 + raw60396 + packed31 header4 + 1024*5 =65531.
        var input = new byte[Chinese7935ItemInfoView.MaximumPayloadBytes];
        var prefix = Convert.FromHexString(RequiredZero + "DA01ECD703");
        prefix.CopyTo(input, 0);
        var offset = prefix.Length + 60396;
        Convert.FromHexString("FA018028").CopyTo(input, offset);
        offset += 4;
        var encodedMinusOne = Convert.FromHexString("FFFFFFFF0F");
        for (var index = 0; index < Chinese7935ItemInfoView.MaximumRepeated31Values; index++)
        {
            encodedMinusOne.CopyTo(input, offset);
            offset += 5;
        }
        Assert.Equal(input.Length, offset);
        Assert.True(Chinese7935ItemInfoView.TryParseKnownPayload(input, out var parsed));
        Assert.NotNull(parsed);
        byte[]? output = [1, 2, 3];
        Assert.False(parsed.TryToPayloadBytes(out output));
        Assert.Null(output);
        Assert.Throws<InvalidOperationException>(() => parsed.ToPayloadBytes());
        Assert.Equal(60396, parsed.CopyData27()!.Length);
        Assert.Equal(Chinese7935ItemInfoView.MaximumRepeated31Values, parsed.Values31.Length);
        Assert.All(parsed.Values31.ToArray(), value => Assert.Equal(-1, value));
        Assert.Equal(prefix, input[..prefix.Length]);
    }
}
