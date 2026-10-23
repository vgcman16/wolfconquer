using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeRuneEquippedQueriesTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void AnyRemainderWornRunesRejectUnknownPhysicalIdentityWithoutChangingCache(int corruption)
    {
        using var fixture = new Fixture();
        var first = fixture.Rune(11, 4_031_107, 101, "first");
        var second = fixture.Rune(12, 4_031_109, 121, "second");
        var cache = (LegacyNativeGameItemRuntime[])ReviewCache.GetValue(fixture.Storage)!;
        switch (corruption)
        {
            case 0: cache[1] = null!; break;
            case 1: cache[1] = first; break;
            case 2: cache[1] = new LegacyNativeGameItemRuntime { Uid = 99, Position = 121 }; break;
            case 3: cache[1] = new LegacyNativeGameItemRuntime { Uid = second.Uid, Position = 121 }; break;
            default: second.Uid = 999; break;
        }
        var physical = cache.ToArray();
        fixture.Client.Equipment!.Alternante = true;
        Assert.Empty(fixture.Storage.WornObjects);
        Assert.Same(cache, ReviewCache.GetValue(fixture.Storage));
        Assert.Equal(physical.Length, cache.Length);
        for (int i = 0; i < physical.Length; i++) Assert.Same(physical[i], cache[i]);
        Assert.Throws<InvalidDataException>(() => fixture.Storage.ContainsUID(11));
        Assert.Throws<InvalidDataException>(() => fixture.Storage.ContainsUID(12));
    }

    [Fact]
    public void AnyRemainderMissingCacheReturnsNoWornEffectWithoutRepairingTheOwner()
    {
        using var fixture = new Fixture();
        fixture.Rune(11, 4_031_107, 101, "first");
        var field = typeof(LegacyNativeRuneItemStorage).GetField("objects", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(fixture.Storage, null);
        Assert.Empty(fixture.Storage.WornObjects);
        Assert.Null(field.GetValue(fixture.Storage));
        Assert.Throws<InvalidDataException>(() => fixture.Storage.ContainsUID(11));
    }

    [Fact]
    public void AnyRemainderAlternateShadowUsesFreshPhysicalPositionsAndReturnsOwnedReferences()
    {
        using var fixture = new Fixture();
        var normal = fixture.Rune(11, 4_031_107, 101, "first");
        var alternate = fixture.Rune(12, 4_031_109, 121, "second");
        fixture.Client.Equipment!.Alternante = true;
        Assert.Same(alternate, Assert.Single(fixture.Storage.WornObjects));
        alternate.Position = 211;
        Assert.Same(normal, Assert.Single(fixture.Storage.WornObjects));
        Assert.NotSame(fixture.Storage.WornObjects, fixture.Storage.WornObjects);
        Assert.Equal((ushort)101, normal.Position); Assert.Equal((ushort)211, alternate.Position);
    }
}
