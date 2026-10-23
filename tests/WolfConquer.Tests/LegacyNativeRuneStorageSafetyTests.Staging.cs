using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeRuneStorageSafetyTests
{
    [Theory]
    [InlineData(7u)] [InlineData(8u)]
    public void GenuineComparerFailureDuringCloneOrIncomingInsertLeavesBothPublishedOwnersUntouched(uint trigger)
    {
        using var scope = new Scope(); var original = Item(7, 101);
        Assert.True(scope.Rune.Add(original));
        var comparer = InstallComparer(scope); var dictionary = scope.Dictionary; var cache = scope.Cache;
        comparer.Trigger = trigger; comparer.OnHash = () => throw new InvalidOperationException("owned comparer fault");
        var exception = Assert.Throws<InvalidOperationException>(() => scope.Rune.Add(Item(8, 103)));
        Assert.Equal("owned comparer fault", exception.Message);
        Assert.Same(dictionary, scope.Dictionary); Assert.Same(cache, scope.Cache);
        Assert.Same(original, Assert.Single(dictionary).Value); Assert.Same(original, Assert.Single(cache));
        Assert.False(scope.Rune.ContainsUID(8)); Assert.Equal(1, scope.Rune.Count);
    }

    [Theory]
    [InlineData("uid")] [InlineData("player")] [InlineData("slot")] [InlineData("owner")]
    [InlineData("row-uid")] [InlineData("row-position")] [InlineData("row-id")]
    [InlineData("cache-row")] [InlineData("cache-reference")] [InlineData("dictionary-reference")]
    [InlineData("dictionary-prefix")] [InlineData("incoming-uid")]
    [InlineData("incoming-position")] [InlineData("incoming-id")]
    public void LastRealComparerCannotAuthorizeStaleBindingsOrRawRowDriftAndNeverPublishesPreparedPrefix(string drift)
    {
        using var scope = new Scope(); var original = Item(7, 101); var incoming = Item(8, 103);
        Assert.True(scope.Rune.Add(original)); var player = scope.Client.Player;
        var comparer = InstallComparer(scope); var dictionary = scope.Dictionary; var cache = scope.Cache;
        object? expectedDictionary = null; object? expectedCache = null;
        comparer.Trigger = 8;
        comparer.OnHash = () =>
        {
            switch (drift)
            {
                case "uid": player.UID++; break;
                case "player":
                    scope.Client.Player = new LegacyNativePlayerRuntime(scope.Client) { UID = player.UID }; break;
                case "slot": scope.Client.Rune = new(scope.Client); break;
                case "owner": scope.Rune.Owner = null; break;
                case "row-uid": original.Uid = 99; break;
                case "row-position": original.Position = 211; break;
                case "row-id": original.ITEM_ID++; break;
                case "cache-row": cache[0] = Item(7, 101); break;
                case "cache-reference": StorageCache.SetValue(scope.Rune, new[] { original }); break;
                case "dictionary-reference":
                    StorageItems.SetValue(scope.Rune, new Dictionary<uint, LegacyNativeGameItemRuntime> { [7] = original }); break;
                case "dictionary-prefix": dictionary.Add(9, Item(9)); break;
                case "incoming-uid": incoming.Uid = 99; break;
                case "incoming-position": incoming.Position = 211; break;
                case "incoming-id": incoming.ITEM_ID++; break;
            }
            expectedDictionary = StorageItems.GetValue(scope.Rune);
            expectedCache = StorageCache.GetValue(scope.Rune);
        };
        Assert.False(scope.Rune.Add(incoming)); Assert.Null(comparer.OnHash);
        Assert.Same(expectedDictionary, StorageItems.GetValue(scope.Rune));
        Assert.Same(expectedCache, StorageCache.GetValue(scope.Rune));
        Assert.False(dictionary.ContainsKey(8));
        Assert.DoesNotContain(incoming, (LegacyNativeGameItemRuntime[])StorageCache.GetValue(scope.Rune)!);
        // Supported test raw writes remain observable; refusal is not a fabricated rollback.
    }

    [Fact]
    public void ValidCustomComparerAndOldEscapedCacheKeepOrderAndUseOnlyCurrentAuthoritativeRows()
    {
        using var scope = new Scope(); var first = Item(7, 101); var second = Item(8, 103);
        Assert.True(scope.Rune.Add(first)); var escaped = scope.Rune.Objects; var comparer = InstallComparer(scope);
        escaped[0] = null!;
        Assert.True(scope.Rune.Add(second));
        Assert.Same(comparer, scope.Dictionary.Comparer);
        Assert.Equal(new[] { first, second }, scope.Cache);
        Assert.Same(first, scope.Rune.TryGetItem((byte)101)); Assert.Same(second, scope.Rune.TryGetItem((byte)103));
        Assert.Null(escaped[0]);
    }

    [Fact]
    public void ReadAdmissionDoesNotInvokeRetainedDictionaryComparerOrAuthorizeItsSideEffects()
    {
        using var scope = new Scope(); var first = Item(7, 101);
        Assert.True(scope.Rune.Add(first)); var comparer = InstallComparer(scope);
        comparer.Trigger = 7; comparer.OnHash = () => throw new InvalidOperationException("must remain staged");
        Assert.Equal(1, scope.Rune.Count); Assert.Same(first, Assert.Single(scope.Rune.Objects));
        Assert.True(scope.Rune.ContainsUID(7)); Assert.True(scope.Rune.TryGetItem(7u, out var row));
        Assert.Same(first, row); Assert.Same(first, scope.Rune.TryGetItem((byte)101));
        Assert.Same(first, scope.Rune.TryGetItem(181305u));
        Assert.False(scope.Rune.Free(101)); Assert.True(scope.Rune.Free(103));
        Assert.Same(first, Assert.Single(scope.Rune.WornObjects)); Assert.NotNull(comparer.OnHash);
        Assert.Throws<InvalidOperationException>(() => scope.Rune.Add(Item(8)));
        Assert.Null(comparer.OnHash); Assert.Same(first, Assert.Single(scope.Cache));
    }
}
