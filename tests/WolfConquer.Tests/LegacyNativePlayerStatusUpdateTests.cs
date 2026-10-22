using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using DataType = WolfConquer.Core.World.LegacyNativeMsgUpdate.DataType;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativePlayerStatusUpdateTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ExplicitAndZeroFlagOverloadsSendAllFieldsViaThePhysicalOwner(bool zeroFlag) => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeSpellTestFixture();
        var holderQueue = new Queue<byte[]>();
        var holder = NativeSpellTestFixture.CreateOwner(holderQueue);
        holder.Player = f.Owner.Player;
        holder.Player.View = null!; // scren:false must not read View or require native map publication.
        f.Packet.InitWriter(); // Native caller-owned packet prestate; SendUpdate does not reset its cursor.
        const uint time = uint.MaxValue;
        if (zeroFlag)
            holder.Player.SendUpdate(f.Packet, DataType.AzureShield, time, 3, 4);
        else
            holder.Player.SendUpdate(f.Packet, (Flag)(-1), time, 3, 4, DataType.AzureShield);
        var expected = new LegacyMsgUpdatePacket(17,
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.AzureShield, 0, zeroFlag ? 0 : uint.MaxValue, time, 3, 4)]);
        Assert.Equal(expected.ToFrameBytes(), Assert.Single(f.Queue));
        Assert.Empty(holderQueue);
        Assert.Same(f.Owner, holder.Player.Owner);
        Assert.Null(holder.Player.View);
    });

    [Fact]
    public Task ArrayOverloadSendsAllWordsIncludingZeroAndCopiesBeforeReturn() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeSpellTestFixture();
        f.Owner.Player.View = null!;
        uint[] words = [0, 1, uint.MaxValue];
        f.Owner.Player.SendUpdate(words, DataType.StatusFlag);
        words[1] = 99;
        var expected = new LegacyMsgUpdatePacket(17,
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.StatusFlag, 0, Values: [0, 1, uint.MaxValue])]);
        Assert.Equal(expected.ToFrameBytes(), Assert.Single(f.Queue));
        Assert.Equal(99u, words[1]);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public Task ScreenFaultOccursOnlyAfterTheActualOwnerQueueSend(int overload) => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeSpellTestFixture();
        f.Owner.Player.View = null!;
        f.Packet.InitWriter();
        Assert.Throws<NullReferenceException>(() =>
        {
            if (overload == 0) f.Owner.Player.SendUpdate(f.Packet, Flag.Ride, 1, 2, 3, DataType.StatusFlag, true);
            else if (overload == 1) f.Owner.Player.SendUpdate(f.Packet, DataType.StatusFlag, 1, 2, 3, true);
            else f.Owner.Player.SendUpdate(new uint[] { 1, 2, 3 }, DataType.StatusFlag, true);
        });
        Assert.Single(f.Queue);
        Assert.Null(f.Owner.Player.View);
    });

    [Fact]
    public Task ArrayNullFaultDoesNotPublishAnOwnerSendAndEmptyArrayStillSendsRequiredUid() => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeSpellTestFixture();
        Assert.Throws<NullReferenceException>(() => f.Owner.Player.SendUpdate((uint[])null!, DataType.StatusFlag));
        Assert.Empty(f.Queue);
        f.Owner.Player.SendUpdate(Array.Empty<uint>(), DataType.StatusFlag);
        Assert.Equal(Convert.FromHexString("0A008008081112020819"), Assert.Single(f.Queue));
    });
}
