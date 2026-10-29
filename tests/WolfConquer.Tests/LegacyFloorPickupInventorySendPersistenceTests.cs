using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyFloorPickupInventoryPersistenceTests
{
    // Native MsgGameItem.Send233..264 and TimeLeftInMinutes182..195. Expected
    // records below use literal DBItem184/Perfection80 offsets, not the planner,
    // projection, copy helpers, or production serializer as a postimage oracle.
    [Theory]
    [InlineData("append", 0)]
    [InlineData("append", 211)]
    [InlineData("merge", 0)]
    [InlineData("merge", 211)]
    [InlineData("whole-stack", 0)]
    [InlineData("whole-stack", 211)]
    public void SendDurabilityAndNonEquipmentPlusNormalizationHaveExactDurableImages(string path, ushort maximum)
    {
        var (original, entry, inventory, source) = CreateSendPersistencePlan(path, ItemId, maximum: maximum);
        var character = File.ReadAllBytes(CharacterPath);
        var beforeItems = File.ReadAllBytes(ItemPath);
        var expected = ExpectedSendImage(beforeItems, path, source, maximum == 0 ? (ushort)321 : maximum,
            plus: 0, activate: 0, minutes: 77, expiration: source.Expiration);

        Assert.Equal(path == "merge", inventory.IsMerge);
        Assert.Equal(maximum == 0 ? (ushort)321 : maximum, inventory.Item.MaximumDurability);
        Assert.Equal((byte)0, inventory.Item.Plus);
        Assert.True(Persist(original, entry, inventory, out var reason), reason);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(expected, File.ReadAllBytes(ItemPath));
        Assert.False(LegacyTradeJournal.HasPending(Users));
    }

    [Theory]
    [InlineData("append", 730000, 0)]
    [InlineData("append", 730001, 1)]
    [InlineData("append", 730002, 2)]
    [InlineData("append", 730003, 3)]
    [InlineData("append", 730004, 4)]
    [InlineData("append", 730005, 5)]
    [InlineData("append", 730006, 6)]
    [InlineData("append", 730007, 7)]
    [InlineData("append", 730008, 8)]
    [InlineData("append", 730009, 0)]
    [InlineData("append", 410003, 9)]
    [InlineData("merge", 730000, 0)]
    [InlineData("merge", 730001, 1)]
    [InlineData("merge", 730002, 2)]
    [InlineData("merge", 730003, 3)]
    [InlineData("merge", 730004, 4)]
    [InlineData("merge", 730005, 5)]
    [InlineData("merge", 730006, 6)]
    [InlineData("merge", 730007, 7)]
    [InlineData("merge", 730008, 8)]
    [InlineData("merge", 730009, 0)]
    [InlineData("merge", 410003, 9)]
    [InlineData("whole-stack", 730001, 1)]
    [InlineData("whole-stack", 730008, 8)]
    public void SendPlusOverrideAndItsNeighborBoundariesPersistWithoutChangingOtherMetadata(
        string path, int itemId, byte expectedPlus)
    {
        var (original, entry, inventory, source) = CreateSendPersistencePlan(path, itemId, maximum: 211);
        var character = File.ReadAllBytes(CharacterPath);
        var expected = ExpectedSendImage(File.ReadAllBytes(ItemPath), path, source, maximum: 211,
            plus: expectedPlus, activate: 0, minutes: 77, expiration: source.Expiration);

        Assert.Equal(expectedPlus, inventory.Item.Plus);
        Assert.Equal((ushort)211, inventory.Item.MaximumDurability);
        Assert.True(Persist(original, entry, inventory, out var reason), reason);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(expected, File.ReadAllBytes(ItemPath));
    }

    [Theory]
    [InlineData(350001, "append", 0)]
    [InlineData(370001, "append", 0)]
    [InlineData(350001, "merge", 0)]
    [InlineData(370001, "merge", 0)]
    [InlineData(350001, "whole-stack", 0)]
    [InlineData(370001, "whole-stack", 0)]
    [InlineData(350001, "append", 60)]
    [InlineData(370001, "append", 60)]
    [InlineData(350001, "merge", 60)]
    [InlineData(370001, "merge", 60)]
    [InlineData(350001, "whole-stack", 60)]
    [InlineData(370001, "whole-stack", 60)]
    public void AccessoriesPersistActivatedWithZeroSavedMinutesAndTheNativeAddOrWholeStackExpiry(
        int itemId, string path, uint catalogMinutes)
    {
        var (original, entry, inventory, source) = CreateSendPersistencePlan(path, itemId, minutes: catalogMinutes);
        var character = File.ReadAllBytes(CharacterPath);
        var expectedExpiry = catalogMinutes != 0 && path != "whole-stack"
            ? Now.AddMinutes(catalogMinutes).ToBinary() : source.Expiration;
        var expected = ExpectedSendImage(File.ReadAllBytes(ItemPath), path, source, maximum: 321,
            plus: 9, activate: 1, minutes: 0, expiration: expectedExpiry);

        Assert.Equal(path == "merge", inventory.IsMerge);
        Assert.Equal(1u, inventory.Item.Activate);
        Assert.Equal(0u, inventory.Item.TimeLeftInMinutes);
        Assert.Equal(expectedExpiry, inventory.Item.Expiration);
        Assert.True(Persist(original, entry, inventory, out var reason), reason);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(expected, File.ReadAllBytes(ItemPath));
    }

    [Theory]
    [InlineData("append", 1)]
    [InlineData("append", 2)]
    [InlineData("append", 7)]
    [InlineData("merge", 1)]
    [InlineData("merge", 2)]
    [InlineData("merge", 7)]
    [InlineData("whole-stack", 1)]
    [InlineData("whole-stack", 2)]
    [InlineData("whole-stack", 7)]
    public void ExistingActiveNonAccessoryPickupPersistsZeroPublicMinutesWithoutChangingActivationOrExpiry(
        string path, uint activate)
    {
        var itemId = path == "merge" ? 730008 : path == "whole-stack" ? 410003 : ItemId;
        var maximum = path == "whole-stack" ? (ushort)211 : (ushort)0;
        var expectedMaximum = maximum == 0 ? (ushort)321 : maximum;
        var expectedPlus = path == "merge" ? (byte)8 : path == "whole-stack" ? (byte)9 : (byte)0;
        var (original, entry, inventory, source) = CreateSendPersistencePlan(path, itemId,
            maximum: maximum, existingActivate: activate);
        var character = File.ReadAllBytes(CharacterPath);
        var beforeItems = File.ReadAllBytes(ItemPath);
        var expected = ExpectedSendImage(beforeItems, path, source, expectedMaximum,
            expectedPlus, activate, minutes: 0, expiration: source.Expiration);

        Assert.Equal(path == "merge", inventory.IsMerge);
        Assert.Equal(activate, source.Activate);
        Assert.Equal(77u, source.TimeLeftInMinutes);
        if (path == "merge")
        {
            Assert.Equal(activate, BinaryPrimitives.ReadUInt32LittleEndian(beforeItems.AsSpan(sizeof(int) + 44)));
            Assert.Equal(77u, BinaryPrimitives.ReadUInt32LittleEndian(beforeItems.AsSpan(sizeof(int) + 48)));
        }
        Assert.Equal(activate, inventory.Item.Activate);
        Assert.Equal(source.Expiration, inventory.Item.Expiration);
        Assert.Equal(0u, inventory.Item.TimeLeftInMinutes);
        Assert.Equal(expectedMaximum, inventory.Item.MaximumDurability);
        Assert.Equal(expectedPlus, inventory.Item.Plus);
        Assert.True(Persist(original, entry, inventory, out var reason), reason);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(expected, File.ReadAllBytes(ItemPath));
        Assert.Equal(activate, source.Activate);
        Assert.Equal(77u, source.TimeLeftInMinutes);
        Assert.False(LegacyTradeJournal.HasPending(Users));
    }

    [Theory]
    [InlineData("append", 1)]
    [InlineData("append", 2)]
    [InlineData("append", 7)]
    [InlineData("merge", 1)]
    [InlineData("merge", 2)]
    [InlineData("merge", 7)]
    [InlineData("whole-stack", 1)]
    [InlineData("whole-stack", 2)]
    [InlineData("whole-stack", 7)]
    public void ClaimedNonzeroPublicMinutesForAnAlreadyActiveNonAccessoryCannotWriteAnything(
        string path, uint activate)
    {
        var (original, entry, inventory, source) = CreateSendPersistencePlan(path, ItemId,
            existingActivate: activate);
        var forgedRecord = SendDbRecordOracle(inventory.Item, minutes: 77);
        var forgedItem = ParseSendFixtureRecord(forgedRecord, SendPerfectionRecordOracle(inventory.Item));
        var forged = inventory with
        {
            Item = forgedItem,
            UpdatedItems = inventory.UpdatedItems with
            {
                InventoryItems = inventory.UpdatedItems.InventoryItems
                    .Select(item => item.Uid == inventory.Item.Uid ? forgedItem : item).ToArray(),
            },
        };

        Assert.Equal(activate, forgedItem.Activate);
        Assert.Equal(source.Expiration, forgedItem.Expiration);
        Assert.Equal(77u, forgedItem.TimeLeftInMinutes);
        AssertSendRejectedWithExactJournal(original, entry, forged);
    }

    [Theory]
    [InlineData("append", "maximum")]
    [InlineData("append", "plus-reset")]
    [InlineData("append", "plus-override")]
    [InlineData("append", "activate")]
    [InlineData("append", "saved-minutes")]
    [InlineData("append", "pre-normalization")]
    [InlineData("append", "unrelated-metadata")]
    [InlineData("merge", "maximum")]
    [InlineData("merge", "plus-reset")]
    [InlineData("merge", "plus-override")]
    [InlineData("merge", "activate")]
    [InlineData("merge", "saved-minutes")]
    [InlineData("merge", "pre-normalization")]
    [InlineData("merge", "unrelated-metadata")]
    [InlineData("whole-stack", "maximum")]
    [InlineData("whole-stack", "plus-reset")]
    [InlineData("whole-stack", "plus-override")]
    [InlineData("whole-stack", "activate")]
    [InlineData("whole-stack", "saved-minutes")]
    [InlineData("whole-stack", "pre-normalization")]
    [InlineData("whole-stack", "unrelated-metadata")]
    public void PreNormalizationOrForgedSendPostimagesCannotWriteAssetsOrAlterTheJournal(string path, string mutation)
    {
        var itemId = mutation is "activate" or "saved-minutes" ? 350001 :
            mutation == "plus-override" ? 730008 : ItemId;
        var (original, entry, inventory, source) = CreateSendPersistencePlan(path, itemId);
        var record = SendDbRecordOracle(inventory.Item);
        if (mutation == "maximum") BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(10), 0);
        else if (mutation is "plus-reset" or "plus-override") record[28] = 9;
        else if (mutation == "activate") BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(44), 0);
        else if (mutation == "saved-minutes") BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(48), uint.MaxValue);
        else if (mutation == "unrelated-metadata") record[164] ^= 1;
        else record = SendDbRecordOracle(source, position: 0,
            stack: path == "merge" ? checked((ushort)(source.StackSize + 1)) : source.StackSize);
        var forgedItem = ParseSendFixtureRecord(record, SendPerfectionRecordOracle(source));
        var forged = inventory with
        {
            Item = forgedItem,
            UpdatedItems = inventory.UpdatedItems with
            {
                InventoryItems = inventory.UpdatedItems.InventoryItems
                    .Select(item => item.Uid == inventory.Item.Uid ? forgedItem : item).ToArray(),
            },
        };
        AssertSendRejectedWithExactJournal(original, entry, forged);
    }

    [Fact]
    public void APreviouslyPendingJournalIsNotOverwrittenOrRecoveredByPickupPersistence()
    {
        var (original, entry, inventory, _) = CreateSendPersistencePlan("append", ItemId);
        var journalRoot = Path.Combine(Users, ".managed-trade-journal");
        Directory.CreateDirectory(journalRoot);
        var journalPath = Path.Combine(journalRoot, "pending.json");
        var pending = Encoding.UTF8.GetBytes("{\"syntheticPendingSentinel\":\"must-not-be-overwritten\"}\n");
        File.WriteAllBytes(journalPath, pending);
        var character = File.ReadAllBytes(CharacterPath);
        var items = File.ReadAllBytes(ItemPath);

        Assert.False(Persist(original, entry, inventory, out var reason));
        Assert.StartsWith(LegacyTradeJournal.RecoveryRequired, reason);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(items, File.ReadAllBytes(ItemPath));
        Assert.Equal(pending, File.ReadAllBytes(journalPath));
        Assert.Equal(new[] { "pending.json" }, Directory.GetFiles(journalRoot).Select(Path.GetFileName).ToArray());
    }

    [Theory]
    [InlineData("append", 0)]
    [InlineData("append", 1)]
    [InlineData("append", 2)]
    [InlineData("merge", 0)]
    [InlineData("merge", 1)]
    [InlineData("merge", 2)]
    [InlineData("whole-stack", 0)]
    [InlineData("whole-stack", 1)]
    [InlineData("whole-stack", 2)]
    public void InterruptedNormalizedPickupRecoversTheExactNativeSendImageOnlyOnce(string path, int replacementCount)
    {
        var itemId = path == "merge" ? 730008 : path == "whole-stack" ? 370001 : ItemId;
        var accessory = path == "whole-stack";
        var (original, entry, inventory, source) = CreateSendPersistencePlan(path, itemId);
        var character = File.ReadAllBytes(CharacterPath);
        var expected = ExpectedSendImage(File.ReadAllBytes(ItemPath), path, source, maximum: 321,
            plus: accessory ? (byte)9 : path == "merge" ? (byte)8 : (byte)0,
            activate: accessory ? 1u : 0, minutes: accessory ? 0u : 77, expiration: source.Expiration);

        Assert.False(LegacyNpcSalePersistence.TryPersistFloorPickupInventory(original,
            original with { PlayerItems = inventory.UpdatedItems }, Items, entry, inventory,
            LegacyJournalInterruption.StopAfter(replacementCount), out var reason));
        Assert.StartsWith(LegacyTradeJournal.RecoveryRequired, reason);
        Assert.True(LegacyTradeJournal.TryRecover(Users, Items, out var recovered, out reason), reason);
        Assert.Equal(1, recovered);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(expected, File.ReadAllBytes(ItemPath));
        Assert.False(LegacyTradeJournal.HasPending(Users));
        Assert.True(LegacyTradeJournal.TryRecover(Users, Items, out recovered, out reason), reason);
        Assert.Equal(0, recovered);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(expected, File.ReadAllBytes(ItemPath));
    }

    private (WorldSession Original, FloorItemRuntimeEntry Entry, LegacyFloorPickupInventoryChange Inventory,
        PlayerItemSnapshotItem Source) CreateSendPersistencePlan(string path, int itemId, ushort maximum = 0,
        uint minutes = 0, uint existingActivate = 0)
    {
        var original = WriteSession(mergeCandidate: false);
        var destinationStack = path == "merge" && itemId is 350001 or 370001 ? (ushort)0 : (ushort)2;
        var destination = SendFixtureItem(10, (uint)(path == "merge" ? itemId : 1_000_010), destinationStack,
            maximum, activate: path == "merge" ? existingActivate : 0);
        var initial = original.PlayerItems with
        { InventoryItems = [destination, original.PlayerItems.InventoryItems[1]] };
        File.WriteAllBytes(ItemPath, PlayerItemSnapshotLoader.ToLegacyBytes(initial));
        original = original with { PlayerItems = initial };
        var ground = SendFixtureItem(900, (uint)itemId, path == "whole-stack" ? (ushort)3 : (ushort)1,
            maximum, position: 9, activate: path == "merge" ? 0 : existingActivate);
        var packet = LegacyFloorItemPacket.FromDroppedItem(ground, original.Character, 5000);
        var entry = FloorItemRuntimeEntry.ForItem(original.Character.MapId, packet, ground);
        var fields = Enumerable.Repeat("0", 47).ToArray();
        fields[21] = "321";
        fields[38] = minutes.ToString(CultureInfo.InvariantCulture);
        fields[46] = "10";
        var catalog = new ItemType(itemId, "SendProjection", fields);
        var beforePlanningCharacter = File.ReadAllBytes(CharacterPath);
        var beforePlanningItems = File.ReadAllBytes(ItemPath);
        var beforeGround = SendDbRecordOracle(ground);
        var beforeDestination = SendDbRecordOracle(destination);
        Assert.True(LegacyFloorPickupInventory.TryPlan(original.PlayerItems, ground.WithPosition(0), catalog,
            Now, out var inventory, out var reason), reason);
        Assert.Equal(beforePlanningCharacter, File.ReadAllBytes(CharacterPath));
        Assert.Equal(beforePlanningItems, File.ReadAllBytes(ItemPath));
        Assert.Equal(beforeGround, SendDbRecordOracle(ground));
        Assert.Equal(beforeDestination, SendDbRecordOracle(destination));
        return (original, entry, inventory, path == "merge" ? destination : ground);
    }

    private static byte[] ExpectedSendImage(byte[] before, string path, PlayerItemSnapshotItem source,
        ushort maximum, byte plus, uint activate, uint minutes, long expiration)
    {
        var record = SendDbRecordOracle(source, position: 0, maximum: maximum, plus: plus,
            activate: activate, minutes: minutes,
            stack: path == "merge" ? checked((ushort)(source.StackSize + 1)) : source.StackSize, expiration: expiration);
        if (path != "merge") return AppendSendRecordOracle(before, record, SendPerfectionRecordOracle(source));
        var expected = before.ToArray();
        record.CopyTo(expected.AsSpan(sizeof(int), 184));
        return expected;
    }

    private void AssertSendRejectedWithExactJournal(WorldSession original, FloorItemRuntimeEntry entry,
        LegacyFloorPickupInventoryChange inventory)
    {
        var journalRoot = Path.Combine(Users, ".managed-trade-journal");
        Directory.CreateDirectory(journalRoot);
        var markerPath = Path.Combine(Users, "unrelated-sentinel.bin");
        var marker = new byte[] { 0, 17, 255, 9, 34, 80 };
        File.WriteAllBytes(markerPath, marker);
        var character = File.ReadAllBytes(CharacterPath);
        var items = File.ReadAllBytes(ItemPath);
        Assert.Empty(Directory.EnumerateFileSystemEntries(journalRoot));
        Assert.False(LegacyTradeJournal.HasPending(Users));
        Assert.False(Persist(original, entry, inventory, out var reason));
        Assert.Equal("floorPickupInventoryPlanChanged", reason);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(items, File.ReadAllBytes(ItemPath));
        Assert.Equal(marker, File.ReadAllBytes(markerPath));
        Assert.True(Directory.Exists(journalRoot));
        Assert.Empty(Directory.EnumerateFileSystemEntries(journalRoot));
        Assert.False(LegacyTradeJournal.HasPending(Users));
    }

    private static PlayerItemSnapshotItem SendFixtureItem(uint uid, uint itemId, ushort stack,
        ushort maximum, ushort position = 0, uint activate = 0) => new()
    {
        Uid = uid, ItemId = itemId, Durability = 73, MaximumDurability = maximum, Position = position,
        SocketProgress = 0x12345678, SocketOne = 13, SocketTwo = 23, Effect = 201,
        Plus = 9, Bless = 7, Bound = 1, Enchant = 123, Suspicious = 1, Locked = 1, PlusProgress = 123456,
        Inscribed = 1, Activate = activate, TimeLeftInMinutes = 77, StackSize = stack, Color = 9, UnlockTimer = -123,
        ItemPoints = 98765, PurificationItemId = 823009, PurificationLevel = 6, PurificationDuration = 456,
        PurificationAddedOn = 638000000000000000, RefineryEffectId = 987, RefineryEffectLevel = 7,
        RefineryEffectPercent = 8, RefineryEffectPercent2 = 9, RefineryEffectDuration = 765,
        RefineryEffectAddedOn = 638100000000000000, AnimaItemId = 170009, RuneExperience = 8765,
        Attribute1 = 1001, Attribute2 = 1002, Attribute3 = 1003, Attribute4 = 1004, Attribute5 = 1005,
        Expiration = new DateTime(2027, 1, 2, 3, 4, 5, DateTimeKind.Utc).ToBinary(),
        MythSoul = 45, MythSoulPoints = 6789, PerfectionLevel = 23, PerfectionProgress = 4567,
        OwnerUid = 987654, OwnerName = "owner" + uid, Signature = "signature" + uid,
    };

    private static byte[] AppendSendRecordOracle(byte[] before, byte[] record, byte[] perfection)
    {
        // This fixture has two inventory items, equipment40, warehouse70 plus
        // its deposited71: four top-level DB records, five total, five perfection.
        Assert.Equal(4, BinaryPrimitives.ReadInt32LittleEndian(before));
        const int dbBytes = 5 * 184;
        var perfectionStart = sizeof(int) + dbBytes;
        Assert.Equal(5, BinaryPrimitives.ReadInt32LittleEndian(before.AsSpan(perfectionStart)));
        Assert.Equal(sizeof(int) + dbBytes + sizeof(int) + 5 * 80, before.Length);
        var expected = new byte[before.Length + 184 + 80];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 5);
        before.AsSpan(sizeof(int), 2 * 184).CopyTo(expected.AsSpan(sizeof(int)));
        record.CopyTo(expected.AsSpan(sizeof(int) + 2 * 184));
        before.AsSpan(sizeof(int) + 2 * 184, 3 * 184).CopyTo(expected.AsSpan(sizeof(int) + 3 * 184));
        var outputPerfectionStart = perfectionStart + 184;
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(outputPerfectionStart), 6);
        before.AsSpan(perfectionStart + sizeof(int), 2 * 80)
            .CopyTo(expected.AsSpan(outputPerfectionStart + sizeof(int)));
        perfection.CopyTo(expected.AsSpan(outputPerfectionStart + sizeof(int) + 2 * 80));
        before.AsSpan(perfectionStart + sizeof(int) + 2 * 80, 3 * 80)
            .CopyTo(expected.AsSpan(outputPerfectionStart + sizeof(int) + 3 * 80));
        return expected;
    }

    private static byte[] SendDbRecordOracle(PlayerItemSnapshotItem source, ushort? position = null,
        ushort? maximum = null, byte? plus = null, uint? activate = null, uint? minutes = null,
        ushort? stack = null, long? expiration = null)
    {
        var record = new byte[184];
        void U16(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(offset), value);
        void U32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(offset), value);
        void I64(int offset, long value) => BinaryPrimitives.WriteInt64LittleEndian(record.AsSpan(offset), value);
        U32(0, source.Uid); U32(4, source.ItemId); U16(8, source.Durability);
        U16(10, maximum ?? source.MaximumDurability); U16(12, position ?? source.Position);
        U32(16, source.SocketProgress); record[20] = source.SocketOne; record[21] = source.SocketTwo;
        U32(24, source.Effect); record[28] = plus ?? source.Plus; record[29] = source.Bless;
        record[30] = source.Bound; record[31] = source.Enchant; record[32] = source.Suspicious; record[33] = source.Locked;
        U32(36, source.PlusProgress); U16(40, source.Inscribed); U32(44, activate ?? source.Activate);
        U32(48, minutes ?? source.TimeLeftInMinutes); U16(52, stack ?? source.StackSize);
        U32(56, source.WarehouseId); U32(60, source.Color);
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(64), source.UnlockTimer);
        U32(68, source.ItemPoints); U32(84, source.PurificationItemId); U32(88, source.PurificationLevel);
        U32(92, source.PurificationDuration); I64(96, source.PurificationAddedOn);
        U32(104, source.RefineryEffectId); U32(108, source.RefineryEffectLevel);
        U32(112, source.RefineryEffectPercent); U32(116, source.RefineryEffectPercent2);
        U32(120, source.RefineryEffectDuration); I64(128, source.RefineryEffectAddedOn);
        Assert.Empty(source.DepositedItems);
        U32(140, source.AnimaItemId); U32(144, source.RuneExperience); U32(148, source.Attribute1);
        U32(152, source.Attribute2); U32(156, source.Attribute3); U32(160, source.Attribute4); U32(164, source.Attribute5);
        I64(168, expiration ?? source.Expiration); U32(176, source.MythSoul); U32(180, source.MythSoulPoints);
        return record;
    }

    private static byte[] SendPerfectionRecordOracle(PlayerItemSnapshotItem source)
    {
        var record = new byte[80];
        BinaryPrimitives.WriteUInt32LittleEndian(record, source.Uid);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(4), source.PerfectionLevel);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(8), source.PerfectionProgress);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(12), source.OwnerUid);
        var owner = Encoding.UTF8.GetBytes(source.OwnerName);
        var signature = Encoding.UTF8.GetBytes(source.Signature);
        Assert.True(owner.Length < 32 && signature.Length < 32);
        owner.CopyTo(record.AsSpan(16, 32));
        signature.CopyTo(record.AsSpan(48, 32));
        return record;
    }

    private static PlayerItemSnapshotItem ParseSendFixtureRecord(byte[] record, byte[] perfection)
    {
        var bytes = new byte[sizeof(int) + 184 + sizeof(int) + 80];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        record.CopyTo(bytes.AsSpan(sizeof(int)));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(sizeof(int) + 184), 1);
        perfection.CopyTo(bytes.AsSpan(sizeof(int) + 184 + sizeof(int)));
        return Assert.Single(PlayerItemSnapshotLoader.Parse(bytes).InventoryItems);
    }
}
