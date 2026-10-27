using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaMutationTests
{
    // Attribute-free adapter. Only the corrected-only test class may call it.
    internal static Task RunCorrectedOnlyFlagCapacityFailure(bool remove) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Ninja.Flag = remove ? 1UL : 0;
        var before = new FlagMutationWitness(scope);
        using var tiny = new LegacyNativePacket(8);
        unsafe { Marshal.Copy(Enumerable.Repeat((byte)0xA5, tiny.Capacity).ToArray(), 0, (nint)tiny.Memory, tiny.Capacity); }
        tiny.Size = 123;
        tiny.Seek(0);
        var storage = new PacketImage(tiny);
        var bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        bin.SetValue(null, new ConcurrentQueue<LegacyNativePacket>([tiny]));
        Exception? error;
        try { error = Record.Exception(() => InvokeFlagMutation(scope, remove)); }
        finally { scope.RestoreOwnedBin(); }

        before.RequireUnchanged();
        storage.RequireUnchanged();
        Assert.IsType<InvalidOperationException>(error);
    });
}
