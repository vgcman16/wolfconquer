using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapLoadingTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(99)]
    public void RealMapDictionaryConstructionAndAddPreserveRawKeysNoLazyFallbackAndMutableBase(int capacity)
    {
        var dictionary = new LegacyNativeMapDictionary<string, object>(capacity);
        var value = new object();
        Assert.Equal(0, dictionary.Count);
        Assert.True(dictionary.Add("not-a-number", value));
        Assert.False(dictionary.Add("not-a-number", new object()));
        Assert.Same(value, dictionary.Base["not-a-number"]);
        Assert.True(dictionary.TryGetValue("not-a-number", out var found));
        Assert.Same(value, found);
        dictionary.Base["other"] = value;
        Assert.Equal(2, dictionary.Count);
        Assert.True(dictionary.ContainsValue(value));
        Assert.False(dictionary.ContainsValue(new object()));
        var values = dictionary.Values;
        dictionary.Remove("other");
        Assert.Single(values);
        dictionary.Remove("missing");
        Assert.Single(dictionary.Base);
        dictionary.Clear();
        Assert.Empty(values);
        Assert.False(dictionary.TryGetValue("not-a-number", out found));
        Assert.Null(found);
    }

    [Fact]
    public void DefaultMapDictionaryConstructorAndNegativeCapacityKeepNativeBehavior()
    {
        var dictionary = new LegacyNativeMapDictionary<uint, string>();
        Assert.Empty(dictionary.Base);
        Assert.Throws<ArgumentOutOfRangeException>(() => new LegacyNativeMapDictionary<uint, string>(-1));
        Assert.Throws<ArgumentNullException>(() => new LegacyNativeMapDictionary<string, int>().Add(null!, 1));
    }

    [Fact]
    public void OnlyValuesRepairsNullPhysicalDictionaryAndRetainedOldCollectionStaysOld()
    {
        var dictionary = new LegacyNativeMapDictionary<uint, object>();
        var old = dictionary.Base;
        var row = new object();
        old.Add(1, row);
        var retained = dictionary.Values;
        typeof(LegacyNativeMapDictionary<uint, object>)
            .GetField("DictBase", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(dictionary, null);

        Assert.Null(dictionary.Base);
        Assert.Throws<NullReferenceException>(() => dictionary.Count);
        Assert.Throws<NullReferenceException>(() => dictionary.Add(2, row));
        Assert.Empty(dictionary.Values);
        Assert.NotSame(old, dictionary.Base);
        Assert.Same(row, Assert.Single(retained));
        Assert.True(dictionary.Add(2, row));
        Assert.Single(dictionary.Values);
        Assert.Single(retained);
    }

    [Fact]
    public Task MobConstructorKeepsWholeNativeDefaultsAndDoesNotAllocateMissingCatalogOrAdvanceUid() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            LegacyNativePoolMaps.ServerMaps = null;
            uint before = LegacyNativeMobCollection.GenerateUid.Count;
            var collection = new LegacyNativeMobCollection(700);
            Assert.Null(collection.DMap);
            Assert.Equal("", collection.LocationSpawn);
            Assert.NotNull(collection.SyncRoot);
            Assert.Equal((byte)3, LegacyNativeMobCollection.Multiple);
            Assert.Equal(700u, typeof(LegacyNativeMobCollection)
                .GetField("DmapID", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(collection));
            Assert.Null(LegacyNativePoolMaps.ServerMaps);
            Assert.False(collection.ReadMap());
            Assert.Equal(before, LegacyNativeMobCollection.GenerateUid.Count);
        });

    [Fact]
    public Task MobConstructionAndReadMapRetainActualMutableMapOwnerAliases() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var first = Map(id: 700);
        LegacyNativePoolMaps.ServerMaps!.Add(700, first);
        var collection = new LegacyNativeMobCollection(700);
        Assert.Same(first, collection.DMap);
        Assert.Same(collection, first.MonstersColletion);
        var second = Map(id: 700);
        LegacyNativePoolMaps.ServerMaps.Base[700] = second;
        Assert.True(collection.ReadMap());
        Assert.Same(second, collection.DMap);
        Assert.Same(collection, second.MonstersColletion);
        Assert.Same(collection, first.MonstersColletion);
        LegacyNativePoolMaps.ServerMaps.Base.Remove(700);
        Assert.False(collection.ReadMap());
        Assert.Null(collection.DMap);
        Assert.Same(collection, second.MonstersColletion);
    });

    [Fact]
    public Task MobReadMapWithNullCatalogKeepsAlreadyBoundMapInsteadOfResettingIt() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var map = Map(id: 700);
            LegacyNativePoolMaps.ServerMaps!.Add(700, map);
            var collection = new LegacyNativeMobCollection(700);
            LegacyNativePoolMaps.ServerMaps = null;
            Assert.True(collection.ReadMap());
            Assert.Same(map, collection.DMap);
            Assert.Same(collection, map.MonstersColletion);
        });

    [Fact]
    public Task MobNullRowFaultsAfterTryGetOutPublishesNullRatherThanInventingMap() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            LegacyNativePoolMaps.ServerMaps!.Base[700] = null!;
            Assert.Throws<NullReferenceException>(() => new LegacyNativeMobCollection(700));
            var collection = new LegacyNativeMobCollection(701);
            LegacyNativePoolMaps.ServerMaps.Base[701] = null!;
            Assert.Throws<NullReferenceException>(() => collection.ReadMap());
            Assert.Null(collection.DMap);
        });
}
