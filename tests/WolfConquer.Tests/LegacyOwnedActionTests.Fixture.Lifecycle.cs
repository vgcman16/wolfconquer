using System.Buffers;
using System.Net;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedActionTests
{
    private sealed partial class ActionFixture
    {
        private readonly Dictionary<uint, ConquerTcpConnection> connections = [];
        private readonly Dictionary<uint, CancellationTokenSource> lifetimes = [];
        private Exception? observationFailure;
        private bool closeStarted;
        internal ConquerTcpConnection Connection => ConnectionFor(Uid);
        internal ConquerTcpConnection ConnectionFor(uint uid) => connections[uid];
        private async Task CreateAndLoginAsync(bool admit)
        {
            var options = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), Clock,
                LegacySignedMillisecondClock.FromTimeProvider(Clock, 10000));
            Handler = new(Data.Loader, null, Catalog, options, Log,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: Runtime);
            NewConnection(Uid, Sent); NewConnection(Peer, PeerSent);
            await SendAsync(new LegacyMsgLoginGamePacket(Uid).ToFrameBytes());
            if (admit)
            {
                Assert.True(Store.TryGet(Connection, out _), Log.ToString());
                await SendAsync(new LegacyMsgLoginGamePacket(Peer).ToFrameBytes(), Peer);
                Assert.True(Store.TryGet(ConnectionFor(Peer), out _), Log.ToString());
            }
        }
        private void NewConnection(uint uid, List<byte[]> output)
        {
            var lifetime = new CancellationTokenSource(); lifetimes.Add(uid, lifetime);
            connections.Add(uid, new(null, new IPEndPoint(IPAddress.Loopback, 9958), (memory, token) =>
            {
                token.ThrowIfCancellationRequested(); var bytes = memory.ToArray();
                try { BeforeSend?.Invoke(uid, bytes); }
                catch (Xunit.Sdk.XunitException error) { observationFailure ??= error; throw; }
                output.Add(bytes); Order.Add((uid, bytes)); return ValueTask.CompletedTask;
            }, _ => { lifetime.Cancel(); return ValueTask.CompletedTask; }, new LegacyNativeGameClientRuntime()));
        }
        internal async Task SendAsync(byte[] bytes, uint uid = Uid)
        {
            await Handler.HandleFrameAsync(ConnectionFor(uid), Read(bytes), lifetimes[uid].Token);
            if (observationFailure is { } error) ExceptionDispatchInfo.Capture(error).Throw();
        }
        internal Task StanceAsync(uint value, uint uid = Uid) => SendAsync(Stance(value).ToFrameBytes(), uid);
        internal Task WalkAsync() => SendAsync(new MsgMovementPacket { Direction = 6, Uid = 999,
            Running = MsgMovementMode.Walk, Timestamp = 123456, MapId = 999 }.ToFrameBytes());
        internal Task JumpAsync(uint x = 432, uint y = 381) => SendAsync(new MsgActionPacket
        { ObjId = 999, TargetPositionX = x, TargetPositionY = y, Timestamp = 123456,
            Type = MsgActionType.Jump, Facing = 6 }.ToFrameBytes());
        internal Task TickAsync() => Handler.SendDueLegacyStaminaUpdatesAsync(CancellationToken.None).AsTask();
        internal Task EquipFanAsync() => SendAsync(new MsgItemUsagePacket(Left, 5, MsgItemUsageAction.Equip, 1234, 0, 0, 0).ToFrameBytes());
        internal async Task PairAsync()
        {
            await SendAsync(new MsgAttackPacket(new(Uid, Peer, 430, 380, MsgAttackType.InteractionRequest, 0, 0, 190,
                MsgAttackEffect.None, 0)).ToFrameBytes());
            await SendAsync(new MsgAttackPacket(new(Peer, Uid, 430, 380, MsgAttackType.InteractionAccept, 0, 0, 190,
                MsgAttackEffect.None, 0)).ToFrameBytes(), Peer);
            Assert.Equal(Peer, Current.InteractionPartnerUid); Assert.Equal(Uid, Get(Peer).InteractionPartnerUid);
        }
        internal async Task CloseAsync()
        {
            // Tests may close explicitly before reopening the owned data; Run
            // still invokes cleanup. Do not repeat disconnect after disposing
            // the cancellation sources captured by the transport callbacks.
            if (closeStarted) return;
            closeStarted = true;
            BeforeSend = null; var errors = new List<Exception>();
            foreach (var connection in connections.Values)
            {
                try { await connection.DisconnectAsync(); } catch (Exception error) { errors.Add(error); }
                try { await connection.CompleteTransportCloseAsync(); } catch (Exception error) { errors.Add(error); }
            }
            if (Runtime is not null) try { await Runtime.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            foreach (var lifetime in lifetimes.Values)
                try { lifetime.Dispose(); } catch (Exception error) { errors.Add(error); }
            lifetimes.Clear();
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException(errors);
        }
    }
    private static MsgActionPacket Stance(uint value) => new()
    { ObjId = 999, DwParam = value, DwParam2 = 17, DwParam3 = 123456789, Timestamp = 123456,
        Type = MsgActionType.ChangeStance, Facing = 6, PositionX = 430, PositionY = 380, MapId = 1002 };
    private static PacketFrame Read(byte[] bytes)
    { Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _)); return frame; }
}
