using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeRoleViewTests
{
    [Fact]
    public void CanSeeRejectsNullOtherMapsAndOwnUidWithoutRequiringPublishedClientMap()
    {
        var client = Client();
        var view = client.Player.View;
        var obj = Model(2);
        Assert.Null(client.Map);
        Assert.True(view.CanSee(obj));
        Assert.False(view.CanSee(null!));
        obj.Map = 1003;
        Assert.False(view.CanSee(obj));
        obj.Map = 1002;
        obj.UID = client.Player.UID;
        obj.ObjType = LegacyNativeMapObjectType.Monster;
        Assert.False(view.CanSee(obj));
    }

    [Fact]
    public void DynamicBypassBelongsToObservedObjectNotOwnerAndNeverBypassesMapIdentity()
    {
        var client = Client();
        var obj = Model(2);
        obj.DynamicID = 99;
        Assert.False(client.Player.View.CanSee(obj));
        client.Player.AllowDynamic = true;
        Assert.False(client.Player.View.CanSee(obj));
        obj.AllowDynamic = true;
        Assert.True(client.Player.View.CanSee(obj));
        obj.Map = 999;
        Assert.False(client.Player.View.CanSee(obj));
        obj.Map = 1002;
        obj.AllowDynamic = false;
        obj.DynamicID = client.Player.DynamicID;
        Assert.True(client.Player.View.CanSee(obj));
    }

    [Theory]
    [InlineData(118, 118, true)]
    [InlineData(82, 82, true)]
    [InlineData(119, 100, false)]
    [InlineData(100, 119, false)]
    [InlineData(81, 100, false)]
    public void CanSeeUsesInclusiveEighteenCellNativeChebyshevThreshold(int x, int y, bool visible)
    {
        var client = Client(100, 100);
        Assert.Equal(visible, client.Player.View.CanSee(Model(2, (ushort)x, (ushort)y)));
    }

    [Theory]
    [InlineData(32767, 0, false)]
    [InlineData(32768, 0, true)]
    [InlineData(65535, 65535, true)]
    [InlineData(65535, 19, false)]
    public void CanSeeRetainsSignedShortDistanceOverflowRatherThanNormalizingCoordinates(int x, int y, bool visible)
    {
        var client = Client(0, 0);
        Assert.Equal(visible, client.Player.View.CanSee(Model(2, (ushort)x, (ushort)y)));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void Map10428ExcludesOnlyPlayerObjects(int type, bool visible)
    {
        var client = Client();
        client.Player.Map = 10428;
        var obj = Model(2, type: (LegacyNativeMapObjectType)type);
        obj.Map = 10428;
        Assert.Equal(visible, client.Player.View.CanSee(obj));
    }

    [Fact]
    public void DeadObservedObjectsAreStillVisible()
    {
        var client = Client();
        var obj = Model(2);
        obj.Alive = false;
        Assert.True(client.Player.View.CanSee(obj));
    }

    [Fact]
    public void RolesWithNullMapIsEmptyAndDoesNotEvaluateCallerPredicate()
    {
        var client = Client();
        var calls = 0;
        Assert.Empty(client.Player.View.Roles(LegacyNativeMapObjectType.Player));
        Assert.Empty(client.Player.View.Roles(LegacyNativeMapObjectType.Monster, _ =>
        {
            calls++;
            throw new InvalidOperationException("No roles means no predicate call.");
        }));
        Assert.Equal(0, calls);
        Assert.Null(client.Map);
    }

    [Fact]
    public void RolesWithConstructedMapButUnpublishedViewFaultsInsteadOfInventingEmptyView()
    {
        var client = Client();
        client.Map = new LegacyNativeGameMap(72, 72, 1002);
        var calls = 0;

        Assert.Throws<NullReferenceException>(() => client.Player.View.Roles(LegacyNativeMapObjectType.Player).ToArray());
        Assert.Throws<NullReferenceException>(() => client.Player.View.Roles(LegacyNativeMapObjectType.Player, _ =>
        {
            calls++;
            return true;
        }).ToArray());
        Assert.Equal(0, calls);
        Assert.Null(client.Map.View);
    }

    [Fact]
    public void RolesUsesActualMapViewNotSrcCacheAndAppliesAlivePredicateOnlyAfterCanSee()
    {
        var client = Client();
        var mapView = PublishModelView(client);
        var dead = Model(2);
        dead.Alive = false;
        var wrongMap = Model(3);
        wrongMap.Map = 99;
        var live = Model(4, 20);
        var far = Model(5, 37);
        var self = Model(client.Player.UID);
        foreach (var obj in new[] { dead, wrongMap, live, far, self })
            Assert.True(mapView.EnterMap(obj));
        client.Player.View.src[(byte)LegacyNativeMapObjectType.Player].Add(999, Model(999));

        Assert.Equal(new ILegacyNativeMapObj[] { dead, live }, client.Player.View.Roles(LegacyNativeMapObjectType.Player));
        var aliveArguments = new List<bool>();
        var filtered = client.Player.View.Roles(LegacyNativeMapObjectType.Player, alive =>
        {
            aliveArguments.Add(alive);
            return !alive;
        }).ToArray();
        Assert.Same(dead, Assert.Single(filtered));
        Assert.Equal(new[] { false, true }, aliveArguments);
        Assert.Same(mapView, client.Map!.View);
    }

    [Fact]
    public void RolesPreservesLazyVisibilityAndDoesNotCatchCallerPredicateFaults()
    {
        var client = Client();
        var mapView = PublishModelView(client);
        var obj = Model(2);
        Assert.True(mapView.EnterMap(obj));
        var roles = client.Player.View.Roles(LegacyNativeMapObjectType.Player);
        obj.Map = 99;
        Assert.Empty(roles); // Visibility is evaluated during iteration, not Roles() creation.
        obj.Map = 1002;
        Assert.Same(obj, Assert.Single(roles));
        var fault = new InvalidOperationException("MODEL predicate fault.");
        Assert.Same(fault, Assert.Throws<InvalidOperationException>(() =>
            client.Player.View.Roles(LegacyNativeMapObjectType.Player, _ => throw fault).ToArray()));
    }
}
