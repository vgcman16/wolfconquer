using System.Reflection;
using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeClassExperienceTests
{
    [Fact]
    public Task ColdSetterKeepsNativeDefaultsAndWorksBeyondThePacketConstructorStackLimit() => NativePacketTestThread.Run(() =>
    {
        var owner = new LegacyNativeGameClientRuntime();
        Assert.False(owner.FullLoading);
        Assert.False(owner.Fake);
        Assert.Null(owner.Socket);
        Assert.Equal(0U, owner.Player.ClassExperience);
        DeepCall(85, () => owner.Player.ClassExperience = uint.MaxValue);
        Assert.Equal(uint.MaxValue, owner.Player.ClassExperience);
        Assert.False(owner.FullLoading);
        Assert.Null(owner.Socket);
    });

    [Fact]
    public Task HotSetterCommitsBeforeTheGenuinePacketStackGuardFault() => NativePacketTestThread.Run(() =>
    {
        var owner = new LegacyNativeGameClientRuntime { FullLoading = true };
        var error = Assert.Throws<Exception>(() => DeepCall(85, () => owner.Player.ClassExperience = 73));
        Assert.Equal("[WARNING]: Stack trace call stack's frame count excceeded 70", error.Message);
        Assert.Equal(73U, owner.Player.ClassExperience);
        Assert.True(owner.FullLoading);
        Assert.Null(owner.Socket);
    });

    [Fact]
    public Task HotSetterQueuesEveryAssignmentThroughItsActualOwnerEvenWhenAnotherClientHoldsThePlayer() => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope();
        var queue = new Queue<byte[]>();
        var physicalOwner = new LegacyNativeGameClientRuntime { FullLoading = true, Socket = Socket(queue) };
        var holderQueue = new Queue<byte[]>();
        var holder = new LegacyNativeGameClientRuntime { FullLoading = false, Socket = Socket(holderQueue) };
        var player = physicalOwner.Player;
        player.UID = 17;
        holder.Player = player; // Controlled physical-reference prestate, not host adoption.
        var flags = player.BitVector;
        var random = player.MyRandom;

        holder.Player.ClassExperience = 300;
        holder.Player.ClassExperience = 300; // Native setter has no same-value suppression.

        Assert.Equal(300U, player.ClassExperience);
        Assert.Same(physicalOwner, player.Owner);
        Assert.Same(player, holder.Player);
        Assert.Same(flags, player.BitVector);
        Assert.Same(random, player.MyRandom);
        Assert.Equal(2, queue.Count);
        var first = queue.Dequeue();
        var second = queue.Dequeue();
        Assert.NotSame(first, second);
        Assert.Equal(Convert.FromHexString("0D00800808111205087910AC02"), first);
        Assert.Equal(first, second);
        Assert.Empty(holderQueue);
        Assert.False(holder.FullLoading);
    });

    [Fact]
    public Task HotSetterHonorsFakeAndDeadSocketSendGatesWithoutRepairingTheOwner() => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope();
        var fake = new LegacyNativeGameClientRuntime { FullLoading = true, Fake = true };
        fake.Player.ClassExperience = 41;
        Assert.Equal(41U, fake.Player.ClassExperience);
        Assert.Null(fake.Socket); // Fake short-circuits before the null Socket dereference.
        var queue = new Queue<byte[]>();
        var dead = new LegacyNativeGameClientRuntime { FullLoading = true, Socket = Socket(queue) };
        dead.Socket.Alive = false;
        dead.Player.ClassExperience = 42;
        Assert.Equal(42U, dead.Player.ClassExperience);
        Assert.Empty(queue);
        dead.Send((LegacyNativePacket?)null); // Packet overload; dead precedes the null-message gate.
        Assert.Empty(queue);
    });

    private static LegacyNativeSecuritySocket Socket(Queue<byte[]> queue)
    {
        var socket = new LegacyNativeSecuritySocket(null!, null!) { Alive = true };
        typeof(LegacyNativeSecuritySocket).GetField("SendQueue", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(socket, queue); // Explicit MODEL queue, never a native Create substitute.
        return socket;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DeepCall(int depth, Action body)
    {
        if (depth == 0) body();
        else DeepCall(depth - 1, body);
        GC.KeepAlive(depth); // No tail-call elimination of the actual physical stack.
    }

    private sealed class SealScope : IDisposable
    {
        private static readonly FieldInfo Field = typeof(LegacyNativePacket).GetField("seal", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? previous = Field.GetValue(null);
        internal SealScope() => Field.SetValue(null, null);
        public void Dispose() => Field.SetValue(null, previous);
    }
}
