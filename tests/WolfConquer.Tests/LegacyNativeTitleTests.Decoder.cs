using WolfConquer.Core.World;
using Message = WolfConquer.Core.World.LegacyNativeMsgTitleStorage;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTitleTests
{
    [Fact]
    public Task TitleDecoderRewindsCallerCursorUsesHeaderLengthIgnoresOpcodeAndDoesNotUseSize() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            // Root's observed pinned action5 payload. The deliberately unrelated
            // opcodeCAFE does not alter the native generic protobuf read path.
            byte[] input = Convert.FromHexString("1800FECA0805100718A11F20032A0908A11F100318642008");
            scope.Input(input);
            var old = new Message.TitleStorage { dwparam1 = 999 };
            var result = old;
            scope.Packet.GetTitleStorage(out result);
            Assert.NotSame(old, result);
            Assert.Equal(Message.Action.UnEquip, result.ActionID);
            Assert.Equal(7u, result.dwparam1);
            Assert.Equal(4001u, result.dwparam2);
            Assert.Equal(3u, result.dwparam3);
            Assert.NotNull(result.Title);
            Assert.Equal(4001u, result.Title.ID);
            Assert.Equal(3u, result.Title.SubId);
            Assert.Equal(100u, result.Title.dwparam1);
            Assert.Equal(8u, result.Title.dwparam2);
            Assert.Equal(24, scope.Packet.Position);
            Assert.Equal(321, scope.Packet.Size);
            Assert.Equal(input, scope.Memory(input.Length));
            Assert.Empty(scope.Queue);
        });

    [Fact]
    public void TitleDecoderPublishesNewDefaultOuterBeforeNullStreamFault()
    {
        var old = new Message.TitleStorage { dwparam1 = 999, Title = new Message.Title { ID = 17 } };
        var result = old;
        Assert.Throws<NullReferenceException>(() => Message.GetTitleStorage(null!, out result));
        Assert.NotSame(old, result);
        Assert.Equal(Message.Action.UpdateScore, result.ActionID);
        Assert.Equal(0u, result.dwparam1);
        Assert.Equal(0u, result.dwparam2);
        Assert.Equal(0u, result.dwparam3);
        Assert.Null(result.Title);
    }

    [Fact]
    public Task InvalidShortDeclaredLengthFaultsAfterHeaderReadBeforePayloadOrModelRead() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            foreach (byte length in new byte[] { 0, 1, 3 })
            {
                scope.Input([length, 0, 9, 9]);
                var old = new Message.TitleStorage { dwparam1 = 999 };
                var result = old;
                Assert.Throws<OverflowException>(() => scope.Packet.GetTitleStorage(out result));
                Assert.NotSame(old, result);
                Assert.Equal(0u, result.dwparam1);
                Assert.Null(result.Title);
                Assert.Equal(4, scope.Packet.Position);
                Assert.Equal(321, scope.Packet.Size);
                Assert.Equal(new byte[] { length, 0, 9, 9 }, scope.Memory(4));
                Assert.Empty(scope.Queue);
            }
        });
}
