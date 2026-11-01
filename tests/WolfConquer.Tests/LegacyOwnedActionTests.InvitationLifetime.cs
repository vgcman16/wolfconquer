using System.Net;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedActionTests
{
    [Fact]
    public Task RealRecipientEofRetiresItsPendingInvitationAndAReboundNativeOwnerNeedsANewActualRequest() => Run(async f =>
    {
        var files = f.Data.Files(); await RequestInvitation(f, files);
        var old = f.ConnectionFor(Peer); var oldBinding = old.NativeGameClient!.OrdinarySpellBinding!;
        await old.CompleteTransportCloseAsync();
        Assert.True(oldBinding.IsRetired); Assert.False(f.Store.TryGet(old, out _)); f.Data.AssertFiles(files);
        using var lifetime = new CancellationTokenSource(); var sent = new List<byte[]>();
        var replacement = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (memory, token) =>
        { token.ThrowIfCancellationRequested(); sent.Add(memory.ToArray()); return ValueTask.CompletedTask; },
            _ => { lifetime.Cancel(); return ValueTask.CompletedTask; }, new LegacyNativeGameClientRuntime());
        try
        {
            await f.Handler.HandleFrameAsync(replacement, Read(new LegacyMsgLoginGamePacket(Peer).ToFrameBytes()), lifetime.Token);
            Assert.True(f.Store.TryGet(replacement, out var rebound), f.Log.ToString()); Assert.NotNull(rebound);
            Assert.NotSame(oldBinding, replacement.NativeGameClient!.OrdinarySpellBinding);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(replacement.NativeGameClient.OrdinarySpellBinding!));
            sent.Clear(); ClearOutput(f); var owner = f.Current;
            await f.Handler.HandleFrameAsync(replacement, Read(Invitation(MsgAttackType.InteractionAccept, Peer, Uid)), lifetime.Token);
            Assert.Empty(sent); Assert.Empty(f.Order); Assert.Same(owner, f.Current);
            Assert.True(f.Store.TryGet(replacement, out var still)); Assert.Same(rebound, still); f.Data.AssertFiles(files);
            await f.SendAsync(Invitation(MsgAttackType.InteractionRequest, Uid, Peer, 190));
            Assert.Equal(2, sent.Count); Assert.All(sent, bytes => Assert.Equal(InvitationGolden(46, Uid, Peer, 190), bytes));
            sent.Clear(); ClearOutput(f);
            await f.Handler.HandleFrameAsync(replacement, Read(Invitation(MsgAttackType.InteractionAccept, Peer, Uid)), lifetime.Token);
            Assert.Equal(InvitationGolden(47, Peer, Uid, 190), Assert.Single(f.Sent));
            Assert.Equal(InvitationGolden(47, Peer, Uid, 190), Assert.Single(sent));
            Assert.Equal(Peer, f.Current.InteractionPartnerUid); Assert.True(f.Store.TryGet(replacement, out still));
            Assert.Equal(Uid, still!.InteractionPartnerUid); f.Data.AssertFiles(files);
            // A repeated old close must not remove the newer private pair.
            await old.CompleteTransportCloseAsync();
            Assert.True(f.Store.TryGet(replacement, out var retained)); Assert.Same(still, retained);
            Assert.Equal(Peer, f.Current.InteractionPartnerUid);
        }
        finally
        {
            await replacement.DisconnectAsync(); await replacement.CompleteTransportCloseAsync();
        }
    });
}
