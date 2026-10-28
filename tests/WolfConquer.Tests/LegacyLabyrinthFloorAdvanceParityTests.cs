using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyLabyrinthFloorAdvanceParityTests : IDisposable
{
    private const long AccountId = 1_293_600;
    private const uint CharacterUid = 1_293_600;
    private const uint ObserverUid = 1_293_601;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyLabyrinthFloorAdvanceParityTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<uint, uint, long, long, long> FloorAdvanceCases => new()
    {
        { 1_153, 721_537, 1_352, 27, 227 },
        { 1_154, 721_538, 1_353, 25, 268 },
        { 1_155, 721_539, 1_354, 5, 290 },
    };

    public static TheoryData<uint, bool> TokenGateCases => new()
    {
        { 1_153, false },
        { 1_153, true },
        { 1_154, false },
        { 1_154, true },
        { 1_155, false },
        { 1_155, true },
    };

    [Theory]
    [MemberData(nameof(FloorAdvanceCases))]
    public async Task Option2ConsumesExactlyOneTokenPersistsAndUsesSourcePacketOrder(
        uint npcId,
        uint tokenItemId,
        long destinationMapId,
        long destinationX,
        long destinationY)
    {
        var route = RouteFor(npcId);
        Assert.Equal(tokenItemId, route.TokenItemId);
        Assert.Equal(destinationMapId, route.DestinationMapId);
        Assert.Equal(destinationX, route.DestinationX);
        Assert.Equal(destinationY, route.DestinationY);

        var initialItems = Items(
            InventoryItem(200, tokenItemId),
            InventoryItem(300, 720_001, stackSize: 7),
            InventoryItem(100, tokenItemId));
        using var harness = CreateHarness(route, initialItems, includeObserver: true);

        var handled = await harness.InvokeAsync();

        Assert.True(handled);
        var updated = harness.CurrentSession;
        var expectedItems = RemoveInventoryUid(harness.OriginalSession.PlayerItems, uid: 100);
        Assert.NotSame(harness.OriginalSession, updated);
        Assert.Equal(harness.OriginalSession.PlayerItems.InventoryItems.Count - 1, updated.PlayerItems.InventoryItems.Count);
        Assert.Single(updated.PlayerItems.InventoryItems, item => item.ItemId == tokenItemId);
        Assert.Contains(updated.PlayerItems.InventoryItems, item => item.Uid == 200 && item.ItemId == tokenItemId);
        AssertItemsEqual(expectedItems, updated.PlayerItems);
        AssertCharacterState(
            harness.OriginalSession.Character,
            updated.Character,
            route.DestinationMapId,
            route.DestinationX,
            route.DestinationY,
            route.SourceMapId,
            route.SourceX,
            route.SourceY,
            dynamicId: "0",
            previousDynamicId: "77");
        Assert.Null(updated.ActiveNpcId);

        Assert.Equal(2, harness.SessionWriteCount);
        Assert.True(harness.SessionCommittedBeforeFirstSend);
        AssertItemsEqual(expectedItems, Assert.IsType<PlayerItemSnapshot>(harness.ItemsAtCommit));
        AssertCharacterState(
            harness.OriginalSession.Character,
            Assert.IsType<CharacterSnapshot>(harness.CharacterAtCommit),
            route.DestinationMapId,
            route.DestinationX,
            route.DestinationY,
            route.SourceMapId,
            route.SourceX,
            route.SourceY,
            dynamicId: "0",
            previousDynamicId: "77");
        AssertItemsEqual(expectedItems, harness.ReloadItems());
        AssertCharacterState(
            harness.OriginalSession.Character,
            harness.ReloadCharacter(),
            route.DestinationMapId,
            route.DestinationX,
            route.DestinationY,
            route.SourceMapId,
            route.SourceX,
            route.SourceY,
            dynamicId: "0",
            previousDynamicId: "77");

        AssertSourceOrderedSuccessPackets(
            harness,
            route,
            itemMutationAssertion: bytes => AssertRemoveInventory(bytes, uid: 100));
    }

    [Fact]
    public async Task Option2StackedTokenConsumesOneUnitPersistsRemainderAndUpdatesItemBeforeTeleport()
    {
        var route = RouteFor(1_153);
        var initialItems = Items(
            InventoryItem(100, route.TokenItemId, stackSize: 3),
            InventoryItem(300, 720_001, stackSize: 7));
        using var harness = CreateHarness(route, initialItems, includeObserver: true);

        var handled = await harness.InvokeAsync();

        Assert.True(handled);
        var updated = harness.CurrentSession;
        var expectedItems = SetInventoryStackSize(harness.OriginalSession.PlayerItems, uid: 100, stackSize: 2);
        var expectedToken = Assert.Single(expectedItems.InventoryItems, item => item.Uid == 100);
        Assert.Equal(harness.OriginalSession.PlayerItems.InventoryItems.Count, updated.PlayerItems.InventoryItems.Count);
        var remainingToken = Assert.Single(updated.PlayerItems.InventoryItems, item => item.Uid == 100);
        Assert.Equal(route.TokenItemId, remainingToken.ItemId);
        Assert.Equal((ushort)2, remainingToken.StackSize);
        AssertItemsEqual(expectedItems, updated.PlayerItems);
        AssertCharacterState(
            harness.OriginalSession.Character,
            updated.Character,
            route.DestinationMapId,
            route.DestinationX,
            route.DestinationY,
            route.SourceMapId,
            route.SourceX,
            route.SourceY,
            dynamicId: "0",
            previousDynamicId: "77");
        Assert.Null(updated.ActiveNpcId);

        Assert.Equal(2, harness.SessionWriteCount);
        AssertItemsEqual(expectedItems, Assert.IsType<PlayerItemSnapshot>(harness.ItemsAtCommit));
        AssertItemsEqual(expectedItems, harness.ReloadItems());
        Assert.Equal((ushort)2, Assert.Single(harness.ReloadItems().InventoryItems, item => item.Uid == 100).StackSize);
        Assert.DoesNotContain(harness.ClientSent, IsRemoveInventoryPacket);
        AssertSourceOrderedSuccessPackets(
            harness,
            route,
            itemMutationAssertion: bytes => AssertStackUpdate(bytes, expectedToken));
    }

    [Theory]
    [MemberData(nameof(TokenGateCases))]
    public async Task Option2MissingOrBoundOnlyTokenUsesExactSourceDialogWithoutMutation(
        uint npcId,
        bool boundOnly)
    {
        var route = RouteFor(npcId);
        var initialItems = boundOnly
            ? Items(
                InventoryItem(100, route.TokenItemId, bound: 1),
                InventoryItem(300, 720_001, stackSize: 7))
            : Items(InventoryItem(300, 720_001, stackSize: 7));
        using var harness = CreateHarness(route, initialItems, includeObserver: true);

        var handled = await harness.InvokeAsync();

        Assert.True(handled);
        Assert.Same(harness.OriginalSession, harness.CurrentSession);
        Assert.Equal(route.NpcId, harness.CurrentSession.ActiveNpcId);
        Assert.Equal(0, harness.SessionWriteCount);
        AssertExactMissingTokenDialog(harness.ClientSent, route);
        Assert.Empty(harness.ObserverSent);
        AssertNoSuccessPackets(harness.ClientSent, harness.ObserverSent);
        Assert.Equal(harness.OriginalCharacterBytes, File.ReadAllBytes(harness.CharacterPath));
        Assert.Equal(harness.OriginalItemBytes, File.ReadAllBytes(harness.ItemPath));
        AssertItemsEqual(harness.OriginalSession.PlayerItems, harness.ReloadItems());
        AssertCharacterState(
            harness.OriginalSession.Character,
            harness.ReloadCharacter(),
            route.SourceMapId,
            route.SourceX,
            route.SourceY,
            harness.OriginalSession.Character.PreviousMapId,
            harness.OriginalSession.Character.PreviousMapX,
            harness.OriginalSession.Character.PreviousMapY,
            dynamicId: "77");
    }

    [Theory]
    [MemberData(nameof(FloorAdvanceCases))]
    public async Task Option2UnboundEligibilityStillRemovesLowestUidMatchingToken(
        uint npcId,
        uint tokenItemId,
        long destinationMapId,
        long destinationX,
        long destinationY)
    {
        var route = RouteFor(npcId);
        var initialItems = Items(
            InventoryItem(20, tokenItemId, bound: 0),
            InventoryItem(10, tokenItemId, bound: 1),
            InventoryItem(30, 720_001));
        using var harness = CreateHarness(route, initialItems, includeObserver: false);

        var handled = await harness.InvokeAsync();

        Assert.True(handled);
        Assert.Equal(destinationMapId, harness.CurrentSession.Character.MapId);
        Assert.Equal(destinationX, harness.CurrentSession.Character.X);
        Assert.Equal(destinationY, harness.CurrentSession.Character.Y);
        Assert.Null(harness.CurrentSession.ActiveNpcId);
        Assert.DoesNotContain(harness.CurrentSession.PlayerItems.InventoryItems, item => item.Uid == 10);
        var eligibleToken = Assert.Single(
            harness.CurrentSession.PlayerItems.InventoryItems,
            item => item.ItemId == tokenItemId);
        Assert.Equal(20U, eligibleToken.Uid);
        Assert.Equal((byte)0, eligibleToken.Bound);

        var expectedItems = RemoveInventoryUid(harness.OriginalSession.PlayerItems, uid: 10);
        AssertItemsEqual(expectedItems, harness.CurrentSession.PlayerItems);
        AssertItemsEqual(expectedItems, harness.ReloadItems());
        Assert.Equal(2, harness.SessionWriteCount);
        var removal = Assert.Single(harness.ClientSent, IsRemoveInventoryPacket);
        AssertRemoveInventory(removal, uid: 10);
        Assert.DoesNotContain(harness.ClientSent, IsNpcReplyPacket);
    }

    [Fact]
    public async Task CharacterPersistenceFailureRollsBackItemSidecarAndSendsNoSuccessPackets()
    {
        var route = RouteFor(1_153);
        using var harness = CreateHarness(
            route,
            Items(InventoryItem(100, route.TokenItemId), InventoryItem(300, 720_001)),
            includeObserver: true);
        using var locked = new FileStream(
            harness.CharacterPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        var handled = await harness.InvokeAsync();

        Assert.True(handled);
        AssertFailureIsAtomic(harness, route, expectedReason: "character-persistence-");
    }

    [Fact]
    public async Task ItemPersistenceFailureLeavesCharacterFileUntouchedAndSendsNoSuccessPackets()
    {
        var route = RouteFor(1_153);
        using var harness = CreateHarness(
            route,
            Items(InventoryItem(100, route.TokenItemId), InventoryItem(300, 720_001)),
            includeObserver: true);
        using var locked = new FileStream(
            harness.ItemPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        var handled = await harness.InvokeAsync();

        Assert.True(handled);
        AssertFailureIsAtomic(harness, route, expectedReason: "item-persistence-");
    }

    [Fact]
    public async Task Option2ReplayIsFencedByClearedNpcAndStaleSessionCannotAdvanceTwice()
    {
        var route = RouteFor(1_153);
        using var harness = CreateHarness(
            route,
            Items(
                InventoryItem(10, route.TokenItemId),
                InventoryItem(20, route.TokenItemId),
                InventoryItem(30, 720_001)),
            includeObserver: true);

        Assert.True(await harness.InvokeAsync());
        var committedSessionWriteCount = harness.SessionWriteCount;
        var clientFrameCount = harness.ClientSent.Count;
        var observerFrameCount = harness.ObserverSent.Count;
        var wireFrameCount = harness.Wire.Count;
        var committedCharacterBytes = File.ReadAllBytes(harness.CharacterPath);
        var committedItemBytes = File.ReadAllBytes(harness.ItemPath);

        Assert.True(await harness.InvokeAsync());
        Assert.True(await harness.InvokeAsync(harness.OriginalSession));

        Assert.Equal(2, committedSessionWriteCount);
        Assert.Equal(committedSessionWriteCount, harness.SessionWriteCount);
        Assert.Equal(clientFrameCount, harness.ClientSent.Count);
        Assert.Equal(observerFrameCount, harness.ObserverSent.Count);
        Assert.Equal(wireFrameCount, harness.Wire.Count);
        Assert.Equal(committedCharacterBytes, File.ReadAllBytes(harness.CharacterPath));
        Assert.Equal(committedItemBytes, File.ReadAllBytes(harness.ItemPath));
        Assert.Null(harness.CurrentSession.ActiveNpcId);
        Assert.Equal(route.DestinationMapId, harness.CurrentSession.Character.MapId);
        var remainingToken = Assert.Single(
            harness.CurrentSession.PlayerItems.InventoryItems,
            item => item.ItemId == route.TokenItemId);
        Assert.Equal(20U, remainingToken.Uid);
        Assert.Equal(1, harness.ClientSent.Count(IsRemoveInventoryPacket));
        Assert.Equal(1, harness.ClientSent.Count(IsTeleportPacket));
        Assert.Single(harness.ObserverSent, IsRemoveEntityPacket);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private AdvanceHarness CreateHarness(
        FloorAdvanceRoute route,
        PlayerItemSnapshot initialItems,
        bool includeObserver)
    {
        var caseRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(caseRoot);
        return new AdvanceHarness(caseRoot, route, initialItems, includeObserver);
    }

    private static void AssertSourceOrderedSuccessPackets(
        AdvanceHarness harness,
        FloorAdvanceRoute route,
        Action<byte[]> itemMutationAssertion)
    {
        Assert.Equal(6, harness.ClientSent.Count);
        Assert.Single(harness.ObserverSent);
        Assert.Equal(7, harness.Wire.Count);

        Assert.Equal(WireEndpoint.Client, harness.Wire[0].Endpoint);
        itemMutationAssertion(harness.Wire[0].Bytes);

        Assert.Equal(WireEndpoint.Observer, harness.Wire[1].Endpoint);
        Assert.True(LegacyRemoveEntityPacket.TryParse(ReadFrame(harness.Wire[1].Bytes), out var leave));
        Assert.Equal(CharacterUid, leave.Uid);

        for (var index = 1; index < harness.ClientSent.Count; index++)
        {
            Assert.Equal(WireEndpoint.Client, harness.Wire[index + 1].Endpoint);
            Assert.Equal(harness.ClientSent[index], harness.Wire[index + 1].Bytes);
        }

        AssertTeleportTransition(harness.ClientSent.Skip(1).ToArray(), route);
        Assert.DoesNotContain(harness.ClientSent, IsNpcReplyPacket);
        Assert.DoesNotContain(harness.ClientSent, bytes =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out _));
    }

    private static void AssertTeleportTransition(
        IReadOnlyList<byte[]> sent,
        FloorAdvanceRoute route)
    {
        Assert.Equal(5, sent.Count);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[0]), out var teleport));
        Assert.Equal(CharacterUid, teleport.ObjId);
        Assert.Equal(MsgActionType.Teleport, teleport.Type);
        Assert.Equal(checked((uint)route.DestinationMapId), teleport.DwParam);
        Assert.Equal(route.DestinationMapId, teleport.DwParam3);
        Assert.Equal(checked((uint)route.DestinationX), teleport.PositionX);
        Assert.Equal(checked((uint)route.DestinationY), teleport.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[1]), out var startup));
        Assert.Equal(CharacterUid, startup.ObjId);
        Assert.Equal(MsgActionType.LegacyMerchantStartup, startup.Type);
        Assert.Equal(2U, startup.DwParam);
        Assert.Equal(route.DestinationMapId, startup.DwParam3);
        Assert.Equal(checked((uint)route.DestinationX), startup.PositionX);
        Assert.Equal(checked((uint)route.DestinationY), startup.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[2]), out var stopVending));
        Assert.Equal(CharacterUid, stopVending.ObjId);
        Assert.Equal(MsgActionType.StopVending, stopVending.Type);
        Assert.Equal(checked((uint)route.DestinationMapId), stopVending.DwParam);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[3]), out var color));
        Assert.Equal(CharacterUid, color.ObjId);
        Assert.Equal(MsgActionType.LegacyColor, color.Type);
        Assert.Equal(checked((uint)route.DestinationX), color.PositionX);
        Assert.Equal(checked((uint)route.DestinationY), color.PositionY);

        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(sent[4]), out var mapStatus));
        Assert.Equal(LegacyMapStatusPacket.FromMap(route.DestinationMapId, mapsById: null), mapStatus);
    }

    private static void AssertExactMissingTokenDialog(
        IReadOnlyList<byte[]> sent,
        FloorAdvanceRoute route)
    {
        Assert.Equal(4, sent.Count);

        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal(route.MissingTokenConfirmText, dialog.Text);

        var option = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, option.OptionId);
        Assert.Equal(route.MissingTokenConfirmOptionText, option.Text);

        var avatar = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal(route.MissingTokenConfirmAvatarId, avatar.InputMaxLength);

        AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Finish);
    }

    private static void AssertFailureIsAtomic(
        AdvanceHarness harness,
        FloorAdvanceRoute route,
        string expectedReason)
    {
        Assert.Same(harness.OriginalSession, harness.CurrentSession);
        Assert.Equal(route.NpcId, harness.CurrentSession.ActiveNpcId);
        Assert.Equal(0, harness.SessionWriteCount);
        Assert.Equal(harness.OriginalCharacterBytes, File.ReadAllBytes(harness.CharacterPath));
        Assert.Equal(harness.OriginalItemBytes, File.ReadAllBytes(harness.ItemPath));
        AssertItemsEqual(harness.OriginalSession.PlayerItems, harness.ReloadItems());
        AssertCharacterState(
            harness.OriginalSession.Character,
            harness.ReloadCharacter(),
            route.SourceMapId,
            route.SourceX,
            route.SourceY,
            harness.OriginalSession.Character.PreviousMapId,
            harness.OriginalSession.Character.PreviousMapX,
            harness.OriginalSession.Character.PreviousMapY,
            dynamicId: "77");
        AssertNoSuccessPackets(harness.ClientSent, harness.ObserverSent);
        Assert.Empty(harness.ObserverSent);
        Assert.Contains(expectedReason, harness.LogText, StringComparison.Ordinal);
    }

    private static void AssertNoSuccessPackets(
        IEnumerable<byte[]> clientSent,
        IEnumerable<byte[]> observerSent)
    {
        Assert.DoesNotContain(clientSent, IsRemoveInventoryPacket);
        Assert.DoesNotContain(clientSent, IsGameItemPacket);
        Assert.DoesNotContain(clientSent, bytes => MsgActionPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(clientSent, bytes => LegacyMapStatusPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(observerSent, IsRemoveEntityPacket);
    }

    private static void AssertCharacterState(
        CharacterSnapshot source,
        CharacterSnapshot actual,
        long mapId,
        long x,
        long y,
        long previousMapId,
        long previousMapX,
        long previousMapY,
        string dynamicId,
        string? previousDynamicId = null)
    {
        Assert.Equal(source.Uid, actual.Uid);
        Assert.Equal(source.Name, actual.Name);
        Assert.Equal(source.Body, actual.Body);
        Assert.Equal(source.Face, actual.Face);
        Assert.Equal(source.Hair, actual.Hair);
        Assert.Equal(source.Class, actual.Class);
        Assert.Equal(source.FirstClass, actual.FirstClass);
        Assert.Equal(source.SecondClass, actual.SecondClass);
        Assert.Equal(source.Reborn, actual.Reborn);
        Assert.Equal(source.Level, actual.Level);
        Assert.Equal(source.Experience, actual.Experience);
        Assert.Equal(mapId, actual.MapId);
        Assert.Equal(x, actual.X);
        Assert.Equal(y, actual.Y);
        Assert.Equal(previousMapId, actual.PreviousMapId);
        Assert.Equal(previousMapX, actual.PreviousMapX);
        Assert.Equal(previousMapY, actual.PreviousMapY);
        Assert.Equal(source.Strength, actual.Strength);
        Assert.Equal(source.Agility, actual.Agility);
        Assert.Equal(source.Vitality, actual.Vitality);
        Assert.Equal(source.Spirit, actual.Spirit);
        Assert.Equal(source.AttributePoints, actual.AttributePoints);
        Assert.Equal(source.MinHitPoints, actual.MinHitPoints);
        Assert.Equal(source.MinMana, actual.MinMana);
        Assert.Equal(source.MaxMana, actual.MaxMana);
        Assert.Equal(source.Money, actual.Money);
        Assert.Equal(source.ConquerPoints, actual.ConquerPoints);
        Assert.Equal(source.BoundConquerPoints, actual.BoundConquerPoints);
        Assert.Equal(source.BankConquerPoints, actual.BankConquerPoints);
        Assert.Equal(source.PkPoints, actual.PkPoints);
        Assert.Equal(source.Online, actual.Online);
        Assert.Equal(source.ExpBallUsed, actual.ExpBallUsed);
        Assert.Equal(source.SourceFile, actual.SourceFile);
        Assert.Equal(dynamicId, actual.Attributes["DynamicID"]);
        if (previousDynamicId is not null)
        {
            Assert.Equal(previousDynamicId, actual.Attributes["PDinamycID"]);
        }

        var mutableKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Map",
            "X",
            "Y",
            "PMap",
            "PMapX",
            "PMapY",
            "DynamicID",
        };
        if (previousDynamicId is not null)
        {
            mutableKeys.Add("PDinamycID");
        }
        var sourceAttributes = source.Attributes
            .Where(pair => !mutableKeys.Contains(pair.Key))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => pair.Key + "=" + pair.Value)
            .ToArray();
        var actualAttributes = actual.Attributes
            .Where(pair => !mutableKeys.Contains(pair.Key))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => pair.Key + "=" + pair.Value)
            .ToArray();
        Assert.Equal(sourceAttributes, actualAttributes);
        Assert.Equal(
            source.PaidNobility.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase),
            actual.PaidNobility.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase));
    }

    private static void AssertItemsEqual(PlayerItemSnapshot expected, PlayerItemSnapshot actual)
    {
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            PlayerItemSnapshotLoader.ToLegacyBytes(actual));
    }

    private static PlayerItemSnapshot RemoveInventoryUid(PlayerItemSnapshot items, uint uid)
    {
        return items with
        {
            InventoryItems = items.InventoryItems.Where(item => item.Uid != uid).ToArray(),
        };
    }

    private static PlayerItemSnapshot SetInventoryStackSize(
        PlayerItemSnapshot items,
        uint uid,
        ushort stackSize)
    {
        return items with
        {
            InventoryItems = items.InventoryItems
                .Select(item => item.Uid == uid ? item.WithStackSize(stackSize) : item)
                .ToArray(),
        };
    }

    private static PlayerItemSnapshot Items(params PlayerItemSnapshotItem[] inventoryItems)
    {
        return new PlayerItemSnapshot(inventoryItems, [], [], [], [], []);
    }

    private static PlayerItemSnapshotItem InventoryItem(
        uint uid,
        uint itemId,
        ushort stackSize = 1,
        byte bound = 0)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = 77,
            MaximumDurability = 99,
            Position = 0,
            StackSize = stackSize,
            Bound = bound,
            Color = 5,
            Plus = 2,
            Bless = 1,
            SocketProgress = 123,
        };
    }

    private static LegacyNpcReplyPacket AssertNpcReply(
        byte[] bytes,
        LegacyNpcReplyInteractType interactType)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(interactType, packet.InteractType);
        return packet;
    }

    private static void AssertRemoveInventory(byte[] bytes, uint uid)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(MsgItemUsageAction.RemoveInventory, packet.Action);
        Assert.Equal(uid, packet.Id);
    }

    private static void AssertStackUpdate(byte[] bytes, PlayerItemSnapshotItem expectedItem)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(
            LegacyGameItemPacket.FromSnapshotItem(
                expectedItem,
                mode: LegacyGameItemPacket.UpdateItemMode),
            packet);
    }

    private static bool IsRemoveInventoryPacket(byte[] bytes)
    {
        return MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Action == MsgItemUsageAction.RemoveInventory;
    }

    private static bool IsGameItemPacket(byte[] bytes)
    {
        return LegacyGameItemPacket.TryParse(ReadFrame(bytes), out _);
    }

    private static bool IsTeleportPacket(byte[] bytes)
    {
        return MsgActionPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Type == MsgActionType.Teleport;
    }

    private static bool IsRemoveEntityPacket(byte[] bytes)
    {
        return LegacyRemoveEntityPacket.TryParse(ReadFrame(bytes), out _);
    }

    private static bool IsNpcReplyPacket(byte[] bytes)
    {
        return LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out _);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(
            new ReadOnlySequence<byte>(bytes),
            out var frame,
            out _,
            out _));
        return frame;
    }

    private static FloorAdvanceRoute RouteFor(uint npcId)
    {
        return npcId switch
        {
            1_153 => new FloorAdvanceRoute(
                NpcId: 1_153,
                TokenItemId: 721_537,
                SourceMapId: 1_351,
                SourceX: 18,
                SourceY: 127,
                DestinationMapId: 1_352,
                DestinationX: 27,
                DestinationY: 227,
                MissingTokenConfirmText: "You have no Sky Token. Hurry to loot one from the Gold Ghosts.",
                MissingTokenConfirmOptionText: "I~will~stay~here.",
                MissingTokenConfirmAvatarId: 31),
            1_154 => new FloorAdvanceRoute(
                NpcId: 1_154,
                TokenItemId: 721_538,
                SourceMapId: 1_352,
                SourceX: 27,
                SourceY: 227,
                DestinationMapId: 1_353,
                DestinationX: 25,
                DestinationY: 268,
                MissingTokenConfirmText: "You don't have an EarthToken. I cannot teleport you to the next floor.",
                MissingTokenConfirmOptionText: "I~will~stay~here.",
                MissingTokenConfirmAvatarId: 31),
            1_155 => new FloorAdvanceRoute(
                NpcId: 1_155,
                TokenItemId: 721_539,
                SourceMapId: 1_353,
                SourceX: 25,
                SourceY: 268,
                DestinationMapId: 1_354,
                DestinationX: 5,
                DestinationY: 290,
                MissingTokenConfirmText: "Sorry, you don't have 1 SoulToken.",
                MissingTokenConfirmOptionText: "Sigh...",
                MissingTokenConfirmAvatarId: 63),
            _ => throw new ArgumentOutOfRangeException(nameof(npcId), npcId, "Unknown Labyrinth floor NPC."),
        };
    }

    private sealed class AdvanceHarness : IDisposable
    {
        private readonly FloorAdvanceRoute _route;
        private readonly ConquerTcpConnection _connection;
        private readonly ConquerTcpConnection? _observerConnection;
        private readonly StringWriter _log = new();
        private bool? _sessionCommittedBeforeFirstSend;
        public bool FailObserver;
        public WorldSession? PublishedAtFailure;

        public AdvanceHarness(
            string caseRoot,
            FloorAdvanceRoute route,
            PlayerItemSnapshot initialItems,
            bool includeObserver)
        {
            _route = route;
            UsersRoot = Path.Combine(caseRoot, "Users");
            PlayerItemsRoot = Path.Combine(caseRoot, "PlayersItems");
            Directory.CreateDirectory(UsersRoot);
            CharacterPath = Path.Combine(UsersRoot, AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
            ItemPath = Path.Combine(PlayerItemsRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
            WriteCharacter(CharacterPath, route);
            var itemLoader = new PlayerItemSnapshotLoader(PlayerItemsRoot);
            itemLoader.SaveByCharacterUid(CharacterUid, initialItems);

            var character = ReloadCharacter();
            var normalizedItems = ReloadItems();
            OriginalSession = new WorldSession(
                AccountId,
                Username: "labyrinth-floor-advance-test",
                Character: character,
                PlayerItems: normalizedItems,
                PlayerSpells: PlayerSpellSnapshot.Empty,
                PlayerProficiencies: PlayerProficiencySnapshot.Empty,
                UsersRoot: UsersRoot,
                ActiveNpcId: route.NpcId);
            OriginalCharacterBytes = File.ReadAllBytes(CharacterPath);
            OriginalItemBytes = File.ReadAllBytes(ItemPath);

            ConquerTcpConnection? connection = null;
            Sessions = new WorldSessionStore((changedConnection, previous, current) =>
            {
                if (!ReferenceEquals(changedConnection, connection) ||
                    previous is null ||
                    ReferenceEquals(previous, current))
                {
                    return;
                }

                if (FailObserver && current.ActiveNpcId is null)
                {
                    FailObserver = false; PublishedAtFailure = current;
                    throw new IOException("Actual floor observer failed after publication.");
                }
                SessionWriteCount++;
                ItemsAtCommit = ReloadItems();
                CharacterAtCommit = ReloadCharacter();
            });
            connection = new ConquerTcpConnection(
                null,
                new IPEndPoint(IPAddress.Loopback, 9_958),
                (frame, _) =>
                {
                    _sessionCommittedBeforeFirstSend ??=
                        Sessions.TryGet(connection!, out var current) &&
                        !ReferenceEquals(current, OriginalSession);
                    var bytes = frame.ToArray();
                    ClientSent.Add(bytes);
                    Wire.Add(new WireExposure(WireEndpoint.Client, bytes));
                    return ValueTask.CompletedTask;
                });
            _connection = connection;
            Sessions.Set(_connection, OriginalSession);

            if (includeObserver)
            {
                var observerConnection = new ConquerTcpConnection(
                    null,
                    new IPEndPoint(IPAddress.Loopback, 9_959),
                    (frame, _) =>
                    {
                        var bytes = frame.ToArray();
                        ObserverSent.Add(bytes);
                        Wire.Add(new WireExposure(WireEndpoint.Observer, bytes));
                        return ValueTask.CompletedTask;
                    });
                _observerConnection = observerConnection;
                Sessions.Set(observerConnection, CreateObserverSession(character, route));
            }

            var recovery = new LegacyTradeRecoveryState(UsersRoot, PlayerItemsRoot);
            Assert.True(recovery.TryEnsureInitialized(out var recoveryReason), recoveryReason);
            Context = new WorldPacketHandlerContext(
                _connection,
                _log,
                Session: OriginalSession,
                Sessions: Sessions,
                PlayerItemsRoot: PlayerItemsRoot,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13) { TradeRecovery = recovery };
        }

        public string UsersRoot { get; }

        public string PlayerItemsRoot { get; }

        public string CharacterPath { get; }

        public string ItemPath { get; }

        public byte[] OriginalCharacterBytes { get; }

        public byte[] OriginalItemBytes { get; }

        public WorldSession OriginalSession { get; }

        public WorldSessionStore Sessions { get; }

        public WorldPacketHandlerContext Context { get; }

        public List<byte[]> ClientSent { get; } = [];

        public List<byte[]> ObserverSent { get; } = [];

        public List<WireExposure> Wire { get; } = [];

        public int SessionWriteCount { get; private set; }

        public PlayerItemSnapshot? ItemsAtCommit { get; private set; }

        public CharacterSnapshot? CharacterAtCommit { get; private set; }

        public bool SessionCommittedBeforeFirstSend => _sessionCommittedBeforeFirstSend == true;

        public string LogText => _log.ToString();

        public bool HasCurrentSession => Sessions.TryGet(_connection, out _);

        public WorldSession CurrentSession
        {
            get
            {
                Assert.True(Sessions.TryGet(_connection, out var session));
                return Assert.IsType<WorldSession>(session);
            }
        }

        public void SetBeforeAction(WorldSession replacement) => Sessions.Set(_connection, replacement);

        public ValueTask<bool> InvokeAsync(WorldSession? suppliedSession = null)
        {
            var session = suppliedSession ?? CurrentSession;
            return WorldPacketHandlers.TryHandleLegacyLabyrinthNpcAsync(
                Context with { Session = session },
                session,
                _route.NpcId,
                optionId: 2,
                CancellationToken.None);
        }

        public PlayerItemSnapshot ReloadItems()
        {
            return new PlayerItemSnapshotLoader(PlayerItemsRoot).LoadByCharacterUid(CharacterUid);
        }

        public CharacterSnapshot ReloadCharacter()
        {
            return Assert.IsType<CharacterSnapshot>(
                new CharacterSnapshotLoader(UsersRoot).LoadByAccountId(AccountId));
        }

        public void Dispose()
        {
            _log.Dispose();
        }

        private static WorldSession CreateObserverSession(
            CharacterSnapshot source,
            FloorAdvanceRoute route)
        {
            var attributes = new Dictionary<string, string>(source.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["UID"] = ObserverUid.ToString(CultureInfo.InvariantCulture),
                ["Name"] = "LabyrinthObserver",
                ["X"] = (route.SourceX + 1).ToString(CultureInfo.InvariantCulture),
                ["Y"] = (route.SourceY + 1).ToString(CultureInfo.InvariantCulture),
            };
            var character = source with
            {
                Uid = ObserverUid,
                Name = "LabyrinthObserver",
                X = route.SourceX + 1,
                Y = route.SourceY + 1,
                SourceFile = "fixture",
                Attributes = attributes,
            };
            return new WorldSession(
                AccountId: ObserverUid,
                Username: "labyrinth-observer",
                Character: character,
                PlayerItems: PlayerItemSnapshot.Empty,
                PlayerSpells: PlayerSpellSnapshot.Empty,
                PlayerProficiencies: PlayerProficiencySnapshot.Empty);
        }

        private static void WriteCharacter(string path, FloorAdvanceRoute route)
        {
            File.WriteAllText(
                path,
                "[Character]\r\n" +
                $"UID={CharacterUid}\r\n" +
                "Name=LabyrinthHero\r\n" +
                "Body=2003\r\n" +
                "Face=154\r\n" +
                "Haire=420\r\n" +
                "Class=45\r\n" +
                "FirstClass=15\r\n" +
                "SecoundeClass=25\r\n" +
                "Reborn=2\r\n" +
                "Level=137\r\n" +
                "Experience=987654\r\n" +
                $"Map={route.SourceMapId}\r\n" +
                $"X={route.SourceX}\r\n" +
                $"Y={route.SourceY}\r\n" +
                "PMap=1002\r\n" +
                "PMapX=430\r\n" +
                "PMapY=378\r\n" +
                "Strength=123\r\n" +
                "Agility=124\r\n" +
                "Vitaliti=125\r\n" +
                "Spirit=126\r\n" +
                "Atributes=17\r\n" +
                "MinHitPoints=5432\r\n" +
                "MinMana=654\r\n" +
                "MaxMana=765\r\n" +
                "Money=1234567\r\n" +
                "ConquerPoints=2345\r\n" +
                "BoundConquerPoints=3456\r\n" +
                "BankCps=4567\r\n" +
                "PkPoints=12\r\n" +
                "Online=1\r\n" +
                "ExpBallUsed=3\r\n" +
                "DynamicID=77\r\n" +
                "Sentinel=keep-me\r\n" +
                "QuestMarker=alpha\r\n" +
                "[PaidNobility]\r\n" +
                "Donation=123456\r\n" +
                "Rank=4\r\n",
                Encoding.Latin1);
        }
    }

    private sealed record WireExposure(WireEndpoint Endpoint, byte[] Bytes);

    private enum WireEndpoint
    {
        Client,
        Observer,
    }

    private readonly record struct FloorAdvanceRoute(
        uint NpcId,
        uint TokenItemId,
        long SourceMapId,
        long SourceX,
        long SourceY,
        long DestinationMapId,
        long DestinationX,
        long DestinationY,
        string MissingTokenConfirmText,
        string MissingTokenConfirmOptionText,
        ushort MissingTokenConfirmAvatarId);
}
