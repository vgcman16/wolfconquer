using Flags = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeStatusFlagsTests
{
    [Theory]
    [InlineData(0, false, -1)]
    [InlineData(0, false, 0)]
    [InlineData(0, false, 31)]
    [InlineData(32, false, -1)]
    [InlineData(32, false, 32)]
    [InlineData(32, true, -1)]
    [InlineData(32, true, 32)]
    [InlineData(32, true, 33)]
    [InlineData(32, true, int.MaxValue)]
    [InlineData(64, true, int.MinValue)]
    [InlineData(64, true, 64)]
    [InlineData(64, true, int.MaxValue)]
    public void InvalidRequestedIndexIsUnavailableAndCannotAuthorizeANegatedQuery(int size, bool populated, int index)
    {
        var flags = new Flags(size);
        if (populated) Assert.True(flags.TryAdd(6, 7, false, 0));
        var before = CaptureVector(flags);
        Assert.False(flags.TryContainFlag(index, out bool present));
        Assert.False(present);
        bool granted = false;
        Assert.Throws<InvalidDataException>(() =>
        {
            if (!flags.ContainFlag(index)) granted = true;
        });
        Assert.False(granted);
        AssertVectorUnchanged(flags, before);
    }

    [Theory]
    [InlineData(32, 31)]
    [InlineData(64, 63)]
    public void LastPhysicalIndexStillDistinguishesValidAbsenceAndPresence(int size, int index)
    {
        var flags = new Flags(size);
        Assert.True(flags.TryContainFlag(index, out bool absent));
        Assert.False(absent);
        Assert.False(flags.ContainFlag(index));
        Assert.True(flags.TryAdd(index, 7, false, 0));
        Assert.True(flags.TryContainFlag(index, out bool present));
        Assert.True(present);
        Assert.True(flags.ContainFlag(index));
    }
}
