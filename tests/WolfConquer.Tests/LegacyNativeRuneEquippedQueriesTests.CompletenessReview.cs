using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeRuneEquippedQueriesTests
{
    private static readonly FieldInfo ReviewCache = typeof(LegacyNativeRuneItemStorage)
        .GetField("objects", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly FieldInfo ReviewItems = typeof(LegacyNativeRuneItemStorage)
        .GetField("items", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static void ReviewQueriesRefuse(LegacyNativeRuneItemStorage rune)
    {
        Assert.False(rune.IsEquipped("normal", out var normal)); Assert.Null(normal);
        Assert.False(rune.IsEquipped("alternate", out var alternate)); Assert.Null(alternate);
        byte level = 77;
        Assert.False(rune.IsEquipped("normal", ref level)); Assert.Equal((byte)0, level);
        Assert.Empty(rune.WornObjects); Assert.Equal((byte)0, rune.EquippedCount);
        Assert.Null(rune.EquippedRedRune); Assert.Null(rune.EquippedBlueRune);
    }

    [Theory]
    [InlineData("normal-prefix")] [InlineData("alternate-prefix")]
    [InlineData("empty-cache")] [InlineData("missing-inactive")]
    public void ReviewIncompleteCacheCannotGrantEffectsFromAnOtherwiseOwnedPrefix(string corruption)
    {
        using var fixture = new Fixture(); fixture.Ready();
        var normal = fixture.Rune(1, 4_031_107, 101, "normal");
        var alternate = fixture.Rune(2, 4_031_109, 121, "alternate");
        var inactive = fixture.Rune(3, 4_031_101, 211, "bag");
        fixture.Client.Equipment!.Alternante = true;
        var dictionary = (Dictionary<uint, LegacyNativeGameItemRuntime>)ReviewItems.GetValue(fixture.Storage)!;
        var entries = dictionary.ToArray();
        LegacyNativeGameItemRuntime[] corrupted = corruption switch
        {
            "normal-prefix" => [normal],
            "alternate-prefix" => [alternate],
            "empty-cache" => [],
            _ => [normal, alternate],
        };
        ReviewCache.SetValue(fixture.Storage, corrupted);
        ReviewQueriesRefuse(fixture.Storage);
        Assert.Same(corrupted, fixture.Storage.Objects); Assert.Equal(entries, dictionary.ToArray());
        Assert.Same(dictionary, ReviewItems.GetValue(fixture.Storage));
        Assert.Equal((ushort)101, normal.Position); Assert.Equal((ushort)121, alternate.Position);
        Assert.Equal((ushort)211, inactive.Position);
    }

    [Fact]
    public void ReviewExtraAuthoritativeEntryCannotBeHiddenByAValidCachedPrefix()
    {
        using var fixture = new Fixture(); fixture.Ready();
        var normal = fixture.Rune(1, 4_031_107, 101, "normal");
        var dictionary = (Dictionary<uint, LegacyNativeGameItemRuntime>)ReviewItems.GetValue(fixture.Storage)!;
        // Model the precise stale-cache state after a writer's authoritative Add
        // but before its cache publication. This is not a fake query/owner callback.
        var omitted = new LegacyNativeGameItemRuntime { Uid = 2, Position = 121 };
        dictionary.Add(omitted.Uid, omitted);
        var cache = fixture.Storage.Objects;
        fixture.Client.Equipment!.Alternante = true;
        ReviewQueriesRefuse(fixture.Storage);
        Assert.Same(cache, fixture.Storage.Objects); Assert.Same(normal, Assert.Single(cache));
        Assert.Same(omitted, dictionary[2]); Assert.Equal(2, dictionary.Count);
    }

    [Theory]
    [InlineData((ushort)101, false)] [InlineData((ushort)102, false)]
    [InlineData((ushort)103, false)] [InlineData((ushort)120, false)]
    [InlineData((ushort)121, true)] [InlineData((ushort)122, true)]
    [InlineData((ushort)123, true)] [InlineData((ushort)139, true)] [InlineData((ushort)140, true)]
    public void ReviewDistinctUidsCannotGrantTwoNamedEffectsFromOnePhysicalEquippedSlot(ushort position, bool alternate)
    {
        using var fixture = new Fixture(); fixture.Ready();
        var first = fixture.Rune(1, 4_031_107, position, "normal");
        var second = fixture.Rune(2, 4_031_109, position, "alternate");
        fixture.Client.Equipment!.Alternante = alternate;
        var cache = fixture.Storage.Objects;
        ReviewQueriesRefuse(fixture.Storage);
        Assert.Same(cache, fixture.Storage.Objects); Assert.Same(first, cache[0]); Assert.Same(second, cache[1]);
        Assert.Equal(position, first.Position); Assert.Equal(position, second.Position);
    }

    [Theory]
    [InlineData((ushort)0)] [InlineData((ushort)211)] [InlineData((ushort)212)]
    public void ReviewDuplicateInactiveLocationsDoNotInvalidateOneGenuineWornRune(ushort inactivePosition)
    {
        using var fixture = new Fixture(); fixture.Ready();
        var normal = fixture.Rune(1, 4_031_107, 101, "normal");
        fixture.Rune(2, 4_031_108, inactivePosition, "stored-a");
        fixture.Rune(3, 4_031_109, inactivePosition, "stored-b");
        Assert.Same(normal, Assert.Single(fixture.Storage.WornObjects));
        Assert.True(fixture.Storage.IsEquipped("normal", out var found)); Assert.Same(normal, found);
        Assert.Equal((byte)1, fixture.Storage.EquippedCount);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ReviewNormalAndAlternateSlotPairStillUsesTheGenuineNativeFallback(bool alternate)
    {
        using var fixture = new Fixture(); fixture.Ready();
        var normal = fixture.Rune(1, 4_031_107, 101, "normal");
        var reserve = fixture.Rune(2, 4_031_109, 121, "alternate");
        fixture.Client.Equipment!.Alternante = alternate;
        Assert.Same(alternate ? reserve : normal, Assert.Single(fixture.Storage.WornObjects));
        Assert.True(fixture.Storage.IsEquipped(alternate ? "alternate" : "normal", out var found));
        Assert.Same(alternate ? reserve : normal, found);
        Assert.Equal((byte)1, fixture.Storage.EquippedCount);
    }
}
