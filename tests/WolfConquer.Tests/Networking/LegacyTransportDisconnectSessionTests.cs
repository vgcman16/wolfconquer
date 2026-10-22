using System.Buffers;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Genuine legacy-game DH/cipher/socket birth and LoginGame dispatch. The actual
// observed connection is strongly retained after peer EOF; no GC, forged closed
// flag, manual session removal, or simulated transport cancellation is used.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyTransportDisconnectSessionTests
{
    private const uint Uid = 1_399_101;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealLegacyPeerCloseRemovesNonTradingSessionWhileActualConnectionRemainsStronglyReachable(bool coalesced)
    {
        using var beastMetadata = new NativeLoginBeastMetadataScope(1000);
        using var files = new SnapshotInput();
        using var log = new NativePlayerDiagnosticLog();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(files.Users),
            handoffService: null, worldData: null,
            runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled, log: log,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13);

        await NativePlayerLoopback.RunAsync(ConquerTcpWireMode.LegacyGameServer, async fixture =>
        {
            var wire = await fixture.ConnectGameAsync(coalesced);
            var birth = await fixture.NextAsync();
            var connection = birth.Connection;
            var client = Assert.IsType<LegacyNativeGameClientRuntime>(connection.NativeGameClient);
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(connection));
            await wire.SendAsync(new LegacyMsgLoginGamePacket(Uid).ToFrameBytes(), fixture.Token);
            var login = await fixture.NextAsync();
            Assert.Equal(LegacyMsgLoginGamePacket.Opcode, login.Opcode);
            Assert.Same(connection, login.Connection);
            var accepted = Assert.IsType<WorldSession>(login.After);
            Assert.Equal((long)Uid, accepted.AccountId);
            Assert.Equal((long)Uid, accepted.Character.Uid);
            Assert.False(accepted.LegacyTradeActive);
            Assert.Same(client, accepted.NativeGameClient);
            Assert.Same(accepted, handler.ReadCurrentSessionForNativeOwnerObservation(connection));
            log.AssertNoWorldRuntimeDiagnostics();

            await fixture.CloseClientTransportAsync();

            // Server EOF is the orderly-close barrier, not an eventual-GC/polling proof.
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(connection));
            Assert.Same(client, connection.NativeGameClient);
            log.AssertNoWorldRuntimeDiagnostics();
            GC.KeepAlive(connection); // Retain the key; this never forces collection.
            GC.KeepAlive(wire);
        }, handler.HandleFrameAsync, handler.ReadCurrentSessionForNativeOwnerObservation);
    }

    [Fact]
    public async Task CancelledAdHocFrameScopeDoesNotRemoveNonTradingSessionOrCloseItsActualLegacySocket()
    {
        using var beastMetadata = new NativeLoginBeastMetadataScope(1000);
        using var files = new SnapshotInput();
        using var log = new NativePlayerDiagnosticLog();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(files.Users),
            handoffService: null, worldData: null,
            runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled, log: log,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13);

        await NativePlayerLoopback.RunAsync(ConquerTcpWireMode.LegacyGameServer, async fixture =>
        {
            var wire = await fixture.ConnectGameAsync(false);
            var birth = await fixture.NextAsync();
            var connection = birth.Connection;
            await wire.SendAsync(new LegacyMsgLoginGamePacket(Uid).ToFrameBytes(), fixture.Token);
            var login = await fixture.NextAsync();
            var accepted = Assert.IsType<WorldSession>(login.After);
            Assert.False(accepted.LegacyTradeActive);
            using var scope = new CancellationTokenSource();
            scope.Cancel();
            var canceledFrame = ReadFrame(new MsgActionPacket
            {
                ObjId = Uid, Type = MsgActionType.ChangeDirection, Facing = 2,
            }.ToFrameBytes());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                handler.HandleFrameAsync(connection, canceledFrame, scope.Token).AsTask());

            Assert.Same(accepted, handler.ReadCurrentSessionForNativeOwnerObservation(connection));
            // A real encrypted frame still dispatches on the same open socket.
            await wire.SendAsync(new MsgActionPacket
            {
                ObjId = Uid, Type = MsgActionType.ChangeDirection, Facing = 3,
            }.ToFrameBytes(), fixture.Token);
            var direction = await fixture.NextAsync();
            Assert.Equal(MsgActionPacket.Opcode, direction.Opcode);
            Assert.Same(connection, direction.Connection);
            Assert.Same(accepted, direction.Before);
            var current = Assert.IsType<WorldSession>(direction.After);
            Assert.Equal("3", current.Character.Attributes["Angle"]);
            Assert.False(current.LegacyTradeActive);
            Assert.Same(current, handler.ReadCurrentSessionForNativeOwnerObservation(connection));
            log.AssertNoWorldRuntimeDiagnostics();
            GC.KeepAlive(connection);
            GC.KeepAlive(wire);
        }, handler.HandleFrameAsync, handler.ReadCurrentSessionForNativeOwnerObservation);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed class SnapshotInput : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-transport-disconnect-" + Guid.NewGuid().ToString("N"));
        internal string Users => Path.Combine(root, "Users");

        internal SnapshotInput()
        {
            Directory.CreateDirectory(Users);
            File.WriteAllText(Path.Combine(Users, Uid.ToString(CultureInfo.InvariantCulture) + ".ini"),
                $"""
                [Character]
                UID={Uid}
                Name=TransportFixture
                Body=1003
                Face=154
                Haire=27
                Class=10
                FirstClass=0
                SecoundeClass=0
                Reborn=0
                Level=120
                Experience=0
                Map=1002
                X=410
                Y=354
                PMap=1002
                PMapX=410
                PMapY=354
                Strength=100
                Agility=50
                Vitaliti=100
                Spirit=0
                Atributes=0
                MinHitPoints=1000
                MinMana=100
                Money=100
                ConquerPoints=50
                BoundConquerPoints=0
                BankCps=0
                PkPoints=0
                Online=0
                SecurityPass=0,0,0
                WHMoney=0
                Angle=1
                Day={DateTime.Now.DayOfYear}
                """);
        }

        public void Dispose()
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
