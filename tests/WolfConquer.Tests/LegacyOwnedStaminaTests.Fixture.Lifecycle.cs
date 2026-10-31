using System.Buffers;
using System.Net;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedStaminaTests
{
    private sealed partial class StaminaFixture
    {
        private readonly List<ConquerTcpConnection> connections = [];
        private readonly List<CancellationTokenSource> lifetimes = [];
        private Exception? observationFailure;
        internal ConquerTcpConnection Connection => connections[0];
        internal ConquerTcpConnection PeerConnection => connections[1];
        private async Task CreateAndLoginAsync()
        {
            var options = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), Clock,
                LegacySignedMillisecondClock.FromTimeProvider(Clock, 10000));
            Handler = new(Data.Loader, null, Catalog, options, Log,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: Runtime);
            NewConnection(Uid, Sent); await SendAsync(new LegacyMsgLoginGamePacket(Uid).ToFrameBytes());
            if (Options.Peer)
            {
                NewConnection(Peer, PeerSent);
                await Handler.HandleFrameAsync(PeerConnection, Read(new LegacyMsgLoginGamePacket(Peer).ToFrameBytes()));
                Assert.True(Store.TryGet(PeerConnection, out _), Log.ToString());
            }
        }
        private void NewConnection(uint uid, List<byte[]> sent)
        {
            var lifetime = new CancellationTokenSource(); lifetimes.Add(lifetime);
            var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (memory, token) =>
            {
                token.ThrowIfCancellationRequested(); var bytes = memory.ToArray();
                try { BeforeSend?.Invoke(uid, bytes); } catch (Exception failure) { observationFailure ??= failure; throw; }
                sent.Add(bytes); Order.Add((uid, bytes)); return ValueTask.CompletedTask;
            }, _ => { lifetime.Cancel(); return ValueTask.CompletedTask; }, new LegacyNativeGameClientRuntime());
            connections.Add(connection);
        }
        internal async Task SendAsync(byte[] bytes)
        {
            await Handler.HandleFrameAsync(Connection, Read(bytes));
            if (observationFailure is { } failure) ExceptionDispatchInfo.Capture(failure).Throw();
        }
        internal Task TickAsync() => Handler.SendDueLegacyStaminaUpdatesAsync(CancellationToken.None).AsTask();
        internal Task EquipFanAsync() => SendAsync(new MsgItemUsagePacket(Left, 5, MsgItemUsageAction.Equip, 1234, 0, 0, 0).ToFrameBytes());
        internal Task UnequipFanAsync() => SendAsync(new MsgItemUsagePacket(Left, 5, MsgItemUsageAction.Unequip, 1235, 0, 0, 0).ToFrameBytes());
        internal Task QueryAsync() => SendAsync(LegacyRuneStoragePacket.Startup().ToFrameBytes());
        internal Task SitAsync() => SendAsync(new MsgActionPacket { ObjId = Uid, DwParam = 250,
            Timestamp = 511043495, Type = MsgActionType.ChangeStance }.ToFrameBytes());
        internal async Task CloseAsync()
        {
            BeforeSend = null; var errors = new List<Exception>();
            foreach (var connection in connections)
            {
                try { await connection.DisconnectAsync(); } catch (Exception error) { errors.Add(error); }
                try { await connection.CompleteTransportCloseAsync(); } catch (Exception error) { errors.Add(error); }
            }
            connections.Clear();
            if (Runtime is not null) try { await Runtime.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            foreach (var lifetime in lifetimes) lifetime.Dispose(); lifetimes.Clear();
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException(errors);
        }
        internal void DisposeMetadata() { beasts?.Dispose(); beasts = null; Log.Dispose(); }
    }
    private static PacketFrame Read(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _)); return frame;
    }
}
