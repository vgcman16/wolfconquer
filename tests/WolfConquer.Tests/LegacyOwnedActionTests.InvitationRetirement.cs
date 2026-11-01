using System.Net;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedActionTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task PairedEofIsCleanedByTheSurvivorsActualNextFrameAndCannotRetireANewBindingPair(bool requesterEnds) => Run(async f =>
    {
        await f.PairAsync(); ClearOutput(f);
        uint endedUid = requesterEnds ? Uid : Peer, survivor = requesterEnds ? Peer : Uid;
        var files = f.Data.Files();
        var ended = f.ConnectionFor(endedUid); var oldBinding = ended.NativeGameClient!.OrdinarySpellBinding!;
        var survivorBinding = f.ConnectionFor(survivor).NativeGameClient!.OrdinarySpellBinding!;
        var before = f.Get(survivor); var stamp = survivorBinding.Player.AttackStamp;
        await ended.CompleteTransportCloseAsync();
        Assert.True(oldBinding.IsRetired); Assert.False(f.Store.TryGet(ended, out _));
        // EOF does not write the other owner's holder outside its own gate.
        Assert.Same(before, f.Get(survivor)); Assert.Equal(endedUid, before.InteractionPartnerUid);
        Assert.Equal(190U, before.ActiveInteractionDance); f.Data.AssertFiles(files);

        f.BeforeSend = (recipient, _) =>
        {
            Assert.Equal(survivor, recipient); f.Data.AssertFiles(files); AssertCoherent(f, 0, survivor);
            Assert.Null(f.Get(survivor).InteractionPartnerUid); Assert.Equal(0U, f.Get(survivor).ActiveInteractionDance);
            Assert.Same(survivorBinding, f.ConnectionFor(survivor).NativeGameClient!.OrdinarySpellBinding);
            Assert.Equal(stamp, survivorBinding.Player.AttackStamp);
            Assert.Equal(before.Character.MinHitPoints, f.Get(survivor).Character.MinHitPoints);
            Assert.Equal(before.Character.MinMana, f.Get(survivor).Character.MinMana);
            Assert.Equal(before.Character.Attributes["Stamina"], f.Get(survivor).Character.Attributes["Stamina"]);
        };
        try { await f.StanceAsync(0, survivor); } finally { f.BeforeSend = null; }
        Assert.Equal(2, f.Order.Count);
        Assert.Equal(EndedPairSkinGolden(survivor, survivor == Uid ? 3U : 0U), f.Order[0].Bytes);
        Assert.Equal(EndedPairStanceGolden(survivor), f.Order[1].Bytes); ClearOutput(f);

        await using var rebound = new ReconnectedInvitationEndpoint(f, endedUid);
        await rebound.SendAsync(new LegacyMsgLoginGamePacket(endedUid).ToFrameBytes());
        var replacement = rebound.Current;
        Assert.NotSame(oldBinding, rebound.Binding); Assert.True(f.Store.IsBoundSpellOwnerCurrent(rebound.Binding));
        Assert.Null(replacement.InteractionPartnerUid); f.Data.AssertFiles(files); rebound.Clear(); ClearOutput(f);

        // The new actual binding cannot accept the departed binding's old pair.
        var retained = f.Get(survivor);
        await rebound.SendAsync(Invitation(MsgAttackType.InteractionAccept, endedUid, survivor));
        Assert.Empty(f.Order); Assert.Same(retained, f.Get(survivor)); Assert.Same(replacement, rebound.Current); f.Data.AssertFiles(files);

        f.BeforeSend = (_, _) =>
        {
            f.Data.AssertFiles(files); AssertCoherent(f, uid: survivor); AssertReconnectedInvitationOwner(f, rebound);
            Assert.Null(f.Get(survivor).InteractionPartnerUid); Assert.Null(rebound.Current.InteractionPartnerUid);
            Assert.Equal(190U, f.Get(survivor).ActiveInteractionDance); Assert.Equal(190U, rebound.Current.ActiveInteractionDance);
        };
        try { await f.SendAsync(Invitation(MsgAttackType.InteractionRequest, survivor, endedUid, 190), survivor); }
        finally { f.BeforeSend = null; }
        Assert.Equal(new[] { endedUid, endedUid }, f.Order.Select(row => row.Recipient));
        Assert.All(f.Order, row => Assert.Equal(InvitationGolden(46, survivor, endedUid, 190), row.Bytes)); rebound.Clear(); ClearOutput(f);

        f.BeforeSend = (_, _) =>
        {
            f.Data.AssertFiles(files); AssertCoherent(f, uid: survivor); AssertReconnectedInvitationOwner(f, rebound);
            Assert.Equal(endedUid, f.Get(survivor).InteractionPartnerUid); Assert.Equal(survivor, rebound.Current.InteractionPartnerUid);
        };
        try { await rebound.SendAsync(Invitation(MsgAttackType.InteractionAccept, endedUid, survivor)); }
        finally { f.BeforeSend = null; }
        Assert.Equal(new[] { survivor, endedUid }, f.Order.Select(row => row.Recipient));
        Assert.All(f.Order, row => Assert.Equal(InvitationGolden(47, endedUid, survivor, 190), row.Bytes)); rebound.Clear(); ClearOutput(f);

        // Explicit negative stale-owner call to the actual retirement API. It
        // cannot consume a transition belonging to a different real binding.
        f.Store.RetireInteractionInvitations(oldBinding);
        await ended.CompleteTransportCloseAsync();
        var ownerAfter = f.Get(survivor); var replacementAfter = rebound.Current;
        await rebound.SendAsync(Invitation(MsgAttackType.InteractionAccept, endedUid, survivor));
        Assert.Empty(f.Order); Assert.Same(ownerAfter, f.Get(survivor)); Assert.Same(replacementAfter, rebound.Current);
        var moved = ExpectedCoordinates(f, files, (Uid, 430, 380, 431, 380), (Peer, 430, 380, 431, 380));
        f.BeforeSend = (_, _) =>
        {
            f.Data.AssertFiles(moved); AssertCoherent(f, uid: survivor); AssertReconnectedInvitationOwner(f, rebound);
            Assert.Equal(431L, f.Get(survivor).Character.X); Assert.Equal(431L, rebound.Current.Character.X);
            Assert.Equal(endedUid, f.Get(survivor).InteractionPartnerUid); Assert.Equal(survivor, rebound.Current.InteractionPartnerUid);
        };
        try { await f.SendAsync(new MsgMovementPacket { Uid = 999, Direction = 6, Running = MsgMovementMode.Walk,
            Timestamp = 123456, MapId = 999 }.ToFrameBytes(), survivor); } finally { f.BeforeSend = null; }
        Assert.Contains(f.Order, row => LegacyInterActionWalkPacket.TryParse(Read(row.Bytes), out var walk) &&
            walk.Uid == survivor && walk.OpponentUid == endedUid);
        f.Data.AssertFiles(moved);
    });

    private static void AssertReconnectedInvitationOwner(ActionFixture f, ReconnectedInvitationEndpoint endpoint)
    {
        var current = endpoint.Current; var binding = endpoint.Binding; var native = binding.Player;
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.True(binding.Items.Image.IsCurrentPlain());
        Assert.Equal(current.Character.Uid, (long)native.UID); Assert.Equal(current.Character.X, (long)native.X);
        Assert.Equal(current.Character.Y, (long)native.Y); Assert.Equal(current.Character.MapId, (long)native.Map);
        Assert.Equal(current.Character.MinHitPoints, (long)native.HitPoints); Assert.Equal(current.Character.MinMana, (long)native.Mana);
        Assert.Equal(LegacyStaminaState.RequireCurrent(current.Character), native.Stamina);
        Assert.Equal(LegacyActionState.RequireCurrent(current.Character), (uint)native.Action);
        var saved = PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshotLoader.Parse(File.ReadAllBytes(f.ItemsPath(endpoint.Uid))));
        Assert.Equal(saved, PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
        Assert.Equal(saved, PlayerItemSnapshotLoader.ToLegacyBytes(binding.Items.Image.FinalSnapshot));
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }

    private static byte[] EndedPairSkinGolden(uint uid, uint skin)
    { var body = new List<byte>(); Field(body, 1, 6); Field(body, 2, uid); if (skin != 0) Field(body, 3, skin); return Frame(2415, body); }
    private static byte[] EndedPairStanceGolden(uint uid)
    {
        var body = new List<byte>(); Field(body, 1, uid); Field(body, 4, 17); Field(body, 5, 123456789); Field(body, 9, 123456);
        Field(body, 12, 81); Field(body, 13, 6); Field(body, 14, 430); Field(body, 15, 380); Field(body, 17, 1002); return Frame(2046, body);
    }

    private sealed class ReconnectedInvitationEndpoint : IAsyncDisposable
    {
        private readonly ActionFixture fixture;
        private readonly CancellationTokenSource lifetime = new();
        private Exception? failure;
        internal uint Uid { get; }
        internal ConquerTcpConnection Connection { get; }
        internal WorldSession Current => Assert.IsType<WorldSession>(fixture.Store.TryGet(Connection, out var current) ? current : null);
        internal LegacyOrdinarySpellBinding Binding => Assert.IsType<LegacyOrdinarySpellBinding>(Connection.NativeGameClient!.OrdinarySpellBinding);
        internal ReconnectedInvitationEndpoint(ActionFixture fixture, uint uid)
        {
            this.fixture = fixture; Uid = uid;
            Connection = new(null, new IPEndPoint(IPAddress.Loopback, 9958), (memory, token) =>
            {
                token.ThrowIfCancellationRequested(); var bytes = memory.ToArray();
                try { fixture.BeforeSend?.Invoke(uid, bytes); }
                catch (Xunit.Sdk.XunitException error) { failure ??= error; throw; }
                fixture.Order.Add((uid, bytes)); return ValueTask.CompletedTask;
            }, _ => { lifetime.Cancel(); return ValueTask.CompletedTask; }, new LegacyNativeGameClientRuntime());
        }
        internal async Task SendAsync(byte[] bytes)
        {
            await fixture.Handler.HandleFrameAsync(Connection, Read(bytes), lifetime.Token);
            if (failure is { } error) ExceptionDispatchInfo.Capture(error).Throw();
        }
        internal void Clear() => fixture.Order.Clear();
        public async ValueTask DisposeAsync()
        { try { await Connection.DisconnectAsync(); await Connection.CompleteTransportCloseAsync(); } finally { lifetime.Dispose(); } }
    }
}
