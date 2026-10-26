using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatGearWireTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(65536)]
    public void RawStreamRejectsMissingHeaderAndUnrepresentableLengthUnchanged(int length)
    {
        using var scope = new NativeCombatGearQueryScope();
        var bytes = FramePattern(length);
        using var stream = new MemoryStream(bytes, writable: false);
        stream.Position = length + 7L;
        Assert.Equal("stream", Assert.Throws<ArgumentOutOfRangeException>(() => LegacyNativeCombatGearWire.WrapStream(stream)).ParamName);
        AssertInputUnchanged(stream, bytes, length + 7L);
        Assert.False(stream.CanWrite);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(65535)]
    public void RawStreamAcceptsMinimumAndMaximumHeaderLengthsWithoutChangingInput(int length)
    {
        using var scope = new NativeCombatGearQueryScope();
        var bytes = FramePattern(length); bytes[2] = 0xEF; bytes[3] = 0xBE;
        using var stream = new MemoryStream(bytes, writable: false);
        stream.Position = length - 1;
        var frame = LegacyNativeCombatGearWire.WrapStream(stream);
        AssertBoundedFrame(frame, bytes[4..], 0xBEEF);
        AssertInputUnchanged(stream, bytes, length - 1);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 65535)]
    [InlineData(65531, 65535)]
    public void ObjectPayloadAcceptsEmptyAndMaximumRepresentableBytesAndCallerOpcode(int length, int opcode)
    {
        using var scope = new NativeCombatGearQueryScope();
        var bytes = FramePattern(length);
        using var stream = new MemoryStream(bytes, writable: false);
        stream.Position = length + 3L;
        var frame = ObjectPayloadWrapper(stream, checked((ushort)opcode));
        AssertBoundedFrame(frame, bytes, checked((ushort)opcode));
        AssertInputUnchanged(stream, bytes, length + 3L);
    }

    [Theory]
    [InlineData(65532)]
    [InlineData(70000)]
    public void ObjectPayloadRejectsHeaderOverflowWithoutChangingBytesOrPosition(int length)
    {
        using var scope = new NativeCombatGearQueryScope();
        var bytes = FramePattern(length);
        using var stream = new MemoryStream(bytes);
        stream.Position = 3;
        Assert.Equal("stream", Assert.Throws<ArgumentOutOfRangeException>(() => ObjectPayloadWrapper(stream, 2421)).ParamName);
        AssertInputUnchanged(stream, bytes, 3);
    }

    [Fact]
    public void BothFrameHelpersClearlyRejectMissingOrClosedStreamsWhileInactiveEntryDoesNoWork()
    {
        using var scope = new NativeCombatGearQueryScope();
        Assert.Equal("stream", Assert.Throws<ArgumentNullException>(() => LegacyNativeCombatGearWire.WrapStream(null!)).ParamName);
        Assert.Equal("stream", Assert.Throws<ArgumentNullException>(() => ObjectPayloadWrapper(null!, 0)).ParamName);
        var bytes = FramePattern(8);
        var closed = new MemoryStream(bytes); closed.Position = 3; closed.Dispose();
        Assert.Throws<ObjectDisposedException>(() => LegacyNativeCombatGearWire.WrapStream(closed));
        Assert.Throws<ObjectDisposedException>(() => ObjectPayloadWrapper(closed, 0));
        Assert.Equal(bytes, closed.ToArray());
        LegacyNativeCombatGearTable.m_Load = false;
        Assert.Null(LegacyNativeCombatGearWire.WrapStream(closed));
        Assert.Null(LegacyNativeCombatGearWire.WrapStream(null!));
        foreach (var route in NullProjectionRoutes()) Assert.Null(route());
        Assert.Equal(bytes, closed.ToArray());
    }

    [Fact]
    public void AllSixActiveTypedProjectionsRefuseAbsentOuterMessages()
    {
        using var scope = new NativeCombatGearQueryScope();
        var routes = NullProjectionRoutes();
        Assert.Equal(6, routes.Length);
        foreach (var route in routes) Assert.Equal("message", Assert.Throws<ArgumentNullException>(() => route()).ParamName);
    }
}
