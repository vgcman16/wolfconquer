using WolfConquer.Core.World;
using Query = WolfConquer.Core.World.LegacyNativeMsgNinja.MsgNinjaInfo;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaInfoBehaviorTests
{
    [Theory]
    [InlineData("05005A0908", false)]
    [InlineData("07005A09080110", false)]
    [InlineData("08005A090800", true)]
    public Task InfoMalformedPayloadRetainsTheOriginalReferenceAndAllFields(string hex, bool invalidFrame) => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); byte[] input = Convert.FromHexString(hex);
        Copy(scope.Packet, input); scope.Packet.Seek(237);
        var expected = new Query { Type = Query.TypeID.Hat, ID = 91, Points = 92, Position = 93 };
        var output = new Query { Type = expected.Type, ID = expected.ID, Points = expected.Points, Position = expected.Position };
        var old = output;
        if (invalidFrame) Assert.Throws<InvalidDataException>(() => scope.Packet.GetNinjaInfo(out output));
        else Assert.Throws<EndOfStreamException>(() => scope.Packet.GetNinjaInfo(out output));
        Assert.Same(old, output); AssertValues(expected, output);
        Assert.Equal(237, scope.Packet.Position); Assert.Equal(input.Length, scope.Packet.Size);
        Assert.Equal(input, NativeSpellTestFixture.Bytes(scope.Packet)); scope.NoRecycleOrSend();
    });

    [Fact]
    public unsafe Task InfoDisposedInputCannotReplaceTheOriginalOutReference() => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); Copy(scope.Packet, [4, 0, 0x5A, 0x09]); scope.Packet.Seek(237);
        var old = new Query { Type = Query.TypeID.Hat, ID = 91, Points = 92, Position = 93 }; var output = old;
        nuint cursor = (nuint)scope.Packet.stream; scope.Packet.Dispose();
        Assert.Throws<ObjectDisposedException>(() => scope.Packet.GetNinjaInfo(out output));
        Assert.Same(old, output); AssertValues(new Query { Type = Query.TypeID.Hat, ID = 91, Points = 92, Position = 93 }, output);
        Assert.Equal(cursor, (nuint)scope.Packet.stream); Assert.Equal((nuint)0, (nuint)scope.Packet.Memory);
        Assert.Equal(4, scope.Packet.Size); scope.NoRecycleOrSend();
    });

    [Fact]
    public Task InfoHeaderOnlyPayloadPublishesRealDefaultsFromTheExistingBeyondSizeCursor() => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope(); byte[] input = [4, 0, 0x5A, 0x09]; Copy(scope.Packet, input); scope.Packet.Seek(237);
        var old = new Query { Type = Query.TypeID.Hat, ID = 91, Points = 92, Position = 93 }; var output = old;
        scope.Packet.GetNinjaInfo(out output);
        Assert.NotSame(old, output); AssertValues(new Query(), output);
        AssertValues(new Query { Type = Query.TypeID.Hat, ID = 91, Points = 92, Position = 93 }, old);
        Assert.Equal(4, scope.Packet.Position); Assert.Equal(4, scope.Packet.Size);
        Assert.Equal(input, NativeSpellTestFixture.Bytes(scope.Packet)); scope.NoRecycleOrSend();
    });
}
