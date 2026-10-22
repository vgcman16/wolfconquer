using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using MapType = WolfConquer.Core.World.LegacyNativeMapObjectType;

namespace WolfConquer.Tests;

// Real map/view lookup components with MODEL IMapObj storage, not native Player spawn/admission.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeSpellRangeTests
{
    private const uint TargetUid = 71;

    [Fact]
    public void NullMapReturnsFalseAndNullOutWithoutAllocatingAViewEvenForInvalidType()
    {
        var client = new LegacyNativeGameClientRuntime();
        var view = client.Player.View;
        ILegacyNativeMapObj found = Model(TargetUid);

        Assert.False(view.TryGetValue(TargetUid, out found, (MapType)255));
        Assert.Null(found);
        Assert.False(client.IsInSpellRange(TargetUid, 255));
        Assert.Null(client.Map);
        Assert.Same(view, client.Player.View);
        Assert.Same(client, view.Owner);
    }

    [Fact]
    public void ExistingMapWithUnpublishedViewFaultsWithoutClearingThePriorOutReferenceOrRepairingIt()
    {
        var client = new LegacyNativeGameClientRuntime { Map = new LegacyNativeGameMap(1, 1, 1002) };
        var retained = Model(TargetUid);
        ILegacyNativeMapObj found = retained;

        Assert.Throws<NullReferenceException>(() => client.Player.View.TryGetValue(TargetUid, out found, MapType.Monster));
        Assert.Same(retained, found);
        Assert.Throws<NullReferenceException>(() => client.IsInSpellRange(TargetUid, 255));
        Assert.Null(client.Map.View);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(2)]
    public void EachSupportedBucketReturnsTheActualObjectWithoutVisibilityOrMetadataFiltering(int typeValue)
    {
        var type = (MapType)typeValue;
        var client = ClientWithMap();
        client.Player.UID = TargetUid; // CanSee would reject self; raw lookup must not.
        var item = Model(TargetUid, 35, 35, type);
        Assert.True(client.Map!.View!.EnterMap(item));

        Assert.True(client.Player.View.TryGetValue(TargetUid, out var found, type));
        Assert.Same(item, found);
        Assert.True(client.IsInSpellRange(TargetUid, 0));
        // MODEL Map/DynamicID/Alive/AllowDynamic getters throw if any filter reads them.
    }

    [Fact]
    public void FirstFoundMonsterThenPlayerThenSobNpcPriorityNeverFallsBackAfterOutOfRangeMatch()
    {
        var client = ClientWithMap();
        var map = client.Map!.View!;
        var monster = Model(TargetUid, 53, 35, MapType.Monster);
        var player = Model(TargetUid, 47, 35, MapType.Player);
        var sob = Model(TargetUid, 35, 35, MapType.SobNpc);
        Assert.True(map.EnterMap(sob));
        Assert.True(map.EnterMap(player));
        Assert.True(map.EnterMap(monster));

        Assert.False(client.IsInSpellRange(TargetUid, 0));
        Assert.True(client.IsInSpellRange(TargetUid, 18));
        Assert.True(map.LeaveMap(monster));
        Assert.False(client.IsInSpellRange(TargetUid, 0));
        Assert.True(client.IsInSpellRange(TargetUid, 12));
        Assert.True(map.LeaveMap(player));
        Assert.True(client.IsInSpellRange(TargetUid, 0));
        Assert.True(map.LeaveMap(sob));
        Assert.False(client.IsInSpellRange(TargetUid, 255));
    }

    [Fact]
    public void RangeDoesNotWidenTheNativeThreeByThreeBlockLookupWindow()
    {
        var client = ClientWithMap();
        var neighbor = Model(TargetUid, 53, 35);
        var nextBlock = Model(TargetUid + 1, 54, 35);
        Assert.True(client.Map!.View!.EnterMap(neighbor));
        Assert.True(client.Map.View.EnterMap(nextBlock));

        Assert.True(client.Player.View.TryGetValue(TargetUid, out var found, MapType.Monster));
        Assert.Same(neighbor, found);
        Assert.False(client.IsInSpellRange(TargetUid, 17));
        Assert.True(client.IsInSpellRange(TargetUid, 18));
        Assert.False(client.Player.View.TryGetValue(TargetUid + 1, out found, MapType.Monster));
        Assert.Null(found);
        Assert.False(client.IsInSpellRange(TargetUid + 1, 255));
    }

    [Fact]
    public void ForeignViewOwnerControlsSearchWhileCurrentQueryPlayerControlsDistanceAndBothReferencesStayLive()
    {
        var viewOwner = ClientWithMap();
        var originalViewPlayer = viewOwner.Player;
        var view = originalViewPlayer.View;
        var item = Model(TargetUid, 36, 35);
        Assert.True(viewOwner.Map!.View!.EnterMap(item));
        var query = new LegacyNativeGameClientRuntime();
        var originalQueryPlayer = query.Player;
        query.Player.X = query.Player.Y = 200;
        query.Player.View = view;

        Assert.True(query.IsInSpellRange(TargetUid, 165));
        Assert.False(query.IsInSpellRange(TargetUid, 164));
        Assert.Null(query.Map); // Lookup belongs to the foreign view owner, not this client.

        viewOwner.Player = new LegacyNativePlayerRuntime(viewOwner); // Current search origin becomes0,0.
        Assert.False(query.IsInSpellRange(TargetUid, 255));
        viewOwner.Player.X = viewOwner.Player.Y = 35;
        Assert.True(query.IsInSpellRange(TargetUid, 165));

        query.Player = new LegacyNativePlayerRuntime(query) { X = 36, Y = 35, View = view };
        Assert.True(query.IsInSpellRange(TargetUid, 0));
        Assert.NotSame(originalQueryPlayer, query.Player);
        Assert.NotSame(originalViewPlayer, viewOwner.Player);
        Assert.Same(viewOwner, view.Owner);
        Assert.True(view.TryGetValue(TargetUid, out var found, MapType.Monster));
        Assert.Same(item, found);
        viewOwner.Map = null;
        Assert.False(query.IsInSpellRange(TargetUid, 255));
    }

    [Fact]
    public void RawBucketStorageReturnsAnInterfaceAliasWithoutCheckingConcreteClassOrDeclaredObjectType()
    {
        var client = ClientWithMap();
        var item = new OtherModelMapObj { UID = TargetUid, X = 35, Y = 35, ObjType = MapType.Player };
        // Deliberate MODEL prestate in the actual native live list, as in ViewPtr tests.
        client.Map!.View!.m_setBlock[1, 1].GetObjects(MapType.Monster).Add(item);

        Assert.True(client.Player.View.TryGetValue(TargetUid, out var found, MapType.Monster));
        Assert.Same(item, found);
        Assert.IsType<OtherModelMapObj>(found);
        Assert.Equal(MapType.Player, found.ObjType);
        Assert.True(client.IsInSpellRange(TargetUid, 0));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(255)]
    public void InvalidBucketTypeFaultIsNotCaughtAsVisibilityFailure(int type)
    {
        var client = ClientWithMap();
        var retained = Model(TargetUid);
        ILegacyNativeMapObj found = retained;

        Assert.Throws<IndexOutOfRangeException>(() => client.Player.View.TryGetValue(TargetUid, out found, (MapType)type));

        Assert.Same(retained, found);
        Assert.NotNull(client.Map!.View);
    }

    [Fact]
    public void BrokenFirstNeighborhoodBlockFaultsWithoutSkippingToALaterMatch()
    {
        var client = ClientWithMap();
        var item = Model(TargetUid, 35, 35);
        Assert.True(client.Map!.View!.EnterMap(item));
        client.Map.View.m_setBlock[0, 0] = null!;
        ILegacyNativeMapObj found = item;

        Assert.Throws<NullReferenceException>(() => client.Player.View.TryGetValue(TargetUid, out found, MapType.Monster));
        Assert.Same(item, found);
        Assert.Throws<NullReferenceException>(() => client.IsInSpellRange(TargetUid, 255));
        Assert.Null(client.Map.View.m_setBlock[0, 0]);
    }

    [Fact]
    public void AValidLookupMissClearsOutAndUnsearchedNpcBucketNeverSuppliesSpellRange()
    {
        var client = ClientWithMap();
        var item = Model(TargetUid, 35, 35, MapType.Npc);
        Assert.True(client.Map!.View!.EnterMap(item));
        ILegacyNativeMapObj found = item;

        Assert.False(client.Player.View.TryGetValue(TargetUid, out found, MapType.Monster));
        Assert.Null(found);
        Assert.True(client.Player.View.TryGetValue(TargetUid, out found, MapType.Npc));
        Assert.Same(item, found);
        Assert.False(client.IsInSpellRange(TargetUid, 255));
    }

    [Theory]
    [InlineData(35, 35, 0, true)]
    [InlineData(36, 35, 0, false)]
    [InlineData(35, 36, 0, false)]
    [InlineData(36, 36, 1, true)]
    public void ZeroAndOneRangesUseInclusiveNativeChebyshevDistance(int x, int y, int range, bool expected)
    {
        var client = ClientWithMap();
        Assert.True(client.Map!.View!.EnterMap(Model(TargetUid, (ushort)x, (ushort)y)));
        Assert.Equal(expected, client.IsInSpellRange(TargetUid, (byte)range));
    }

    [Theory]
    [InlineData(255, 255, true)]
    [InlineData(256, 255, false)]
    [InlineData(255, 254, false)]
    public void MaximumByteRangeIsInclusiveAndNotClampedToViewThreshold(int x, int range, bool expected)
    {
        var query = ForeignOriginQuery((ushort)x, 0);
        Assert.Equal(expected, query.IsInSpellRange(TargetUid, (byte)range));
    }

    [Theory]
    [InlineData(32767, 0, 255, false)]
    [InlineData(32768, 0, 0, true)]
    [InlineData(65535, 65535, 0, true)]
    [InlineData(65535, 19, 18, false)]
    [InlineData(65535, 19, 19, true)]
    public void NativeSignedShortDifferencesAreNotReplacedWithNormalizedIntegerDistance(int x, int y, int range, bool expected)
    {
        var query = ForeignOriginQuery((ushort)x, (ushort)y);
        Assert.True(query.Player.View.TryGetValue(TargetUid, out _, MapType.Monster));
        Assert.Equal(expected, query.IsInSpellRange(TargetUid, (byte)range));
    }

    private static LegacyNativeGameClientRuntime ClientWithMap(int width = 90, int height = 90, ushort x = 35, ushort y = 35)
    {
        var client = new LegacyNativeGameClientRuntime
        {
            Map = new LegacyNativeGameMap(width, height, 1002) { View = new LegacyNativeMapView(width, height) }
        };
        client.Player.UID = 17;
        client.Player.X = x;
        client.Player.Y = y;
        return client;
    }

    private static LegacyNativeGameClientRuntime ForeignOriginQuery(ushort x, ushort y)
    {
        var viewOwner = ClientWithMap(1, 1, 0, 0);
        Assert.True(viewOwner.Map!.View!.EnterMap(Model(TargetUid, 0, 0)));
        var query = new LegacyNativeGameClientRuntime();
        query.Player.X = x;
        query.Player.Y = y;
        query.Player.View = viewOwner.Player.View;
        return query;
    }

    private static ModelMapObj Model(uint uid, ushort x = 35, ushort y = 35, MapType type = MapType.Monster) =>
        new() { UID = uid, X = x, Y = y, ObjType = type };

    private class ModelMapObj : ILegacyNativeMapObj
    {
        public uint UID { get; set; }
        public ushort X { get; set; }
        public ushort Y { get; set; }
        public MapType ObjType { get; set; }
        public uint IndexInScreen { get; set; }
        public bool AllowDynamic { get => throw Unused(); set => throw Unused(); }
        public uint Map => throw Unused();
        public uint DynamicID => throw Unused();
        public bool Alive => throw Unused();
        public bool IsTrap() => throw Unused();
        public LegacyNativePacket GetArray(LegacyNativePacket stream, bool View, bool st) => throw Unused();
        public void Send(LegacyNativePacket msg) => throw Unused();
        private static InvalidOperationException Unused() => new("MODEL visibility/packet/send path must not be used by raw lookup.");
    }

    private sealed class OtherModelMapObj : ModelMapObj { }
}
