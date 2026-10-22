using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Direct shared-helper/store/projector proof, not fake native Player adoption,
// refresh-entry-edge admission, map4006/SpawinEffect handling, or client proof.
public sealed class LegacyFloorItemVisibilityTests
{
    [Theory]
    [InlineData("0", 0u)]
    [InlineData("4294967295", uint.MaxValue)]
    public void CanonicalDynamicIdAcceptsEntireUInt32Range(string raw, uint expected)
    {
        var viewer = Viewer(attributes: Attributes(("DynamicID", raw)));

        Assert.True(LegacyFloorItemVisibility.TryReadDynamicId(viewer, out var actual));

        Assert.Equal(expected, actual);
        Assert.True(LegacyFloorItemVisibility.CanBroadcast(viewer, Entry(expected)));
        Assert.True(LegacyFloorItemVisibility.CanRefresh(viewer, Entry(expected)));
    }

    [Fact]
    public void MissingDynamicIdProjectsNativeBirthDefaultZeroWithoutMutatingAttributes()
    {
        var attributes = Attributes(("unrelated", "bad-value"));
        var viewer = Viewer(attributes: attributes);

        Assert.True(LegacyFloorItemVisibility.TryReadDynamicId(viewer, out var actual));

        Assert.Equal(0u, actual);
        Assert.True(LegacyFloorItemVisibility.CanBroadcast(viewer, Entry(0)));
        Assert.True(LegacyFloorItemVisibility.CanRefresh(viewer, Entry(0)));
        Assert.Same(attributes, viewer.Attributes);
        Assert.Single(attributes);
        Assert.False(attributes.ContainsKey("DynamicID"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-id")]
    [InlineData("-1")]
    [InlineData("4294967296")]
    [InlineData("1.0")]
    [InlineData("0x10")]
    public void MalformedNegativeOrOverflowingDynamicIdDoesNotFallBackToZero(string raw)
    {
        var viewer = Viewer(attributes: Attributes(("DynamicID", raw)));

        Assert.False(LegacyFloorItemVisibility.TryReadDynamicId(viewer, out _));

        Assert.False(LegacyFloorItemVisibility.CanBroadcast(viewer, Entry(0)));
        Assert.False(LegacyFloorItemVisibility.CanRefresh(viewer, Entry(0)));
        Assert.Equal(raw, viewer.Attributes["DynamicID"]);
    }

    [Fact]
    public void AllCaseAliasesAreReadEvenInCaseSensitiveDictionaryAndMustAgreeNumerically()
    {
        var attributes = Attributes(("DynamicID", "7"), ("DynamicId", "007"), ("dynamicid", "7"));
        var viewer = Viewer(attributes: attributes);

        Assert.True(LegacyFloorItemVisibility.TryReadDynamicId(viewer, out var actual));

        Assert.Equal(7u, actual);
        Assert.True(LegacyFloorItemVisibility.CanBroadcast(viewer, Entry(7)));
        Assert.True(LegacyFloorItemVisibility.CanRefresh(viewer, Entry(7)));
        Assert.Equal(3, attributes.Count);
    }

    [Fact]
    public void ConflictingOrMalformedCaseAliasesDenyRatherThanUsingFirstMatchingKey()
    {
        var conflict = Viewer(attributes: Attributes(("DynamicID", "0"), ("dynamicid", "7")));
        var malformed = Viewer(attributes: Attributes(("DynamicID", "0"), ("DYNAMICID", "bad")));
        foreach (var viewer in new[] { conflict, malformed })
        {
            Assert.False(LegacyFloorItemVisibility.TryReadDynamicId(viewer, out _));
            Assert.False(LegacyFloorItemVisibility.CanBroadcast(viewer, Entry(0)));
            Assert.False(LegacyFloorItemVisibility.CanRefresh(viewer, Entry(0)));
        }
    }

    [Theory]
    [InlineData(17, true)]
    [InlineData(18, false)]
    [InlineData(19, false)]
    public void OrdinaryBroadcastUsesChebyshevStrictlyLessThanEighteenIncludingOwner(int distance, bool expected)
    {
        var entry = Entry(7);
        var viewer = Viewer(x: 100 + distance, y: 100 + distance,
            attributes: Attributes(("DynamicID", "7")));
        Assert.Equal(viewer.Uid, entry.Packet.ItemOwnerUid);

        Assert.Equal(expected, LegacyFloorItemVisibility.CanBroadcast(viewer, entry));
    }

    [Theory]
    [InlineData(17, true)]
    [InlineData(18, true)]
    [InlineData(19, false)]
    public void RefreshRoleVisibilityUsesChebyshevInclusiveEighteen(int distance, bool expected)
    {
        var viewer = Viewer(x: 100 + distance, y: 100 + distance,
            attributes: Attributes(("DynamicID", "7")));

        Assert.Equal(expected, LegacyFloorItemVisibility.CanRefresh(viewer, Entry(7)));
    }

    [Theory]
    [InlineData(1002L, 0u, 0u, true)]
    [InlineData(1002L, uint.MaxValue, uint.MaxValue, true)]
    [InlineData(1002L, 7u, 0u, false)]
    [InlineData(1002L, 0u, 7u, false)]
    [InlineData(1015L, 7u, 7u, false)]
    public void BroadcastAndRefreshRequireBothPhysicalMapAndCapturedDynamicScope(
        long viewerMap, uint viewerDynamicId, uint floorDynamicId, bool expected)
    {
        var viewer = Viewer(map: viewerMap,
            attributes: Attributes(("DynamicID", viewerDynamicId.ToString(CultureInfo.InvariantCulture))));
        var entry = Entry(floorDynamicId);

        Assert.Equal(expected, LegacyFloorItemVisibility.CanBroadcast(viewer, entry));
        Assert.Equal(expected, LegacyFloorItemVisibility.CanRefresh(viewer, entry));
    }

    [Fact]
    public void RefreshRejectsFloorUidEqualToPlayerUidWhileBroadcastStillIncludesOwner()
    {
        var viewer = Viewer();
        var entry = Entry(0) with { Packet = Entry(0).Packet with { Uid = 77 } };

        Assert.True(LegacyFloorItemVisibility.CanBroadcast(viewer, entry));
        Assert.False(LegacyFloorItemVisibility.CanRefresh(viewer, entry));
    }

    [Fact]
    public void PlayerOnlyMap10428ExclusionIsNotAppliedToOrdinaryFloorItems()
    {
        var viewer = Viewer(map: 10428, attributes: Attributes(("DynamicID", "7")));
        var entry = Entry(7) with { MapId = 10428 };

        Assert.True(LegacyFloorItemVisibility.CanBroadcast(viewer, entry));
        Assert.True(LegacyFloorItemVisibility.CanRefresh(viewer, entry));
    }

    [Fact]
    public void CapturedScopeSurvivesRecordCopyAndActualGroundPlacementDespiteCurrentOwnerChange()
    {
        var original = Entry(7);
        var copy = original with { Packet = original.Packet with { X = 101 } };
        var oldOwner = Viewer(attributes: Attributes(("DynamicID", "7")));
        var currentOwner = oldOwner with { Attributes = Attributes(("DynamicID", "9")) };
        var store = new FloorItemRuntimeStateStore();
        store.AddVisible(Entry(9) with { Packet = Entry(9).Packet with { Uid = 901, X = 101 } }, DateTimeOffset.UnixEpoch);

        Assert.True(store.TryAddVisibleOnGround(copy, DateTimeOffset.UnixEpoch, 0, null, out var placed));

        Assert.Equal((uint?)7, original.DynamicId);
        Assert.Equal((uint?)7, copy.DynamicId);
        Assert.Equal((uint?)7, placed.DynamicId);
        Assert.Equal((100, 99), ((int)placed.Packet.X, (int)placed.Packet.Y));
        Assert.True(LegacyFloorItemVisibility.CanBroadcast(oldOwner, placed));
        Assert.True(LegacyFloorItemVisibility.CanRefresh(oldOwner, placed));
        Assert.False(LegacyFloorItemVisibility.CanBroadcast(currentOwner, placed));
        Assert.False(LegacyFloorItemVisibility.CanRefresh(currentOwner, placed));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(7u)]
    [InlineData(uint.MaxValue)]
    public void UntaggedLegacyEntryDoesNotAcquireCapturedScopeAuthority(uint viewerDynamicId)
    {
        var untagged = FloorItemRuntimeEntry.ForItem(1002, Packet());
        var viewer = Viewer(attributes: Attributes(("DynamicID", viewerDynamicId.ToString(CultureInfo.InvariantCulture))));

        Assert.Null(untagged.DynamicId);
        Assert.False(LegacyFloorItemVisibility.CanBroadcast(viewer, untagged));
        Assert.False(LegacyFloorItemVisibility.CanRefresh(viewer, untagged));
    }

    [Fact]
    public void VisibilityScopeDoesNotPartitionGroundOccupancyOrAddAStorePickupDeny()
    {
        var now = DateTimeOffset.UnixEpoch;
        var store = new FloorItemRuntimeStateStore();
        var existing = Entry(7);
        store.AddVisible(existing, now);
        var otherScope = Entry(9) with { Packet = Entry(9).Packet with { Uid = 901 } };
        Assert.False(store.TryAddVisibleOnGround(otherScope, now, 0,
            (_, x, y) => x == 100 && y == 100, out _));
        Assert.True(store.TryAddVisibleOnGround(otherScope, now, 0, null, out var scattered));
        Assert.Equal((99, 99), ((int)scattered.Packet.X, (int)scattered.Packet.Y));
        Assert.Equal((uint?)9, scattered.DynamicId);
        var wrongScopeOwner = Viewer(attributes: Attributes(("DynamicID", "9")));
        Assert.False(LegacyFloorItemVisibility.CanBroadcast(wrongScopeOwner, existing));

        Assert.Equal(FloorItemRuntimePickupStatus.Available,
            store.TryConsumePickupEntry(existing.Packet.Uid, 77, now, out var consumed));

        Assert.Same(existing, consumed);
        Assert.Same(scattered, Assert.Single(store.SnapshotVisible(now)));
    }

    [Fact]
    public void OrdinaryItemMoneyAndCpFactoriesKeepExplicitCapturedScopeWithoutChangingPacket()
    {
        var packet = Packet();
        FloorItemRuntimeEntry[] entries =
        [
            FloorItemRuntimeEntry.ForItem(1002, packet, dynamicId: uint.MaxValue),
            FloorItemRuntimeEntry.ForMoney(1002, packet, 31, dynamicId: uint.MaxValue),
            FloorItemRuntimeEntry.ForConquerPoints(1002, packet, 107, dynamicId: uint.MaxValue),
        ];

        foreach (var entry in entries)
        {
            Assert.Equal((uint?)uint.MaxValue, entry.DynamicId);
            Assert.Same(packet, entry.Packet);
            Assert.Equal((uint?)uint.MaxValue, (entry with { OwnerOnly = true }).DynamicId);
        }
    }

    [Theory]
    [InlineData(false, 0u)]
    [InlineData(false, uint.MaxValue)]
    [InlineData(true, 0u)]
    [InlineData(true, uint.MaxValue)]
    public void PureMonsterPlacementCarriesContextScopeOnRealRuntimeEntry(bool money, uint dynamicId)
    {
        var monster = new LegacyMonsterInstance(400001, 1002, 100, 100, 200, 1, 0, 10,
            1, "fixture", false, 1, 0, DynamicId: 11);
        var selection = new LegacyMonsterDeathDropSelection(
            money ? LegacyMonsterDeathDropKind.Money : LegacyMonsterDeathDropKind.Item,
            "supplied fixture selection", DropCount: 1,
            ItemId: money ? 1090001u : 1002000u, MoneyAmount: money ? 31u : 0u);

        var projection = LegacyMonsterDropPlacement.Project(monster, Viewer(), selection,
            new LegacyMonsterDropPlacementRolls(900),
            new LegacyMonsterDropPlacementContext { DynamicId = dynamicId, IsGroundValid = (_, _, _) => true });

        var decision = Assert.Single(projection.Placed);
        var entry = Assert.IsType<FloorItemRuntimeEntry>(decision.RuntimeEntry);
        Assert.Equal((uint?)dynamicId, entry.DynamicId);
        Assert.Equal(money ? FloorItemRuntimePickupKind.Money : FloorItemRuntimePickupKind.Item, entry.Kind);
        Assert.Equal(money ? 31 : 0, entry.Amount);
        Assert.Same(decision.Packet, entry.Packet);
        var matchingViewer = Viewer(attributes: Attributes(("DynamicID", dynamicId.ToString(CultureInfo.InvariantCulture))));
        Assert.True(LegacyFloorItemVisibility.CanBroadcast(matchingViewer, entry));
    }

    private static FloorItemRuntimeEntry Entry(uint dynamicId) =>
        FloorItemRuntimeEntry.ForItem(1002, Packet(), ownerOnly: true, dynamicId: dynamicId);

    private static LegacyFloorItemPacket Packet() =>
        new(900, 1002000, 100, 100, 40, LegacyFloorItemDropType.Visible,
            0, 0, 77, 0, 0, 0, 0, 0, "");

    private static Dictionary<string, string> Attributes(params (string Key, string Value)[] pairs)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in pairs) attributes.Add(pair.Key, pair.Value);
        return attributes;
    }

    private static CharacterSnapshot Viewer(
        long uid = 77, long map = 1002, long x = 100, long y = 100,
        IReadOnlyDictionary<string, string>? attributes = null) =>
        new(uid, "fixture", 1003, 0, 0, 15, 15, 15, 0, 1, 0,
            map, x, y, map, x, y, 0, 0, 0, 0, 0, 1, 0,
            0, 0, 0, 0, 0, 0, "fixture.ini",
            attributes ?? new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal));
}
