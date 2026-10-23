using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeActivityPacketTests
{
    [Theory]
    [InlineData(0u, "0500390800")] [InlineData(1u, "0500390801")]
    [InlineData(256u, "0500390800")] [InlineData(uint.MaxValue, "05003908FF")]
    public Task ClaimWriterNarrowsToOneByteAndReaderWidensThatByteOnly(uint value, string expected) => NativePacketTestThread.Run(() =>
    {
        using var fixture = new NativeSpellTestFixture();
        fixture.Packet.ActivityClaimsCreate(value);
        Assert.Equal(Convert.FromHexString(expected), NativeSpellTestFixture.Bytes(fixture.Packet));
        fixture.Packet.Seek(4);
        fixture.Packet.GetActivityClaims(out var read);
        Assert.Equal(unchecked((uint)(byte)value), read);
        Assert.Equal(5, fixture.Packet.Position);
        Assert.Equal(1, Marshal.SizeOf<LegacyNativeMsgActivityTasks>());
        Assert.Equal(1, Marshal.SizeOf<LegacyNativeMsgActivityRewardsInfo>());
    });

    [Fact]
    public Task NullAndEmptyTaskArraysEmitTheSameNativeSixBytePacketAndDoNotTouchUserState() => NativePacketTestThread.Run(() =>
    {
        using var fixture = new NativeSpellTestFixture();
        fixture.Packet.ActivityTasksCreate(LegacyNativeMsgActivityTasks.Action.InitializeList);
        Assert.Equal(Convert.FromHexString("06000C090300"), NativeSpellTestFixture.Bytes(fixture.Packet));
        fixture.Packet.ActivityTasksCreate(LegacyNativeMsgActivityTasks.Action.InitializeList, []);
        Assert.Equal(Convert.FromHexString("06000C090300"), NativeSpellTestFixture.Bytes(fixture.Packet));
        Assert.Empty(fixture.Queue);
    });

    [Fact]
    public Task TaskCountWrapDoesNotTruncateTheActualArrayLoop() => NativePacketTestThread.Run(() =>
    {
        using var fixture = new NativeSpellTestFixture();
        var tasks = Enumerable.Range(0, 256).Select(value => new LegacyNativeActivenessRuntime.Task { ID = (uint)value }).ToArray();
        fixture.Packet.ActivityTasksCreate((LegacyNativeMsgActivityTasks.Action)255, tasks);
        var bytes = NativeSpellTestFixture.Bytes(fixture.Packet);
        Assert.Equal(1542, bytes.Length);
        Assert.Equal((byte)255, bytes[4]);
        Assert.Equal((byte)0, bytes[5]);
        Assert.Equal(255u, BitConverter.ToUInt32(bytes, 6 + 255 * 6));
    });

    [Fact]
    public Task NullLaterTaskLeavesTheExactNativePartialWriteAndUnfinalizedHeader() => NativePacketTestThread.Run(() =>
    {
        using var fixture = new NativeSpellTestFixture();
        Poison(fixture.Packet);
        Assert.Throws<NullReferenceException>(() => fixture.Packet.ActivityTasksCreate(
            LegacyNativeMsgActivityTasks.Action.InitializeList, [new() { ID = 7, Completed = 8, Progres = 9 }, null!]));
        Assert.Equal(12, fixture.Packet.Position);
        Assert.Equal(321, fixture.Packet.Size);
        var expected = Enumerable.Repeat((byte)0xA5, 28).ToArray();
        Convert.FromHexString("0302070000000809").CopyTo(expected, 4);
        Assert.Equal(expected, Memory(fixture.Packet, 28));
    });

    private static unsafe void Poison(LegacyNativePacket packet)
    {
        new Span<byte>(packet.Memory, LegacyNativePacket.MAX_SIZE).Fill(0xA5);
        packet.Size = 321; packet.Seek(237);
    }
    private static unsafe byte[] Memory(LegacyNativePacket packet, int length) => new ReadOnlySpan<byte>(packet.Memory, length).ToArray();
}
