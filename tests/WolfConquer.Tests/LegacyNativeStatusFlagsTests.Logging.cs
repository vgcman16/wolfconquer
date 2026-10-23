using Flags = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeStatusFlagsTests
{
    [Fact]
    public void InvalidRegistrationOwnerReturnsExplicitRefusalsWithoutLoggingRepairOrCacheAuthority()
    {
        var flags = new Flags(32); Assert.True(flags.TryAdd(6, 7, false, 0));
        var retained = flags.GetFlags(); var registrations = flags.ArrayFlags;
        flags.ArrayFlags = null!; var invalid = CaptureVector(flags);
        Assert.False(flags.TryContainFlag(6, out bool present)); Assert.False(present);
        Assert.Throws<InvalidDataException>(() => flags.ContainFlag(6));
        Assert.Empty(flags.GetFlags()); Assert.Empty(flags.GetFlags());
        Assert.False(flags.TryAdd(7, 9, false, 0)); AssertVectorUnchanged(flags, invalid);
        flags.ArrayFlags = registrations;
        var recovered = flags.GetFlags(); Assert.NotSame(retained, recovered);
        Assert.Same(Assert.Single(retained), Assert.Single(recovered));
    }
}
