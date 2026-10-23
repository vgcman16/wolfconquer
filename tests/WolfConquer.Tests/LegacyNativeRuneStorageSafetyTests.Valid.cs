using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeRuneStorageSafetyTests
{
    [Theory]
    [InlineData(0u)] [InlineData(99u)]
    public void AssignedGenuineStorageAllowsCurrentPlayerUidAndKeepsRowAliasesAndOrder(uint playerUid)
    {
        using var scope = new Scope();
        scope.Client.Player.UID = playerUid;
        var first = Item(7, 211);
        var second = Item(8, 212);
        Assert.True(scope.Rune.Add(first));
        var escaped = scope.Rune.Objects;
        Assert.True(scope.Rune.Add(second));
        Assert.Same(first, Assert.Single(escaped));
        Assert.Equal(new[] { first, second }, scope.Rune.Objects);
        Assert.Equal(new uint[] { 7, 8 }, scope.Dictionary.Keys);
        Assert.Equal(new[] { first, second }, scope.Cache);
        Assert.NotSame(scope.Cache, scope.Rune.Objects);
        Assert.NotSame(scope.Rune.Objects, scope.Rune.Objects);
        Assert.True(scope.Rune.TryGetItem(7u, out var found)); Assert.Same(first, found);
        Assert.Same(first, scope.Rune.TryGetItem((byte)211));
        Assert.Same(second, scope.Rune.TryGetItem(181305u)); // Item-ID query excludes211 only.
        Assert.True(scope.Rune.ContainsUID(7)); Assert.False(scope.Rune.ContainsUID(9));
        Assert.Null(scope.Rune.TryGetItem((byte)103)); Assert.Null(scope.Rune.TryGetItem(88u));
    }

    [Fact]
    public void DefensiveArrayEditsCannotAlterStorageButValidRowAliasesRemainLive()
    {
        using var scope = new Scope(); var first = Item(7, 101);
        Assert.True(scope.Rune.Add(first));
        var escaped = scope.Rune.Objects; escaped[0] = null!;
        Assert.Same(first, Assert.Single(scope.Rune.Objects));
        first.Position = 103;
        Assert.True(scope.Rune.Free(101)); Assert.False(scope.Rune.Free(103));
        Assert.Same(first, scope.Rune.TryGetItem((byte)103));
        Assert.Null(escaped[0]); Assert.Equal(1, scope.Rune.Count);
    }

    [Fact]
    public void StorageConstructorRemainsUnassignedAndOnlyGenuineAssignmentEnablesWrites()
    {
        using var scope = new Scope();
        var replacement = new LegacyNativeRuneItemStorage(scope.Client);
        Assert.Same(scope.Rune, scope.Client.Rune);
        Assert.Empty(replacement.Objects); Assert.False(replacement.Add(Item(7)));
        Assert.False(replacement.Free(212));
        scope.Client.Rune = replacement;
        scope.Client.Equipment = null; // Raw loader writes do not require equipped-query equipment.
        Assert.True(replacement.Add(Item(7))); Assert.Equal(1, replacement.Count);
        Assert.False(scope.Rune.Add(Item(8))); Assert.False(scope.Rune.Free(212));
        Assert.Empty(scope.Rune.Objects);
    }

    [Theory]
    [InlineData(255)] [InlineData(256)] [InlineData(257)] [InlineData(300)]
    public void ExactCountDoesNotWrapOrInventATotalCapacityLimit(int count)
    {
        using var scope = new Scope();
        for (uint uid = 1; uid <= count; uid++) Assert.True(scope.Rune.Add(Item(uid)));
        Assert.Equal(count, scope.Rune.Count); Assert.Equal(typeof(int), scope.Rune.Count.GetType());
        Assert.Equal(count, scope.Rune.Objects.Length);
        Assert.True(scope.Rune.Free(211)); // Original fallback: no211 occupant, even at total>=100.
        scope.Cache[0].Position = 211;
        Assert.False(scope.Rune.Free(211)); Assert.True(scope.Rune.Free(212));
    }

    [Theory]
    [InlineData((ushort)101)] [InlineData((ushort)211)] [InlineData((ushort)212)]
    public void AddUsesExactlyPosition211CapacityBeforeAnyNewIncomingPosition(ushort incomingPosition)
    {
        using var scope = new Scope();
        for (uint uid = 1; uid <= 100; uid++) Assert.True(scope.Rune.Add(Item(uid, 211)));
        var dictionary = scope.Dictionary; var cache = scope.Cache;
        Assert.False(scope.Rune.Add(Item(101, incomingPosition)));
        Assert.Same(dictionary, scope.Dictionary); Assert.Same(cache, scope.Cache);
        Assert.False(scope.Rune.ContainsUID(101));
        cache[0].Position = 212;
        Assert.True(scope.Rune.Add(Item(101, incomingPosition)));
        Assert.Equal(101, scope.Rune.Count);
    }

    [Theory]
    [InlineData((ushort)0)] [InlineData((ushort)101)] [InlineData((ushort)120)]
    [InlineData((ushort)121)] [InlineData((ushort)135)] [InlineData(ushort.MaxValue)]
    public void FreeAndRawLookupPreserveExactValidPositionOccupancyWithoutGuessedEquipRules(ushort position)
    {
        using var scope = new Scope(); var row = Item(7, position);
        Assert.True(scope.Rune.Free(position)); Assert.True(scope.Rune.Add(row));
        Assert.False(scope.Rune.Free(position)); Assert.True(scope.Rune.Free(212));
        Assert.True(scope.Rune.Add(Item(8, position))); // Add itself does not invent slot admission.
        if (position is not (0 or 211 or 212)) Assert.Empty(scope.Rune.WornObjects);
    }

    [Fact]
    public async Task CooperatingConcurrentAddsPublishCompleteDictionaryAndCacheWithoutLostRows()
    {
        using var scope = new Scope();
        var rows = Enumerable.Range(1, 32).Select(uid => Item((uint)uid)).ToArray();
        var results = await Task.WhenAll(rows.Select(row => Task.Run(() => scope.Rune.Add(row))));
        Assert.All(results, value => Assert.True(value)); Assert.Equal(32, scope.Rune.Count);
        Assert.Equal(scope.Dictionary.Values, scope.Cache);
        Assert.Equal(32, scope.Cache.Select(row => row.Uid).Distinct().Count());
        foreach (var row in rows) Assert.Same(row, scope.Dictionary[row.Uid]);
    }
}
