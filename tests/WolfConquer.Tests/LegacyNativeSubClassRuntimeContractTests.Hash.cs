using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Sort = WolfConquer.Core.Data.LegacyNativeDbLevExp.Sort;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeSubClassRuntimeContractTests
{
    // Actual Framework/x86 source-shaped GetHashPoint terms, not values inferred
    // from modern-runtime casts. The dictionary key deliberately differs from ID.
    [Theory]
    [InlineData(0, 255, 0u)]
    [InlineData(1, 255, 255u)]
    [InlineData(6, 255, 25500000u)]
    [InlineData(9, 255, 4025163520u)]
    [InlineData(10, 9, 410065408u)]
    [InlineData(11, 255, 3084393472u)]
    [InlineData(15, 255, 1774567424u)]
    [InlineData(18, 9, 1239023616u)]
    [InlineData(18, 255, 0u)]
    [InlineData(19, 9, 3800301568u)]
    [InlineData(20, 9, 0u)]
    [InlineData(255, 9, 0u)]
    public void PhysicalIdHashTermsMatchObservedNativeX86Conversions(byte id, byte phrase, uint expected)
    {
        var storage = new LegacyNativeSubClassRuntime();
        storage.src[Sort.Warlock] = new() { ID = (Sort)id, Phrase = phrase, Level = 255 };
        Assert.Equal(expected, storage.GetHashPoint());
        Assert.Equal((Sort)id, storage.src[Sort.Warlock].ID);
        Assert.Equal(phrase, storage.src[Sort.Warlock].Phrase);
    }

    [Fact]
    public void MultiplePhysicalAliasedTermsWrapTheNativeUnsignedAccumulator()
    {
        var storage = new LegacyNativeSubClassRuntime();
        storage.src[Sort.MartialArtist] = new() { ID = Sort.Wrangler, Phrase = 255 };
        storage.src[Sort.Warlock] = new() { ID = Sort.Wrangler, Phrase = 255 };
        Assert.Equal(3755359744u, storage.GetHashPoint());
    }
}
