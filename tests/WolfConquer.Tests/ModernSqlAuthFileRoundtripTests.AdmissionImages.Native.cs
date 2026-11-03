using WolfConquer.Core.Data.SqlWorld;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    private static async Task AssertSqlColdImageNativePreparationOnlyAsync(SqlWorldStateBundle cold)
    {
        // Actual SQL-loaded DATA and real socket-born participants. No file-backed loader,
        // authentication, Store admission, sidecar publication or backend switch is supplied.
        Assert.True(LegacyCapturedCharacterPrefix.TryCreate(cold.Core, out var captured, out var reason), reason);
        var source = Assert.IsType<LegacyCapturedCharacterPrefix>(captured);
        using var metadata = new NativeLoginBeastMetadataScope(1000);
        using var log = new NativePlayerDiagnosticLog();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, log: log);
        await NativePlayerLoopback.RunAsync(ConquerTcpWireMode.LegacyGameServer, async fixture =>
        {
            var wire = await fixture.ConnectGameAsync(false);
            var birth = await fixture.NextAsync();
            Assert.Equal(NativePlayerLoopback.ProbeOpcode, birth.Opcode); Assert.Null(birth.Before); Assert.Null(birth.After);
            var client = Assert.IsType<LegacyNativeGameClientRuntime>(birth.Connection.NativeGameClient);
            var player = client.Player; Assert.Same(client, player.Owner);
            player.Online = 9;
            var before = LegacyNativePlayerRuntime.ExistingPrefixLoad.Read(player);
            var flags = player.BitVector; var random = player.MyRandom;
            Assert.True(LegacyNativeCapturedCharacterPrefix.TryPrepare(birth.Connection, source, out var prepared, out reason), reason);
            var ready = Assert.IsType<LegacyNativeCapturedCharacterPrefix>(prepared);
            Assert.True(ready.IsCurrent());
            Assert.Equal(cold.AccountId, ready.Image.AccountId); Assert.Equal(cold.CharacterUid, ready.PreparedPlayerValues.Uid);
            Assert.NotEqual((long)ready.PreparedPlayerValues.Uid, cold.AccountId);
            Assert.Equal(1U, source.SavedPlayerValues.Online); Assert.Equal(10U, ready.PreparedPlayerValues.Online);
            Assert.Equal(1L, ready.PreparedSnapshot.Online);
            Assert.Equal(cold.Core.CopyCharacterBytes(), ready.CopyOnlineCharacterBytes());
            Assert.Equal(cold.Core.CopyItemBytes(), ready.Image.CopyItemBytes());
            Assert.Equal(cold.ItemUids.ToArray(), ready.Image.ItemUids.ToArray());
            Assert.Equal(before, LegacyNativePlayerRuntime.ExistingPrefixLoad.Read(player));
            Assert.Equal(0U, player.UID); Assert.Equal(0U, player.Class); Assert.Equal((ushort)0, player.Level);
            Assert.Same(flags, player.BitVector); Assert.Same(random, player.MyRandom);
            Assert.Null(client.Beasts); Assert.Null(client.HairfaceStorage); Assert.Null(client.OrdinarySpellBinding);
            Assert.False(client.FullLoading); Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
            Assert.True(ready.IsCurrent());
            GC.KeepAlive(wire);
        }, handler.HandleFrameAsync, handler.ReadCurrentSessionForNativeOwnerObservation);
        log.AssertNoWorldRuntimeDiagnostics();
    }
}
