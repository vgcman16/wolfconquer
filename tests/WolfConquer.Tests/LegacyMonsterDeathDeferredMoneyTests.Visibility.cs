using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMonsterDeathDeferredMoneyTests
{
    [Fact]
    public async Task ActualMoneyBirthCapturesTypedMonsterScopeAndBroadcastUsesStrictSeventeenCellView()
    {
        using var fixture = new Fixture(41, [50_000], monsterDynamicId: 77, killerDynamicId: 77);
        var near = AddVisibilityViewer(fixture, PlayerUid + 1, 27, 10, DynamicAttributes(77));
        var boundary = AddVisibilityViewer(fixture, PlayerUid + 2, 28, 10, DynamicAttributes(77));
        var otherInstance = AddVisibilityViewer(fixture, PlayerUid + 3, 11, 10, DynamicAttributes(88));
        var otherMap = AddVisibilityViewer(fixture, PlayerUid + 4, 11, 10, DynamicAttributes(77), mapId: 1003);

        await InvokeActualDropBodyAsync(fixture);

        var entry = Assert.Single(fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.Equal((uint?)77, entry.DynamicId);
        var origin = Assert.Single(FloorPackets(fixture));
        Assert.Equal(entry.Packet, origin);
        Assert.Equal(origin, Assert.Single(VisibilityPackets(near.Sent)));
        Assert.Empty(VisibilityPackets(boundary.Sent));
        Assert.Empty(VisibilityPackets(otherInstance.Sent));
        Assert.Empty(VisibilityPackets(otherMap.Sent));
    }

    [Theory]
    [InlineData(FloorItemRuntimePickupKind.Item)]
    [InlineData(FloorItemRuntimePickupKind.Money)]
    [InlineData(FloorItemRuntimePickupKind.ConquerPoints)]
    public async Task ActualTypedOrdinaryBroadcasterChecksOriginDistanceInsteadOfUnconditionallySending(
        FloorItemRuntimePickupKind kind)
    {
        using var fixture = new Fixture(40, [50_000], monsterDynamicId: 77, killerDynamicId: 77);
        var entry = VisibilityEntry(fixture, kind);
        fixture.Floor.AddVisible(entry);
        var origin = fixture.Session.Character with { X = 28, Y = 10 };
        var originSession = fixture.Session with { Character = origin };
        fixture.Sessions.Set(fixture.Context.Connection, originSession);
        var context = fixture.Context with { Session = originSession };
        var near = AddVisibilityViewer(fixture, PlayerUid + 1, 27, 10, DynamicAttributes(77));
        var boundary = AddVisibilityViewer(fixture, PlayerUid + 2, 28, 10, DynamicAttributes(77));

        await InvokeTypedFloorBroadcastAsync(context, origin, entry);

        Assert.Empty(FloorPackets(fixture));
        Assert.Equal(entry.Packet, Assert.Single(VisibilityPackets(near.Sent)));
        Assert.Empty(VisibilityPackets(boundary.Sent));
        Assert.Same(entry, Assert.Single(fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow)));
    }

    [Fact]
    public async Task ActualMoneyBroadcastUsesCapturedMonsterInstanceRatherThanKillerInstance()
    {
        using var fixture = new Fixture(41, [50_000], monsterDynamicId: 77, killerDynamicId: 88);
        var capturedInstance = AddVisibilityViewer(fixture, PlayerUid + 1, 11, 10, DynamicAttributes(77));
        var killerInstance = AddVisibilityViewer(fixture, PlayerUid + 2, 11, 10, DynamicAttributes(88));

        await InvokeActualDropBodyAsync(fixture);

        var entry = Assert.Single(fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.Equal((uint?)77, entry.DynamicId);
        Assert.Equal(1, fixture.Attributes.MoneyReads);
        Assert.Empty(FloorPackets(fixture));
        Assert.Empty(VisibilityPackets(killerInstance.Sent));
        Assert.Equal(entry.Packet, Assert.Single(VisibilityPackets(capturedInstance.Sent)));
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("overflow")]
    [InlineData("conflict")]
    public async Task ActualNormalMapBroadcastAllowsMissingZeroButRejectsPresentInvalidViewerScope(string scenario)
    {
        using var fixture = new Fixture(41, [50_000]);
        var missing = AddVisibilityViewer(fixture, PlayerUid + 1, 11, 10, new Dictionary<string, string>());
        var invalidAttributes = scenario switch
        {
            "malformed" => new Dictionary<string, string>(StringComparer.Ordinal) { ["DynamicID"] = "not-a-number" },
            "overflow" => new Dictionary<string, string>(StringComparer.Ordinal) { ["DynamicID"] = "4294967296" },
            "conflict" => new Dictionary<string, string>(StringComparer.Ordinal)
                { ["DynamicID"] = "0", ["DynamicId"] = "1" },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        var invalid = AddVisibilityViewer(fixture, PlayerUid + 2, 11, 10, invalidAttributes);

        await InvokeActualDropBodyAsync(fixture);

        var entry = Assert.Single(fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.Equal((uint?)0, entry.DynamicId);
        Assert.Equal(entry.Packet, Assert.Single(VisibilityPackets(missing.Sent)));
        Assert.Empty(VisibilityPackets(invalid.Sent));
    }

    [Theory]
    [InlineData(18, 19, true)]
    [InlineData(17, 18, false)]
    [InlineData(18, 18, false)]
    [InlineData(19, 20, false)]
    public async Task ActualMovementRefreshUsesInclusiveCurrentBoundaryAndGeometricPreviousHysteresis(
        int currentDistance, int previousDistance, bool expected)
    {
        using var fixture = new Fixture(40, [50_000]);
        var entry = VisibilityEntry(fixture, FloorItemRuntimePickupKind.Money);
        fixture.Floor.AddVisible(entry);
        var viewer = AddVisibilityViewer(fixture, PlayerUid + 1, 10 + currentDistance, 10, DynamicAttributes(77));
        // Native Player.InView's old-distance guard is geometric, not old map/DID eligibility.
        var previous = viewer.Session.Character with
        {
            X = 10 + previousDistance, MapId = 9999, Attributes = DynamicAttributes(88)
        };

        await InvokeFloorRefreshAsync(fixture, viewer, previous);

        var packets = VisibilityPackets(viewer.Sent);
        Assert.Equal(expected ? 1 : 0, packets.Count);
        if (expected) Assert.Equal(entry.Packet, Assert.Single(packets));
    }

    [Fact]
    public async Task ActualForcedRefreshStillExcludesFloorUidEqualToViewerUid()
    {
        using var fixture = new Fixture(40, [50_000]);
        var viewer = AddVisibilityViewer(fixture, PlayerUid + 1, 11, 10, DynamicAttributes(77));
        var entry = VisibilityEntry(fixture, FloorItemRuntimePickupKind.Money, uid: PlayerUid + 1);
        fixture.Floor.AddVisible(entry);

        await InvokeFloorRefreshAsync(fixture, viewer);

        Assert.Empty(VisibilityPackets(viewer.Sent));
        Assert.Same(entry, Assert.Single(fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow)));
    }

    [Fact]
    public async Task ActualOrdinaryRefreshReadsPhysicalSpawinEffectHolderAndNotSnapshotLookalike()
    {
        using var fixture = new Fixture(40, [50_000]);
        var native = new LegacyNativeGameClientRuntime();
        native.Player.SpawinEffect = true;
        var attributes = DynamicAttributes(77);
        attributes["SpawinEffect"] = "0";
        var viewer = AddVisibilityViewer(fixture, PlayerUid + 1, 11, 10, attributes, native: native);
        var entry = VisibilityEntry(fixture, FloorItemRuntimePickupKind.Money);
        fixture.Floor.AddVisible(entry);

        await InvokeFloorRefreshAsync(fixture, viewer);

        Assert.Empty(VisibilityPackets(viewer.Sent));
        native.Player.SpawinEffect = false;
        attributes["SpawinEffect"] = "1";
        await InvokeFloorRefreshAsync(fixture, viewer);
        Assert.Equal(entry.Packet, Assert.Single(VisibilityPackets(viewer.Sent)));
    }

    [Theory]
    [InlineData(810u, true)]
    [InlineData(1_001_000u, false)]
    public async Task ActualMap4006OrdinaryRefreshOnlyAllows810BeforePhysicalSpawnEffectGuard(uint itemId, bool expected)
    {
        using var fixture = new Fixture(40, [50_000], mapId: 4006);
        var native = new LegacyNativeGameClientRuntime();
        native.Player.SpawinEffect = true;
        var viewer = AddVisibilityViewer(fixture, PlayerUid + 1, 11, 10, DynamicAttributes(77), native: native);
        var entry = VisibilityEntry(fixture, FloorItemRuntimePickupKind.Item, itemId: itemId);
        fixture.Floor.AddVisible(entry);

        await InvokeFloorRefreshAsync(fixture, viewer);

        var packets = VisibilityPackets(viewer.Sent);
        Assert.Equal(expected ? 1 : 0, packets.Count);
        if (expected) Assert.Equal(entry.Packet, Assert.Single(packets));
    }

    [Fact]
    public async Task ActualRemovalHelperKeepsEscapedFloorScopeAfterStoreConsumptionByDifferentInstancePicker()
    {
        using var fixture = new Fixture(40, [50_000], monsterDynamicId: 77, killerDynamicId: 88);
        var entry = VisibilityEntry(fixture, FloorItemRuntimePickupKind.Money);
        fixture.Floor.AddVisible(entry);
        var scopedViewer = AddVisibilityViewer(fixture, PlayerUid + 1, 11, 10, DynamicAttributes(77));
        var pickerInstanceViewer = AddVisibilityViewer(fixture, PlayerUid + 2, 11, 10, DynamicAttributes(88));
        Assert.Equal(FloorItemRuntimePickupStatus.Available,
            fixture.Floor.TryConsumePickupEntry(entry.Packet.Uid, PlayerUid, DateTimeOffset.UtcNow, out var consumed));
        Assert.Same(entry, consumed);

        // Actual post-pickup removal helper, not full packet admission/persistence proof.
        await InvokeStaticFloorMethodAsync("SendLegacyFloorItemRemoveAsync",
            [typeof(WorldPacketHandlerContext), typeof(CharacterSnapshot), typeof(FloorItemRuntimeEntry), typeof(CancellationToken)],
            [fixture.Context, fixture.Session.Character, consumed, CancellationToken.None]);

        Assert.Empty(FloorPackets(fixture));
        Assert.Empty(VisibilityPackets(pickerInstanceViewer.Sent));
        Assert.Equal(entry.Packet with { DropType = LegacyFloorItemDropType.Remove },
            Assert.Single(VisibilityPackets(scopedViewer.Sent)));
        Assert.Empty(fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task ActualFrameHandlerExpiryBodyRemovesOnlyForCapturedInstanceViewers()
    {
        using var fixture = new Fixture(40, [50_000]);
        var handler = new WorldServerFrameHandler((CharacterSnapshotLoader?)null,
            ConquerProtocolProfile.VisibleDx8D13, fixture.Log);
        var sessions = ReadActualHandlerOwner<WorldSessionStore>(handler, "_sessions");
        var floor = ReadActualHandlerOwner<FloorItemRuntimeStateStore>(handler, "_floorItems");
        var scoped = AddVisibilityViewer(fixture, PlayerUid + 1, 27, 10, DynamicAttributes(77), sessions: sessions);
        var boundary = AddVisibilityViewer(fixture, PlayerUid + 2, 28, 10, DynamicAttributes(77), sessions: sessions);
        var other = AddVisibilityViewer(fixture, PlayerUid + 3, 11, 10, DynamicAttributes(88), sessions: sessions);
        var malformed = AddVisibilityViewer(fixture, PlayerUid + 4, 11, 10,
            new Dictionary<string, string> { ["DynamicID"] = "4294967296" }, sessions: sessions);
        var entry = VisibilityEntry(fixture, FloorItemRuntimePickupKind.Money);
        var now = DateTimeOffset.UtcNow;
        floor.AddVisible(entry, now.AddSeconds(-41));
        var method = typeof(WorldServerFrameHandler).GetMethod("SendDueFloorItemExpirationsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic, binder: null,
            [typeof(DateTimeOffset), typeof(CancellationToken)], modifiers: null);
        Assert.NotNull(method);

        // Execute the genuine expiry body at an explicit instant, not an invented timer or sleep.
        await Assert.IsType<ValueTask>(method.Invoke(handler, [now, CancellationToken.None]));

        Assert.Equal(entry.Packet with { DropType = LegacyFloorItemDropType.Remove },
            Assert.Single(VisibilityPackets(scoped.Sent)));
        Assert.Empty(VisibilityPackets(boundary.Sent));
        Assert.Empty(VisibilityPackets(other.Sent));
        Assert.Empty(VisibilityPackets(malformed.Sent));
        Assert.Empty(floor.SnapshotVisible(now));
        Assert.Contains("floor item expire remove itemCount=1 viewerCount=1", fixture.Log.ToString());
    }

    private static FloorItemRuntimeEntry VisibilityEntry(Fixture fixture, FloorItemRuntimePickupKind kind,
        uint uid = 90_000_001, uint itemId = 1_001_000)
    {
        var packet = LegacyFloorItemPacket.FromMonsterDrop(itemId, 10, 10, fixture.Session.Character, uid);
        return kind switch
        {
            FloorItemRuntimePickupKind.Item => FloorItemRuntimeEntry.ForItem(fixture.Monster.MapId, packet, dynamicId: 77),
            FloorItemRuntimePickupKind.Money => FloorItemRuntimeEntry.ForMoney(fixture.Monster.MapId, packet, 125, dynamicId: 77),
            FloorItemRuntimePickupKind.ConquerPoints => FloorItemRuntimeEntry.ForConquerPoints(fixture.Monster.MapId, packet, 10, dynamicId: 77),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static Dictionary<string, string> DynamicAttributes(uint dynamicId) => new(StringComparer.Ordinal)
    {
        ["DynamicID"] = dynamicId.ToString(CultureInfo.InvariantCulture)
    };

    private static VisibilityViewer AddVisibilityViewer(Fixture fixture, uint uid, long x, long y,
        IReadOnlyDictionary<string, string> attributes, long? mapId = null,
        LegacyNativeGameClientRuntime? native = null, WorldSessionStore? sessions = null)
    {
        var sent = new ConcurrentQueue<byte[]>();
        var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            (frame, _) => { sent.Enqueue(frame.ToArray()); return ValueTask.CompletedTask; }, nativeGameClient: native);
        var session = fixture.Session with
        {
            AccountId = uid, Username = "loot-viewer-" + uid,
            Character = fixture.Session.Character with
            {
                Uid = uid, Name = "LootViewer" + uid, MapId = mapId ?? fixture.Monster.MapId,
                X = x, Y = y, Attributes = attributes
            },
            NativeGameClient = native
        };
        (sessions ?? fixture.Sessions).Set(connection, session);
        return new VisibilityViewer(connection, session, sent);
    }

    private static List<LegacyFloorItemPacket> VisibilityPackets(ConcurrentQueue<byte[]> sent)
    {
        var result = new List<LegacyFloorItemPacket>();
        foreach (var bytes in sent.ToArray())
            if (LegacyFloorItemPacket.TryParse(ReadFrame(bytes), out var packet)) result.Add(packet);
        return result;
    }

    private static ValueTask InvokeTypedFloorBroadcastAsync(WorldPacketHandlerContext context,
        CharacterSnapshot origin, FloorItemRuntimeEntry entry) => InvokeStaticFloorMethodAsync("SendFloorItemVisibleAsync",
            [typeof(WorldPacketHandlerContext), typeof(CharacterSnapshot), typeof(FloorItemRuntimeEntry), typeof(CancellationToken)],
            [context, origin, entry, CancellationToken.None]);

    private static ValueTask InvokeFloorRefreshAsync(Fixture fixture, VisibilityViewer viewer,
        CharacterSnapshot? previous = null) => InvokeStaticFloorMethodAsync("SendVisibleFloorItemsAsync",
            [typeof(WorldPacketHandlerContext), typeof(CharacterSnapshot), typeof(CancellationToken), typeof(CharacterSnapshot)],
            [fixture.Context with { Connection = viewer.Connection, Session = viewer.Session },
                viewer.Session.Character, CancellationToken.None, previous]);

    private static async ValueTask InvokeStaticFloorMethodAsync(string name, Type[] signature, object?[] arguments)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic,
            binder: null, signature, modifiers: null);
        Assert.NotNull(method);
        await Assert.IsType<ValueTask>(method.Invoke(null, arguments));
    }

    private static T ReadActualHandlerOwner<T>(WorldServerFrameHandler handler, string fieldName)
    {
        var field = typeof(WorldServerFrameHandler).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field.GetValue(handler));
    }

    private sealed record VisibilityViewer(ConquerTcpConnection Connection, WorldSession Session, ConcurrentQueue<byte[]> Sent);
}
