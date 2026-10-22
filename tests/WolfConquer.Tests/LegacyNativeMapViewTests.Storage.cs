using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using MapType = WolfConquer.Core.World.LegacyNativeMapObjectType;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeMapViewTests
{
    [Fact]
    public void ObjectTypeKeepsExactByteOrdinalsIncludingNonBucketCountSentinel()
    {
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(MapType)));
        Assert.Equal(new[] { "Player", "Monster", "SobNpc", "StaticRole", "Item", "Npc", "PokerTable", "Count" },
            Enum.GetNames<MapType>());
        Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 }, Enum.GetValues<MapType>().Select(value => (byte)value));
    }

    [Fact]
    public void MapObjectInterfaceRetainsNativeWidthsSettersAndPacketContract()
    {
        var properties = typeof(ILegacyNativeMapObj).GetProperties();
        Assert.Equal(new[] { "AllowDynamic", "UID", "X", "Y", "Map", "DynamicID", "Alive", "IndexInScreen", "ObjType" },
            properties.Select(property => property.Name));
        Assert.Equal(new[] { typeof(bool), typeof(uint), typeof(ushort), typeof(ushort), typeof(uint), typeof(uint),
            typeof(bool), typeof(uint), typeof(MapType) }, properties.Select(property => property.PropertyType));
        Assert.Equal(new[] { "AllowDynamic", "IndexInScreen" },
            properties.Where(property => property.SetMethod is not null).Select(property => property.Name));
        var packet = typeof(ILegacyNativeMapObj).GetMethod("GetArray")!;
        Assert.Equal(typeof(LegacyNativePacket), packet.ReturnType);
        Assert.Equal(new[] { typeof(LegacyNativePacket), typeof(bool), typeof(bool) },
            packet.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(typeof(void), typeof(ILegacyNativeMapObj).GetMethod("Send")!.ReturnType);
        Assert.Equal(typeof(bool), typeof(ILegacyNativeMapObj).GetMethod("IsTrap")!.ReturnType);
    }

    [Fact]
    public void ViewPtrOwnsSevenDistinctLiveBucketLists()
    {
        var view = new LegacyNativeViewPtr();
        var other = new LegacyNativeViewPtr();
        var lists = Enumerable.Range(0, 7).Select(index => view.GetObjects((MapType)index)).ToArray();

        Assert.Equal(7, lists.Distinct().Count());
        for (var i = 0; i < 7; i++)
        {
            Assert.Empty(lists[i]);
            Assert.Same(lists[i], view.GetObjects((MapType)i));
            Assert.NotSame(lists[i], other.GetObjects((MapType)i));
        }
    }

    [Fact]
    public void ViewPtrPreservesDuplicateReferencesUidDuplicatesOrderAndListAliases()
    {
        var view = new LegacyNativeViewPtr();
        var first = Model(7);
        var second = Model(7);
        var list = view.GetObjects(MapType.Monster);
        view.AddObject(first);
        view.AddObject(second);
        view.AddObject(first);
        Assert.Equal(new ILegacyNativeMapObj[] { first, second, first }, list);

        view.RemoveObject(second); // Same UID is not an equality/removal key.
        Assert.Equal(new ILegacyNativeMapObj[] { first, first }, list);
        view.RemoveObject(first); // Only the first equal reference is removed.
        Assert.Same(first, Assert.Single(list));
        list.Add(second);
        Assert.Same(list, view.GetObjects(MapType.Monster));
        view.Clear(MapType.Monster);
        Assert.Empty(list);
        Assert.Same(list, view.GetObjects(MapType.Monster));
    }

    [Fact]
    public void ViewPtrLookupSkipsNullUsesFirstUidAndKeepsUncheckedGenericCasts()
    {
        var view = new LegacyNativeViewPtr();
        var first = Model(7);
        var second = Model(7);
        view.GetObjects(MapType.Monster).Add(null!);
        view.AddObject(first);
        view.AddObject(second);

        Assert.True(view.ContainObject(MapType.Monster, 7));
        Assert.True(view.TryGetObject<ModelMapObj>(MapType.Monster, 7, out var found));
        Assert.Same(first, found);
        Assert.False(view.ContainObject(MapType.Monster, 99));
        Assert.False(view.TryGetObject<ModelMapObj>(MapType.Monster, 99, out var missing));
        Assert.Null(missing);
        Assert.False(view.TryGetObject<int>(MapType.Monster, 99, out var missingValue));
        Assert.Equal(0, missingValue);
        Assert.Throws<InvalidCastException>(() => view.TryGetObject<OtherModelMapObj>(MapType.Monster, 7, out _));
        Assert.Throws<InvalidCastException>(() => view.TryGetObject<int>(MapType.Monster, 7, out _));
    }

    [Fact]
    public void ViewPtrDoesNotRepairCountSentinelOrOutOfRangeBucketTypes()
    {
        var view = new LegacyNativeViewPtr();
        var invalid = Model(7, type: MapType.Count);

        Assert.Throws<IndexOutOfRangeException>(() => view.AddObject(invalid));
        Assert.Throws<IndexOutOfRangeException>(() => view.RemoveObject(invalid));
        Assert.Throws<IndexOutOfRangeException>(() => view.GetObjects(MapType.Count));
        Assert.Throws<IndexOutOfRangeException>(() => view.Clear(MapType.Count));
        Assert.Throws<IndexOutOfRangeException>(() => view.ContainObject(MapType.Count, 7));
        Assert.Throws<IndexOutOfRangeException>(() => view.TryGetObject<ModelMapObj>((MapType)255, 7, out _));
    }
}
