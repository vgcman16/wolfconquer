using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data.SqlWorld;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    private sealed record SqlConnectionNativeImage(LegacyNativeGameClientRuntime Client,
        LegacyNativePlayerRuntime Player, LegacyNativePrefixValues Values,
        object Flags, object Random, bool FullLoading, bool Fake);

    private static SqlConnectionNativeImage CaptureSqlConnectionNative(ConquerTcpConnection connection)
    {
        var client = Assert.IsType<LegacyNativeGameClientRuntime>(connection.NativeGameClient);
        var player = Assert.IsType<LegacyNativePlayerRuntime>(client.Player);
        Assert.Same(client, player.Owner); Assert.NotNull(player.BitVector); Assert.NotNull(player.MyRandom);
        return new(client, player, LegacyNativePlayerRuntime.ExistingPrefixLoad.Read(player),
            player.BitVector, player.MyRandom, client.FullLoading, client.Fake);
    }

    private static void AssertSqlConnectionNativeUnchanged(SqlConnectionNativeImage before, ConquerTcpConnection connection)
    {
        Assert.Same(before.Client, connection.NativeGameClient); Assert.Same(before.Player, before.Client.Player);
        Assert.Same(before.Client, before.Player.Owner);
        Assert.Equal(before.Values, LegacyNativePlayerRuntime.ExistingPrefixLoad.Read(before.Player));
        Assert.Same(before.Flags, before.Player.BitVector); Assert.Same(before.Random, before.Player.MyRandom);
        Assert.Equal(before.FullLoading, before.Client.FullLoading); Assert.Equal(before.Fake, before.Client.Fake);
        Assert.Null(before.Client.Inventory); Assert.Null(before.Client.Equipment); Assert.Null(before.Client.Rune);
        Assert.Null(before.Client.Beasts); Assert.Null(before.Client.HairfaceStorage);
        Assert.Null(before.Client.OrdinarySpellBinding);
    }

    private static void AssertSqlConnectionRefused(SqlConnectionHandoffResult result, SqlAccountWorldHandoffFailure failure)
    {
        Assert.False(result.IsRetained); Assert.Equal(failure, result.Failure);
        Assert.Equal(0L, result.AccountId); Assert.Equal(0U, result.CharacterUid); Assert.Null(result.Username);
    }

    private static void AssertSqlConnectionRetained(SqlConnectionHandoffResult result, SqlAccountWorldHandoffOffer offer)
    {
        Assert.True(result.IsRetained); Assert.Equal(SqlAccountWorldHandoffFailure.None, result.Failure);
        Assert.Equal(offer.AccountId, result.AccountId); Assert.Equal(offer.CharacterUid, result.CharacterUid);
        Assert.NotEqual(result.AccountId, (long)result.CharacterUid); Assert.Equal(offer.Username, result.Username);
        Assert.False((result.ToString() ?? string.Empty).Contains(offer.Token, StringComparison.Ordinal));
    }

    private static async Task AssertSqlConnectionReadAsync(SqlConnectionFixture f, SqlConnectionPeer peer)
    {
        var result = Assert.IsType<SqlWorldStateBundleReadResult>(await f.Handler.ReadRetainedSqlWorldBundleAsync(peer.Connection));
        Assert.Equal(SqlWorldStateReadStatus.Found, result.Status); Assert.Equal(SqlWorldStateFailure.None, result.Failure);
        var record = Assert.IsType<SqlWorldStateBundleRecord>(result.Record);
        Assert.Equal(1L, record.Revision);
        AssertBundleImage(Assert.IsType<SqlWorldStateBundle>(f.Sql.FirstImage), record.Image);
        AssertSqlConnectionNativeUnchanged(peer.Before, peer.Connection);
        Assert.Null(f.Handler.ReadCurrentSessionForNativeOwnerObservation(peer.Connection)); f.AssertFilesUnchanged();
    }
}
