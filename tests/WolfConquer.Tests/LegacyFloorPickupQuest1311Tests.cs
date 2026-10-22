using System.Buffers;
using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Native FloorMap's post-pickup quest action, not atomic item/quest persistence.
public sealed class LegacyFloorPickupQuest1311Tests : IDisposable
{
    private const uint Uid = 1_300_411, FloorUid = 900_411, ItemId = 711_352, QuestId = 1_311;
    private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-floor-quest1311-" + Guid.NewGuid().ToString("N"));
    private string QuestsRoot => Path.Combine(root, "Quests");
    private string QuestPath => Path.Combine(QuestsRoot, Uid + ".bin");
    private string UsersRoot => Path.Combine(root, "Users");
    private string ItemsRoot => Path.Combine(root, "Items");
    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> Handlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }

    [Theory]
    [InlineData("ordinary")]
    [InlineData("tails")]
    [InlineData("wrap")]
    [InlineData("empty")]
    public async Task AcceptedQuestIncrementsOneUncheckedUintAndPreservesTimeStatusTailsAndOtherQuests(string vector)
    {
        uint[] intentions = vector switch { "tails" => [7, 88, 99], "wrap" => [uint.MaxValue, 55], "empty" => [], _ => [7] };
        var f = Create(intentions);
        var expected = Incremented(f.Initial.Character.PlayerQuests);
        var originalBytes = PlayerQuestSnapshotLoader.Serialize(f.Initial.Character.PlayerQuests);

        await Pickup(f);

        AssertPickupCommitted(f);
        AssertQuestSaved(f, expected);
        Assert.Equal(originalBytes, PlayerQuestSnapshotLoader.Serialize(f.Initial.Character.PlayerQuests));
        var quest = GetQuest(expected);
        Assert.Equal(PlayerQuestStatus.Accepted, quest.Status);
        Assert.Equal(123U, quest.Time);
        Assert.Equal(MsgQuestDataPacket.CreateObjectiveFrame(quest), Assert.Single(QuestFrames(f)));
        Assert.Equal(4, f.Sent.Count);
        await AssertReplaySilent(f);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("finished")]
    [InlineData("available")]
    public async Task PickupDoesNotAutoAcceptOrAdvanceMissingFinishedOrAvailableQuest(string state)
    {
        var status = state == "missing" ? (PlayerQuestStatus?)null :
            state == "finished" ? PlayerQuestStatus.Finished : PlayerQuestStatus.Available;
        var f = Create([7, 88], status);
        var original = File.ReadAllBytes(QuestPath);

        await Pickup(f);

        AssertPickupCommitted(f);
        Assert.Equal(original, File.ReadAllBytes(QuestPath));
        Assert.Equal(original, PlayerQuestSnapshotLoader.Serialize(Current(f).Character.PlayerQuests));
        Assert.Equal(state != "missing", Current(f).Character.PlayerQuests.ContainsQuest(QuestId));
        Assert.Empty(QuestFrames(f));
        Assert.Equal(3, f.Sent.Count);
        await AssertReplaySilent(f);
    }

    [Fact]
    public async Task DifferentKnownItemDoesNotIncrementAcceptedQuest1311()
    {
        var f = Create([7], itemId: 50_000);
        var original = File.ReadAllBytes(QuestPath);
        await Pickup(f);
        AssertPickupCommitted(f);
        Assert.Equal(original, File.ReadAllBytes(QuestPath));
        Assert.Equal(original, PlayerQuestSnapshotLoader.Serialize(Current(f).Character.PlayerQuests));
        Assert.Empty(QuestFrames(f));
        Assert.Equal(3, f.Sent.Count);
    }

    [Theory]
    [InlineData("unknown-catalog")]
    [InlineData("identity")]
    [InlineData("inventory-full")]
    public async Task ActualPickupDenialCannotChangeQuestFileOrQuestSessionOrSendObjective(string denial)
    {
        var f = Create([7, 88]);
        var context = f.Context;
        if (denial == "unknown-catalog") context = context with { WorldData = LegacyItemWorldDataFixture.Create() };
        else if (denial == "identity") f.Client.Player.ServerID = 43;
        else
        {
            var current = Current(f);
            f.Sessions.Set(f.Connection, current with { PlayerItems = current.PlayerItems with
            { InventoryItems = Enumerable.Range(0, 40).Select(index => new PlayerItemSnapshotItem
                { Uid = (uint)(610_000 + index), ItemId = ItemId, StackSize = 1 }).ToArray() } });
            new PlayerItemSnapshotLoader(ItemsRoot).SaveByCharacterUid(Uid, Current(f).PlayerItems);
        }
        var before = Current(f);
        var questBytes = File.ReadAllBytes(QuestPath);
        var itemBytes = PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems);
        var characterBytes = File.ReadAllBytes(f.CharacterPath);
        var persistedItems = File.ReadAllBytes(f.ItemPath);

        await Pickup(f, context);

        Assert.Same(before, Current(f));
        Assert.Equal(itemBytes, PlayerItemSnapshotLoader.ToLegacyBytes(Current(f).PlayerItems));
        Assert.Equal(questBytes, PlayerQuestSnapshotLoader.Serialize(Current(f).Character.PlayerQuests));
        Assert.Equal(questBytes, File.ReadAllBytes(QuestPath));
        Assert.Same(f.Entry, Assert.Single(f.Floor.SnapshotVisible(DateTimeOffset.UtcNow)));
        Assert.Empty(f.Sent);
        Assert.Equal(characterBytes, File.ReadAllBytes(f.CharacterPath));
        Assert.Equal(persistedItems, File.ReadAllBytes(f.ItemPath));
    }

    [Fact]
    public async Task StaleQuestFileRefusesQuestPublicationWithoutUndoingTheAlreadyCommittedItemPickup()
    {
        var f = Create([7, 88]);
        var stale = f.Initial.Character.PlayerQuests.WithQuest(GetQuest(f.Initial.Character.PlayerQuests) with { Intentions = [91, 88] });
        new PlayerQuestSnapshotLoader(QuestsRoot).SaveByCharacterUid(Uid, stale);
        var diskBytes = File.ReadAllBytes(QuestPath);

        await Pickup(f);

        AssertQuestSaveFailureLeavesPickup(f, diskBytes);
        Assert.Contains("questPersistenceBaselineStale", f.Log.ToString(), StringComparison.Ordinal);
        await AssertReplaySilent(f);
        Assert.Equal(diskBytes, File.ReadAllBytes(QuestPath));
    }

    [WindowsQuestPickupFact]
    public async Task LockedQuestReplacementRetainsQuestPreimageButDoesNotRollbackItemPickupOrRetryOnReplay()
    {
        var f = Create([7, 88]);
        var original = File.ReadAllBytes(QuestPath);
        using (File.Open(QuestPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await Pickup(f);
            AssertQuestSaveFailureLeavesPickup(f, original);
            Assert.Contains("questPersistence:", f.Log.ToString(), StringComparison.Ordinal);
        }
        await AssertReplaySilent(f);
        Assert.Equal(original, File.ReadAllBytes(QuestPath));
    }

    [Fact]
    public async Task MissingAcceptedQuestFileCannotBeCreatedFromStaleSessionButItemPickupRemainsCommitted()
    {
        var f = Create([7, 88]);
        File.Delete(QuestPath); // Only this test's disposable synthetic quest file.
        await Pickup(f);
        AssertPickupCommitted(f);
        Assert.False(File.Exists(QuestPath));
        Assert.Equal(f.InitialQuestBytes, PlayerQuestSnapshotLoader.Serialize(Current(f).Character.PlayerQuests));
        Assert.Empty(QuestFrames(f));
        Assert.Equal(3, f.Sent.Count);
        Assert.Contains("questPersistenceBaselineMissing", f.Log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task QuestFileAndHolderPublishAfterPickupMessagesButBeforeObjectiveWithoutOrdinaryAutosave()
    {
        Fixture? f = null;
        Exception? callbackFailure = null;
        var pickupMessages = 0;
        var objectiveMessages = 0;
        f = Create([7, 88], beforeSend: bytes => callbackFailure ??= Record.Exception(() =>
        {
            Assert.NotNull(f);
            AssertPickupInventory(f);
            if (ReadFrame(bytes).Header.Opcode == MsgQuestDataPacket.Opcode)
            {
                objectiveMessages++;
                Assert.Equal(3, pickupMessages);
                AssertQuestSaved(f, Incremented(f.Initial.Character.PlayerQuests));
            }
            else
            {
                pickupMessages++;
                Assert.Equal(f.InitialQuestBytes, File.ReadAllBytes(QuestPath));
                Assert.Equal(f.InitialQuestBytes,
                    PlayerQuestSnapshotLoader.Serialize(Current(f).Character.PlayerQuests));
            }
        }));

        await Pickup(f);

        Assert.Null(callbackFailure); // Assertions cannot disappear inside a dispatch callback.
        Assert.Equal(3, pickupMessages);
        Assert.Equal(1, objectiveMessages);
        Assert.Single(f.OrdinaryPublications); // Initial Set only; persisted pickup/quest commits both bypass autosave.
        Assert.All(f.OrdinaryPublications, session => Assert.Equal(
            f.InitialQuestBytes, PlayerQuestSnapshotLoader.Serialize(session.Character.PlayerQuests)));
        Assert.Equal(MsgQuestDataPacket.Opcode, ReadFrame(f.Sent.Last()).Header.Opcode);
        AssertQuestSaved(f, Incremented(f.Initial.Character.PlayerQuests));
    }

    [Fact]
    public async Task ExplicitMissingQuestRootUsesLoggedSessionOnlyUpdateWithoutClaimingDiskDurability()
    {
        var f = Create([7], configuredRoot: false);
        var original = File.ReadAllBytes(QuestPath);
        await Pickup(f);
        AssertPickupCommitted(f);
        var expected = Incremented(f.Initial.Character.PlayerQuests);
        Assert.Equal(PlayerQuestSnapshotLoader.Serialize(expected), PlayerQuestSnapshotLoader.Serialize(Current(f).Character.PlayerQuests));
        Assert.Equal(original, File.ReadAllBytes(QuestPath));
        Assert.Single(QuestFrames(f));
        Assert.Contains("session-only", f.Log.ToString(), StringComparison.OrdinalIgnoreCase);
        await AssertReplaySilent(f);
    }

    private void AssertQuestSaveFailureLeavesPickup(Fixture f, byte[] diskBytes)
    {
        // Pickup is already committed. This assertion deliberately does NOT
        // promise rollback or multi-file atomicity between items and quests.
        AssertPickupCommitted(f);
        Assert.Equal(diskBytes, File.ReadAllBytes(QuestPath));
        Assert.Equal(f.InitialQuestBytes,
            PlayerQuestSnapshotLoader.Serialize(Current(f).Character.PlayerQuests));
        Assert.Empty(QuestFrames(f));
        Assert.Equal(3, f.Sent.Count);
    }

    private static void AssertPickupInventory(Fixture f)
    {
        Assert.Contains(Current(f).PlayerItems.InventoryItems, item => item.Uid == f.Entry.Item!.Uid && item.StackSize == 3);
        Assert.Equal(2, Current(f).PlayerItems.InventoryItems.Count);
        Assert.Equal(f.Initial.Character.Money, Current(f).Character.Money);
        Assert.Equal(f.Initial.Character.ConquerPoints, Current(f).Character.ConquerPoints);
        Assert.Empty(f.Floor.SnapshotVisible(DateTimeOffset.UtcNow));
        var expectedItems = PlayerItemSnapshotLoader.ToLegacyBytes(f.Initial.PlayerItems with
            { InventoryItems = [.. f.Initial.PlayerItems.InventoryItems, f.Entry.Item!.WithPosition(0)] });
        // Independent native Send oracle: only ordinary quest-item Plus resets.
        expectedItems[sizeof(int) + f.Initial.PlayerItems.InventoryItems.Count * PlayerItemSnapshotLoader.LegacyDbItemSize + 28] = 0;
        Assert.Equal(expectedItems, PlayerItemSnapshotLoader.ToLegacyBytes(Current(f).PlayerItems));
        Assert.Equal(expectedItems, File.ReadAllBytes(f.ItemPath));
        Assert.Equal(expectedItems, PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshotLoader(f.ItemsRoot).LoadByCharacterUid(Uid)));
        Assert.Equal(f.InitialCharacterBytes, File.ReadAllBytes(f.CharacterPath));
    }

    private static void AssertPickupCommitted(Fixture f)
    {
        AssertPickupInventory(f);
        var frames = f.Sent.Select(ReadFrame).Take(3).ToArray();
        Assert.Equal(3, frames.Length);
        Assert.True(LegacyGameItemPacket.TryParse(frames[0], out var item));
        Assert.Equal(f.Entry.Item!.Uid, item.Uid);
        Assert.True(LegacyFloorItemPacket.TryParse(frames[1], out var floor));
        Assert.Equal(LegacyFloorItemDropType.Remove, floor.DropType);
        Assert.True(LegacyMsgMessagePacket.TryParse(frames[2], out _));
    }

    private void AssertQuestSaved(Fixture f, PlayerQuestSnapshot expected)
    {
        var bytes = PlayerQuestSnapshotLoader.Serialize(expected);
        Assert.Equal(bytes, File.ReadAllBytes(QuestPath));
        Assert.Equal(bytes, PlayerQuestSnapshotLoader.Serialize(new PlayerQuestSnapshotLoader(QuestsRoot).LoadByCharacterUid(Uid)));
        Assert.Equal(bytes, PlayerQuestSnapshotLoader.Serialize(Current(f).Character.PlayerQuests));
    }

    private async Task AssertReplaySilent(Fixture f)
    {
        var before = Current(f);
        var disk = File.ReadAllBytes(QuestPath);
        var characterBytes = File.ReadAllBytes(f.CharacterPath);
        var itemBytes = File.ReadAllBytes(f.ItemPath);
        var frames = f.Sent.Count;
        await Pickup(f);
        Assert.Same(before, Current(f));
        Assert.Equal(frames, f.Sent.Count);
        Assert.Equal(disk, File.ReadAllBytes(QuestPath));
        Assert.Equal(characterBytes, File.ReadAllBytes(f.CharacterPath));
        Assert.Equal(itemBytes, File.ReadAllBytes(f.ItemPath));
    }

    private static PlayerQuestSnapshot Incremented(PlayerQuestSnapshot original)
    {
        var quest = GetQuest(original);
        var values = quest.Intentions.Count == 0 ? new uint[1] : quest.Intentions.ToArray();
        values[0] = unchecked(values[0] + 1);
        return original.WithQuest(quest with { Intentions = values });
    }

    private static PlayerQuestSnapshotEntry GetQuest(PlayerQuestSnapshot quests)
    { Assert.True(quests.TryGetQuest(QuestId, out var quest)); return quest; }
    private static byte[][] QuestFrames(Fixture f) => f.Sent.Where(bytes => ReadFrame(bytes).Header.Opcode == MsgQuestDataPacket.Opcode).ToArray();
    private static WorldSession Current(Fixture f)
    { Assert.True(f.Sessions.TryGet(f.Connection, out var current)); return Assert.IsType<WorldSession>(current); }
    private static async Task Pickup(Fixture f, WorldPacketHandlerContext? context = null)
    {
        var writer = new PacketWriter(LegacyFloorItemPacket.Opcode, sizeof(uint)); writer.WriteUInt32(0, FloorUid);
        await Handlers.HandleAsync(context ?? f.Context, ReadFrame(writer.ToArray()));
    }

    private Fixture Create(uint[] intentions, PlayerQuestStatus? status = PlayerQuestStatus.Accepted,
        uint itemId = ItemId, bool configuredRoot = true, Action<byte[]>? beforeSend = null)
    {
        Directory.CreateDirectory(QuestsRoot);
        Directory.CreateDirectory(UsersRoot);
        Directory.CreateDirectory(ItemsRoot);
        var characterPath = Path.Combine(UsersRoot, Uid + ".ini");
        var itemPath = Path.Combine(ItemsRoot, Uid + ".bin");
        var quests = new PlayerQuestSnapshot([new(2001, PlayerQuestStatus.Finished, 77, [9, 8]), new(2002, PlayerQuestStatus.Accepted, 66, [4])]);
        if (status is { } actualStatus) quests = quests.WithQuest(new(QuestId, actualStatus, 123, intentions));
        new PlayerQuestSnapshotLoader(QuestsRoot).SaveByCharacterUid(Uid, quests);
        var client = new LegacyNativeGameClientRuntime(); client.Player.UID = Uid;
        var sent = new ConcurrentQueue<byte[]>();
        var connection = new ConquerTcpConnection(null, null, (bytes, _) =>
        { var copy = bytes.ToArray(); beforeSend?.Invoke(copy); sent.Enqueue(copy); return ValueTask.CompletedTask; }, nativeGameClient: client);
        var text = "[Character]\nUID=" + Uid + "\nName=FloorQuest\n" + string.Join('\n', new[]
        {
            "Body=1008", "Face=154", "Haire=27", "Class=10", "FirstClass=0", "SecoundeClass=0", "Reborn=0",
            "Level=100", "Experience=0", "Map=1002", "X=430", "Y=380", "PMap=1002", "PMapX=430", "PMapY=380",
            "Strength=10", "Agility=10", "Vitaliti=10", "Spirit=10", "Atributes=0", "MinHitPoints=330", "MinMana=100",
            "Money=100", "ConquerPoints=50", "BoundConquerPoints=0", "BankCps=0", "PkPoints=0", "Online=1", "WHMoney=25", "SecurityPass=0,0,0,",
        });
        File.WriteAllText(characterPath, text);
        var initial = new WorldSession(Uid, "fixture", CharacterSnapshotParser.ParseText(text) with { PlayerQuests = quests },
            new PlayerItemSnapshot([new PlayerItemSnapshotItem { Uid = 620_412, ItemId = itemId, StackSize = 2 }], [], [], [], []),
            PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty, UsersRoot: UsersRoot) { NativeGameClient = client };
        new PlayerItemSnapshotLoader(ItemsRoot).SaveByCharacterUid(Uid, initial.PlayerItems);
        var publications = new List<WorldSession>();
        var sessions = new WorldSessionStore((_, _, updated) => publications.Add(updated)); sessions.Set(connection, initial);
        var identity = new LegacyLocalServerIdentity(42);
        Assert.True(LegacyServerIdentityRuntime.TryInitialize(connection, initial, identity));
        var packet = new LegacyFloorItemPacket(FloorUid, itemId, 430, 380, 40, LegacyFloorItemDropType.Visible, 0, 2, Uid, 0, 0, 0, 0, 0, "");
        var entry = FloorItemRuntimeEntry.ForItem(1002, packet, new PlayerItemSnapshotItem
            { Uid = 620_413, ItemId = itemId, Durability = 10, MaximumDurability = 10, StackSize = 3, Plus = 2 });
        var floor = new FloorItemRuntimeStateStore(); floor.AddVisible(entry);
        var fields = Enumerable.Repeat("0", 66).ToArray(); fields[0] = itemId.ToString(); fields[1] = "QuestPickup";
        fields[ItemType.DurabilityFieldIndex] = "10";
        var data = LegacyItemWorldDataFixture.Create(new ItemType((int)itemId, fields[1], fields));
        var log = new StringWriter();
        var context = new WorldPacketHandlerContext(connection, log, initial, WorldData: data, Sessions: sessions,
            FloorItems: floor, PlayerItemsRoot: ItemsRoot, PlayerQuestsRoot: configuredRoot ? QuestsRoot : null,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13)
            { LocalServerIdentity = identity };
        return new(connection, client, initial, sessions, floor, entry, sent, log, context, publications,
            PlayerQuestSnapshotLoader.Serialize(quests), characterPath, ItemsRoot, itemPath, File.ReadAllBytes(characterPath));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    { Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _)); return frame; }
    private sealed record Fixture(ConquerTcpConnection Connection, LegacyNativeGameClientRuntime Client, WorldSession Initial,
        WorldSessionStore Sessions, FloorItemRuntimeStateStore Floor, FloorItemRuntimeEntry Entry, ConcurrentQueue<byte[]> Sent,
        StringWriter Log, WorldPacketHandlerContext Context, List<WorldSession> OrdinaryPublications, byte[] InitialQuestBytes,
        string CharacterPath, string ItemsRoot, string ItemPath, byte[] InitialCharacterBytes);
    private sealed class WindowsQuestPickupFactAttribute : FactAttribute
    { public WindowsQuestPickupFactAttribute() { if (!OperatingSystem.IsWindows()) Skip = "Requires Windows replacement-sharing refusal."; } }
}
