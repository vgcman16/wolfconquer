using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapLoadingTests
{
    private static ConcurrentDictionary<uint, LegacyNativeGameClientRuntime> Clients(LegacyNativeGameMap map) =>
        (ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>)typeof(LegacyNativeGameMap)
            .GetField("Clients", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(map)!;

    [Fact]
    public Task WholeEnquerAndDenquerRetainSameClientPlayerSpatialAndCacheOwners() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var map = Map(32, 32);
            scope.Player.X = 2; scope.Player.Y = 3;
            var empty = map.Values;

            map.Enquer(scope.Client);

            Assert.Same(scope.Client, Assert.Single(Clients(map)).Value);
            Assert.Same(map, scope.Client.Map);
            Assert.Same(scope.Player, Assert.Single(map.View.GetAllMapRoles(LegacyNativeMapObjectType.Player)));
            var populated = map.Values;
            Assert.NotSame(empty, populated);
            Assert.Same(scope.Client, Assert.Single(populated));
            Assert.Same(populated, map.Values);
            map.Denquer(scope.Client);
            Assert.Empty(Clients(map));
            Assert.Empty(map.View.GetAllMapRoles(LegacyNativeMapObjectType.Player));
            Assert.Empty(map.Values);
            Assert.Same(scope.Client, Assert.Single(populated)); // Old returned array stays old.
            Assert.Same(map, scope.Client.Map); // Native Denquer does not clear the client field.
            Assert.Empty(scope.Queue);
        });

    [Fact]
    public Task CachedValuesSetterIsGenuinelyEmptyAndDoesNotAdoptSuppliedClients() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var map = Map();
            var original = map.Values;
            map.Values = [scope.Client];
            Assert.Same(original, map.Values);
            Assert.Empty(map.Values);
            Assert.Empty(Clients(map));
            Assert.Null(scope.Client.Map);
        });

    [Fact]
    public Task DuplicateUidAdmissionDoesNotReplaceRetainedClientOrBindSecondClientMap() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            using var other = new NativeSpellTestFixture();
            var map = Map();
            scope.Player.X = 2; scope.Player.Y = 3;
            other.Owner.Player.X = 4; other.Owner.Player.Y = 5;
            map.Enquer(scope.Client);
            var held = map.Values;

            map.Enquer(other.Owner);

            Assert.Same(scope.Client, Assert.Single(Clients(map)).Value);
            Assert.Same(scope.Player, Assert.Single(map.View.GetAllMapRoles(LegacyNativeMapObjectType.Player)));
            Assert.Null(other.Owner.Map);
            Assert.Same(held, map.Values);
        });

    [Fact]
    public Task EnquerKeepsNativeAdmissionWhenViewRejectsOutOfBoundsPlayer() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var map = Map(8, 8);
            scope.Player.X = 8; scope.Player.Y = 1;
            map.Enquer(scope.Client);
            Assert.Same(scope.Client, Assert.Single(Clients(map)).Value);
            Assert.Same(map, scope.Client.Map);
            Assert.Same(scope.Client, Assert.Single(map.Values));
            Assert.Empty(map.View.GetAllMapRoles(LegacyNativeMapObjectType.Player));
        });

    [Fact]
    public Task NullViewFaultRetainsClientDictionaryPrefixWithoutPublishingMapOrCacheUpdate() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var map = new LegacyNativeGameMap(8, 8, 700);
            scope.Player.X = 2; scope.Player.Y = 3;

            Assert.Throws<NullReferenceException>(() => map.Enquer(scope.Client));

            Assert.Same(scope.Client, Assert.Single(Clients(map)).Value);
            Assert.Null(scope.Client.Map);
            Assert.Empty(map.Values);
            map.View = new LegacyNativeMapView(8, 8);
            map.Enquer(scope.Client); // Duplicate UID branch does not repair failed prefix.
            Assert.Null(scope.Client.Map);
            Assert.Empty(map.Values);
            Assert.Empty(map.View.GetAllMapRoles(LegacyNativeMapObjectType.Player));
        });

    [Fact]
    public Task DenquerByForeignSameUidRemovesDictionaryKeyButLeavesOriginalSpatialReference() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            using var other = new NativeSpellTestFixture();
            var map = Map();
            scope.Player.X = 2; scope.Player.Y = 3;
            other.Owner.Player.X = 2; other.Owner.Player.Y = 3;
            map.Enquer(scope.Client);
            _ = map.Values;

            map.Denquer(other.Owner);

            Assert.Empty(Clients(map));
            Assert.Empty(map.Values);
            Assert.Same(scope.Player, Assert.Single(map.View.GetAllMapRoles(LegacyNativeMapObjectType.Player)));
            Assert.Same(map, scope.Client.Map);
            Assert.Null(other.Owner.Map);
        });

    [Fact]
    public Task MapSystemMessageUsesNativeStaleUsersCacheUntilValuesGetterRefreshesIt() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var map = Map();
            scope.Player.X = 2; scope.Player.Y = 3;
            map.Enquer(scope.Client);
            map.SendSysMesage("first");
            Assert.Empty(scope.Queue);
            _ = map.Values;
            map.SendSysMesage("second");
            Assert.Single(scope.Queue);
            map.Denquer(scope.Client);
            map.SendSysMesage("third");
            Assert.Equal(2, scope.Queue.Count); // Source Users stays cached until Values reads Update.
            _ = map.Values;
            map.SendSysMesage("fourth");
            Assert.Equal(2, scope.Queue.Count);
        });

    [Fact]
    public Task RetainedValuesArrayIsRealMutableAliasUsedBySystemMessagePublication() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            using var other = new NativeSpellTestFixture();
            var map = Map();
            scope.Player.X = 2; scope.Player.Y = 3;
            map.Enquer(scope.Client);
            var cache = map.Values;
            cache[0] = other.Owner;

            map.SendSysMesage("alias");

            Assert.Empty(scope.Queue);
            Assert.Single(other.Queue);
            Assert.Same(scope.Client, Assert.Single(Clients(map)).Value);
            Assert.Same(other.Owner, Assert.Single(map.Values));
        });

    [Fact]
    public void MapPhysicalConstructionPreservesNativeSeparateOwnersAndUninitializedLoaderState()
    {
        var first = new LegacyNativeGameMap(8, 9, 700);
        var second = new LegacyNativeGameMap(8, 9, 700);
        Assert.Equal(700u, first.ID);
        Assert.Equal(8, first.bounds.Width); Assert.Equal(9, first.bounds.Height);
        Assert.Null(first.View);
        Assert.Null(first.MonstersColletion);
        Assert.Null(first.Magnolia);
        Assert.Empty(first.soldierRemains);
        Assert.Empty(first.Portals);
        Assert.Equal("", first.Name);
        Assert.Equal(0u, first.BaseID);
        Assert.Equal(0u, first.TypeStatus);
        Assert.Equal(0u, first.RecordSteedRace);
        Assert.Null(first.FloorType);
        Assert.Null(first.Altitude);
        Assert.NotSame(first.SyncRoot, second.SyncRoot);
        Assert.NotSame(first.Portals, second.Portals);
        Assert.NotSame(first.soldierRemains, second.soldierRemains);
        Assert.NotSame(Clients(first), Clients(second));
        Assert.Empty(first.Values);
    }

    [Fact]
    public void PortalRetainsAllNativeUshortPropertiesWithoutManagedProjection()
    {
        var portal = new LegacyNativePortal
        {
            MapID = ushort.MaxValue, X = 1, Y = 2,
            Destiantion_MapID = 1002, Destiantion_X = 410, Destiantion_Y = 354
        };
        Assert.Equal((ushort)65535, portal.MapID);
        Assert.Equal((ushort)1, portal.X); Assert.Equal((ushort)2, portal.Y);
        Assert.Equal((ushort)1002, portal.Destiantion_MapID);
        Assert.Equal((ushort)410, portal.Destiantion_X);
        Assert.Equal((ushort)354, portal.Destiantion_Y);
        Assert.All(typeof(LegacyNativePortal).GetProperties(), property => Assert.Equal(typeof(ushort), property.PropertyType));
    }
}
