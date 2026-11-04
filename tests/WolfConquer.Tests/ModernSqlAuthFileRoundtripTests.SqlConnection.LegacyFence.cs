using WolfConquer.Core.Accounts;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task ActualSqlConnectionRetainedOwnerFencesLegacyAdmissionGameplayAndGmOutput() => RunSqlConnectionAsync(async (f, network) =>
    {
        var offer = await f.Sql.RequireOfferAsync(); var peer = await f.ConnectAsync(network);
        AssertSqlConnectionRetained(await f.RetainAsync(peer, offer), offer);
        var before = await f.Sql.SnapshotAsync();
        var legacy = f.LegacyHandoff.Issue(SqlWorldStateTestData.FirstCharacter, "owned-legacy", SqlHandoffFixture.Endpoint);
        byte[][] requests =
        [
            new InternalWorldEnterRequestPacket(legacy.Token).ToFrameBytes(),
            new LegacyMsgLoginGamePacket(SqlWorldStateTestData.FirstCharacter).ToFrameBytes(),
            new LegacyMsgMessagePacket(LegacyMsgMessagePacket.WhiteColor, LegacyMsgMessagePacket.TalkChatType,
                uint.MaxValue, uint.MaxValue, 0, 0, 0, 1, 0, 0,
                ["Forged[GM]", "ALLUSERS", string.Empty, "@wolf help", string.Empty, string.Empty, string.Empty]).ToFrameBytes(),
        ];
        foreach (byte[] request in requests)
        {
            await peer.Wire.SendAsync(request, network.Token);
            var dispatch = await network.NextAsync(); Assert.Same(peer.Connection, dispatch.Connection);
            Assert.Null(dispatch.Before); Assert.Null(dispatch.After);
            await AssertSqlConnectionReadAsync(f, peer);
        }
        Assert.True(f.LegacyTickets.TryGet(legacy.Token, out var pending)); Assert.Equal(legacy, pending);
        Assert.False(pending.IsRedeemed); await AssertSqlHandoffRowsUnchangedAsync(f.Sql, before);
        await f.AssertExcludedAsync(offer.AccountId); await f.CloseAsync(network, peer);
        // The genuine captured peer EOF makes this an actual no-output check,
        // including the otherwise early GM-refusal path, not a fake send queue.
        Assert.Empty(peer.Wire.CopyCapturedServerFramesAtEof());
    });

    [ModernSqlAuthFixtureFact]
    public Task ActualSqlConnectionEarlierLegacyRedemptionPreventsSqlConsumptionOnSameSocket() => RunSqlConnectionAsync(async (f, network) =>
    {
        var offer = await f.Sql.RequireOfferAsync(); var peer = await f.ConnectAsync(network);
        var legacy = f.LegacyHandoff.Issue(SqlWorldStateTestData.FirstCharacter, "owned-legacy", SqlHandoffFixture.Endpoint);
        await peer.Wire.SendAsync(new InternalWorldEnterRequestPacket(legacy.Token).ToFrameBytes(), network.Token);
        var dispatched = await network.NextAsync(); Assert.Same(peer.Connection, dispatched.Connection);
        Assert.True(f.LegacyTickets.TryGet(legacy.Token, out var redeemed)); Assert.True(redeemed.IsRedeemed);
        Assert.Null(dispatched.Before); Assert.Null(dispatched.After);
        var before = await f.Sql.SnapshotAsync();
        AssertSqlConnectionRefused(await f.RetainAsync(peer, offer), SqlAccountWorldHandoffFailure.SessionConflict);
        Assert.Null(await f.Handler.ReadRetainedSqlWorldBundleAsync(peer.Connection));
        await AssertSqlHandoffRowsUnchangedAsync(f.Sql, before);
        await f.CloseAsync(network, peer, emptyOutput: false);
        var output = Assert.Single(peer.Wire.CopyCapturedServerFramesAtEof());
        Assert.True(PacketHeader.TryRead(output, out var header)); Assert.Equal(WorldPacketOpcodes.InternalSnapshotResponse, header.Opcode);
        // The SQL offer itself remains genuinely redeemable on a fresh owner.
        await AssertSqlHandoffRedeemedAsync(f.Sql, await f.Sql.RedeemAsync(offer));
    }, attachFileLoader: false);
}
