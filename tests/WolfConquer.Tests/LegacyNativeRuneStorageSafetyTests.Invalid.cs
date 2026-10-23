using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeRuneStorageSafetyTests
{
    [Theory]
    [InlineData("null-cache")] [InlineData("null-dictionary")] [InlineData("null-row")]
    [InlineData("truncated-cache")] [InlineData("extra-cache")] [InlineData("reordered-cache")]
    [InlineData("duplicate-cache")] [InlineData("foreign-row")]
    [InlineData("zero-uid")] [InlineData("changed-uid")] [InlineData("wrong-key")]
    public void InvalidFullGraphNeverBecomesAbsenceOrFreePermissionAndIsNotSilentlyRepaired(string corruption)
    {
        using var scope = new Scope(); var first = Item(7, 101); var second = Item(8, 121);
        Assert.True(scope.Rune.Add(first)); Assert.True(scope.Rune.Add(second));
        switch (corruption)
        {
            case "null-cache": StorageCache.SetValue(scope.Rune, null); break;
            case "null-dictionary": StorageItems.SetValue(scope.Rune, null); break;
            case "null-row": scope.Cache[1] = null!; break;
            case "truncated-cache": StorageCache.SetValue(scope.Rune, new[] { first }); break;
            case "extra-cache": StorageCache.SetValue(scope.Rune, new[] { first, second, Item(9) }); break;
            case "reordered-cache": StorageCache.SetValue(scope.Rune, new[] { second, first }); break;
            case "duplicate-cache": scope.Cache[1] = first; break;
            case "foreign-row": scope.Cache[1] = Item(8, 121); break;
            case "zero-uid": second.Uid = 0; break;
            case "changed-uid": second.Uid = 99; break;
            case "wrong-key":
                StorageItems.SetValue(scope.Rune, new Dictionary<uint, LegacyNativeGameItemRuntime>
                    { [7] = first, [99] = second }); break;
        }
        var dictionary = StorageItems.GetValue(scope.Rune); var cache = StorageCache.GetValue(scope.Rune);
        AssertUnavailable(scope);
        Assert.Same(dictionary, StorageItems.GetValue(scope.Rune));
        Assert.Same(cache, StorageCache.GetValue(scope.Rune));
    }

    [Theory]
    [InlineData("null-owner")] [InlineData("foreign-owner")] [InlineData("missing-slot")]
    [InlineData("replacement-slot")] [InlineData("null-player")] [InlineData("foreign-player")]
    public void InvalidPhysicalOwnershipRefusesWritesAndPermissionWithoutRemovingReadableRows(string corruption)
    {
        using var scope = new Scope(); var row = Item(7, 101);
        Assert.True(scope.Rune.Add(row));
        var foreign = new LegacyNativeGameClientRuntime();
        foreign.Player.UID = scope.Client.Player.UID;
        switch (corruption)
        {
            case "null-owner": scope.Rune.Owner = null; break;
            case "foreign-owner": scope.Rune.Owner = foreign; foreign.Rune = scope.Rune; break;
            case "missing-slot": scope.Client.Rune = null; break;
            case "replacement-slot": scope.Client.Rune = new(scope.Client); break;
            case "null-player": scope.Client.Player = null!; break;
            case "foreign-player": scope.Client.Player = foreign.Player; break;
        }
        var dictionary = scope.Dictionary; var cache = scope.Cache;
        Assert.False(scope.Rune.Add(Item(8))); Assert.False(scope.Rune.Free(103));
        Assert.False(scope.Rune.Free(211)); Assert.False(scope.Rune.Free(212));
        Assert.Same(dictionary, scope.Dictionary); Assert.Same(cache, scope.Cache);
        Assert.Equal(1, scope.Rune.Count); Assert.Same(row, Assert.Single(scope.Rune.Objects));
    }

    [Fact]
    public void NullAndZeroUidIncomingRowsAreRefusedBeforeAnyPublication()
    {
        using var scope = new Scope(); Assert.True(scope.Rune.Add(Item(7)));
        var dictionary = scope.Dictionary; var cache = scope.Cache;
        Assert.False(scope.Rune.Add(null!)); Assert.False(scope.Rune.Add(Item(0)));
        Assert.False(scope.Rune.Add(Item(7, 101)));
        Assert.Same(dictionary, scope.Dictionary); Assert.Same(cache, scope.Cache);
        Assert.Equal(1, scope.Rune.Count);
    }

    [Fact]
    public void NullableConstructionCannotBeRetrofittedIntoAWriteOwner()
    {
        using var scope = new Scope(); var orphan = new LegacyNativeRuneItemStorage(null) { Owner = scope.Client };
        scope.Client.Rune = orphan;
        Assert.False(orphan.Add(Item(7))); Assert.False(orphan.Free(212));
        Assert.Empty(orphan.Objects); Assert.Equal(0, orphan.Count);
        Assert.Same(orphan, scope.Client.Rune); Assert.Same(scope.Client, orphan.Owner);
    }

    [Fact]
    public void InvalidUidQueryClearsCallerOutputBeforeReportingUnavailable()
    {
        using var scope = new Scope(); var row = Item(7);
        Assert.True(scope.Rune.Add(row)); scope.Cache[0] = null!;
        LegacyNativeGameItemRuntime? found = row;
        Assert.Throws<InvalidDataException>(() => scope.Rune.TryGetItem(7u, out found));
        Assert.Null(found);
    }
}
