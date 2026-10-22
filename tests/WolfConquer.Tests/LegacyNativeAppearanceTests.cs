using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeAppearanceTests
{
    [Fact]
    public Task ColdSettersKeepZeroDefaultsAndNativeOverflowWithoutAllocatingPackets() => NativePacketTestThread.Run(() =>
    {
        var owner = new LegacyNativeGameClientRuntime();
        var player = owner.Player;
        Assert.Equal((ushort)0, player.Body);
        Assert.Equal((ushort)0, player.Face);
        Assert.Equal((ushort)0, player.TransformationID);
        Assert.Equal(0u, player.Mesh);
        DeepCall(85, () =>
        {
            player.Body = 65535;
            player.Face = 65535;
            player.TransformationID = 65535;
        });
        Assert.Equal(unchecked((uint)(65535 * 10_000_000 + 65535 * 10_000 + 65535)), player.Mesh);
        Assert.False(owner.FullLoading);
        Assert.Null(owner.Socket);
        Assert.Null(owner.Map);
    });

    [Theory]
    [InlineData("Body")]
    [InlineData("Face")]
    [InlineData("TransformationID")]
    public Task HotSetterCommitsBeforeTheRealPacketStackGuardFault(string property) => NativePacketTestThread.Run(() =>
    {
        var owner = new LegacyNativeGameClientRuntime { FullLoading = true };
        var player = owner.Player;
        Action assign = property switch
        {
            "Body" => () => player.Body = 2002,
            "Face" => () => player.Face = 2002,
            _ => () => player.TransformationID = 2002,
        };
        var error = Assert.Throws<Exception>(() => DeepCall(85, assign));
        Assert.Equal("[WARNING]: Stack trace call stack's frame count excceeded 70", error.Message);
        Assert.Equal((ushort)2002, typeof(LegacyNativePlayerRuntime).GetProperty(property)!.GetValue(player));
        Assert.Null(owner.Socket);
        Assert.True(owner.FullLoading);
    });

    [Fact]
    public Task BodyBroadcastsAfterOwnerAndKeepsDuplicatesWhileFaceOnlySendsToOwner() => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope();
        var queue = new Queue<byte[]>();
        var owner = new LegacyNativeGameClientRuntime { Socket = Socket(queue) };
        var player = owner.Player;
        player.UID = 17;
        player.Map = 1002;
        player.X = 20;
        player.Y = 20;
        player.Body = 1002;
        player.Face = 154;
        var view = player.View;
        // Explicit component prestate; no native LoadMap/host map adoption claim.
        owner.Map = new LegacyNativeGameMap(72, 72, 1002) { View = new LegacyNativeMapView(72, 72) };
        var seen = new List<byte[]>();
        var visible = new Recipient
        {
            UID = 18, Map = 1002, X = 21, Y = 20,
            OnSend = packet =>
            {
                Assert.Single(queue); // Actual owner queue is populated before view callbacks.
                seen.Add(Copy(packet));
            },
        };
        owner.Map.View.EnterMap(visible);
        owner.Map.View.EnterMap(visible); // Native MapView does not deduplicate.
        owner.Map.View.EnterMap(new Recipient { UID = 19, Map = 1003, X = 21, Y = 20 });
        owner.Map.View.EnterMap(new Recipient { UID = 20, Map = 1002, X = 39, Y = 20 });
        owner.FullLoading = true;

        player.Body = 2002;

        var bodyFrame = queue.Dequeue();
        Assert.Equal(2, seen.Count);
        Assert.All(seen, bytes => Assert.Equal(bodyFrame, bytes));
        AssertMesh(bodyFrame, 17, 1_542_002);
        Assert.Same(view, player.View);
        Assert.Same(owner, view.Owner);
        seen.Clear();

        player.Face = 155;

        Assert.Empty(seen);
        AssertMesh(queue.Dequeue(), 17, 1_552_002);
        player.TransformationID = 1;
        Assert.Equal(2, seen.Count);
        AssertMesh(queue.Dequeue(), 17, 11_552_002);
        Assert.Same(owner, player.Owner);
        Assert.True(owner.FullLoading);
    });

    [Fact]
    public Task ViewFailureEscapesAfterStateAndOwnerPacketArePublished() => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope();
        var queue = new Queue<byte[]>();
        var owner = new LegacyNativeGameClientRuntime { Socket = Socket(queue) };
        owner.Player.UID = 17;
        owner.Player.Map = 1002;
        owner.Map = new LegacyNativeGameMap(18, 18, 1002) { View = new LegacyNativeMapView(18, 18) };
        var expected = new InvalidOperationException("MODEL recipient failure");
        owner.Map.View.EnterMap(new Recipient { UID = 18, Map = 1002, OnSend = _ => throw expected });
        var later = new Recipient { UID = 19, Map = 1002 };
        owner.Map.View.EnterMap(later);
        owner.FullLoading = true;

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => owner.Player.Body = 1008));

        Assert.Equal((ushort)1008, owner.Player.Body);
        AssertMesh(Assert.Single(queue), 17, 1008);
        Assert.Equal(0, later.Sends);
    });

    [Fact]
    public Task FakeOwnerGateDoesNotSuppressTheSeparateBodyViewBroadcast() => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope();
        var owner = new LegacyNativeGameClientRuntime { FullLoading = true, Fake = true };
        owner.Player.Map = 1002;
        owner.Player.UID = 17;
        owner.Map = new LegacyNativeGameMap(18, 18, 1002) { View = new LegacyNativeMapView(18, 18) };
        var recipient = new Recipient { UID = 18, Map = 1002 };
        owner.Map.View.EnterMap(recipient);

        owner.Player.Body = 1008;

        Assert.Equal(1, recipient.Sends);
        Assert.Null(owner.Socket);
        Assert.True(owner.Fake);
        Assert.Equal((ushort)1008, owner.Player.Body);
    });

    private static void AssertMesh(byte[] bytes, uint uid, uint mesh)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        Assert.True(LegacyMsgUpdatePacket.TryParse(frame, out var update));
        Assert.Equal(uid, update.Uid);
        var entry = Assert.Single(update.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.Mesh, entry.Type);
        Assert.Equal((ulong)mesh, entry.Value);
    }

    private static unsafe byte[] Copy(LegacyNativePacket packet) =>
        new ReadOnlySpan<byte>(packet.Memory, packet.Size).ToArray();

    private static LegacyNativeSecuritySocket Socket(Queue<byte[]> queue)
    {
        var socket = new LegacyNativeSecuritySocket(null!, null!) { Alive = true };
        typeof(LegacyNativeSecuritySocket).GetField("SendQueue", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(socket, queue); // Explicit MODEL queue, not native Create/OS send proof.
        return socket;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DeepCall(int depth, Action action)
    {
        if (depth == 0) action();
        else DeepCall(depth - 1, action);
        GC.KeepAlive(depth);
    }

    // MODEL interface recipient, not native Player/GetArray implementation or a live client.
    private sealed class Recipient : ILegacyNativeMapObj
    {
        public bool AllowDynamic { get; set; }
        public uint UID { get; set; }
        public ushort X { get; set; }
        public ushort Y { get; set; }
        public uint Map { get; set; }
        public uint DynamicID => 0;
        public bool Alive => false;
        public uint IndexInScreen { get; set; }
        public LegacyNativeMapObjectType ObjType => LegacyNativeMapObjectType.Player;
        internal Action<LegacyNativePacket>? OnSend;
        internal int Sends;
        public bool IsTrap() => throw new NotSupportedException();
        public LegacyNativePacket GetArray(LegacyNativePacket stream, bool View, bool st) => throw new NotSupportedException();
        public void Send(LegacyNativePacket msg) { Sends++; OnSend?.Invoke(msg); }
    }

    private sealed class SealScope : IDisposable
    {
        private static readonly FieldInfo Field = typeof(LegacyNativePacket).GetField("seal", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? previous = Field.GetValue(null);
        internal SealScope() => Field.SetValue(null, null);
        public void Dispose() => Field.SetValue(null, previous);
    }
}
