using System.Buffers.Binary;
using System.Runtime.InteropServices;
using WolfConquer.Core.World;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Tests;

public sealed class LegacyNativeClientItemWritebackTests
{
    private static readonly DateTime Added = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 11, 1, 13, 0, 0, DateTimeKind.Local);

    [Fact]
    public void CompleteWritebackMutatesTheReused184ByteRecordWithoutResettingUntouchedFieldsOrPadding()
    {
        var before = Enumerable.Repeat((byte)0xA5, 184).ToArray();
        var record = MemoryMarshal.Read<LegacyNativeClientItemRecord>(before);
        var item = Item();
        var child = item.Deposite[99];

        var returned = record.GetDBItem(item);

        var expected = Expected(before);
        Assert.Equal(expected, Bytes(record));
        Assert.Equal(expected, Bytes(returned));
        Assert.Equal(0xA5A5A5A5U, record.test3);
        Assert.Equal(0xA5A5A5A5U, record.test4);
        Assert.Same(child, item.Deposite[99]);
        Assert.Equal(77U, RawInactiveMinutes(item));
        Assert.Equal(7U, item.Activate);
        returned.UID = 1;
        Assert.Equal(0x12345678U, record.UID); // Returned struct is a value copy.
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void ShortRelicArrayThrowsAfterTheExactNativeAssignmentPrefixWithoutRollingBack(int length)
    {
        var before = Enumerable.Repeat((byte)0xA5, 184).ToArray();
        var record = MemoryMarshal.Read<LegacyNativeClientItemRecord>(before);
        var item = Item();
        item.RelicAttributes = item.RelicAttributes.Take(length).ToArray();
        var expected = Expected(before);
        var faultOffset = 148 + length * 4;
        before.AsSpan(faultOffset).CopyTo(expected.AsSpan(faultOffset));

        Assert.Throws<IndexOutOfRangeException>(() => record.GetDBItem(item));

        Assert.Equal(expected, Bytes(record));
        Assert.Equal(length, item.RelicAttributes.Length);
    }

    [Fact]
    public void NullDepositMapFaultRetainsEarlierExtraAssignmentsButDoesNotReachTimerRemainingOrRelicAssignments()
    {
        var before = Enumerable.Repeat((byte)0xA5, 184).ToArray();
        var record = MemoryMarshal.Read<LegacyNativeClientItemRecord>(before);
        var item = Item();
        item.Deposite = null!;
        var expected = Expected(before);
        before.AsSpan(64, 4).CopyTo(expected.AsSpan(64));
        before.AsSpan(72, 4).CopyTo(expected.AsSpan(72));
        before.AsSpan(136).CopyTo(expected.AsSpan(136));

        Assert.Throws<NullReferenceException>(() => record.GetDBItem(item));

        Assert.Equal(expected, Bytes(record));
    }

    [Fact]
    public void NullItemFaultOccursBeforeAnyAssignmentToTheReusedRecord()
    {
        var before = Enumerable.Repeat((byte)0xA5, 184).ToArray();
        var record = MemoryMarshal.Read<LegacyNativeClientItemRecord>(before);
        Assert.Throws<NullReferenceException>(() => record.GetDBItem(null!));
        Assert.Equal(before, Bytes(record));
    }

    private static uint RawInactiveMinutes(LegacyNativeGameItemRuntime item)
    {
        var activation = item.Activate;
        item.Activate = 0;
        var minutes = item.TimeLeftInMinutes;
        item.Activate = activation;
        return minutes;
    }

    private static byte[] Bytes(LegacyNativeClientItemRecord record)
    {
        var bytes = new byte[184];
        MemoryMarshal.Write(bytes.AsSpan(), in record);
        return bytes;
    }

    private static LegacyNativeGameItemRuntime Item()
    {
        var item = new LegacyNativeGameItemRuntime
        {
            Uid = 0x12345678, ITEM_ID = 410003, Durability = 19, MaximDurability = 271,
            Position = 9, SocketProgress = 99, SocketOne = (Flags.Gem)13, SocketTwo = (Flags.Gem)3,
            Effect = (Flags.ItemEffect)200, Plus = 12, Bless = 1, Bound = 1, Enchant = 17,
            Suspicious = 1, Locked = 2, PlusProgress = 43, Inscribed = 4, Activate = 7,
            TimeLeftInMinutes = 77, StackSize = 7, WH_ID = 1000, Color = (Flags.Color)5,
            UnLockTimer = 20000101, ItemPoints = 33, AnimaItemID = 4200001, RuneEXP = 987,
            EndDate = End, MythSoul = 4, MythSoulPoints = 99,
            Purification = new() { ItemUID = 123, PurificationItemID = 823009, PurificationLevel = 2,
                PurificationDuration = 55, AddedOn = Added },
            Refinary = new() { ItemUID = 456, EffectID = 724440, EffectLevel = 4,
                EffectPercent = 22, EffectPercent2 = 33, EffectDuration = 44, AddedOn = Added },
            RelicAttributes = [new((LegacyNativeChiAttribute)1, 10, false),
                new((LegacyNativeChiAttribute)2, 11, true), new((LegacyNativeChiAttribute)3, 12, false),
                new((LegacyNativeChiAttribute)4, 13, true), new((LegacyNativeChiAttribute)5, 14, false)],
        };
        // Native count is dictionary count, not the child's UID or a DTO projection.
        item.Deposite.TryAdd(99, new() { Uid = 88 });
        return item;
    }

    private static byte[] Expected(byte[] before)
    {
        // Independent native DB184 offsets; untouched bytes retain their preimage.
        var bytes = before.ToArray();
        void U16(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
        void U32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        void I64(int offset, long value) => BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(offset), value);
        U32(0, 0x12345678); U32(4, 410003); U16(8, 19); U16(10, 271); U16(12, 9);
        U32(16, 99); bytes[20] = 13; bytes[21] = 3; U32(24, 200);
        bytes[28] = 12; bytes[29] = 1; bytes[30] = 1; bytes[31] = 17; bytes[32] = 1; bytes[33] = 2;
        U32(36, 43); U16(40, 4); U32(44, 7); U32(48, 0); U16(52, 7); U32(56, 1000);
        U32(60, 5); U32(64, 20000101); U32(68, 33); U32(72, 0);
        U32(84, 823009); U32(88, 2); U32(92, 55); I64(96, Added.Ticks);
        U32(104, 724440); U32(108, 4); U32(112, 22); U32(116, 33); U32(120, 44);
        I64(128, Added.Ticks); U32(136, 1); U32(140, 4200001); U32(144, 987);
        U32(148, 10001); U32(152, 11102); U32(156, 12003); U32(160, 13104); U32(164, 14005);
        I64(168, End.ToBinary()); U32(176, 4); U32(180, 99);
        return bytes;
    }
}
