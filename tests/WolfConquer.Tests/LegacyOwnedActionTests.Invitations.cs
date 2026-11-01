using System.Net.Sockets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedActionTests
{
    [Fact]
    public Task RealInvitationCommitsBothOwnersBeforeSourceRequestAndAcceptFramesAndCannotBeReplayed() => Run(async f =>
    {
        var files = f.Data.Files();
        await RequestInvitation(f, files);
        f.BeforeSend = (_, _) => { f.Data.AssertFiles(files); AssertInvitationLinks(f, paired: true); };
        try { await f.SendAsync(Invitation(MsgAttackType.InteractionAccept, Peer, Uid), Peer); }
        finally { f.BeforeSend = null; }
        Assert.Equal(new[] { Uid, Peer }, f.Order.Select(row => row.Recipient));
        Assert.All(f.Order, row => Assert.Equal(InvitationGolden(47, Peer, Uid, 190), row.Bytes));
        ClearOutput(f); var accepted = new[] { f.Current, f.Get(Peer) };
        await f.SendAsync(Invitation(MsgAttackType.InteractionAccept, Peer, Uid), Peer);
        Assert.Empty(f.Order); Assert.Same(accepted[0], f.Current); Assert.Same(accepted[1], f.Get(Peer));
        f.Data.AssertFiles(files); AssertInvitationLinks(f, paired: true);
        // A legitimate immutable-record replacement must retain the private
        // accepted pair over the SAME actual bindings, not old Accept records.
        await f.StanceAsync(190); ClearOutput(f);
        var moved = ExpectedCoordinates(f, files, (Uid, 430, 380, 431, 380), (Peer, 430, 380, 431, 380));
        await f.WalkAsync(); f.Data.AssertFiles(moved); AssertInvitationLinks(f, paired: true);
        Assert.Contains(f.Sent, bytes => LegacyInterActionWalkPacket.TryParse(Read(bytes), out var packet) && packet.Uid == Uid && packet.OpponentUid == Peer);
    });

    [Theory]
    [InlineData("no-request")] [InlineData("reverse-role")] [InlineData("wrong-opponent")]
    public Task ActualUnsolicitedOrWrongAcceptDoesNotConsumeAnotherPendingInvitation(string mistake) => Run(async f =>
    {
        var files = f.Data.Files();
        if (mistake != "no-request") await RequestInvitation(f, files);
        var owner = f.Current; var peer = f.Get(Peer);
        uint sender = mistake == "reverse-role" ? Uid : Peer;
        uint opponent = mistake == "wrong-opponent" ? Uid + 99 : sender == Uid ? Peer : Uid;
        await f.SendAsync(Invitation(MsgAttackType.InteractionAccept, sender, opponent), sender);
        Assert.Empty(f.Order); f.Data.AssertFiles(files); Assert.Same(owner, f.Current); Assert.Same(peer, f.Get(Peer));
        Assert.Null(f.Current.InteractionPartnerUid); Assert.Null(f.Get(Peer).InteractionPartnerUid);
        if (mistake == "no-request") await RequestInvitation(f, files);
        await f.SendAsync(Invitation(MsgAttackType.InteractionAccept, Peer, Uid), Peer);
        AssertInvitationLinks(f, paired: true); Assert.Equal(new[] { Uid, Peer }, f.Order.Select(row => row.Recipient));
        f.Data.AssertFiles(files);
    });

    [Fact]
    public Task MatchingRefusalConsumesOnlyThePendingRequestAndARealNewRequestCanThenBeAccepted() => Run(async f =>
    {
        var files = f.Data.Files(); await RequestInvitation(f, files);
        f.BeforeSend = (_, _) => { f.Data.AssertFiles(files); AssertInvitationLinks(f, paired: false); };
        try { await f.SendAsync(Invitation(MsgAttackType.InteractionRefuse, Peer, Uid), Peer); }
        finally { f.BeforeSend = null; }
        Assert.Equal(InvitationGolden(48, Peer, Uid, 0), Assert.Single(f.Sent)); Assert.Empty(f.PeerSent);
        ClearOutput(f); var owner = f.Current; var peer = f.Get(Peer);
        await f.SendAsync(Invitation(MsgAttackType.InteractionAccept, Peer, Uid), Peer);
        Assert.Empty(f.Order); Assert.Same(owner, f.Current); Assert.Same(peer, f.Get(Peer)); f.Data.AssertFiles(files);
        await RequestInvitation(f, files); await f.SendAsync(Invitation(MsgAttackType.InteractionAccept, Peer, Uid), Peer);
        AssertInvitationLinks(f, paired: true); f.Data.AssertFiles(files);
    });

    [Fact]
    public Task ActualStopRequiresTheSamePrivatePairAndClearsBothBeforeOriginalThenSwappedScreenFrames() => Run(async f =>
    {
        var files = f.Data.Files(); await f.PairAsync(); ClearOutput(f);
        var owner = f.Current; var peer = f.Get(Peer);
        await f.SendAsync(Invitation(MsgAttackType.InteractionStopEffect, Uid, Peer + 99));
        Assert.Empty(f.Order); Assert.Same(owner, f.Current); Assert.Same(peer, f.Get(Peer));
        f.BeforeSend = (_, _) => { f.Data.AssertFiles(files); AssertInvitationLinks(f, paired: false); };
        try { await f.SendAsync(Invitation(MsgAttackType.InteractionStopEffect, Uid, Peer)); }
        finally { f.BeforeSend = null; }
        Assert.Equal(new[] { Uid, Peer, Uid, Peer }, f.Order.Select(row => row.Recipient));
        Assert.All(f.Order.Take(2), row => Assert.Equal(InvitationGolden(50, Uid, Peer, 190), row.Bytes));
        Assert.All(f.Order.Skip(2), row => Assert.Equal(InvitationGolden(50, Peer, Uid, 190), row.Bytes));
        ClearOutput(f); await f.SendAsync(Invitation(MsgAttackType.InteractionAccept, Peer, Uid), Peer);
        Assert.Empty(f.Order); f.Data.AssertFiles(files);
        await RequestInvitation(f, files); await f.SendAsync(Invitation(MsgAttackType.InteractionAccept, Peer, Uid), Peer);
        AssertInvitationLinks(f, paired: true);
    });

    [Fact]
    public Task AcceptNotificationFailureRetainsTheCommittedPairAndNeverRenewsItsConsumedInvitation() => Run(async f =>
    {
        var files = f.Data.Files(); await RequestInvitation(f, files); bool failed = false;
        f.BeforeSend = (recipient, _) =>
        {
            f.Data.AssertFiles(files); AssertInvitationLinks(f, paired: true); Assert.Equal(Uid, recipient);
            failed = true; throw new SocketException((int)SocketError.ConnectionReset);
        };
        try { await f.SendAsync(Invitation(MsgAttackType.InteractionAccept, Peer, Uid), Peer); }
        finally { f.BeforeSend = null; }
        Assert.True(failed); Assert.Empty(f.Order); AssertInvitationLinks(f, paired: true); f.Data.AssertFiles(files);
        var owner = f.Current; var peer = f.Get(Peer);
        await f.SendAsync(Invitation(MsgAttackType.InteractionAccept, Peer, Uid), Peer);
        Assert.Empty(f.Order); Assert.Same(owner, f.Current); Assert.Same(peer, f.Get(Peer)); f.Data.AssertFiles(files);
    });

    private static async Task RequestInvitation(ActionFixture f, Dictionary<string, byte[]> files)
    {
        ClearOutput(f);
        f.BeforeSend = (_, _) =>
        {
            f.Data.AssertFiles(files); Assert.Null(f.Current.InteractionPartnerUid); Assert.Null(f.Get(Peer).InteractionPartnerUid);
            Assert.Equal(190U, f.Current.ActiveInteractionDance); Assert.Equal(190U, f.Get(Peer).ActiveInteractionDance);
        };
        try { await f.SendAsync(Invitation(MsgAttackType.InteractionRequest, Uid, Peer, 190)); }
        finally { f.BeforeSend = null; }
        Assert.Empty(f.Sent); Assert.Equal(2, f.PeerSent.Count);
        Assert.All(f.PeerSent, bytes => Assert.Equal(InvitationGolden(46, Uid, Peer, 190), bytes));
        Assert.Equal(new[] { Peer, Peer }, f.Order.Select(row => row.Recipient)); ClearOutput(f);
    }
    private static void AssertInvitationLinks(ActionFixture f, bool paired)
    {
        Assert.Equal(paired ? Peer : (uint?)null, f.Current.InteractionPartnerUid);
        Assert.Equal(paired ? Uid : (uint?)null, f.Get(Peer).InteractionPartnerUid);
        Assert.Equal(paired ? 190U : 0U, f.Current.ActiveInteractionDance);
        Assert.Equal(paired ? 190U : 0U, f.Get(Peer).ActiveInteractionDance);
        AssertCoherent(f); AssertCoherent(f, uid: Peer);
    }
    private static byte[] Invitation(MsgAttackType kind, uint actor, uint other, uint dance = 0) =>
        new MsgAttackPacket(new(actor, other, 430, 380, kind, 0, 0, dance, MsgAttackEffect.None, 19)).ToFrameBytes();
    private static byte[] InvitationGolden(uint kind, uint actor, uint other, uint dance)
    {
        var body = new List<byte>();
        foreach (var row in new (int Field, ulong Value)[] { (4, actor), (5, other), (7, 430), (8, 380), (13, kind),
            (15, 0), (17, 0), (21, dance), (25, 0), (26, 19) }) Field(body, row.Field, row.Value);
        return Frame(2135, body);
    }
}
