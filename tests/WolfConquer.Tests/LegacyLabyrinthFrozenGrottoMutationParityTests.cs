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

public sealed class LegacyLabyrinthFrozenGrottoMutationParityTests : IDisposable
{
    private const long AccountId = 1_293_600;
    private const uint CharacterUid = 1_293_600;
    private const uint ObserverUid = 1_293_601;
    private const uint FrozenFroto4NpcId = 13_124;
    private const uint FrozenGrottoGuardian5NpcId = 13_125;
    private const uint GrottoKeyItemId = 721_347;
    private const uint ShadowJadeItemId = 720_599;
    private const long SourceMapId = 2_054;
    private const long SourceX = 379;
    private const long SourceY = 470;
    private const long DestinationMapId = 2_055;
    private const long DestinationX = 526;
    private const long DestinationY = 562;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyLabyrinthFrozenGrottoMutationParityTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task GuardianOption2WithFreeSlotAndNoUnboundJadePersistsOneUnboundJadeAndOnlySendsAddFrame()
    {
        var initialItems = Items(
            InventoryItem(10, ShadowJadeItemId, bound: 1, seed: 1),
            InventoryItem(40, 410_001, stackSize: 4, seed: 2)) with
        {
            EquipmentItems = [InventoryItem(8_000, 130_009, seed: 3).WithPosition(4)],
            WarehouseItems = [InventoryItem(9_000, 410_301, stackSize: 3, seed: 4).WithWarehouseId(12)],
        };
        using var harness = CreateHarness(
            FrozenGrottoGuardian5NpcId,
            optionId: 2,
            initialItems,
            includeObserver: true);

        Assert.True(await harness.InvokeAsync());

        var updated = harness.CurrentSession;
        Assert.NotSame(harness.OriginalSession, updated);
        Assert.Null(updated.ActiveNpcId);
        Assert.Equal(harness.OriginalSession.PlayerItems.InventoryItems.Count + 1, updated.PlayerItems.InventoryItems.Count);
        Assert.Contains(updated.PlayerItems.InventoryItems, item =>
            item.Uid == 10 && item.ItemId == ShadowJadeItemId && item.Bound == 1);
        var grantedJade = Assert.Single(updated.PlayerItems.InventoryItems, item =>
            item.ItemId == ShadowJadeItemId && item.Bound == 0);
        Assert.Equal(9_001U, grantedJade.Uid);
        Assert.Equal((ushort)0, grantedJade.Position);
        Assert.Equal((ushort)1, grantedJade.StackSize);
        Assert.Equal((ushort)321, grantedJade.Durability);
        Assert.Equal((ushort)321, grantedJade.MaximumDurability);
        AssertSideCollectionsEqual(harness.OriginalSession.PlayerItems, updated.PlayerItems);

        Assert.Equal(1, harness.SessionWriteCount);
        Assert.True(harness.PersistenceMatchedAtCommit);
        Assert.True(harness.SessionCommittedBeforeFirstSend);
        AssertItemsEqual(updated.PlayerItems, harness.ReloadItems());
        Assert.Equal(harness.OriginalCharacterBytes, File.ReadAllBytes(harness.CharacterPath));
        AssertCharacterUnchanged(harness.OriginalSession.Character, harness.ReloadCharacter());

        var addBytes = Assert.Single(harness.ClientSent);
        var add = AssertGameItem(addBytes);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, add.Mode);
        Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(grantedJade), add);
        Assert.DoesNotContain(harness.ClientSent, IsNpcReplyPacket);
        Assert.Empty(harness.ObserverSent);
        Assert.Single(harness.Wire);
        Assert.Equal(WireEndpoint.Client, harness.Wire[0].Endpoint);
    }

    [Fact]
    public async Task GuardianOption2WithExistingUnboundJadeUsesExactNormalizedDialogWithoutMutation()
    {
        using var harness = CreateHarness(
            FrozenGrottoGuardian5NpcId,
            optionId: 2,
            Items(
                InventoryItem(10, ShadowJadeItemId, bound: 0, seed: 5),
                InventoryItem(20, 410_002, seed: 6)),
            includeObserver: true);

        Assert.True(await harness.InvokeAsync());

        AssertUnchangedWithoutCommit(harness, FrozenGrottoGuardian5NpcId);
        AssertExactNpcDialog(
            harness.ClientSent,
            "Sorry, but you already have Shadow Jade.",
            "Ah~yes.",
            avatarId: 0);
        AssertNoItemOrTeleportPackets(harness.ClientSent, harness.ObserverSent);
    }

    [Fact]
    public async Task GuardianOption2FullInventoryTakesPrecedenceAndUsesExactNormalizedDialog()
    {
        var fullInventory = Enumerable.Range(0, 40)
            .Select(index => InventoryItem(
                checked((uint)(100 + index)),
                index == 0 ? ShadowJadeItemId : checked((uint)(410_100 + index)),
                seed: checked((byte)index)))
            .ToArray();
        using var harness = CreateHarness(
            FrozenGrottoGuardian5NpcId,
            optionId: 2,
            Items(fullInventory),
            includeObserver: false);

        Assert.True(await harness.InvokeAsync());

        AssertUnchangedWithoutCommit(harness, FrozenGrottoGuardian5NpcId);
        AssertExactNpcDialog(
            harness.ClientSent,
            "Please make 1 space in your inventory.",
            "Ah~yes.",
            avatarId: 0);
        AssertNoItemOrTeleportPackets(harness.ClientSent, harness.ObserverSent);
    }

    [Fact]
    public async Task GuardianItemPersistenceFailureRollsBackAndSendsNoSuccessPackets()
    {
        using var harness = CreateHarness(
            FrozenGrottoGuardian5NpcId,
            optionId: 2,
            Items(InventoryItem(10, 410_003, seed: 7)),
            includeObserver: true);
        using var locked = new FileStream(
            harness.ItemPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        Assert.True(await harness.InvokeAsync());

        AssertAtomicFailure(harness, FrozenGrottoGuardian5NpcId, "item-persistence-");
    }

    [Fact]
    public async Task GuardianSessionCasChangeAfterDurableWriteRollsBackWithoutSuccessPackets()
    {
        using var harness = CreateHarness(
            FrozenGrottoGuardian5NpcId,
            optionId: 2,
            Items(InventoryItem(10, 410_004, seed: 8)),
            includeObserver: true);

        var race = await harness.InvokeWithSessionCasChangeAsync();

        Assert.True(race.Handled);
        Assert.Same(race.ReplacementSession, harness.CurrentSession);
        Assert.Equal(FrozenGrottoGuardian5NpcId, harness.CurrentSession.ActiveNpcId);
        Assert.Equal(0, harness.SessionWriteCount);
        Assert.Equal(harness.OriginalItemBytes, File.ReadAllBytes(harness.ItemPath));
        Assert.Equal(harness.OriginalCharacterBytes, File.ReadAllBytes(harness.CharacterPath));
        AssertItemsEqual(harness.OriginalSession.PlayerItems, harness.ReloadItems());
        AssertCharacterUnchanged(harness.OriginalSession.Character, harness.ReloadCharacter());
        Assert.Empty(harness.ClientSent);
        Assert.Empty(harness.ObserverSent);
        Assert.Contains("session-commit-InvalidOperationException", harness.LogText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rollback=complete", harness.LogText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GuardianReplayAfterSuccessCannotGrantAnotherJadeEvenWithStaleSession()
    {
        using var harness = CreateHarness(
            FrozenGrottoGuardian5NpcId,
            optionId: 2,
            Items(InventoryItem(10, 410_005, seed: 9)),
            includeObserver: true);

        Assert.True(await harness.InvokeAsync());
        var committedSessionWriteCount = harness.SessionWriteCount;
        var committedSession = harness.CurrentSession;
        var committedFrames = harness.ClientSent.Select(bytes => bytes.ToArray()).ToArray();
        var committedBytes = File.ReadAllBytes(harness.ItemPath);

        Assert.True(await harness.InvokeAsync());
        Assert.True(await harness.InvokeAsync(harness.OriginalSession));

        Assert.Same(committedSession, harness.CurrentSession);
        Assert.Null(harness.CurrentSession.ActiveNpcId);
        Assert.Equal(1, committedSessionWriteCount);
        Assert.Equal(committedSessionWriteCount, harness.SessionWriteCount);
        Assert.Single(harness.CurrentSession.PlayerItems.InventoryItems, item =>
            item.ItemId == ShadowJadeItemId && item.Bound == 0);
        Assert.Equal(committedBytes, File.ReadAllBytes(harness.ItemPath));
        AssertItemsEqual(harness.CurrentSession.PlayerItems, harness.ReloadItems());
        Assert.Equal(committedFrames.Length, harness.ClientSent.Count);
        for (var index = 0; index < committedFrames.Length; index++)
        {
            Assert.Equal(committedFrames[index], harness.ClientSent[index]);
        }

        Assert.Single(harness.ClientSent, IsShadowJadeAddPacket);
        Assert.Empty(harness.ObserverSent);
    }

    [Fact]
    public async Task FrozenOption1ConsumesKeyPersistsCharacterAndUsesExactItemBeforeTeleportOrder()
    {
        using var harness = CreateHarness(
            FrozenFroto4NpcId,
            optionId: 1,
            Items(
                InventoryItem(10, GrottoKeyItemId, seed: 10),
                InventoryItem(30, 410_006, stackSize: 7, seed: 11)),
            includeObserver: true);

        Assert.True(await harness.InvokeAsync());

        var updated = harness.CurrentSession;
        var expectedItems = RemoveInventoryUid(harness.OriginalSession.PlayerItems, uid: 10);
        Assert.NotSame(harness.OriginalSession, updated);
        Assert.Null(updated.ActiveNpcId);
        AssertItemsEqual(expectedItems, updated.PlayerItems);
        AssertCharacterTeleported(harness.OriginalSession.Character, updated.Character);
        Assert.Equal(2, harness.SessionWriteCount);
        Assert.True(harness.PersistenceMatchedAtCommit);
        Assert.True(harness.SessionCommittedBeforeFirstSend);
        AssertItemsEqual(expectedItems, harness.ReloadItems());
        AssertCharacterTeleported(harness.OriginalSession.Character, harness.ReloadCharacter());

        AssertKeySuccessPacketOrder(
            harness,
            itemMutationAssertion: bytes => AssertRemoveInventory(bytes, uid: 10));
    }

    [Fact]
    public async Task FrozenOption1UnboundGateStillRemovesLowestUidKeyAcrossBindings()
    {
        using var harness = CreateHarness(
            FrozenFroto4NpcId,
            optionId: 1,
            Items(
                InventoryItem(10, GrottoKeyItemId, bound: 1, seed: 12),
                InventoryItem(20, GrottoKeyItemId, bound: 0, seed: 13),
                InventoryItem(30, 410_007, seed: 14)),
            includeObserver: false);

        Assert.True(await harness.InvokeAsync());

        var updated = harness.CurrentSession;
        Assert.DoesNotContain(updated.PlayerItems.InventoryItems, item => item.Uid == 10);
        var remainingKey = Assert.Single(updated.PlayerItems.InventoryItems, item => item.ItemId == GrottoKeyItemId);
        Assert.Equal(20U, remainingKey.Uid);
        Assert.Equal((byte)0, remainingKey.Bound);
        var expectedItems = RemoveInventoryUid(harness.OriginalSession.PlayerItems, uid: 10);
        AssertItemsEqual(expectedItems, updated.PlayerItems);
        AssertItemsEqual(expectedItems, harness.ReloadItems());
        AssertCharacterTeleported(harness.OriginalSession.Character, updated.Character);
        AssertKeySuccessPacketOrder(
            harness,
            itemMutationAssertion: bytes => AssertRemoveInventory(bytes, uid: 10));
    }

    [Fact]
    public async Task FrozenOption1StackedLowestUidKeyDecrementsOneUnitBeforeTeleport()
    {
        using var harness = CreateHarness(
            FrozenFroto4NpcId,
            optionId: 1,
            Items(
                InventoryItem(10, GrottoKeyItemId, stackSize: 3, bound: 1, seed: 15),
                InventoryItem(20, GrottoKeyItemId, bound: 0, seed: 16),
                InventoryItem(30, 410_008, seed: 17)),
            includeObserver: true);

        Assert.True(await harness.InvokeAsync());

        var expectedItems = SetInventoryStackSize(
            harness.OriginalSession.PlayerItems,
            uid: 10,
            stackSize: 2);
        var expectedKey = Assert.Single(expectedItems.InventoryItems, item => item.Uid == 10);
        AssertItemsEqual(expectedItems, harness.CurrentSession.PlayerItems);
        AssertItemsEqual(expectedItems, harness.ReloadItems());
        Assert.Equal((ushort)2, Assert.Single(
            harness.CurrentSession.PlayerItems.InventoryItems,
            item => item.Uid == 10).StackSize);
        Assert.Contains(harness.CurrentSession.PlayerItems.InventoryItems, item =>
            item.Uid == 20 && item.Bound == 0 && item.StackSize == 1);
        Assert.DoesNotContain(harness.ClientSent, IsRemoveInventoryPacket);
        AssertCharacterTeleported(harness.OriginalSession.Character, harness.CurrentSession.Character);
        AssertKeySuccessPacketOrder(
            harness,
            itemMutationAssertion: bytes => AssertStackUpdate(bytes, expectedKey));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FrozenOption1MissingOrBoundOnlyKeyUsesExactNormalizedDialogWithoutMutation(bool boundOnly)
    {
        var items = boundOnly
            ? Items(
                InventoryItem(10, GrottoKeyItemId, bound: 1, seed: 18),
                InventoryItem(30, 410_009, seed: 19))
            : Items(InventoryItem(30, 410_009, seed: 19));
        using var harness = CreateHarness(
            FrozenFroto4NpcId,
            optionId: 1,
            items,
            includeObserver: true);

        Assert.True(await harness.InvokeAsync());

        AssertUnchangedWithoutCommit(harness, FrozenFroto4NpcId);
        AssertExactNpcDialog(
            harness.ClientSent,
            "Sorry, you do not have the Grotto Key in your inventory. Kill monsters to obtain that key.",
            "Okay.",
            avatarId: 31);
        AssertNoItemOrTeleportPackets(harness.ClientSent, harness.ObserverSent);
    }

    [Fact]
    public async Task FrozenItemPersistenceFailureLeavesCharacterUntouchedAndSendsNoSuccessPackets()
    {
        using var harness = CreateHarness(
            FrozenFroto4NpcId,
            optionId: 1,
            Items(InventoryItem(10, GrottoKeyItemId, seed: 20)),
            includeObserver: true);
        using var locked = new FileStream(
            harness.ItemPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        Assert.True(await harness.InvokeAsync());

        AssertAtomicFailure(harness, FrozenFroto4NpcId, "item-persistence-");
    }

    [Fact]
    public async Task FrozenCharacterPersistenceFailureRollsBackItemAndSendsNoSuccessPackets()
    {
        using var harness = CreateHarness(
            FrozenFroto4NpcId,
            optionId: 1,
            Items(InventoryItem(10, GrottoKeyItemId, seed: 21)),
            includeObserver: true);
        using var locked = new FileStream(
            harness.CharacterPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        Assert.True(await harness.InvokeAsync());

        AssertAtomicFailure(harness, FrozenFroto4NpcId, "character-persistence-");
    }

    [Fact]
    public async Task FrozenSessionCasChangeAfterDurableWritesRollsBackBothFilesWithoutSuccessPackets()
    {
        using var harness = CreateHarness(
            FrozenFroto4NpcId,
            optionId: 1,
            Items(InventoryItem(10, GrottoKeyItemId, seed: 22)),
            includeObserver: true);

        var race = await harness.InvokeWithSessionCasChangeAsync();

        Assert.True(race.Handled);
        Assert.Same(race.ReplacementSession, harness.CurrentSession);
        Assert.Equal(FrozenFroto4NpcId, harness.CurrentSession.ActiveNpcId);
        Assert.Equal(0, harness.SessionWriteCount);
        Assert.Equal(harness.OriginalItemBytes, File.ReadAllBytes(harness.ItemPath));
        Assert.Equal(harness.OriginalCharacterBytes, File.ReadAllBytes(harness.CharacterPath));
        AssertItemsEqual(harness.OriginalSession.PlayerItems, harness.ReloadItems());
        AssertCharacterUnchanged(harness.OriginalSession.Character, harness.ReloadCharacter());
        Assert.Empty(harness.ClientSent);
        Assert.Empty(harness.ObserverSent);
        Assert.Contains("session-commit-InvalidOperationException", harness.LogText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rollback=complete", harness.LogText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FrozenReplayAfterSuccessCannotConsumeOrTeleportAgainEvenWithStaleSession()
    {
        using var harness = CreateHarness(
            FrozenFroto4NpcId,
            optionId: 1,
            Items(
                InventoryItem(10, GrottoKeyItemId, seed: 23),
                InventoryItem(20, GrottoKeyItemId, seed: 24)),
            includeObserver: true);

        Assert.True(await harness.InvokeAsync());
        var committedSessionWriteCount = harness.SessionWriteCount;
        var committedSession = harness.CurrentSession;
        var clientFrames = harness.ClientSent.Select(bytes => bytes.ToArray()).ToArray();
        var observerFrames = harness.ObserverSent.Select(bytes => bytes.ToArray()).ToArray();
        var committedItemBytes = File.ReadAllBytes(harness.ItemPath);
        var committedCharacterBytes = File.ReadAllBytes(harness.CharacterPath);

        Assert.True(await harness.InvokeAsync());
        Assert.True(await harness.InvokeAsync(harness.OriginalSession));

        Assert.Same(committedSession, harness.CurrentSession);
        Assert.Null(harness.CurrentSession.ActiveNpcId);
        Assert.Equal(2, committedSessionWriteCount);
        Assert.Equal(committedSessionWriteCount, harness.SessionWriteCount);
        var remainingKey = Assert.Single(
            harness.CurrentSession.PlayerItems.InventoryItems,
            item => item.ItemId == GrottoKeyItemId);
        Assert.Equal(20U, remainingKey.Uid);
        Assert.Equal(committedItemBytes, File.ReadAllBytes(harness.ItemPath));
        Assert.Equal(committedCharacterBytes, File.ReadAllBytes(harness.CharacterPath));
        Assert.Equal(clientFrames.Length, harness.ClientSent.Count);
        Assert.Equal(observerFrames.Length, harness.ObserverSent.Count);
        for (var index = 0; index < clientFrames.Length; index++)
        {
            Assert.Equal(clientFrames[index], harness.ClientSent[index]);
        }

        for (var index = 0; index < observerFrames.Length; index++)
        {
            Assert.Equal(observerFrames[index], harness.ObserverSent[index]);
        }

        Assert.Single(harness.ClientSent, IsRemoveInventoryPacket);
        Assert.Single(harness.ClientSent, IsTeleportPacket);
        Assert.Single(harness.ObserverSent, IsRemoveEntityPacket);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private FrozenGrottoHarness CreateHarness(
        uint npcId,
        byte optionId,
        PlayerItemSnapshot initialItems,
        bool includeObserver)
    {
        var caseRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(caseRoot);
        return new FrozenGrottoHarness(caseRoot, npcId, optionId, initialItems, includeObserver);
    }

    private static void AssertKeySuccessPacketOrder(
        FrozenGrottoHarness harness,
        Action<byte[]> itemMutationAssertion)
    {
        Assert.Equal(6, harness.ClientSent.Count);
        Assert.Equal(harness.ObserverSent.Count == 0 ? 6 : 7, harness.Wire.Count);

        Assert.Equal(WireEndpoint.Client, harness.Wire[0].Endpoint);
        itemMutationAssertion(harness.Wire[0].Bytes);
        var transitionWireIndex = 1;
        if (harness.ObserverSent.Count > 0)
        {
            var observerFrame = Assert.Single(harness.ObserverSent);
            Assert.Equal(WireEndpoint.Observer, harness.Wire[1].Endpoint);
            Assert.Equal(observerFrame, harness.Wire[1].Bytes);
            Assert.True(LegacyRemoveEntityPacket.TryParse(ReadFrame(observerFrame), out var leave));
            Assert.Equal(CharacterUid, leave.Uid);
            transitionWireIndex = 2;
        }

        for (var clientIndex = 1; clientIndex < harness.ClientSent.Count; clientIndex++)
        {
            Assert.Equal(WireEndpoint.Client, harness.Wire[transitionWireIndex].Endpoint);
            Assert.Equal(harness.ClientSent[clientIndex], harness.Wire[transitionWireIndex].Bytes);
            transitionWireIndex++;
        }

        AssertTeleportTransition(harness.ClientSent.Skip(1).ToArray());
        Assert.DoesNotContain(harness.ClientSent, IsNpcReplyPacket);
        Assert.DoesNotContain(harness.ClientSent, bytes =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out _));
    }

    private static void AssertTeleportTransition(IReadOnlyList<byte[]> sent)
    {
        Assert.Equal(5, sent.Count);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[0]), out var teleport));
        Assert.Equal(CharacterUid, teleport.ObjId);
        Assert.Equal(MsgActionType.Teleport, teleport.Type);
        Assert.Equal((uint)DestinationMapId, teleport.DwParam);
        Assert.Equal(DestinationMapId, teleport.DwParam3);
        Assert.Equal((uint)DestinationX, teleport.PositionX);
        Assert.Equal((uint)DestinationY, teleport.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[1]), out var startup));
        Assert.Equal(CharacterUid, startup.ObjId);
        Assert.Equal(MsgActionType.LegacyMerchantStartup, startup.Type);
        Assert.Equal(2U, startup.DwParam);
        Assert.Equal(DestinationMapId, startup.DwParam3);
        Assert.Equal((uint)DestinationX, startup.PositionX);
        Assert.Equal((uint)DestinationY, startup.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[2]), out var stopVending));
        Assert.Equal(CharacterUid, stopVending.ObjId);
        Assert.Equal(MsgActionType.StopVending, stopVending.Type);
        Assert.Equal((uint)DestinationMapId, stopVending.DwParam);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[3]), out var color));
        Assert.Equal(CharacterUid, color.ObjId);
        Assert.Equal(MsgActionType.LegacyColor, color.Type);
        Assert.Equal((uint)DestinationX, color.PositionX);
        Assert.Equal((uint)DestinationY, color.PositionY);

        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(sent[4]), out var mapStatus));
        Assert.Equal(LegacyMapStatusPacket.FromMap(DestinationMapId, mapsById: null), mapStatus);
    }

    private static void AssertExactNpcDialog(
        IReadOnlyList<byte[]> sent,
        string text,
        string optionText,
        ushort avatarId)
    {
        Assert.Equal(4, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal(text, dialog.Text);

        var option = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, option.OptionId);
        Assert.Equal(optionText, option.Text);

        var avatar = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal(avatarId, avatar.InputMaxLength);
        AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Finish);
    }

    private static void AssertUnchangedWithoutCommit(FrozenGrottoHarness harness, uint expectedNpcId)
    {
        Assert.Same(harness.OriginalSession, harness.CurrentSession);
        Assert.Equal(expectedNpcId, harness.CurrentSession.ActiveNpcId);
        Assert.Equal(0, harness.SessionWriteCount);
        Assert.Equal(harness.OriginalItemBytes, File.ReadAllBytes(harness.ItemPath));
        Assert.Equal(harness.OriginalCharacterBytes, File.ReadAllBytes(harness.CharacterPath));
        AssertItemsEqual(harness.OriginalSession.PlayerItems, harness.ReloadItems());
        AssertCharacterUnchanged(harness.OriginalSession.Character, harness.ReloadCharacter());
        Assert.Empty(harness.ObserverSent);
    }

    private static void AssertAtomicFailure(
        FrozenGrottoHarness harness,
        uint expectedNpcId,
        string expectedReason)
    {
        AssertUnchangedWithoutCommit(harness, expectedNpcId);
        Assert.Empty(harness.ClientSent);
        AssertNoItemOrTeleportPackets(harness.ClientSent, harness.ObserverSent);
        Assert.Contains(expectedReason, harness.LogText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rollback=", harness.LogText, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertNoItemOrTeleportPackets(
        IEnumerable<byte[]> clientSent,
        IEnumerable<byte[]> observerSent)
    {
        Assert.DoesNotContain(clientSent, bytes =>
            MsgItemUsagePacket.TryParse(ReadFrame(bytes), out _) ||
            LegacyGameItemPacket.TryParse(ReadFrame(bytes), out _) ||
            MsgActionPacket.TryParse(ReadFrame(bytes), out _) ||
            LegacyMapStatusPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(observerSent, IsRemoveEntityPacket);
    }

    private static void AssertCharacterTeleported(CharacterSnapshot source, CharacterSnapshot actual)
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
        Assert.Equal(SourceMapId, actual.PreviousMapId);
        Assert.Equal(SourceX, actual.PreviousMapX);
        Assert.Equal(SourceY, actual.PreviousMapY);
        Assert.Equal("0", actual.Attributes["DynamicID"]);
        Assert.Equal("77", actual.Attributes["PDinamycID"]);
        AssertCharacterNonLocationFieldsEqual(source, actual, previousDynamicIdChanged: true);
    }

    private static void AssertCharacterUnchanged(CharacterSnapshot expected, CharacterSnapshot actual)
    {
        Assert.Equal(expected.MapId, actual.MapId);
        Assert.Equal(expected.X, actual.X);
        Assert.Equal(expected.Y, actual.Y);
        Assert.Equal(expected.PreviousMapId, actual.PreviousMapId);
        Assert.Equal(expected.PreviousMapX, actual.PreviousMapX);
        Assert.Equal(expected.PreviousMapY, actual.PreviousMapY);
        Assert.Equal(expected.Attributes["DynamicID"], actual.Attributes["DynamicID"]);
        AssertCharacterNonLocationFieldsEqual(expected, actual);
    }

    private static void AssertCharacterNonLocationFieldsEqual(
        CharacterSnapshot expected,
        CharacterSnapshot actual,
        bool previousDynamicIdChanged = false)
    {
        Assert.Equal(expected.Uid, actual.Uid);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Body, actual.Body);
        Assert.Equal(expected.Face, actual.Face);
        Assert.Equal(expected.Hair, actual.Hair);
        Assert.Equal(expected.Class, actual.Class);
        Assert.Equal(expected.FirstClass, actual.FirstClass);
        Assert.Equal(expected.SecondClass, actual.SecondClass);
        Assert.Equal(expected.Reborn, actual.Reborn);
        Assert.Equal(expected.Level, actual.Level);
        Assert.Equal(expected.Experience, actual.Experience);
        Assert.Equal(expected.Strength, actual.Strength);
        Assert.Equal(expected.Agility, actual.Agility);
        Assert.Equal(expected.Vitality, actual.Vitality);
        Assert.Equal(expected.Spirit, actual.Spirit);
        Assert.Equal(expected.AttributePoints, actual.AttributePoints);
        Assert.Equal(expected.MinHitPoints, actual.MinHitPoints);
        Assert.Equal(expected.MinMana, actual.MinMana);
        Assert.Equal(expected.MaxMana, actual.MaxMana);
        Assert.Equal(expected.Money, actual.Money);
        Assert.Equal(expected.ConquerPoints, actual.ConquerPoints);
        Assert.Equal(expected.BoundConquerPoints, actual.BoundConquerPoints);
        Assert.Equal(expected.BankConquerPoints, actual.BankConquerPoints);
        Assert.Equal(expected.PkPoints, actual.PkPoints);
        Assert.Equal(expected.Online, actual.Online);
        Assert.Equal(expected.ExpBallUsed, actual.ExpBallUsed);

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
        if (previousDynamicIdChanged)
        {
            mutableKeys.Add("PDinamycID");
        }
        Assert.Equal(
            expected.Attributes
                .Where(pair => !mutableKeys.Contains(pair.Key))
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase),
            actual.Attributes
                .Where(pair => !mutableKeys.Contains(pair.Key))
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase));
        Assert.Equal(
            expected.PaidNobility.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase),
            actual.PaidNobility.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase));
    }

    private static void AssertSideCollectionsEqual(PlayerItemSnapshot expected, PlayerItemSnapshot actual)
    {
        AssertItemsEqual(
            expected with { InventoryItems = [] },
            actual with { InventoryItems = [] });
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
        byte bound = 0,
        byte seed = 0)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = checked((ushort)(70 + seed)),
            MaximumDurability = checked((ushort)(90 + seed)),
            Position = 0,
            SocketProgress = checked((uint)(120 + seed)),
            SocketOne = 1,
            SocketTwo = 2,
            Effect = checked((uint)(10 + seed)),
            Plus = checked((byte)(seed % 12)),
            Bless = checked((byte)(seed % 7)),
            Bound = bound,
            Enchant = checked((byte)(seed % 20)),
            Suspicious = checked((byte)(seed % 2)),
            Locked = checked((byte)((seed + 1) % 2)),
            PlusProgress = checked((uint)(200 + seed)),
            Inscribed = checked((ushort)(seed % 2)),
            Activate = checked((uint)(seed % 2)),
            TimeLeftInMinutes = checked((uint)(300 + seed)),
            StackSize = stackSize,
            Color = checked((uint)(3 + seed % 6)),
            UnlockTimer = 400 + seed,
            ItemPoints = checked((uint)(500 + seed)),
            PurificationItemId = checked((uint)(600 + seed)),
            PurificationLevel = checked((uint)(1 + seed % 5)),
            PurificationDuration = checked((uint)(700 + seed)),
            PurificationAddedOn = 800 + seed,
            RefineryEffectId = checked((uint)(900 + seed)),
            RefineryEffectLevel = checked((uint)(1 + seed % 5)),
            RefineryEffectPercent = checked((uint)(10 + seed)),
            RefineryEffectPercent2 = checked((uint)(20 + seed)),
            RefineryEffectDuration = checked((uint)(1_000 + seed)),
            RefineryEffectAddedOn = 1_100 + seed,
            AnimaItemId = checked((uint)(1_200 + seed)),
            RuneExperience = checked((uint)(1_300 + seed)),
            Attribute1 = checked((uint)(1_400 + seed)),
            Attribute2 = checked((uint)(1_500 + seed)),
            Attribute3 = checked((uint)(1_600 + seed)),
            Attribute4 = checked((uint)(1_700 + seed)),
            Attribute5 = checked((uint)(1_800 + seed)),
            Expiration = 1_900 + seed,
            MythSoul = checked((uint)(2_000 + seed)),
            MythSoulPoints = checked((uint)(2_100 + seed)),
            PerfectionLevel = checked((uint)(seed % 10)),
            PerfectionProgress = checked((uint)(2_200 + seed)),
            OwnerUid = checked((uint)(3_000 + seed)),
            OwnerName = "Owner" + seed.ToString(CultureInfo.InvariantCulture),
            Signature = "Signature" + seed.ToString(CultureInfo.InvariantCulture),
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

    private static LegacyGameItemPacket AssertGameItem(byte[] bytes)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
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

    private static bool IsShadowJadeAddPacket(byte[] bytes)
    {
        return LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Mode == LegacyGameItemPacket.AddItemMode &&
            packet.ItemId == ShadowJadeItemId;
    }

    private static bool IsRemoveInventoryPacket(byte[] bytes)
    {
        return MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Action == MsgItemUsageAction.RemoveInventory;
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

    private static WorldDataCatalog CreateFrozenGrottoWorldData()
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = ShadowJadeItemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "ShadowJade";
        fields[ItemType.DurabilityFieldIndex] = "321";
        fields[46] = "0";
        var itemType = new ItemType(checked((int)ShadowJadeItemId), "ShadowJade", fields);

        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-14T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://labyrinth-frozen-grotto-mutations",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: [itemType],
            itemAddBonuses: [],
            levelExperience: [],
            runeLevelExperience: [],
            runeStorageAttributes: [],
            itemRefineEffects: [],
            itemRefineEffectEx: [],
            itemRefineUpgrades: [],
            transformInfo: [],
            furniture: [],
            hairColorTypes: [],
            hairFaceStorageTypes: [],
            coatStorageTypes: [],
            coatStorageAttributes: [],
            coatColorRules: [],
            xuanbaoAdditionAttributes: [],
            xuanbaoComposeAttributeLimits: [],
            xuanbaoRandomAttributes: [],
            christmasNpcs: [],
            tutorTypes: [],
            tutorBattleLimitTypes: [],
            beastAttributes: [],
            fateExperience: [],
            spiritRates: [],
            hundredWeapons: [],
            gouyuTypes: [],
            gouyuImmortals: [],
            specialEffectAttributes: [],
            daoqiDictTypes: [],
            syndicateFormationTypes: [],
            syndicateSkillTypes: [],
            disdainRules: [],
            jiangHuAttributes: [],
            jiangHuCultivateStatuses: [],
            refineryBoxes: [],
            refinerySouls: [],
            refineryMaterials: [],
            stateCountFiles: [],
            binarySeedFiles: [],
            emptySeedFiles: [],
            backupSeedFiles: [],
            pokerTables: [],
            rouletteTables: [],
            npcTimeEntries: [],
            activityTasks: new ActivityTaskCatalog(
                new ActivityTaskMetadata(0, string.Empty, 0, []),
                [],
                "fixture://activity"),
            meltingTypeRules: [],
            lotteryPrizes: [],
            arenaRecords: [],
            elitePkRecords: [],
            teamElitePkRecords: [],
            skillTeamPkRecords: [],
            classPkWarRecords: [],
            combatGearRanks: [],
            prestigeRanks: [],
            innerPowerRanks: [],
            runeRankings: [],
            staticStatues: [],
            newPlayerRewards: [],
            votes: [],
            couplesPkRecords: [],
            socialAssociations: [],
            guildWarInfo: EmptyGuildWarData("guildwar"),
            superGuildWarInfo: EmptyGuildWarData("superguildwar"),
            classLevelStats: [],
            professionLevelUps: [],
            professionLevelBenefits: [],
            professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [],
            questMissions: [],
            quizQuestions: [],
            magic: [],
            magicEffects: [],
            magicTypeOperations: [],
            titleTypes: [],
            combatGear: [],
            traps: [],
            npcs: [],
            sobNpcs: [],
            portals: [],
            maps: [],
            mapNames: [],
            monsters: [],
            monsterSpawns: [],
            lotteryDictionary: [],
            collectionRewardTiers: [],
            taskRewardTypes: [],
            taskRewardTypeIniEntries: [],
            neiGongHeader: new NeiGongHeader(
                0,
                [],
                "fixture://neigong",
                0,
                string.Empty,
                new Dictionary<string, string>()),
            neiGongSecrets: [],
            neiGongTypes: [],
            subProfessions: [],
            syndicateLevels: [],
            stageGoalInfo: new StageGoalInfo(0),
            stageGoalStages: [],
            stageGoalTasks: [],
            shops: [],
            shopItems: [],
            exchangeShopGoods: [],
            exchangeShopGoodsEx: [],
            exchangeShopLevels: [],
            honorShopItems: [],
            honorShopRecommendations: [],
            racePointShopItems: [],
            racePointShopRecommendations: [],
            goldenLeagueShopItems: [],
            goldenLeagueShopRecommendations: [],
            goldenLeagueShopConfig: new Dictionary<string, string>(),
            booths: [],
            boothItems: []);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private sealed class FrozenGrottoHarness : IDisposable
    {
        private readonly uint _npcId;
        private readonly byte _optionId;
        private readonly ConquerTcpConnection _connection;
        private readonly StringWriter _log = new();
        private bool? _sessionCommittedBeforeFirstSend;
        private bool _replaceSessionDuringCommit;
        private bool _publishingReplacement;
        private WorldSession? _replacementSession;

        public FrozenGrottoHarness(
            string caseRoot,
            uint npcId,
            byte optionId,
            PlayerItemSnapshot initialItems,
            bool includeObserver)
        {
            _npcId = npcId;
            _optionId = optionId;
            UsersRoot = Path.Combine(caseRoot, "Users");
            PlayerItemsRoot = Path.Combine(caseRoot, "PlayersItems");
            Directory.CreateDirectory(UsersRoot);
            CharacterPath = Path.Combine(UsersRoot, AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
            ItemPath = Path.Combine(PlayerItemsRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
            WriteCharacter(CharacterPath);
            var itemLoader = new PlayerItemSnapshotLoader(PlayerItemsRoot);
            itemLoader.SaveByCharacterUid(CharacterUid, initialItems);

            OriginalSession = new WorldSession(
                AccountId,
                Username: "labyrinth-frozen-grotto-test",
                Character: ReloadCharacter(),
                PlayerItems: ReloadItems(),
                PlayerSpells: PlayerSpellSnapshot.Empty,
                PlayerProficiencies: PlayerProficiencySnapshot.Empty,
                UsersRoot: UsersRoot,
                ActiveNpcId: npcId);
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
                    ReferenceEquals(previous, current) ||
                    _publishingReplacement)
                {
                    return;
                }

                if (_replaceSessionDuringCommit && current.ActiveNpcId is null)
                {
                    _replaceSessionDuringCommit = false;
                    _replacementSession = OriginalSession with
                    {
                        Username = "labyrinth-frozen-grotto-cas-replacement",
                    };
                    _publishingReplacement = true;
                    try
                    {
                        Sessions!.Set(_connection, _replacementSession);
                    }
                    finally
                    {
                        _publishingReplacement = false;
                    }

                    throw new InvalidOperationException("Injected session publication race.");
                }

                SessionWriteCount++;
                ItemsAtCommit = ReloadItems();
                CharacterAtCommit = ReloadCharacter();
                PersistenceMatchedAtCommit =
                    PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems)
                        .SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(ItemsAtCommit)) &&
                    CharacterMatchesSession(current.Character, CharacterAtCommit);
            });
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
                ObserverConnection = observerConnection;
                Sessions.Set(observerConnection, CreateObserverSession(OriginalSession.Character));
            }

            Context = new WorldPacketHandlerContext(
                _connection,
                _log,
                Session: OriginalSession,
                WorldData: CreateFrozenGrottoWorldData(),
                Sessions: Sessions,
                PlayerItemsRoot: PlayerItemsRoot,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
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

        private ConquerTcpConnection? ObserverConnection { get; }

        public List<byte[]> ClientSent { get; } = [];

        public List<byte[]> ObserverSent { get; } = [];

        public List<WireExposure> Wire { get; } = [];

        public int SessionWriteCount { get; private set; }

        public PlayerItemSnapshot? ItemsAtCommit { get; private set; }

        public CharacterSnapshot? CharacterAtCommit { get; private set; }

        public bool PersistenceMatchedAtCommit { get; private set; }

        public bool SessionCommittedBeforeFirstSend => _sessionCommittedBeforeFirstSend == true;

        public string LogText => _log.ToString();

        public WorldSession CurrentSession
        {
            get
            {
                Assert.True(Sessions.TryGet(_connection, out var session));
                return Assert.IsType<WorldSession>(session);
            }
        }

        public ValueTask<bool> InvokeAsync(WorldSession? suppliedSession = null)
        {
            var session = suppliedSession ?? CurrentSession;
            return WorldPacketHandlers.TryHandleLegacyLabyrinthNpcAsync(
                Context with { Session = session },
                session,
                _npcId,
                _optionId,
                CancellationToken.None);
        }

        public async Task<CasRaceResult> InvokeWithSessionCasChangeAsync()
        {
            _replaceSessionDuringCommit = true;
            var handled = await InvokeAsync().ConfigureAwait(false);
            return new CasRaceResult(handled, Assert.IsType<WorldSession>(_replacementSession));
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

        private static bool CharacterMatchesSession(CharacterSnapshot expected, CharacterSnapshot actual)
        {
            return expected.MapId == actual.MapId &&
                expected.X == actual.X &&
                expected.Y == actual.Y &&
                expected.PreviousMapId == actual.PreviousMapId &&
                expected.PreviousMapX == actual.PreviousMapX &&
                expected.PreviousMapY == actual.PreviousMapY &&
                string.Equals(
                    expected.Attributes.GetValueOrDefault("DynamicID"),
                    actual.Attributes.GetValueOrDefault("DynamicID"),
                    StringComparison.Ordinal);
        }

        private static WorldSession CreateObserverSession(CharacterSnapshot source)
        {
            var attributes = new Dictionary<string, string>(source.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["UID"] = ObserverUid.ToString(CultureInfo.InvariantCulture),
                ["Name"] = "FrozenGrottoObserver",
                ["X"] = (SourceX + 1).ToString(CultureInfo.InvariantCulture),
                ["Y"] = (SourceY + 1).ToString(CultureInfo.InvariantCulture),
            };
            var character = source with
            {
                Uid = ObserverUid,
                Name = "FrozenGrottoObserver",
                X = SourceX + 1,
                Y = SourceY + 1,
                SourceFile = "fixture",
                Attributes = attributes,
            };
            return new WorldSession(
                AccountId: ObserverUid,
                Username: "frozen-grotto-observer",
                Character: character,
                PlayerItems: PlayerItemSnapshot.Empty,
                PlayerSpells: PlayerSpellSnapshot.Empty,
                PlayerProficiencies: PlayerProficiencySnapshot.Empty);
        }

        private static void WriteCharacter(string path)
        {
            File.WriteAllText(
                path,
                "[Character]\r\n" +
                $"UID={CharacterUid}\r\n" +
                "Name=FrozenGrottoHero\r\n" +
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
                "QuestMarker=frozen-grotto\r\n" +
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

    private sealed record CasRaceResult(bool Handled, WorldSession ReplacementSession);
}
