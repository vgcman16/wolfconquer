using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeClanTests
{
    [Theory]
    [InlineData("")]
    [InlineData("0102")]
    [InlineData("07000000")]
    [InlineData("0700000009000000")]
    [InlineData("07000000090000000B000000")]
    [InlineData("07000000090000000B00000001")]
    [InlineData("07000000090000000B0000000101")]
    [InlineData("07000000090000000B00000001020041")]
    [InlineData("07000000090000000B00000002010041020042")]
    public Task IncompleteClanBodyPreservesEveryOutVariableAndOriginalPacketState(string hex) => NativePacketTestThread.Run(() =>
    {
        byte[] input = new byte[] { 0xA5, 0xA5, 0xA5, 0xA5, 0xA5 }.Concat(Convert.FromHexString(hex)).ToArray();
        using var packet = InitializedClanTarget(input);
        packet.Seek(5);
        var mode = (LegacyNativeMsgClan.Info)99; uint uid = 91, parameter = 92;
        string[] sentinel = ["before"]; string[]? list = sentinel;
        AssertClanPacketRefusal<EndOfStreamException>(packet, () => packet.GetClan(out mode, out uid, out parameter, out list));
        Assert.Equal((LegacyNativeMsgClan.Info)99, mode); Assert.Equal(91u, uid); Assert.Equal(92u, parameter);
        Assert.Same(sentinel, list); Assert.Equal(new[] { "before" }, list);
    });

    [Theory]
    [InlineData(-1)]
    [InlineData(18)]
    public Task InvalidClanInputSizePreservesEveryCallerOutputAndRawCursor(int size) => NativePacketTestThread.Run(() =>
    {
        using var packet = InitializedClanTarget(Convert.FromHexString("A5A5A5A598BADCFE090000000B00000000"));
        packet.Size = size; packet.Seek(4);
        var mode = (LegacyNativeMsgClan.Info)99; uint uid = 91, parameter = 92;
        string[] sentinel = ["before"]; string[]? list = sentinel;
        AssertClanPacketRefusal<InvalidOperationException>(packet, () => packet.GetClan(out mode, out uid, out parameter, out list));
        Assert.Equal((LegacyNativeMsgClan.Info)99, mode); Assert.Equal(91u, uid); Assert.Equal(92u, parameter);
        Assert.Same(sentinel, list); Assert.Equal(new[] { "before" }, list);
    });

    [Fact]
    public Task WholeClanReaderCommitsAtCurrentCursorAndPreservesUnknownModeAndTrailingInput() => NativePacketTestThread.Run(() =>
    {
        byte[] input = Convert.FromHexString("A5A5A5A598BADCFE090000000B0000000200000300E78BBCEE");
        using var packet = InitializedClanTarget(input); packet.Seek(4);
        packet.GetClan(out var mode, out var uid, out var parameter, out var list);
        Assert.Equal((LegacyNativeMsgClan.Info)0xFEDCBA98u, mode); Assert.Equal(9u, uid); Assert.Equal(11u, parameter);
        Assert.Equal(new[] { "", "狼" }, list); Assert.Equal(input.Length - 1, packet.Position);
        Assert.Equal(input.Length, packet.Size); Assert.Equal(input, NativeSpellTestFixture.Bytes(packet));
        Assert.Equal((byte)0xEE, packet.ReadUInt8()); Assert.Equal(input.Length, packet.Position);
    });

    [Fact]
    public Task NullClanReaderNeverAssignsCallerOutputs() => NativePacketTestThread.Run(() =>
    {
        var mode = (LegacyNativeMsgClan.Info)99; uint uid = 91, parameter = 92;
        string[] sentinel = ["before"]; string[]? list = sentinel;
        Assert.Throws<ArgumentNullException>(() => LegacyNativeClanPacket.GetClan(null!, out mode, out uid, out parameter, out list));
        Assert.Equal((LegacyNativeMsgClan.Info)99, mode); Assert.Equal(91u, uid); Assert.Equal(92u, parameter);
        Assert.Same(sentinel, list); Assert.Equal(new[] { "before" }, list);
    });

    [Theory]
    [InlineData(-1)]
    [InlineData(18)]
    public Task InvalidPhysicalCursorRefusesClanReadAndBuildWithoutPublishingAnything(int position) => NativePacketTestThread.Run(() =>
    {
        using var packet = InitializedClanTarget(Convert.FromHexString("A5A5A5A598BADCFE090000000B00000000"));
        packet.Seek(position);
        AssertClanPacketRefusal<InvalidOperationException>(packet, () => packet.ClanMembersCreate([]));
        var mode = (LegacyNativeMsgClan.Info)99; uint uid = 91, parameter = 92;
        string[] sentinel = ["before"]; string[]? list = sentinel;
        AssertClanPacketRefusal<InvalidOperationException>(packet, () => packet.GetClan(out mode, out uid, out parameter, out list));
        Assert.Equal((LegacyNativeMsgClan.Info)99, mode); Assert.Equal(91u, uid); Assert.Equal(92u, parameter);
        Assert.Same(sentinel, list); Assert.Equal(new[] { "before" }, list);
    });

    [Fact]
    public unsafe Task DisposedClanInputAndTargetRefuseWithoutResurrectingStorageOrChangingOutputs() => NativePacketTestThread.Run(() =>
    {
        using var packet = InitializedClanTarget(Convert.FromHexString("A5A5A5A598BADCFE090000000B00000000"));
        packet.Seek(4); nuint cursor = (nuint)packet.stream; int size = packet.Size;
        packet.Dispose();
        Assert.Throws<ObjectDisposedException>(() => packet.ClanCallBackCreate(LegacyNativeMsgClan.Info.Quit, 9, 11, null));
        var mode = (LegacyNativeMsgClan.Info)99; uint uid = 91, parameter = 92;
        string[] sentinel = ["before"]; string[]? list = sentinel;
        Assert.Throws<ObjectDisposedException>(() => packet.GetClan(out mode, out uid, out parameter, out list));
        Assert.Equal((nuint)0, (nuint)packet.Memory); Assert.Equal(cursor, (nuint)packet.stream); Assert.Equal(size, packet.Size);
        Assert.Equal((LegacyNativeMsgClan.Info)99, mode); Assert.Equal(91u, uid); Assert.Equal(92u, parameter);
        Assert.Same(sentinel, list); Assert.Equal(new[] { "before" }, list);
    });
}
