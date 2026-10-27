using System.Buffers.Binary;
using System.Runtime.InteropServices;
using WolfConquer.Core.World;
using ItemQuery = WolfConquer.Core.World.LegacyNativeMsgNinja.MsgNinjaItem;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Fact]
    public Task PrimitiveItemEncoderKeepsOriginalStreamAndDecoderCreatesIndependentFullWidthRows() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var item = new ItemQuery.Item { ID = uint.MaxValue, Level = 2, Position = 3, Points = 4, UK = 5 };
            var query = new ItemQuery { Type = ItemQuery.TypeID.Update, UID = 123, Items = [item, null!, item] };
            byte[] expected = ItemFrame(ItemQuery.TypeID.Update, 123,
                (uint.MaxValue, 2, 3, 4, 5), (uint.MaxValue, 2, 3, 4, 5));
            Assert.Same(scope.Packet, scope.Packet.CreateNinjaItem(query));
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(4, scope.Packet.Position); Assert.Equal(12, scope.Available);
            scope.Packet.Seek(237); // Native decoder always seeks to its own header.
            scope.Packet.GetNinjaItem(out var parsed);
            Assert.NotSame(query, parsed); Assert.Equal(ItemQuery.TypeID.Update, parsed.Type); Assert.Equal(123u, parsed.UID);
            Assert.Equal(2, parsed.Items.Length);
            Assert.NotSame(parsed.Items[0], parsed.Items[1]);
            Assert.All(parsed.Items, row =>
            {
                Assert.NotSame(item, row);
                Assert.Equal(new uint[] { uint.MaxValue, 2, 3, 4, 5 }, new[] { row.ID, row.Level, row.Position, row.Points, row.UK });
            });
            Assert.Equal(expected.Length, scope.Packet.Position);
            Assert.Equal(expected.Length, scope.Packet.Size);
            Assert.Equal(3, query.Items.Length); Assert.Null(query.Items[1]); Assert.Same(item, query.Items[2]);
            Assert.Empty(scope.Queue); scope.Untaken(0);
        });

    [Fact]
    public Task ItemDecoderUsesDeclaredPayloadAndIgnoresOpcodeWithoutTakingRecycledPackets() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            byte[] input = ItemFrame(ItemQuery.TypeID.Add, 55, (0, 0, 0, 0, 0));
            BinaryPrimitives.WriteUInt16LittleEndian(input.AsSpan(2), 9999);
            CopyInto(scope, input);
            scope.Packet.Seek(300);
            scope.Packet.GetNinjaItem(out var parsed);
            Assert.Equal(ItemQuery.TypeID.Add, parsed.Type); Assert.Equal(55u, parsed.UID);
            var row = Assert.Single(parsed.Items);
            Assert.Equal(new uint[5], new[] { row.ID, row.Level, row.Position, row.Points, row.UK });
            Assert.Equal(input.Length, scope.Packet.Position);
            Assert.Equal(12, scope.Available); Assert.Empty(scope.Queue); scope.Untaken(0);
        });

    [Fact]
    public Task ItemDecoderAssignsFreshOutObjectBeforeNullStreamOrHeaderUnderflowFaults() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            ItemQuery parsed = null!;
            Assert.Throws<NullReferenceException>(() => LegacyNativeMsgNinja.GetNinjaItem(null!, out parsed));
            Assert.NotNull(parsed); Assert.Equal(ItemQuery.TypeID.Load, parsed.Type); Assert.Null(parsed.Items);
            ItemQuery before = parsed;
            CopyInto(scope, [3, 0, 89, 9]); // Header length3 yields native negative payload allocation.
            Assert.Throws<OverflowException>(() => scope.Packet.GetNinjaItem(out parsed));
            Assert.NotSame(before, parsed); Assert.NotNull(parsed); Assert.Null(parsed.Items);
            Assert.Equal(4, scope.Packet.Position); Assert.Equal(12, scope.Available); Assert.Empty(scope.Queue);
        });

    [Fact]
    public Task MissingItemMessageRefusesUnchangedAndGenuineDefaultMessageKeepsRequiredFields() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var before = NativeSpellTestFixture.Bytes(scope.Packet);
            var position = scope.Packet.Position;
            Assert.Throws<ArgumentNullException>(() => scope.Packet.CreateNinjaItem(null!));
            Assert.Equal(before, NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(position, scope.Packet.Position);
            Assert.Same(scope.Packet, scope.Packet.CreateNinjaItem(new ItemQuery()));
            Assert.Equal(Convert.FromHexString("0800590908001000"), NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(4, scope.Packet.Position);
            Assert.Throws<InvalidOperationException>(() => LegacyNativeMsgNinja.CreateNinjaItem(null!, new ItemQuery()));
            Assert.Equal(12, scope.Available); scope.Untaken(0); Assert.Empty(scope.Queue);
        });

    private static unsafe void CopyInto(Scope scope, byte[] bytes)
    {
        Marshal.Copy(bytes, 0, (IntPtr)scope.Packet.Memory, bytes.Length);
        scope.Packet.Size = bytes.Length;
    }
}
