using WolfConquer.Core.World;
using ItemQuery = WolfConquer.Core.World.LegacyNativeMsgNinja.MsgNinjaItem;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData("0500590908", false)]
    [InlineData("07005909080110", false)]
    [InlineData("080059090800", true)]
    public Task ItemMalformedPayloadCannotReplaceOriginalRowsOrConsumeAnyResponse(string hex, bool invalidFrame) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); byte[] input = Convert.FromHexString(hex); CopyInto(scope, input); scope.Packet.Seek(237);
        var old = RetainedDecodedItem(); var rows = old.Items; var row = rows[0]; var output = old;
        if (invalidFrame) Assert.Throws<InvalidDataException>(() => scope.Packet.GetNinjaItem(out output));
        else Assert.Throws<EndOfStreamException>(() => scope.Packet.GetNinjaItem(out output));
        AssertRetainedDecodedItem(old, rows, row, output);
        Assert.Equal(237, scope.Packet.Position); Assert.Equal(input.Length, scope.Packet.Size);
        Assert.Equal(input, NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(12, scope.Available); scope.Untaken(0); Assert.Empty(scope.Queue);
    });

    [Fact]
    public unsafe Task ItemDisposedInputRetainsTheOriginalModelAndDoesNotTakeResponseStorage() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); CopyInto(scope, [4, 0, 0x59, 0x09]); scope.Packet.Seek(237);
        var old = RetainedDecodedItem(); var rows = old.Items; var row = rows[0]; var output = old;
        nuint cursor = (nuint)scope.Packet.stream; scope.Packet.Dispose();
        Assert.Throws<ObjectDisposedException>(() => scope.Packet.GetNinjaItem(out output));
        AssertRetainedDecodedItem(old, rows, row, output);
        Assert.Equal(cursor, (nuint)scope.Packet.stream); Assert.Equal((nuint)0, (nuint)scope.Packet.Memory);
        Assert.Equal(4, scope.Packet.Size); Assert.Equal(12, scope.Available); scope.Untaken(0); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task ItemHeaderOnlyPayloadPublishesActualDefaultDtoWithoutInventingRepeatedRows() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); byte[] input = [4, 0, 0x59, 0x09]; CopyInto(scope, input); scope.Packet.Seek(237);
        var old = RetainedDecodedItem(); var rows = old.Items; var row = rows[0]; var output = old;
        scope.Packet.GetNinjaItem(out output);
        Assert.NotSame(old, output); Assert.Equal(ItemQuery.TypeID.Load, output.Type); Assert.Equal(0u, output.UID); Assert.Null(output.Items);
        AssertRetainedDecodedItem(old, rows, row, old);
        Assert.Equal(4, scope.Packet.Position); Assert.Equal(4, scope.Packet.Size);
        Assert.Equal(input, NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(12, scope.Available); scope.Untaken(0); Assert.Empty(scope.Queue);
    });

    private static ItemQuery RetainedDecodedItem() => new()
    {
        Type = ItemQuery.TypeID.Update, UID = 97,
        Items = [new() { ID = 91, Level = 92, Position = 93, Points = 94, UK = 95 }]
    };

    private static void AssertRetainedDecodedItem(ItemQuery old, ItemQuery.Item[] rows, ItemQuery.Item row, ItemQuery actual)
    {
        Assert.Same(old, actual); Assert.Equal(ItemQuery.TypeID.Update, actual.Type); Assert.Equal(97u, actual.UID);
        Assert.Same(rows, actual.Items); Assert.Same(row, Assert.Single(actual.Items));
        Assert.Equal(new uint[] { 91, 92, 93, 94, 95 }, new[] { row.ID, row.Level, row.Position, row.Points, row.UK });
    }
}
