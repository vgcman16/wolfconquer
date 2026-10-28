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

public sealed partial class LegacyLabyrinthSimonEntryMutationParityTests : IDisposable
{
    private const long AccountId = 1_293_600;
    private const uint CharacterUid = 1_293_600;
    private const uint ObserverUid = 1_293_601;
    private const uint SimonNpcId = 1_152;
    private const uint MeteorItemId = 1_088_001;
    private const uint MeteorScrollItemId = 1_088_002;
    private const long SourceMapId = 1_002;
    private const long SourceX = 430;
    private const long SourceY = 378;
    private const long DestinationMapId = 1_351;
    private const long DestinationX = 18;
    private const long DestinationY = 127;
    private const LegacyMsgUpdateDataType VirtutePointsUpdateType = (LegacyMsgUpdateDataType)0x17;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyLabyrinthSimonEntryMutationParityTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Option6StrictlyAboveTwoThousandDeductsPersistsAndUpdatesBeforeTeleport()
    {
        var initialItems = Items(InventoryItem(300, 410_001, stackSize: 7, seed: 1)) with
        {
            EquipmentItems = [InventoryItem(8_000, 130_009, seed: 2).WithPosition(4)],
            WarehouseItems = [InventoryItem(9_000, 410_301, stackSize: 3, seed: 3).WithWarehouseId(12)],
        };
        using var harness = CreateHarness(optionId: 6, virtutePoints: 4_501, initialItems);

        Assert.True(await harness.InvokeAsync());

        var updated = harness.CurrentSession;
        Assert.NotSame(harness.OriginalSession, updated);
        AssertEntryCharacter(harness.OriginalSession.Character, updated.Character, expectedVirtutePoints: 2_501);
        Assert.Null(updated.ActiveNpcId);
        AssertItemsEqual(harness.OriginalSession.PlayerItems, updated.PlayerItems);

        Assert.Equal(2, harness.SessionWriteCount);
        Assert.True(harness.SessionCommittedBeforeFirstSend);
        AssertItemsEqual(harness.OriginalSession.PlayerItems, Assert.IsType<PlayerItemSnapshot>(harness.ItemsAtCommit));
        AssertEntryCharacter(
            harness.OriginalSession.Character,
            Assert.IsType<CharacterSnapshot>(harness.CharacterAtCommit),
            expectedVirtutePoints: 2_501);
        AssertItemsEqual(harness.OriginalSession.PlayerItems, harness.ReloadItems());
        AssertEntryCharacter(harness.OriginalSession.Character, harness.ReloadCharacter(), expectedVirtutePoints: 2_501);
        Assert.Equal(harness.OriginalItemBytes, File.ReadAllBytes(harness.ItemPath));

        AssertVirtueSuccessPacketOrder(harness, expectedVirtutePoints: 2_501);
    }

    [Fact]
    public async Task Option6ExactlyTwoThousandUsesExactDialogWithoutMutation()
    {
        using var harness = CreateHarness(
            optionId: 6,
            virtutePoints: 2_000,
            Items(InventoryItem(300, 410_002, stackSize: 2, seed: 4)));

        Assert.True(await harness.InvokeAsync());

        Assert.Same(harness.OriginalSession, harness.CurrentSession);
        Assert.Equal(SimonNpcId, harness.CurrentSession.ActiveNpcId);
        Assert.Equal(0, harness.SessionWriteCount);
        Assert.Equal(harness.OriginalCharacterBytes, File.ReadAllBytes(harness.CharacterPath));
        Assert.Equal(harness.OriginalItemBytes, File.ReadAllBytes(harness.ItemPath));
        AssertExactMissingDialog(
            harness.ClientSent,
            "Sorry, you don't have enough virtue points.");
        Assert.Empty(harness.ObserverSent);
        AssertNoSuccessPackets(harness.ClientSent, harness.ObserverSent);
    }

