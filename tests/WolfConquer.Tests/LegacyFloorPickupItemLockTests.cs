using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Native own-server SendItemLocked only; extras and physical SQL/MAC/Pipe
// behavior are not manufactured by this pure selected-item projection.
public sealed class LegacyFloorPickupItemLockTests
{
    [Theory]
    [InlineData((byte)0, -1, false, false, false)]
    [InlineData((byte)1, 20260230, false, false, false)]
    [InlineData((byte)3, 20261301, false, false, false)]
    [InlineData((byte)2, 0, false, true, false)]
    [InlineData((byte)2, 20261020, false, true, false)]
    [InlineData((byte)2, 20261021, false, true, false)]
    [InlineData((byte)2, 20261021, true, false, true)]
    [InlineData((byte)2, 20261022, false, false, true)]
    public void OnlyLockedTwoProjectsNativeMidnightLifecycleAndPreservesEveryOtherByte(
        byte locked, int timer, bool exactlyMidnight, bool clears, bool sendsDate)
    {
        var now = new DateTime(2026, 10, 21, exactlyMidnight ? 0 : 12, 0, 0, DateTimeKind.Local);
        var initial = Rich(locked, timer);
        var before = Bytes(initial); var expected = before.ToArray();
        if (clears) expected[sizeof(int) + 33] = 0;
        Assert.True(LegacyFloorPickupItemLock.TryProject(initial, now, out var final, out var date, out var reason), reason);
        Assert.Equal(expected, Bytes(final)); Assert.Equal(before, Bytes(initial));
        Assert.Equal(clears ? (byte)0 : locked, final.Locked);
        Assert.Equal(locked, initial.Locked); Assert.Equal(timer, final.UnlockTimer);
        Assert.Same(initial.DepositedItems, final.DepositedItems);
        if (!clears) Assert.Same(initial, final);
        else Assert.NotSame(initial, final);
        if (!sendsDate) Assert.Null(date);
        else
        {
            Assert.True(date.HasValue);
            // Independent complete native2323 wire oracle: UID,u8type2,u8mode0,
            // reservedu16zero,uncheckedYYYYMMDD parameter. Header is4bytes.
            var expectedDateFrame = new byte[16];
            BinaryPrimitives.WriteUInt16LittleEndian(expectedDateFrame.AsSpan(0, 2), 16);
            BinaryPrimitives.WriteUInt16LittleEndian(expectedDateFrame.AsSpan(2, 2), 2323);
            BinaryPrimitives.WriteUInt32LittleEndian(expectedDateFrame.AsSpan(4, 4), initial.Uid);
            expectedDateFrame[8] = 2;
            BinaryPrimitives.WriteUInt32LittleEndian(expectedDateFrame.AsSpan(12, 4), unchecked((uint)timer));
            Assert.Equal(expectedDateFrame, date.Value.ToFrameBytes());
        }
    }

    [Theory]
    [InlineData(20260230)]
    [InlineData(20261301)]
    [InlineData(-1)]
    [InlineData(1)]
    public void InvalidCalendarForLockedTwoFailsBeforeProducingAChangedImageOrPacket(int timer)
    {
        var initial = Rich(2, timer); var before = Bytes(initial);
        Assert.False(LegacyFloorPickupItemLock.TryProject(initial,
            new DateTime(2026, 10, 21, 12, 0, 0), out var final, out var date, out var reason));
        Assert.Null(final); Assert.Null(date); Assert.Equal("floorPickupItemLockTimerInvalid", reason);
        Assert.Equal(before, Bytes(initial));
    }

    private static byte[] Bytes(PlayerItemSnapshotItem item) =>
        PlayerItemSnapshotLoader.ToLegacyBytes(new([item], [], [], [], []));

    private static PlayerItemSnapshotItem Rich(byte locked, int timer) => new()
    {
        Uid = 0x12345678, ItemId = 410003, Durability = 19, MaximumDurability = 271, Position = 0,
        SocketProgress = 99, SocketOne = 13, SocketTwo = 3, Effect = 200, Plus = 12, Bless = 1, Bound = 1,
        Enchant = 17, Suspicious = 1, Locked = locked, PlusProgress = 43, Inscribed = 4, Activate = 7,
        TimeLeftInMinutes = 0, StackSize = 7, WarehouseId = 1000, Color = 5, UnlockTimer = timer, ItemPoints = 33,
        PurificationItemId = 41, PurificationLevel = 2, PurificationDuration = 55, PurificationAddedOn = 123,
        RefineryEffectId = 99, RefineryEffectLevel = 4, RefineryEffectPercent = 22, RefineryEffectPercent2 = 33,
        RefineryEffectDuration = 44, RefineryEffectAddedOn = 55, AnimaItemId = 4200001, RuneExperience = 987,
        Attribute1 = 11, Attribute2 = 22, Attribute3 = 33, Attribute4 = 44, Attribute5 = 55,
        Expiration = new DateTime(2026, 11, 1, 12, 30, 0, DateTimeKind.Local).ToBinary(),
        MythSoul = 4, MythSoulPoints = 99, PerfectionLevel = 23, PerfectionProgress = 321,
        OwnerUid = 9999, OwnerName = "RichOwner", Signature = "PreserveAllMetadata",
        DepositedItems = [new() { Uid = 81, ItemId = 50001, StackSize = 2, Plus = 9, Locked = 2,
            UnlockTimer = 20260230, PerfectionLevel = 1, OwnerName = "Child" }],
    };
}
