using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    // No test attribute. Only the corrected-only class invokes either adapter.
    internal static Task RunCorrectedOnlyLoadingRentalFailure(int tinyGroup) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        SeedLoadingTransaction(scope);
        var before = new LoadingTransactionWitness(scope);
        var packets = new List<LegacyNativePacket>();
        try
        {
            for (var index = 0; index < 4; index++)
            {
                var packet = new LegacyNativePacket(index == tinyGroup ? 8 : LegacyNativePacket.MAX_SIZE);
                packets.Add(packet);
                PrimeCorrectedLoadingPacket(packet);
            }
            typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!
                .SetValue(null, new ConcurrentQueue<LegacyNativePacket>(packets));
            Exception? error;
            try { error = Record.Exception(() => scope.Ninja.Loading()); }
            finally { scope.RestoreOwnedBin(); }
            before.RequireUnchanged();
            Assert.IsType<InvalidOperationException>(error);
            foreach (var packet in packets) RequireUnwrittenCorrectedLoadingPacket(packet);
        }
        finally
        {
            scope.RestoreOwnedBin();
            foreach (var packet in packets) packet.Dispose();
        }
    });

    internal static Task RunCorrectedOnlySpellCallerFailure(string operation) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        SeedSpellTransaction(scope);
        using var packet = new LegacyNativePacket(8);
        PrimeCorrectedLoadingPacket(packet);
        var caller = new LegacyNativePacketMutationWitness(packet);
        var before = new LoadingTransactionWitness(scope);
        var error = Record.Exception(() => InvokeSpellTransaction(scope, operation, packet));
        before.RequireUnchanged();
        caller.RequireUnchanged();
        Assert.IsType<InvalidOperationException>(error);
    });

    internal static Task RunCorrectedOnlyLoadingEnvelopeFailure() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        SeedLoadingTransaction(scope);
        // The old allocation is large enough, but old Send reaches NativeConsole
        // and its possible unowned exception-file path. Do not run that baseline.
        for (uint index = 0; index < 40; index++)
            scope.Item(1000 + index, 1000 + index, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue);
        var before = new LoadingTransactionWitness(scope);
        var error = Record.Exception(() => scope.Ninja.Loading());
        before.RequireUnchanged();
        Assert.IsType<InvalidDataException>(error);
    });

    private static unsafe void PrimeCorrectedLoadingPacket(LegacyNativePacket packet)
    {
        Marshal.Copy(Enumerable.Repeat((byte)0xa5, packet.Capacity).ToArray(), 0, (nint)packet.Memory, packet.Capacity);
        packet.Size = 123;
        packet.Seek(0);
    }

    private static unsafe void RequireUnwrittenCorrectedLoadingPacket(LegacyNativePacket packet)
    {
        Assert.NotEqual(nint.Zero, (nint)packet.Memory);
        Assert.Equal(123, packet.Size);
        Assert.Equal(0, packet.Position);
        var bytes = new byte[packet.Capacity];
        Marshal.Copy((nint)packet.Memory, bytes, 0, bytes.Length);
        Assert.All(bytes, value => Assert.Equal((byte)0xa5, value));
    }
}
