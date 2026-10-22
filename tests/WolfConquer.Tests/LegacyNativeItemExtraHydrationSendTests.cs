using System.Buffers.Binary;
using System.Runtime.InteropServices;
using WolfConquer.Core.World;
using Extra = WolfConquer.Core.World.LegacyNativeItemExtra;

namespace WolfConquer.Tests;

// Real DBItem -> concrete item -> native extra sender -> DBItem composition.
// This does not wire the source component into the snapshot/host/client owners.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeItemExtraHydrationSendTests
{
    private const uint ItemUid = 0xF1234567u;
    private const long InvalidDateBinary = 3155378976000000000L;
    private static readonly DateTime Past = new(2000, 1, 2, 3, 4, 5);

    [Fact]
    public void ExpiredAtLoadClearsBothExtraStructsSuppressesFrameAndWritesClearedFields()
    {
        var record = RecordWithBothExtras(Past, duration: 60);
        var rawBefore = RawBytes(record);
        var item = record.GetDataItem();

        Assert.Equal(ItemUid, item.Uid);
        Assert.Equal(default(Extra.Purification), item.Purification);
        Assert.Equal(default(Extra.Refinery), item.Refinary);
        Assert.Null(item.CreateItemExtraFrame());
        Assert.Equal(rawBefore, RawBytes(record));

        var written = default(LegacyNativeClientItemRecord).GetDBItem(item);
        Assert.Equal(ItemUid, written.UID);
        Assert.Equal(0u, written.PurificationItemID);
        Assert.Equal(0u, written.PurificationLevel);
        Assert.Equal(0u, written.PurificationDuration);
        Assert.Equal(0L, written.PurificationAddedOn);
        Assert.Equal(0u, written.EffectID);
        Assert.Equal(0u, written.EffectLevel);
        Assert.Equal(0u, written.EffectPercent);
        Assert.Equal(0u, written.EffectPercent2);
        Assert.Equal(0u, written.EffectDuration);
        Assert.Equal(0L, written.EffectAddedOn);
        Assert.Null(written.GetDataItem().CreateItemExtraFrame());
    }

    [Fact]
    public void ExpiringTheSameHydratedItemKeepsUidAndPayloadButSendsAnEmptyCountFrame()
    {
        var future = DateTime.Now.AddDays(1);
        var record = RecordWithBothExtras(future, duration: 60);
        var rawBefore = RawBytes(record);
        var item = record.GetDataItem();
        Assert.True(item.Purification.InLife);
        Assert.True(item.Refinary.InLife);
        Assert.Equal(ItemUid, item.Purification.ItemUID);
        Assert.Equal(ItemUid, item.Refinary.ItemUID);

        var inventory = new LegacyNativeInventoryRuntime(null);
        inventory.AddDBItem(item);
        var child = new LegacyNativeClientItemRecord { UID = 81u, ITEM_ID = 600000u }.GetDataItem();
        item.Deposite.TryAdd(child.Uid, child);
        var deposits = item.Deposite;
        var relics = item.RelicAttributes;
        item.Purification.AddedOn = Past;
        item.Refinary.AddedOn = Past;

        Assert.Equal(ExpectedFrame(0u), item.CreateItemExtraFrame());
        Assert.Same(item, inventory.ClientItems[ItemUid]);
        Assert.Same(deposits, item.Deposite);
        Assert.Same(child, item.Deposite[81u]);
        Assert.Same(relics, item.RelicAttributes);
        Assert.Equal(ItemUid, item.Purification.ItemUID);
        Assert.Equal(ItemUid, item.Refinary.ItemUID);
        Assert.Equal(800001u, item.Purification.PurificationItemID);
        Assert.Equal(724440u, item.Refinary.EffectID);
        Assert.Equal(60u, item.Purification.PurificationDuration);
        Assert.Equal(60u, item.Refinary.EffectDuration);
        Assert.Equal((Extra.Typing)0, item.Purification.Typ);
        Assert.Equal((Extra.Typing)0, item.Refinary.Typ);
        Assert.False(item.Refinary.Available);

        var written = default(LegacyNativeClientItemRecord).GetDBItem(item);
        Assert.Equal(800001u, written.PurificationItemID);
        Assert.Equal(7u, written.PurificationLevel);
        Assert.Equal(60u, written.PurificationDuration);
        Assert.Equal(Past.Ticks, written.PurificationAddedOn);
        Assert.Equal(724440u, written.EffectID);
        Assert.Equal(9u, written.EffectLevel);
        Assert.Equal(11u, written.EffectPercent);
        Assert.Equal(13u, written.EffectPercent2);
        Assert.Equal(60u, written.EffectDuration);
        Assert.Equal(Past.Ticks, written.EffectAddedOn);
        Assert.Equal(1u, written.DepositeCount);
        Assert.Equal(rawBefore, RawBytes(record));
        // A later load is a new concrete item and performs the expiry reset.
        var loadedAgain = written.GetDataItem();
        Assert.NotSame(item, loadedAgain);
        Assert.Equal(default(Extra.Purification), loadedAgain.Purification);
        Assert.Equal(default(Extra.Refinery), loadedAgain.Refinary);
        Assert.Null(loadedAgain.CreateItemExtraFrame());
        Assert.Equal(ExpectedFrame(0u), item.CreateItemExtraFrame());
    }

    [Fact]
    public void PermanentHydratedFieldsUseLoadedUidAndExactNativeFrameThenWriteTicks()
    {
        var record = RecordWithBothExtras(Past, duration: 0);
        var item = record.GetDataItem();
        Assert.Equal(ItemUid, item.Purification.ItemUID);
        Assert.Equal(ItemUid, item.Refinary.ItemUID);
        Assert.Equal(Past, item.Purification.AddedOn);
        Assert.Equal(Past, item.Refinary.AddedOn);
        Assert.Equal(0u, item.Purification.EffectPercent2);
        Assert.Equal((Extra.Typing)0, item.Purification.Typ);
        Assert.Equal((Extra.Typing)0, item.Refinary.Typ);

        Assert.Equal(ExpectedFrame(
            2u,
            ItemUid, 8u, 800001u, 7u, 0u, 0u, 0u, 0u,
            ItemUid, 3u, 301u, 9u, 11u, 13u, 0u), item.CreateItemExtraFrame());
        Assert.Equal(Extra.Typing.Stabilization, item.Purification.Typ);
        Assert.Equal(Extra.Typing.PermanentRefinery, item.Refinary.Typ);
        Assert.True(item.Refinary.Available);
        Assert.Equal(724440u, item.Refinary.EffectID); // Client remap is not a state write.

        var written = default(LegacyNativeClientItemRecord).GetDBItem(item);
        Assert.Equal(Past.Ticks, written.PurificationAddedOn);
        Assert.Equal(Past.Ticks, written.EffectAddedOn);
        Assert.Equal(800001u, written.PurificationItemID);
        Assert.Equal(7u, written.PurificationLevel);
        Assert.Equal(724440u, written.EffectID);
        Assert.Equal(9u, written.EffectLevel);
        Assert.Equal(11u, written.EffectPercent);
        Assert.Equal(13u, written.EffectPercent2);
        Assert.Equal(0u, written.PurificationDuration);
        Assert.Equal(0u, written.EffectDuration);
    }

    [Fact]
    public void LiveHydratedFieldsSendTimedTypesAndWriteNativeActiveMinutesGetter()
    {
        var future = DateTime.Now.AddDays(1);
        var expiry = DateTime.Now.AddDays(2);
        var record = RecordWithBothExtras(future, duration: 600);
        record.Activate = 1u;
        record.TimeLeftInMinutes = 77u;
        record.Expiration = expiry.ToBinary();
        var item = record.GetDataItem();
        Assert.Equal(0u, item.TimeLeftInMinutes);
        var beforeSend = DateTime.Now;
        var frame = Assert.IsType<byte[]>(item.CreateItemExtraFrame());
        var afterSend = DateTime.Now;

        AssertHeader(frame, 68, 2u);
        Assert.Equal(ExpectedFrame(
            2u,
            ItemUid, 6u, 800001u, 7u, 0u, 600u, ReadWord(frame, 32), 0u,
            ItemUid, 2u, 301u, 9u, 11u, 13u, ReadWord(frame, 64)), frame);
        AssertSecondsBetween(frame, 32, future.AddSeconds(600), beforeSend, afterSend);
        AssertSecondsBetween(frame, 64, future.AddSeconds(600), beforeSend, afterSend);
        Assert.Equal(Extra.Typing.PurificationAdding, item.Purification.Typ);
        Assert.Equal(Extra.Typing.RefinaryAdding, item.Refinary.Typ);
        Assert.True(item.Refinary.Available);

        var beforeWrite = DateTime.Now;
        var written = default(LegacyNativeClientItemRecord).GetDBItem(item);
        var afterWrite = DateTime.Now;
        Assert.Equal(1u, written.Activate);
        Assert.Equal(0u, written.TimeLeftInMinutes);
        Assert.InRange(written.RemainingTime,
            (uint)Math.Max(0, (expiry - afterWrite).TotalSeconds - 1),
            (uint)((expiry - beforeWrite).TotalSeconds + 1));
        Assert.Equal(expiry.ToBinary(), written.Expiration);
        Assert.Equal(future.Ticks, written.PurificationAddedOn);
        Assert.Equal(future.Ticks, written.EffectAddedOn);
        Assert.Equal(600u, written.PurificationDuration);
        Assert.Equal(600u, written.EffectDuration);
        Assert.Equal(77u, record.TimeLeftInMinutes);
    }

    [Fact]
    public void PurificationClampsUtcBinaryWhileRefineryDecodesKindAndWriterUsesTicks()
    {
        var utc = new DateTime(2000, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var record = RecordWithBothExtras(Past, duration: 0);
        record.PurificationAddedOn = utc.ToBinary();
        record.EffectAddedOn = utc.ToBinary();
        var item = record.GetDataItem();

        Assert.Equal(DateTime.MinValue, item.Purification.AddedOn);
        Assert.Equal(DateTimeKind.Unspecified, item.Purification.AddedOn.Kind);
        Assert.Equal(utc, item.Refinary.AddedOn);
        Assert.Equal(DateTimeKind.Utc, item.Refinary.AddedOn.Kind);
        Assert.Equal(ExpectedFrame(
            2u,
            ItemUid, 8u, 800001u, 7u, 0u, 0u, 0u, 0u,
            ItemUid, 3u, 301u, 9u, 11u, 13u, 0u), item.CreateItemExtraFrame());

        var written = default(LegacyNativeClientItemRecord).GetDBItem(item);
        Assert.Equal(0L, written.PurificationAddedOn);
        Assert.Equal(utc.Ticks, written.EffectAddedOn);
        Assert.NotEqual(utc.ToBinary(), written.EffectAddedOn);
        var loadedAgain = written.GetDataItem();
        Assert.Equal(DateTimeKind.Unspecified, loadedAgain.Refinary.AddedOn.Kind);
        Assert.Equal(utc.Ticks, loadedAgain.Refinary.AddedOn.Ticks);
    }

    [Fact]
    public void ClampedTimedPurificationExpiresDuringHydrationButPermanentRefineryStillSends()
    {
        var futureUtc = DateTime.UtcNow.AddDays(1);
        var record = RecordWithBothExtras(Past, duration: 0);
        record.PurificationAddedOn = futureUtc.ToBinary();
        record.PurificationDuration = 600u;
        var item = record.GetDataItem();

        Assert.Equal(default(Extra.Purification), item.Purification);
        Assert.Equal(ItemUid, item.Refinary.ItemUID);
        Assert.Equal(ExpectedFrame(1u, ItemUid, 3u, 301u, 9u, 11u, 13u, 0u),
            item.CreateItemExtraFrame());
        var written = default(LegacyNativeClientItemRecord).GetDBItem(item);
        Assert.Equal(0u, written.PurificationItemID);
        Assert.Equal(0u, written.PurificationDuration);
        Assert.Equal(0L, written.PurificationAddedOn);
        Assert.Equal(724440u, written.EffectID);
        Assert.Equal(Past.Ticks, written.EffectAddedOn);
    }

    [Fact]
    public void InvalidRawRefineryBinaryThrowsWithoutReturningAnItemOrRewritingTheRecord()
    {
        var record = RecordWithBothExtras(Past, duration: 0);
        record.PurificationAddedOn = InvalidDateBinary; // Clamped, not the fault.
        record.EffectAddedOn = InvalidDateBinary; // No native clamp on refinery.
        var before = RawBytes(record);
        LegacyNativeGameItemRuntime? returned = null;

        Assert.Throws<ArgumentException>(() => returned = record.GetDataItem());

        Assert.Null(returned);
        Assert.Equal(before, RawBytes(record));
        // The internally allocated, partially populated item is not returned by native code.
        record.EffectAddedOn = Past.Ticks;
        var recovered = record.GetDataItem();
        Assert.Equal(DateTime.MinValue, recovered.Purification.AddedOn);
        Assert.Equal(Past, recovered.Refinary.AddedOn);
        Assert.Equal(ExpectedFrame(
            2u,
            ItemUid, 8u, 800001u, 7u, 0u, 0u, 0u, 0u,
            ItemUid, 3u, 301u, 9u, 11u, 13u, 0u), recovered.CreateItemExtraFrame());
    }

    [Fact]
    public void LaterRefineryLifetimeFaultLeavesEarlierPurificationTypeMutationOnActualItem()
    {
        var item = RecordWithBothExtras(Past, duration: 0).GetDataItem();
        var inventory = new LegacyNativeInventoryRuntime(null);
        inventory.AddDBItem(item);
        item.Refinary.AddedOn = DateTime.MaxValue;
        item.Refinary.EffectDuration = 1u;
        byte[]? returnedFrame = null;

        Assert.Throws<ArgumentOutOfRangeException>(() => returnedFrame = item.CreateItemExtraFrame());

        Assert.Null(returnedFrame);
        Assert.Same(item, inventory.ClientItems[ItemUid]);
        Assert.Equal(Extra.Typing.Stabilization, item.Purification.Typ);
        Assert.Equal((Extra.Typing)0, item.Refinary.Typ);
        Assert.Equal(ItemUid, item.Purification.ItemUID);
        Assert.Equal(ItemUid, item.Refinary.ItemUID);
        Assert.Equal(800001u, item.Purification.PurificationItemID);
        Assert.Equal(724440u, item.Refinary.EffectID);
        Assert.Equal(DateTime.MaxValue, item.Refinary.AddedOn);
        Assert.True(item.Refinary.Available); // Overflow happens before Available is reset.
        var written = default(LegacyNativeClientItemRecord).GetDBItem(item);
        Assert.Equal(DateTime.MaxValue.Ticks, written.EffectAddedOn);
        Assert.Equal(1u, written.EffectDuration);
        Assert.Equal(800001u, written.PurificationItemID);
    }

    [Fact]
    public void ZeroUidHydrationDoesNotInventIdentityEvenForPermanentLiveExtraFields()
    {
        var record = RecordWithBothExtras(Past, duration: 0);
        record.UID = 0u;
        var first = record.GetDataItem();
        var second = record.GetDataItem();

        Assert.NotSame(first, second);
        Assert.Equal(0u, first.Uid);
        Assert.Equal(0u, first.Purification.ItemUID);
        Assert.Equal(0u, first.Refinary.ItemUID);
        Assert.True(first.Purification.InLife);
        Assert.True(first.Refinary.InLife);
        Assert.Null(first.CreateItemExtraFrame());
        Assert.Equal((Extra.Typing)0, first.Purification.Typ);
        Assert.Equal((Extra.Typing)0, first.Refinary.Typ);
        Assert.NotSame(first.Deposite, second.Deposite);
        Assert.NotSame(first.RelicAttributes, second.RelicAttributes);
        Assert.Equal(0u, default(LegacyNativeClientItemRecord).GetDBItem(first).UID);
    }

    [Fact]
    public void ReusedRecordWritebackKeepsReservedWordsAndInactiveRawMinutesAcrossReload()
    {
        var source = RecordWithBothExtras(Past, duration: 0);
        source.TimeLeftInMinutes = uint.MaxValue;
        var item = source.GetDataItem();
        _ = item.CreateItemExtraFrame();
        var reused = new LegacyNativeClientItemRecord
        {
            test3 = 0x11223344u,
            test4 = 0xAABBCCDDu,
            TimeLeftInMinutes = 1u,
            UID = 88u,
            DepositeCount = uint.MaxValue
        };

        var written = reused.GetDBItem(item);

        Assert.Equal(0x11223344u, reused.test3);
        Assert.Equal(0xAABBCCDDu, reused.test4);
        Assert.Equal(0x11223344u, written.test3);
        Assert.Equal(0xAABBCCDDu, written.test4);
        Assert.Equal(ItemUid, reused.UID);
        Assert.Equal(ItemUid, written.UID);
        Assert.Equal(0u, written.Activate);
        Assert.Equal(uint.MaxValue, written.TimeLeftInMinutes);
        Assert.Equal(0u, written.RemainingTime);
        Assert.Equal(0u, written.DepositeCount);
        Assert.Equal(0L, written.Expiration);
        var loadedAgain = written.GetDataItem();
        Assert.NotSame(item, loadedAgain);
        Assert.Equal(uint.MaxValue, loadedAgain.TimeLeftInMinutes);
        Assert.Equal(ItemUid, loadedAgain.Purification.ItemUID);
        Assert.Equal(ItemUid, loadedAgain.Refinary.ItemUID);
        Assert.Equal(ExpectedFrame(
            2u,
            ItemUid, 8u, 800001u, 7u, 0u, 0u, 0u, 0u,
            ItemUid, 3u, 301u, 9u, 11u, 13u, 0u), loadedAgain.CreateItemExtraFrame());
    }

    private static LegacyNativeClientItemRecord RecordWithBothExtras(DateTime addedOn, uint duration) => new()
    {
        UID = ItemUid,
        ITEM_ID = 600000u, // Deliberately non-Rune: no table prerequisite is introduced.
        PurificationItemID = 800001u,
        PurificationLevel = 7u,
        PurificationDuration = duration,
        PurificationAddedOn = addedOn.Ticks,
        EffectID = 724440u,
        EffectLevel = 9u,
        EffectPercent = 11u,
        EffectPercent2 = 13u,
        EffectDuration = duration,
        EffectAddedOn = addedOn.Ticks
    };

    // Literal native word order/header, independent of production packet types/parsers.
    private static byte[] ExpectedFrame(params uint[] words)
    {
        var bytes = new byte[4 + words.Length * 4];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)bytes.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2134);
        for (var index = 0; index < words.Length; index++)
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4 + index * 4), words[index]);
        return bytes;
    }

    private static void AssertHeader(byte[] bytes, int length, uint count)
    {
        Assert.Equal(length, bytes.Length);
        Assert.Equal((ushort)length, BinaryPrimitives.ReadUInt16LittleEndian(bytes));
        Assert.Equal((ushort)2134, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2)));
        Assert.Equal(count, ReadWord(bytes, 4));
    }

    private static uint ReadWord(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));

    private static void AssertSecondsBetween(
        byte[] bytes, int offset, DateTime expires, DateTime before, DateTime after)
    {
        var seconds = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));
        // Native clock reads occur more than once. Bracket those real reads rather
        // than replacing their clock, sleeping, or asserting a race-prone exact second.
        Assert.InRange(seconds,
            (int)(expires - after).TotalSeconds - 1,
            (int)(expires - before).TotalSeconds + 1);
        Assert.True(seconds > 0);
    }

    private static byte[] RawBytes(LegacyNativeClientItemRecord record) =>
        MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref record, 1)).ToArray();
}
