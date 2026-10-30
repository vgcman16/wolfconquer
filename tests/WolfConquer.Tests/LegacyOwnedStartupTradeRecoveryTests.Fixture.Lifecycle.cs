using System.Buffers;
using System.Net;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

internal sealed partial class LegacyOwnedStartupTradeRecoveryFixture
{
    internal void Own(IDisposable resource) => resources.Add(resource);
    internal void Own(LegacyOwnedAdmissionRuntime runtime) => runtimes.Add(runtime);

    internal async Task CloseAsync()
    {
        var errors = new List<Exception>();
        foreach (var connection in connections.Values)
        {
            try { await connection.DisconnectAsync(); } catch (Exception error) { errors.Add(error); }
            try { await connection.CompleteTransportCloseAsync(); } catch (Exception error) { errors.Add(error); }
        }
        connections.Clear();
        for (var index = runtimes.Count - 1; index >= 0; index--)
            try { await runtimes[index].DisposeAsync(); } catch (Exception error) { errors.Add(error); }
        runtimes.Clear();
        for (var index = resources.Count - 1; index >= 0; index--)
            try { resources[index].Dispose(); } catch (Exception error) { errors.Add(error); }
        resources.Clear();
        lifetimes.Clear();
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    internal async Task AssertFreshFrameAsync(LegacyOwnedAdmissionRuntime runtime)
    {
        resources.Add(new NativeLoginBeastMetadataScope(1000));
        Handler = new WorldServerFrameHandler(Data.Loader, null, CreateCatalog(), LegacyRuntimeUpdateLoopOptions.Disabled,
            TextWriter.Null, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: runtime);
        await LoginAsync(LeftUid); await LoginAsync(RightUid);
        foreach (var uid in new[] { LeftUid, RightUid })
        {
            var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(After[CharacterPath(uid)], out _));
            var current = Current(uid);
            Assert.Equal(saved.Money, current.Character.Money);
            Assert.Equal(saved.ConquerPoints, current.Character.ConquerPoints);
            Assert.Equal(saved.Money, current.NativeGameClient!.Player!.Money);
            Assert.Equal(checked((uint)saved.ConquerPoints), current.NativeGameClient.Player.ConquerPoints);
            var expected = PlayerItemSnapshotLoader.Parse(After[ItemsPath(uid)]);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expected), PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
            Assert.Equal(expected.InventoryItems.Select(item => item.Uid).Order(),
                current.NativeGameClient.Inventory!.ClientItems.Keys.Order());
            Assert.False(current.LegacyTradeActive);
        }
    }

    private async Task LoginAsync(uint uid)
    {
        var sent = new List<byte[]>(); frames[uid] = sent;
        var native = new LegacyNativeGameClientRuntime();
        var lifetime = new CancellationTokenSource();
        resources.Add(lifetime);
        lifetimes.Add(uid, lifetime);
        var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            (bytes, token) =>
            {
                token.ThrowIfCancellationRequested();
                sent.Add(bytes.ToArray());
                return ValueTask.CompletedTask;
            }, _ => { lifetime.Cancel(); return ValueTask.CompletedTask; }, native);
        connections.Add(uid, connection);
        await Handler.HandleFrameAsync(connection, ReadFrame(new LegacyMsgLoginGamePacket(uid).ToFrameBytes()), lifetime.Token);
        var current = Current(uid);
        Assert.Equal(uid, current.Character.Uid);
        Assert.Same(native, current.NativeGameClient);
        Assert.NotNull(native.OrdinarySpellBinding);
        Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(connection, current));
    }

    private WorldSession Current(uint uid) => Assert.IsType<WorldSession>(
        Store.TryGet(connections[uid], out var current) ? current : null);
    private async Task TradeAsync(uint uid, LegacyTradePacket packet) => await Handler.HandleFrameAsync(
        connections[uid], ReadFrame(packet.ToNativeFrameBytes()), lifetimes[uid].Token);
    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _)); return frame;
    }
}
