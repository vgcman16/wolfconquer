using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using ItemQuery = WolfConquer.Core.World.LegacyNativeMsgNinja.MsgNinjaItem;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    // A lease of the actual physical Pool reference, not a lookup substitute.
    private sealed class NinjaItemPoll : IDisposable
    {
        private readonly ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? previous = LegacyNativePoolClients.GamePoll;
        internal ConcurrentDictionary<uint, LegacyNativeGameClientRuntime> Rows { get; } = new();

        internal NinjaItemPoll() => LegacyNativePoolClients.GamePoll = Rows;
        public void Dispose() => LegacyNativePoolClients.GamePoll = previous;
    }

    private static Action<LegacyNativeGameClientRuntime, LegacyNativePacket> NinjaItemReceiver()
    {
        MethodInfo? method = typeof(ItemQuery).GetMethod("Process", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        Assert.True(method.IsPrivate);
        return method.CreateDelegate<Action<LegacyNativeGameClientRuntime, LegacyNativePacket>>();
    }

    private static byte[] NinjaViewInput(Scope scope, int type = 1, uint uid = 77,
        params (uint Id, uint Level, uint Position, uint Points, uint Uk)[] rows)
    {
        byte[] input = ItemFrame((ItemQuery.TypeID)type, uid, rows);
        CopyInto(scope, input);
        scope.Packet.Seek(237); // The genuine decoder must start at its own absolute header.
        return input;
    }

    private static void NinjaViewInputUnchanged(Scope scope, byte[] input)
    {
        Assert.Equal(input, NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(input.Length, scope.Packet.Size);
        Assert.Equal(input.Length, scope.Packet.Position);
    }
}