    [Fact]
    public async Task Option6CharacterPersistenceFailurePublishesNothingAndSendsNoPackets()
    {
        using var harness = CreateHarness(
            optionId: 6,
            virtutePoints: 4_501,
            Items(InventoryItem(300, 410_003, stackSize: 2, seed: 5)));
        using var locked = new FileStream(
            harness.CharacterPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        Assert.True(await harness.InvokeAsync());

        AssertAtomicFailure(harness, expectedLogFragment: "character-persistence-");
    }

    [Fact]
    public async Task Option6ObserverFailureRetainsPublishedFilesAndSendsNoPackets()
    {
        using var harness = CreateHarness(
            optionId: 6,
            virtutePoints: 4_501,
            Items(InventoryItem(300, 410_004, stackSize: 2, seed: 6)));

        var race = await harness.InvokeWithObserverFailureAsync();

        Assert.True(race.Handled);
        Assert.False(harness.HasCurrentSession);
        Assert.True(harness.Context.TradeRecovery!.IsBlocked);
        Assert.Null(race.PublishedSession.ActiveNpcId);
        AssertItemsEqual(race.PublishedSession.PlayerItems, harness.ReloadItems());
        Assert.Empty(harness.ClientSent);
        Assert.Empty(harness.ObserverSent);
        Assert.Equal(race.PublishedSession.Character.MapId, harness.ReloadCharacter().MapId);
        Assert.Equal(race.PublishedSession.Character.X, harness.ReloadCharacter().X);
        Assert.Equal(race.PublishedSession.Character.Y, harness.ReloadCharacter().Y);
        Assert.Equal(harness.OriginalItemBytes, File.ReadAllBytes(harness.ItemPath));
        Assert.False(harness.OriginalCharacterBytes.AsSpan().SequenceEqual(File.ReadAllBytes(harness.CharacterPath)));
        Assert.Equal(race.PublishedSession.Character.Attributes["VirtutePoints"],
            harness.ReloadCharacter().Attributes["VirtutePoints"]);
        Assert.Contains("publicationStage=Observer", harness.LogText, StringComparison.Ordinal);
        Assert.Contains("compensationSuppressed=true", harness.LogText, StringComparison.Ordinal);
        Assert.DoesNotContain("rollback=complete", harness.LogText, StringComparison.OrdinalIgnoreCase);
        var retainedItems = File.ReadAllBytes(harness.ItemPath);
        Assert.True(await harness.InvokeAsync(harness.OriginalSession));
        Assert.Equal(retainedItems, File.ReadAllBytes(harness.ItemPath));
        Assert.Empty(harness.ClientSent);
    }

    [Fact]
    public async Task Option6ReplayIsFencedEvenWhenTheCallerSuppliesTheStaleSession()
    {
        using var harness = CreateHarness(
            optionId: 6,
            virtutePoints: 6_001,
            Items(InventoryItem(300, 410_005, stackSize: 2, seed: 7)));

        Assert.True(await harness.InvokeAsync());
        var committedSessionWriteCount = harness.SessionWriteCount;
        var committedSession = harness.CurrentSession;
        var committedCharacterBytes = File.ReadAllBytes(harness.CharacterPath);
        var clientFrameCount = harness.ClientSent.Count;
        var observerFrameCount = harness.ObserverSent.Count;
        var wireFrameCount = harness.Wire.Count;

        Assert.True(await harness.InvokeAsync());
        Assert.True(await harness.InvokeAsync(harness.OriginalSession));

        Assert.Same(committedSession, harness.CurrentSession);
        Assert.Null(harness.CurrentSession.ActiveNpcId);
        Assert.Equal("4001", harness.CurrentSession.Character.Attributes["VirtutePoints"]);
        Assert.Equal(2, committedSessionWriteCount);
        Assert.Equal(committedSessionWriteCount, harness.SessionWriteCount);
        Assert.Equal(clientFrameCount, harness.ClientSent.Count);
        Assert.Equal(observerFrameCount, harness.ObserverSent.Count);
        Assert.Equal(wireFrameCount, harness.Wire.Count);
        Assert.Equal(committedCharacterBytes, File.ReadAllBytes(harness.CharacterPath));
        Assert.Equal(1, harness.ClientSent.Count(IsVirtutePointsUpdatePacket));
        Assert.Equal(1, harness.ClientSent.Count(IsTeleportPacket));
    }

    [Fact]
    public async Task Option7ConsumesFiveSourceOperationsAcrossBothIdsAndBindingsBeforeTeleport()
    {
        var initialItems = Items(
            InventoryItem(10, MeteorScrollItemId, stackSize: 2, bound: 1, seed: 8),
            InventoryItem(20, MeteorItemId, stackSize: 1, bound: 0, seed: 9),
            InventoryItem(30, 410_006, stackSize: 7, bound: 1, seed: 10),
            InventoryItem(40, MeteorScrollItemId, stackSize: 4, bound: 0, seed: 11)) with
        {
            EquipmentItems = [InventoryItem(8_000, 130_009, seed: 12).WithPosition(4)],
            WarehouseItems = [InventoryItem(9_000, 410_301, stackSize: 3, seed: 13).WithWarehouseId(12)],
        };
        using var harness = CreateHarness(optionId: 7, virtutePoints: 8_888, initialItems);

        Assert.True(await harness.InvokeAsync());

        var expectedItems = harness.OriginalSession.PlayerItems with
        {
            InventoryItems = harness.OriginalSession.PlayerItems.InventoryItems
                .Where(item => item.Uid is not 10 and not 20)
                .Select(item => item.Uid == 40 ? item.WithStackSize(2) : item)
                .ToArray(),
        };
        var updated = harness.CurrentSession;
        Assert.NotSame(harness.OriginalSession, updated);
        AssertEntryCharacter(harness.OriginalSession.Character, updated.Character, expectedVirtutePoints: 8_888);
        Assert.Null(updated.ActiveNpcId);
        AssertItemsEqual(expectedItems, updated.PlayerItems);
        AssertItemsEqual(expectedItems, harness.ReloadItems());

        Assert.Equal(2, harness.SessionWriteCount);
        Assert.True(harness.SessionCommittedBeforeFirstSend);
        AssertItemsEqual(expectedItems, Assert.IsType<PlayerItemSnapshot>(harness.ItemsAtCommit));
        AssertEntryCharacter(
            harness.OriginalSession.Character,
            Assert.IsType<CharacterSnapshot>(harness.CharacterAtCommit),
            expectedVirtutePoints: 8_888);
        AssertEntryCharacter(harness.OriginalSession.Character, harness.ReloadCharacter(), expectedVirtutePoints: 8_888);

        var source10 = Assert.Single(harness.OriginalSession.PlayerItems.InventoryItems, item => item.Uid == 10);
        var source40 = Assert.Single(harness.OriginalSession.PlayerItems.InventoryItems, item => item.Uid == 40);
        AssertMeteorUpdate(harness.ClientSent[0], source10.WithStackSize(1));
        AssertRemoveInventory(harness.ClientSent[1], uid: 20);
        AssertMeteorUpdate(harness.ClientSent[2], source40.WithStackSize(3));
        AssertRemoveInventory(harness.ClientSent[3], uid: 10);
        AssertMeteorUpdate(harness.ClientSent[4], source40.WithStackSize(2));
        AssertMeteorSuccessPacketOrder(harness);
    }

    [Fact]
    public async Task Option7FourUnitsAcrossBothIdsAndBindingsUsesExactDialogWithoutMutation()
    {
        using var harness = CreateHarness(
            optionId: 7,
            virtutePoints: 8_888,
            Items(
                InventoryItem(10, MeteorItemId, stackSize: 2, bound: 1, seed: 14),
                InventoryItem(20, MeteorScrollItemId, stackSize: 2, bound: 0, seed: 15),
                InventoryItem(30, 410_007, stackSize: 3, seed: 16)));

        Assert.True(await harness.InvokeAsync());

        Assert.Same(harness.OriginalSession, harness.CurrentSession);
        Assert.Equal(SimonNpcId, harness.CurrentSession.ActiveNpcId);
        Assert.Equal(0, harness.SessionWriteCount);
        Assert.Equal(harness.OriginalCharacterBytes, File.ReadAllBytes(harness.CharacterPath));
        Assert.Equal(harness.OriginalItemBytes, File.ReadAllBytes(harness.ItemPath));
        AssertItemsEqual(harness.OriginalSession.PlayerItems, harness.ReloadItems());
        AssertExactMissingDialog(harness.ClientSent, "Sorry, you don't have 5 Meteors.");
        Assert.Empty(harness.ObserverSent);
        AssertNoSuccessPackets(harness.ClientSent, harness.ObserverSent);
    }

    [Fact]
    public async Task Option7ItemPersistenceFailureRollsBackAndSendsNoPackets()
    {
        using var harness = CreateHarness(
            optionId: 7,
            virtutePoints: 8_888,
            Items(
                InventoryItem(10, MeteorItemId, stackSize: 3, bound: 1, seed: 17),
                InventoryItem(20, MeteorScrollItemId, stackSize: 3, bound: 0, seed: 18)));
        using var locked = new FileStream(
            harness.ItemPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        Assert.True(await harness.InvokeAsync());

        AssertAtomicFailure(harness, expectedLogFragment: "item-persistence-");
    }

    [Fact]
    public async Task Option7CharacterPersistenceFailureRollsBackItemFileAndSendsNoPackets()
    {
        using var harness = CreateHarness(
            optionId: 7,
            virtutePoints: 8_888,
            Items(
                InventoryItem(10, MeteorItemId, stackSize: 3, bound: 1, seed: 19),
                InventoryItem(20, MeteorScrollItemId, stackSize: 3, bound: 0, seed: 20)));
        using var locked = new FileStream(
            harness.CharacterPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        Assert.True(await harness.InvokeAsync());

        AssertAtomicFailure(harness, expectedLogFragment: "character-persistence-");
        Assert.Contains("rollback=", harness.LogText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Option7ObserverFailureRetainsPublishedFilesAndSendsNoPackets()
    {
        using var harness = CreateHarness(
            optionId: 7,
            virtutePoints: 8_888,
            Items(
                InventoryItem(10, MeteorItemId, stackSize: 3, bound: 1, seed: 21),
                InventoryItem(20, MeteorScrollItemId, stackSize: 3, bound: 0, seed: 22)));

        var race = await harness.InvokeWithObserverFailureAsync();

        Assert.True(race.Handled);
        Assert.False(harness.HasCurrentSession);
        Assert.True(harness.Context.TradeRecovery!.IsBlocked);
        Assert.Null(race.PublishedSession.ActiveNpcId);
        AssertItemsEqual(race.PublishedSession.PlayerItems, harness.ReloadItems());
        Assert.Empty(harness.ClientSent);
        Assert.Empty(harness.ObserverSent);
        Assert.Equal(race.PublishedSession.Character.MapId, harness.ReloadCharacter().MapId);
        Assert.Equal(race.PublishedSession.Character.X, harness.ReloadCharacter().X);
        Assert.Equal(race.PublishedSession.Character.Y, harness.ReloadCharacter().Y);
        Assert.False(harness.OriginalItemBytes.AsSpan().SequenceEqual(File.ReadAllBytes(harness.ItemPath)));
        Assert.Contains("publicationStage=Observer", harness.LogText, StringComparison.Ordinal);
        Assert.Contains("compensationSuppressed=true", harness.LogText, StringComparison.Ordinal);
        Assert.DoesNotContain("rollback=complete", harness.LogText, StringComparison.OrdinalIgnoreCase);
        var retainedItems = File.ReadAllBytes(harness.ItemPath);
        Assert.True(await harness.InvokeAsync(harness.OriginalSession));
        Assert.Equal(retainedItems, File.ReadAllBytes(harness.ItemPath));
        Assert.Empty(harness.ClientSent);
    }

    [Fact]
    public async Task Option7ReplayIsFencedEvenWhenTheCallerSuppliesTheStaleSession()
    {
        using var harness = CreateHarness(
            optionId: 7,
            virtutePoints: 8_888,
            Items(
                InventoryItem(10, MeteorItemId, stackSize: 10, bound: 1, seed: 23),
                InventoryItem(30, 410_008, stackSize: 2, seed: 24)));

        Assert.True(await harness.InvokeAsync());
        var committedSessionWriteCount = harness.SessionWriteCount;
        var committedSession = harness.CurrentSession;
        var committedCharacterBytes = File.ReadAllBytes(harness.CharacterPath);
        var committedItemBytes = File.ReadAllBytes(harness.ItemPath);
        var clientFrameCount = harness.ClientSent.Count;
        var observerFrameCount = harness.ObserverSent.Count;
        var wireFrameCount = harness.Wire.Count;

        Assert.True(await harness.InvokeAsync());
        Assert.True(await harness.InvokeAsync(harness.OriginalSession));

        Assert.Same(committedSession, harness.CurrentSession);
        Assert.Null(harness.CurrentSession.ActiveNpcId);
        Assert.Equal((ushort)5, Assert.Single(
            harness.CurrentSession.PlayerItems.InventoryItems,
            item => item.Uid == 10).StackSize);
        Assert.Equal(2, committedSessionWriteCount);
        Assert.Equal(committedSessionWriteCount, harness.SessionWriteCount);
        Assert.Equal(clientFrameCount, harness.ClientSent.Count);
        Assert.Equal(observerFrameCount, harness.ObserverSent.Count);
        Assert.Equal(wireFrameCount, harness.Wire.Count);
        Assert.Equal(committedCharacterBytes, File.ReadAllBytes(harness.CharacterPath));
        Assert.Equal(committedItemBytes, File.ReadAllBytes(harness.ItemPath));
        AssertItemsEqual(harness.CurrentSession.PlayerItems, harness.ReloadItems());
        Assert.Equal(5, harness.ClientSent.Count(IsItemMutationPacket));
        Assert.Equal(1, harness.ClientSent.Count(IsTeleportPacket));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private EntryHarness CreateHarness(byte optionId, uint virtutePoints, PlayerItemSnapshot initialItems)
    {
        var caseRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(caseRoot);
        return new EntryHarness(caseRoot, optionId, virtutePoints, initialItems);
    }

    private static void AssertVirtueSuccessPacketOrder(EntryHarness harness, uint expectedVirtutePoints)
    {
        Assert.Equal(6, harness.ClientSent.Count);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(harness.ClientSent[0]), out var update));
        Assert.Equal(CharacterUid, update.Uid);
        var entry = Assert.Single(update.Updates);
        Assert.Equal(VirtutePointsUpdateType, entry.Type);
        Assert.Equal(expectedVirtutePoints, entry.Value);

        AssertMutationThenTeleportWireOrder(harness, mutationFrameCount: 1);
        Assert.DoesNotContain(harness.ClientSent, IsNpcReplyPacket);
    }

    private static void AssertMeteorSuccessPacketOrder(EntryHarness harness)
    {
        Assert.Equal(10, harness.ClientSent.Count);
        Assert.All(harness.ClientSent.Take(5), frame => Assert.True(IsItemMutationPacket(frame)));
        Assert.All(harness.ClientSent.Skip(5), frame => Assert.False(IsItemMutationPacket(frame)));
        AssertMutationThenTeleportWireOrder(harness, mutationFrameCount: 5);
        Assert.DoesNotContain(harness.ClientSent, IsNpcReplyPacket);
        Assert.DoesNotContain(harness.ClientSent, IsVirtutePointsUpdatePacket);
    }

    private static void AssertMutationThenTeleportWireOrder(EntryHarness harness, int mutationFrameCount)
    {
        Assert.Single(harness.ObserverSent);
        Assert.Equal(harness.ClientSent.Count + 1, harness.Wire.Count);

        for (var index = 0; index < mutationFrameCount; index++)
        {
            Assert.Equal(WireEndpoint.Client, harness.Wire[index].Endpoint);
            Assert.Equal(harness.ClientSent[index], harness.Wire[index].Bytes);
        }

        Assert.Equal(WireEndpoint.Observer, harness.Wire[mutationFrameCount].Endpoint);
        Assert.True(LegacyRemoveEntityPacket.TryParse(
            ReadFrame(harness.Wire[mutationFrameCount].Bytes),
            out var leave));
        Assert.Equal(CharacterUid, leave.Uid);

        for (var index = mutationFrameCount; index < harness.ClientSent.Count; index++)
        {
            Assert.Equal(WireEndpoint.Client, harness.Wire[index + 1].Endpoint);
            Assert.Equal(harness.ClientSent[index], harness.Wire[index + 1].Bytes);
        }

        AssertTeleportTransition(harness.ClientSent.Skip(mutationFrameCount).ToArray());
    }

    private static void AssertTeleportTransition(IReadOnlyList<byte[]> sent)
    {
        Assert.Equal(5, sent.Count);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[0]), out var teleport));
        Assert.Equal(CharacterUid, teleport.ObjId);
        Assert.Equal(MsgActionType.Teleport, teleport.Type);
        Assert.Equal(checked((uint)DestinationMapId), teleport.DwParam);
        Assert.Equal(DestinationMapId, teleport.DwParam3);
        Assert.Equal(checked((uint)DestinationX), teleport.PositionX);
        Assert.Equal(checked((uint)DestinationY), teleport.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[1]), out var startup));
        Assert.Equal(CharacterUid, startup.ObjId);
        Assert.Equal(MsgActionType.LegacyMerchantStartup, startup.Type);
        Assert.Equal(2U, startup.DwParam);
        Assert.Equal(DestinationMapId, startup.DwParam3);
        Assert.Equal(checked((uint)DestinationX), startup.PositionX);
        Assert.Equal(checked((uint)DestinationY), startup.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[2]), out var stopVending));
        Assert.Equal(CharacterUid, stopVending.ObjId);
        Assert.Equal(MsgActionType.StopVending, stopVending.Type);
        Assert.Equal(checked((uint)DestinationMapId), stopVending.DwParam);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[3]), out var color));
        Assert.Equal(CharacterUid, color.ObjId);
        Assert.Equal(MsgActionType.LegacyColor, color.Type);
        Assert.Equal(checked((uint)DestinationX), color.PositionX);
        Assert.Equal(checked((uint)DestinationY), color.PositionY);

        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(sent[4]), out var mapStatus));
        Assert.Equal(LegacyMapStatusPacket.FromMap(DestinationMapId, mapsById: null), mapStatus);
    }

    private static void AssertEntryCharacter(
        CharacterSnapshot source,
        CharacterSnapshot actual,
        uint expectedVirtutePoints)
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
        Assert.Equal(DestinationMapId, actual.MapId);
        Assert.Equal(DestinationX, actual.X);
        Assert.Equal(DestinationY, actual.Y);
        Assert.Equal(source.MapId, actual.PreviousMapId);
        Assert.Equal(source.X, actual.PreviousMapX);
        Assert.Equal(source.Y, actual.PreviousMapY);
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
        Assert.Equal("0", actual.Attributes["DynamicID"]);
        Assert.Equal("77", actual.Attributes["PDinamycID"]);
        Assert.Equal(DestinationMapId.ToString(CultureInfo.InvariantCulture), actual.Attributes["Map"]);
        Assert.Equal(DestinationX.ToString(CultureInfo.InvariantCulture), actual.Attributes["X"]);
        Assert.Equal(DestinationY.ToString(CultureInfo.InvariantCulture), actual.Attributes["Y"]);
        Assert.Equal(source.MapId.ToString(CultureInfo.InvariantCulture), actual.Attributes["PMap"]);
        Assert.Equal(source.X.ToString(CultureInfo.InvariantCulture), actual.Attributes["PMapX"]);
        Assert.Equal(source.Y.ToString(CultureInfo.InvariantCulture), actual.Attributes["PMapY"]);
        Assert.Equal(
            expectedVirtutePoints.ToString(CultureInfo.InvariantCulture),
            actual.Attributes["VirtutePoints"]);

        var mutableKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Map",
            "X",
            "Y",
            "PMap",
            "PMapX",
            "PMapY",
            "DynamicID",
            "PDinamycID",
            "VirtutePoints",
        };
        Assert.Equal(
            source.Attributes
                .Where(pair => !mutableKeys.Contains(pair.Key))
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase),
            actual.Attributes
                .Where(pair => !mutableKeys.Contains(pair.Key))
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase));
        Assert.Equal(
            source.PaidNobility.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase),
            actual.PaidNobility.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase));
    }

    private static void AssertAtomicFailure(EntryHarness harness, string expectedLogFragment)
    {
        Assert.Same(harness.OriginalSession, harness.CurrentSession);
        Assert.Equal(SimonNpcId, harness.CurrentSession.ActiveNpcId);
        Assert.Equal(0, harness.SessionWriteCount);
        Assert.Equal(harness.OriginalCharacterBytes, File.ReadAllBytes(harness.CharacterPath));
        Assert.Equal(harness.OriginalItemBytes, File.ReadAllBytes(harness.ItemPath));
        AssertItemsEqual(harness.OriginalSession.PlayerItems, harness.ReloadItems());
        Assert.Empty(harness.ClientSent);
        Assert.Empty(harness.ObserverSent);
        Assert.Empty(harness.Wire);
        Assert.Contains(expectedLogFragment, harness.LogText, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertExactMissingDialog(IReadOnlyList<byte[]> sent, string expectedText)
    {
        Assert.Equal(4, sent.Count);

        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal(expectedText, dialog.Text);

        var option = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, option.OptionId);
        Assert.Equal("Sigh...", option.Text);

        var avatar = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal((ushort)63, avatar.InputMaxLength);

        AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Finish);
    }

    private static void AssertNoSuccessPackets(
        IEnumerable<byte[]> clientSent,
        IEnumerable<byte[]> observerSent)
    {
        Assert.DoesNotContain(clientSent, IsItemMutationPacket);
        Assert.DoesNotContain(clientSent, IsVirtutePointsUpdatePacket);
        Assert.DoesNotContain(clientSent, frame => MsgActionPacket.TryParse(ReadFrame(frame), out _));
        Assert.DoesNotContain(clientSent, frame => LegacyMapStatusPacket.TryParse(ReadFrame(frame), out _));
        Assert.DoesNotContain(observerSent, frame => LegacyRemoveEntityPacket.TryParse(ReadFrame(frame), out _));
    }

    private static void AssertMeteorUpdate(byte[] bytes, PlayerItemSnapshotItem expectedItem)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(
            LegacyGameItemPacket.FromSnapshotItem(
                expectedItem,
                mode: LegacyGameItemPacket.UpdateItemMode),
            packet);
    }

    private static void AssertRemoveInventory(byte[] bytes, uint uid)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(MsgItemUsageAction.RemoveInventory, packet.Action);
        Assert.Equal(uid, packet.Id);
    }

    private static LegacyNpcReplyPacket AssertNpcReply(
        byte[] bytes,
        LegacyNpcReplyInteractType interactType)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(interactType, packet.InteractType);
        return packet;
    }

    private static bool IsItemMutationPacket(byte[] bytes)
    {
        return (MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var usage) &&
                usage.Action == MsgItemUsageAction.RemoveInventory) ||
            (LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var item) &&
                item.Mode == LegacyGameItemPacket.UpdateItemMode);
    }

    private static bool IsVirtutePointsUpdatePacket(byte[] bytes)
    {
        return LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
            update.Updates.Any(entry => entry.Type == VirtutePointsUpdateType);
    }

    private static bool IsTeleportPacket(byte[] bytes)
    {
        return MsgActionPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Type == MsgActionType.Teleport;
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

    private static void AssertItemsEqual(PlayerItemSnapshot expected, PlayerItemSnapshot actual)
    {
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            PlayerItemSnapshotLoader.ToLegacyBytes(actual));
    }

    private static PlayerItemSnapshot Items(params PlayerItemSnapshotItem[] inventoryItems)
    {
        return new PlayerItemSnapshot(inventoryItems, [], [], [], [], []);
    }

    private static PlayerItemSnapshotItem InventoryItem(
        uint uid,
        uint itemId,
        ushort stackSize = 1,
        byte bound = 0,
        int seed = 0)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = checked((ushort)(70 + seed)),
            MaximumDurability = checked((ushort)(90 + seed)),
            Position = 0,
            SocketProgress = checked((uint)(100 + seed)),
            SocketOne = checked((byte)(seed % 3)),
            SocketTwo = checked((byte)((seed + 1) % 3)),
            Effect = checked((uint)(200 + seed)),
            Plus = checked((byte)(seed % 12)),
            Bless = checked((byte)(seed % 7)),
            Bound = bound,
            Enchant = checked((byte)(seed % 10)),
            Suspicious = checked((byte)(seed % 2)),
            Locked = checked((byte)((seed + 1) % 2)),
            PlusProgress = checked((uint)(300 + seed)),
            Inscribed = checked((ushort)(seed % 2)),
            Activate = checked((uint)(400 + seed)),
            TimeLeftInMinutes = checked((uint)(500 + seed)),
            StackSize = stackSize,
            WarehouseId = 0,
            Color = checked((uint)(3 + seed % 6)),
            UnlockTimer = 600 + seed,
            ItemPoints = checked((uint)(700 + seed)),
            PurificationItemId = checked((uint)(800 + seed)),
            PurificationLevel = checked((uint)(seed % 5)),
            PurificationDuration = checked((uint)(900 + seed)),
            PurificationAddedOn = 1_000 + seed,
            RefineryEffectId = checked((uint)(1_100 + seed)),
            RefineryEffectLevel = checked((uint)(1 + seed % 5)),
            RefineryEffectPercent = checked((uint)(10 + seed)),
            RefineryEffectPercent2 = checked((uint)(20 + seed)),
            RefineryEffectDuration = checked((uint)(1_200 + seed)),
            RefineryEffectAddedOn = 1_300 + seed,
            AnimaItemId = checked((uint)(1_400 + seed)),
            RuneExperience = checked((uint)(1_500 + seed)),
            Attribute1 = checked((uint)(1_600 + seed)),
            Attribute2 = checked((uint)(1_700 + seed)),
            Attribute3 = checked((uint)(1_800 + seed)),
            Attribute4 = checked((uint)(1_900 + seed)),
            Attribute5 = checked((uint)(2_000 + seed)),
            Expiration = 2_100 + seed,
            MythSoul = checked((uint)(2_200 + seed)),
            MythSoulPoints = checked((uint)(2_300 + seed)),
            PerfectionLevel = checked((uint)(seed % 10)),
            PerfectionProgress = checked((uint)(2_400 + seed)),
            OwnerUid = checked((uint)(3_000 + seed)),
            OwnerName = "Owner" + seed.ToString(CultureInfo.InvariantCulture),
            Signature = "Signature" + seed.ToString(CultureInfo.InvariantCulture),
        };
    }

    private sealed class EntryHarness : IDisposable
    {
        private readonly byte _optionId;
        private readonly ConquerTcpConnection _connection;
        private readonly ConquerTcpConnection _observerConnection;
        private readonly StringWriter _log = new();
        private bool? _sessionCommittedBeforeFirstSend;
        private bool _replaceSessionDuringCommit;
        private WorldSession? _replacementSession;
        private WorldSession? _publishedAtFailure;

        public EntryHarness(
            string caseRoot,
            byte optionId,
            uint virtutePoints,
            PlayerItemSnapshot initialItems)
        {
            _optionId = optionId;
            UsersRoot = Path.Combine(caseRoot, "Users");
            PlayerItemsRoot = Path.Combine(caseRoot, "PlayersItems");
            Directory.CreateDirectory(UsersRoot);
            CharacterPath = Path.Combine(
                UsersRoot,
                AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
            ItemPath = Path.Combine(
                PlayerItemsRoot,
                CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
            WriteCharacter(CharacterPath, virtutePoints);
            new PlayerItemSnapshotLoader(PlayerItemsRoot).SaveByCharacterUid(CharacterUid, initialItems);

            var character = ReloadCharacter();
            var normalizedItems = ReloadItems();
            OriginalSession = new WorldSession(
                AccountId,
                Username: "labyrinth-simon-entry-test",
                Character: character,
                PlayerItems: normalizedItems,
                PlayerSpells: PlayerSpellSnapshot.Empty,
                PlayerProficiencies: PlayerProficiencySnapshot.Empty,
                UsersRoot: UsersRoot,
                ActiveNpcId: SimonNpcId);
            OriginalCharacterBytes = File.ReadAllBytes(CharacterPath);
            OriginalItemBytes = File.ReadAllBytes(ItemPath);

            ConquerTcpConnection? connection = null;
            connection = new ConquerTcpConnection(
                null,
                new IPEndPoint(IPAddress.Loopback, 9_958),
                (frame, _) =>
                {
                    _sessionCommittedBeforeFirstSend ??=
                        Sessions!.TryGet(connection!, out var current) &&
                        !ReferenceEquals(current, OriginalSession);
                    var bytes = frame.ToArray();
                    ClientSent.Add(bytes);
                    Wire.Add(new WireExposure(WireEndpoint.Client, bytes));
                    return ValueTask.CompletedTask;
                });
            _connection = connection;

            Sessions = new WorldSessionStore((changedConnection, previous, current) =>
            {
                if (!ReferenceEquals(changedConnection, _connection) ||
                    previous is null ||
                    ReferenceEquals(previous, current))
                {
                    return;
                }

                if (_replaceSessionDuringCommit && current.ActiveNpcId is null)
                {
                    _replaceSessionDuringCommit = false;
                    _publishedAtFailure = current;
                    _replacementSession = OriginalSession with
                    {
                        Username = "labyrinth-simon-entry-cas-replacement",
                    };
                    Sessions!.Set(_connection, _replacementSession);
                    throw new InvalidOperationException("Injected observer failure after actual publication.");
                }

                SessionWriteCount++;
                CharacterAtCommit = ReloadCharacter();
                ItemsAtCommit = ReloadItems();
            });
            Sessions.Set(_connection, OriginalSession);

            _observerConnection = new ConquerTcpConnection(
                null,
                new IPEndPoint(IPAddress.Loopback, 9_959),
                (frame, _) =>
                {
                    var bytes = frame.ToArray();
                    ObserverSent.Add(bytes);
                    Wire.Add(new WireExposure(WireEndpoint.Observer, bytes));
                    return ValueTask.CompletedTask;
                });
            Sessions.Set(_observerConnection, CreateObserverSession(character));

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

        public CharacterSnapshot? CharacterAtCommit { get; private set; }

        public PlayerItemSnapshot? ItemsAtCommit { get; private set; }

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
                SimonNpcId,
                _optionId,
                CancellationToken.None);
        }

        public async Task<PublicationFailureResult> InvokeWithObserverFailureAsync()
        {
            _replaceSessionDuringCommit = true;
            var handled = await InvokeAsync().ConfigureAwait(false);
            return new PublicationFailureResult(handled, Assert.IsType<WorldSession>(_publishedAtFailure), Assert.IsType<WorldSession>(_replacementSession));
        }

        public CharacterSnapshot ReloadCharacter()
        {
            return Assert.IsType<CharacterSnapshot>(
                new CharacterSnapshotLoader(UsersRoot).LoadByAccountId(AccountId));
        }

        public PlayerItemSnapshot ReloadItems()
        {
            return new PlayerItemSnapshotLoader(PlayerItemsRoot).LoadByCharacterUid(CharacterUid);
        }

        public void Dispose()
        {
            _log.Dispose();
        }

        private static WorldSession CreateObserverSession(CharacterSnapshot source)
        {
            var attributes = new Dictionary<string, string>(source.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["UID"] = ObserverUid.ToString(CultureInfo.InvariantCulture),
                ["Name"] = "LabyrinthSimonObserver",
                ["X"] = (SourceX + 1).ToString(CultureInfo.InvariantCulture),
                ["Y"] = (SourceY + 1).ToString(CultureInfo.InvariantCulture),
            };
            var character = source with
            {
                Uid = ObserverUid,
                Name = "LabyrinthSimonObserver",
                X = SourceX + 1,
                Y = SourceY + 1,
                SourceFile = "fixture",
                Attributes = attributes,
            };
            return new WorldSession(
                AccountId: ObserverUid,
                Username: "labyrinth-simon-observer",
                Character: character,
                PlayerItems: PlayerItemSnapshot.Empty,
                PlayerSpells: PlayerSpellSnapshot.Empty,
                PlayerProficiencies: PlayerProficiencySnapshot.Empty);
        }

        private static void WriteCharacter(string path, uint virtutePoints)
        {
            File.WriteAllText(
                path,
                "[Character]\r\n" +
                $"UID={CharacterUid}\r\n" +
                "Name=LabyrinthSimonHero\r\n" +
                "Body=2003\r\n" +
                "Face=154\r\n" +
                "Haire=420\r\n" +
                "Class=45\r\n" +
                "FirstClass=15\r\n" +
                "SecoundeClass=25\r\n" +
                "Reborn=2\r\n" +
                "Level=137\r\n" +
                "Experience=987654\r\n" +
                $"Map={SourceMapId}\r\n" +
                $"X={SourceX}\r\n" +
                $"Y={SourceY}\r\n" +
                "PMap=1000\r\n" +
                "PMapX=300\r\n" +
                "PMapY=301\r\n" +
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
                $"VirtutePoints={virtutePoints.ToString(CultureInfo.InvariantCulture)}\r\n" +
                "Sentinel=keep-me\r\n" +
                "QuestMarker=alpha\r\n" +
                "[PaidNobility]\r\n" +
                "Donation=123456\r\n" +
                "Rank=4\r\n",
                Encoding.Latin1);
        }
    }

    private sealed record PublicationFailureResult(bool Handled, WorldSession PublishedSession, WorldSession ReplacementSession);

    private sealed record WireExposure(WireEndpoint Endpoint, byte[] Bytes);

    private enum WireEndpoint
    {
        Client,
        Observer,
    }
}
