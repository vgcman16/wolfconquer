using WolfConquer.Core.World;
using MapType = WolfConquer.Core.World.LegacyNativeMapObjectType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapViewTests
{
    [Fact]
    public void NeighborhoodIsThreeByThreeBlocksInXThenYOrderNotDistanceOrDynamicFiltering()
    {
        var map = new LegacyNativeMapView(90, 90);
        var first = Model(1, 0, 0);
        first.Map = 999;
        first.DynamicID = 77;
        var second = Model(2, 0, 18);
        var third = Model(3, 18, 0);
        var fourth = Model(4, 53, 53);
        var outside = Model(5, 54, 35);
        var poker = Model(6, 35, 35, MapType.PokerTable);
        foreach (var item in new[] { fourth, third, second, first, outside, poker }) Assert.True(map.EnterMap(item));

        Assert.Equal(new ILegacyNativeMapObj[] { first, second, third, fourth }, map.Roles(MapType.Monster, 35, 35));
        Assert.Equal(4, map.CountRoles(MapType.Monster, 35, 35));
        Assert.False(first.Alive);
        Assert.False(first.AllowDynamic);
        Assert.True(map.TryGetObject<ModelMapObj>(1, MapType.Monster, 35, 35, out var found));
        Assert.Same(first, found); // Distance35 is accepted because block0 is a neighbor.
        Assert.True(map.Contain(1, 35, 35));
        Assert.True(map.Contain(6, 35, 35)); // Scans through the final PokerTable bucket.
        Assert.False(map.Contain(5, 35, 35));
        Assert.False(map.TryGetObject<ModelMapObj>(6, MapType.Monster, 35, 35, out var absentType));
        Assert.Null(absentType);
        Assert.Equal(new ILegacyNativeMapObj[] { first, second, third, fourth, outside }, map.GetAllMapRoles(MapType.Monster));
        Assert.Equal(5, map.GetAllMapRolesCount(MapType.Monster));
    }

    [Fact]
    public void CountRolesCountsNullEntriesWhileBothIteratorsSkipThemAndPreserveDuplicates()
    {
        var map = new LegacyNativeMapView(18, 18);
        var item = Model(1);
        var list = map.m_setBlock[0, 0].GetObjects(MapType.Monster);
        list.Add(null!);
        list.Add(item);
        list.Add(item);
        var calls = 0;

        Assert.Equal(3, map.CountRoles(MapType.Monster, 0, 0));
        Assert.Equal(new ILegacyNativeMapObj[] { item, item }, map.Roles(MapType.Monster, 0, 0));
        Assert.Equal(new ILegacyNativeMapObj[] { item, item }, map.GetAllMapRoles(MapType.Monster));
        Assert.Equal(2, map.GetAllMapRolesCount(MapType.Monster));
        Assert.Empty(map.Roles(MapType.Monster, 0, 0, _ => { calls++; return false; }));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void ClearMapKeepsBucketAliasesOtherTypesAndMovementCounter()
    {
        var map = new LegacyNativeMapView(36, 18);
        var first = Model(1, type: MapType.Player);
        var second = Model(2, 18, 0, MapType.Player);
        var monster = Model(3);
        map.EnterMap(first);
        map.EnterMap(second);
        map.EnterMap(monster);
        var left = map.m_setBlock[0, 0].GetObjects(MapType.Player);
        var right = map.m_setBlock[1, 0].GetObjects(MapType.Player);

        map.ClearMap(MapType.Player);

        Assert.Same(left, map.m_setBlock[0, 0].GetObjects(MapType.Player));
        Assert.Same(right, map.m_setBlock[1, 0].GetObjects(MapType.Player));
        Assert.Empty(left);
        Assert.Empty(right);
        Assert.Same(monster, Assert.Single(map.GetAllMapRoles(MapType.Monster)));
        Assert.False(map.MapContain(MapType.Player, 1));
        Assert.True(map.EnterMap(first));
        Assert.Equal(4u, first.IndexInScreen);
    }

    [Fact]
    public void RolesSnapshotsCurrentBucketWhileGetAllMapRolesEnumeratesItsLiveList()
    {
        var map = new LegacyNativeMapView(18, 18);
        var first = Model(1);
        var second = Model(2);
        var added = Model(3);
        map.EnterMap(first);
        map.EnterMap(second);
        using var snapshot = map.Roles(MapType.Monster, 0, 0).GetEnumerator();
        using var live = map.GetAllMapRoles(MapType.Monster).GetEnumerator();
        Assert.True(snapshot.MoveNext());
        Assert.True(live.MoveNext());
        Assert.Same(first, snapshot.Current);
        Assert.Same(first, live.Current);

        map.EnterMap(added);

        Assert.True(snapshot.MoveNext());
        Assert.Same(second, snapshot.Current);
        Assert.False(snapshot.MoveNext());
        Assert.True(live.MoveNext());
        Assert.Same(second, live.Current);
        Assert.True(live.MoveNext());
        Assert.Same(added, live.Current);
        Assert.False(live.MoveNext());
    }

    [Fact]
    public void LiveEnumerationRemovalShiftsAndSkipsTheNextListElement()
    {
        var map = new LegacyNativeMapView(18, 18);
        var first = Model(1);
        var shifted = Model(2);
        var third = Model(3);
        foreach (var item in new[] { first, shifted, third }) map.EnterMap(item);
        using var live = map.GetAllMapRoles(MapType.Monster).GetEnumerator();
        Assert.True(live.MoveNext());
        Assert.Same(first, live.Current);

        map.LeaveMap(first);

        Assert.True(live.MoveNext());
        Assert.Same(third, live.Current);
        Assert.False(live.MoveNext());
        Assert.Equal(new ILegacyNativeMapObj[] { shifted, third }, map.GetAllMapRoles(MapType.Monster));
    }

    [Fact]
    public void RolesIsDeferredAndSnapshotsLaterBucketsOnlyWhenTheyAreReached()
    {
        var map = new LegacyNativeMapView(36, 18);
        var query = map.Roles(MapType.Monster, 0, 0);
        var first = Model(1);
        var lateCurrent = Model(2);
        var futureBucket = Model(3, 18, 0);
        map.EnterMap(first);
        using var iterator = query.GetEnumerator();
        Assert.True(iterator.MoveNext());
        Assert.Same(first, iterator.Current);

        map.EnterMap(lateCurrent);
        map.EnterMap(futureBucket);

        Assert.True(iterator.MoveNext());
        Assert.Same(futureBucket, iterator.Current);
        Assert.False(iterator.MoveNext());
        Assert.Equal(new ILegacyNativeMapObj[] { first, lateCurrent, futureBucket }, query);
    }

    [Fact]
    public void NestedPredicateEnumerationCanMutateLiveOwnerWithoutTurningRolesSnapshotLive()
    {
        foreach (var snapshot in new[] { true, false })
        {
            var map = new LegacyNativeMapView(18, 18);
            var first = Model(1);
            var second = Model(2);
            var added = Model(3);
            map.EnterMap(first);
            map.EnterMap(second);
            var calls = 0;
            bool Include(ILegacyNativeMapObj _)
            {
                if (calls++ == 0)
                {
                    Assert.Equal(2, map.GetAllMapRolesCount(MapType.Monster));
                    Assert.True(map.EnterMap(added));
                }
                return true;
            }

            var result = (snapshot ? map.Roles(MapType.Monster, 0, 0, Include) :
                map.GetAllMapRoles(MapType.Monster, Include)).ToArray();

            Assert.Equal(snapshot ? new ILegacyNativeMapObj[] { first, second } :
                new ILegacyNativeMapObj[] { first, second, added }, result);
            Assert.Equal(result.Length, calls);
        }
    }

    [Fact]
    public void PredicateFaultsAreDeferredButNeverCaughtAsBucketFetchFailures()
    {
        var map = new LegacyNativeMapView(18, 18);
        map.EnterMap(Model(1));
        var calls = 0;
        var fault = new InvalidOperationException("MODEL predicate fault.");
        bool Fail(ILegacyNativeMapObj _) { calls++; throw fault; }
        var query = map.Roles(MapType.Monster, 0, 0, Fail);
        Assert.Equal(0, calls);

        Assert.Same(fault, Assert.Throws<InvalidOperationException>(() => query.ToArray()));
        Assert.Equal(1, calls);
        Assert.Same(fault, Assert.Throws<InvalidOperationException>(() => map.GetAllMapRolesCount(MapType.Monster, Fail)));
        Assert.Same(fault, Assert.Throws<InvalidOperationException>(() => map.GetMapObject<ModelMapObj>(MapType.Monster, 99, Fail)));
        Assert.Same(fault, Assert.Throws<InvalidOperationException>(() => map.MapContain(MapType.Monster, 99, Fail)));
    }

    [Fact]
    public void OnlyRolesCatchesBrokenBucketFetchAndContinuesToTheNextBlock()
    {
        var map = new LegacyNativeMapView(36, 18);
        var item = Model(1, 18, 0);
        map.EnterMap(item);
        map.m_setBlock[0, 0] = null!; // Controlled corruption of the actual public native grid.

        Assert.Same(item, Assert.Single(map.Roles(MapType.Monster, 0, 0)));
        Assert.Throws<NullReferenceException>(() => map.CountRoles(MapType.Monster, 0, 0));
        Assert.Throws<NullReferenceException>(() => map.GetAllMapRoles(MapType.Monster).ToArray());
        Assert.Throws<NullReferenceException>(() => map.TryGetObject<ModelMapObj>(1, MapType.Monster, 0, 0, out _));
        Assert.Throws<NullReferenceException>(() => map.Contain(1, 0, 0));
        Assert.Throws<NullReferenceException>(() => map.ClearMap(MapType.Monster));
    }

    [Fact]
    public void InvalidTypeIsSkippedByRolesFetchCatchButNotOtherQueries()
    {
        var map = new LegacyNativeMapView(18, 18);

        Assert.Empty(map.Roles(MapType.Count, 0, 0));
        Assert.Throws<IndexOutOfRangeException>(() => map.CountRoles(MapType.Count, 0, 0));
        Assert.Throws<IndexOutOfRangeException>(() => map.GetAllMapRoles(MapType.Count).ToArray());
        Assert.Throws<IndexOutOfRangeException>(() => map.MapContain(MapType.Count, 1));
    }

    [Fact]
    public void GlobalAndNeighborLookupKeepFirstGridMatchPredicatesDefaultsAndUncheckedCasts()
    {
        var map = new LegacyNativeMapView(36, 36);
        var first = Model(7, 0, 18);
        var second = Model(7, 18, 0);
        map.EnterMap(second);
        map.EnterMap(first);

        Assert.Same(first, map.GetMapObject<ModelMapObj>(MapType.Monster, 7));
        Assert.True(map.TryGetObject<ModelMapObj>(7, MapType.Monster, 18, 18, out var neighbor));
        Assert.Same(first, neighbor);
        Assert.True(map.MapContain(MapType.Monster, 7));
        Assert.Same(second, map.GetMapObject<ModelMapObj>(MapType.Monster, 7, item => ReferenceEquals(item, second)));
        Assert.False(map.MapContain(MapType.Monster, 7, _ => false));
        Assert.Null(map.GetMapObject<ModelMapObj>(MapType.Monster, 99));
        Assert.Equal(0, map.GetMapObject<int>(MapType.Monster, 99));
        Assert.False(map.TryGetObject<ModelMapObj>(99, MapType.Monster, 18, 18, out var missing));
        Assert.Null(missing);
        Assert.Throws<InvalidCastException>(() => map.GetMapObject<OtherModelMapObj>(MapType.Monster, 7));
        Assert.Throws<InvalidCastException>(() => map.TryGetObject<OtherModelMapObj>(7, MapType.Monster, 18, 18, out _));
    }
}
