using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;
using Xunit;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativePlayerSessionAdoptionTests
{
    private const uint Uid = 1_293_600;

    [Fact]
    public async Task RealLegacyLoginAndDirectionCopyRetainTheSocketBornClientAndPlayer()
    {
        using var beastMetadata = new NativeLoginBeastMetadataScope(1000);
        using var files = new SnapshotInput();
        using var log = new NativePlayerDiagnosticLog();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(files.Users), log: log);
        await NativePlayerLoopback.RunAsync(ConquerTcpWireMode.LegacyGameServer, async fixture =>
        {
            var wire = await fixture.ConnectGameAsync(false);
            var prelogin = await fixture.NextAsync();
            log.AssertNoWorldRuntimeDiagnostics();
            Assert.Equal(NativePlayerLoopback.ProbeOpcode, prelogin.Opcode);
            Assert.Null(prelogin.Before);
            Assert.Null(prelogin.After);
            var bornClient = Assert.IsType<LegacyNativeGameClientRuntime>(prelogin.Connection.NativeGameClient);
            var bornPlayer = Assert.IsType<LegacyNativePlayerRuntime>(bornClient.Player);
            var random = bornPlayer.MyRandom;
            var flags = bornPlayer.BitVector;
            Assert.Same(bornClient, bornPlayer.Owner);

            await wire.SendAsync(new LegacyMsgLoginGamePacket(Uid).ToFrameBytes(), fixture.Token);
            var login = await fixture.NextAsync();
            log.AssertNoWorldRuntimeDiagnostics();
            Assert.Equal(LegacyMsgLoginGamePacket.Opcode, login.Opcode);
            Assert.Same(prelogin.Connection, login.Connection);
            Assert.Null(login.Before);
            Assert.True(log.Text.Contains(
                $"WorldServer legacy MsgLoginGame Success key={Uid} uid={Uid} map=1015.",
                StringComparison.Ordinal), log.Text); // Actual tail AFTER awaited startup returns.
            var accepted = Assert.IsType<WorldSession>(login.After);
            Assert.Equal((long)Uid, accepted.AccountId);
            Assert.Equal((long)Uid, accepted.Character.Uid);
            Assert.Equal("NetworkOwner", accepted.Character.Name);
            Assert.Same(bornClient, accepted.NativeGameClient);
            Assert.Same(bornPlayer, accepted.NativePlayer);
            Assert.Same(random, accepted.NativePlayer!.MyRandom);
            Assert.Same(flags, accepted.NativePlayer.BitVector);
            Assert.Equal("1", accepted.Character.Attributes["Angle"]);

            await wire.SendAsync(new MsgActionPacket
            {
                ObjId = Uid, Type = MsgActionType.ChangeDirection, Facing = 3,
            }.ToFrameBytes(), fixture.Token);
            var direction = await fixture.NextAsync();
            log.AssertNoWorldRuntimeDiagnostics();
            Assert.Equal(MsgActionPacket.Opcode, direction.Opcode);
            Assert.Same(prelogin.Connection, direction.Connection);
            Assert.Same(accepted, direction.Before);
            var copied = Assert.IsType<WorldSession>(direction.After);
            Assert.NotSame(accepted, copied);
            Assert.NotSame(accepted.Character, copied.Character);
            Assert.Equal("1", accepted.Character.Attributes["Angle"]);
            Assert.Equal("3", copied.Character.Attributes["Angle"]);
            Assert.Same(bornClient, copied.NativeGameClient);
            Assert.Same(bornPlayer, copied.NativePlayer);
            Assert.Same(random, copied.NativePlayer!.MyRandom);
            Assert.Same(flags, copied.NativePlayer.BitVector);

            // The real repeated-login guard must not turn another admission into another birth.
            await wire.SendAsync(new LegacyMsgLoginGamePacket(Uid).ToFrameBytes(), fixture.Token);
            var repeated = await fixture.NextAsync();
            log.AssertNoWorldRuntimeDiagnostics();
            Assert.Equal(LegacyMsgLoginGamePacket.Opcode, repeated.Opcode);
            Assert.Same(copied, repeated.Before);
            Assert.Same(copied, repeated.After);
            Assert.Same(bornClient, repeated.Connection.NativeGameClient);
            Assert.Same(bornPlayer, repeated.After!.NativePlayer);
            Assert.True(log.Text.Contains(
                $"WorldServer ignored repeated legacy MsgLoginGame on accepted connection key={Uid}.",
                StringComparison.Ordinal), log.Text);
        }, handler.HandleFrameAsync, handler.ReadCurrentSessionForNativeOwnerObservation);
    }

    [Fact]
    public async Task RealModernWorldLoginDoesNotInferPlayerFromARealLoadedSession()
    {
        using var files = new SnapshotInput();
        using var log = new NativePlayerDiagnosticLog();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(files.Users), log: log);
        await NativePlayerLoopback.RunAsync(ConquerTcpWireMode.Modern, async fixture =>
        {
            var wire = await fixture.ConnectNongameAsync();
            await wire.SendAsync(new LegacyMsgLoginGamePacket(Uid).ToFrameBytes(), fixture.Token);
            var login = await fixture.NextAsync();
            log.AssertNoWorldRuntimeDiagnostics();
            Assert.Equal(LegacyMsgLoginGamePacket.Opcode, login.Opcode);
            Assert.Null(login.Connection.NativeGameClient);
            Assert.Null(login.Before);
            Assert.True(log.Text.Contains(
                $"WorldServer legacy MsgLoginGame Success key={Uid} uid={Uid} map=1015.",
                StringComparison.Ordinal), log.Text); // Actual tail AFTER awaited startup returns.
            var accepted = Assert.IsType<WorldSession>(login.After);
            Assert.Equal((long)Uid, accepted.Character.Uid);
            Assert.Equal("NetworkOwner", accepted.Character.Name);
            Assert.Null(accepted.NativeGameClient);
            Assert.Null(accepted.NativePlayer);
        }, handler.HandleFrameAsync, handler.ReadCurrentSessionForNativeOwnerObservation);
    }

    private sealed class SnapshotInput : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(), "wolf-native-player-network-" + Guid.NewGuid().ToString("N"));
        internal string Users => Path.Combine(_root, "Users");

        internal SnapshotInput()
        {
            Directory.CreateDirectory(Users);
            File.WriteAllText(Path.Combine(Users, Uid.ToString(CultureInfo.InvariantCulture) + ".ini"),
                $"""
                [Character]
                UID={Uid}
                Name=NetworkOwner
                Body=1008
                Face=154
                Class=7051
                Map=1015
                X=830
                Y=584
                PMap=1000
                PMapX=500
                PMapY=650
                Level=140
                Experience=0
                Reborn=2
                FirstClass=0
                SecoundeClass=7050
                Haire=27
                Agility=36
                Strength=0
                Vitaliti=447
                Spirit=0
                Atributes=12
                MinHitPoints=61258
                MinMana=5000
                Money=1000
                ConquerPoints=0
                BankCps=0
                BoundConquerPoints=0
                PkPoints=0
                Online=0
                Angle=1
                Day={DateTime.Now.DayOfYear}
                """);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true); // Only this test-owned GUID directory.
        }
    }
}
