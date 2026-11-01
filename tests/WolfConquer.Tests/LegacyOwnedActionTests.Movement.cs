using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedActionTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task RealWalkOrJumpLeavesSitThroughItsActualOwnerWithoutRebirthingTheStaminaClock(bool jump) => Run(async f =>
    {
        await f.StanceAsync(250); ClearOutput(f); await Tick(f, 15); ClearOutput(f);
        f.Clock.Advance(TimeSpan.FromMilliseconds(500));
        var before = f.Data.Files(); uint action = jump ? 100U : 0U;
        var expected = ExpectedCoordinates(f, before, (Uid, 430, 380, jump ? 432 : 431, jump ? 381 : 380));
        f.BeforeSend = (_, _) => AssertFilesAndOwner(f, expected, action);
        try { if (jump) await f.JumpAsync(); else await f.WalkAsync(); } finally { f.BeforeSend = null; }
        AssertFilesAndOwner(f, expected, action);
        Assert.Equal(jump ? JumpGolden() : WalkGolden(), Assert.Single(f.Sent));
        Assert.Equal(f.Sent[0], Assert.Single(f.PeerSent));
        f.Clock.Advance(TimeSpan.FromMilliseconds(500)); await Tick(f, 15, held: true);
        f.Clock.Advance(TimeSpan.FromSeconds(2)); await Tick(f, 20); // Old T0+3, not movementTime+3 or a new MinValue.
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ActualInteractionHandshakeOwnsBothMoversWithTheSourceActionAsymmetry(bool jump) => Run(async f =>
    {
        await f.StanceAsync(250); await f.StanceAsync(190, Peer); await f.PairAsync(); ClearOutput(f);
        AssertCoherent(f, 250); AssertCoherent(f, 190, Peer);
        var expected = ExpectedCoordinates(f, f.Data.Files(),
            (Uid, 430, 380, jump ? 432 : 431, jump ? 381 : 380),
            (Peer, 430, 380, jump ? 432 : 431, jump ? 381 : 380));
        f.BeforeSend = (_, _) =>
        {
            f.Data.AssertFiles(expected); AssertCoherent(f, jump ? 100U : 0U); AssertCoherent(f, jump ? 100U : 190U, Peer);
            Assert.Equal(Peer, f.Current.InteractionPartnerUid); Assert.Equal(Uid, f.Get(Peer).InteractionPartnerUid);
        };
        try { if (jump) await f.JumpAsync(); else await f.WalkAsync(); } finally { f.BeforeSend = null; }
        f.Data.AssertFiles(expected); AssertCoherent(f, jump ? 100U : 0U); AssertCoherent(f, jump ? 100U : 190U, Peer);
        Assert.Single(f.Sent); Assert.Equal(f.Sent[0], Assert.Single(f.PeerSent));
        Assert.True(LegacyInterActionWalkPacket.TryParse(Read(f.Sent[0]), out var response));
        Assert.Equal(Uid, response.Uid); Assert.Equal(Peer, response.OpponentUid);
        Assert.Equal(jump ? LegacyInterActionWalkMode.Jump : LegacyInterActionWalkMode.Walk, response.Mode);
        Assert.Equal((ushort)0, f.Native.Player!.Stamina); Assert.Null(f.Current.MagicCooldowns);
    });

    [Theory]
    [InlineData(447U)] [InlineData(65536U)]
    public Task RejectedJumpNeverPublishesJumpStateOrConsumesTheExistingSitDeadline(uint x) => Run(async f =>
    {
        await f.StanceAsync(250); ClearOutput(f); await Tick(f, 15); ClearOutput(f);
        var before = f.Current; var files = f.Data.Files();
        await f.JumpAsync(x, 380);
        Assert.Same(before, f.Current); AssertFilesAndOwner(f, files, 250);
        Assert.DoesNotContain(f.Sent, bytes => MsgActionPacket.TryParse(Read(bytes), out var action) && action.Type == MsgActionType.Jump);
        Assert.Empty(f.PeerSent); Assert.Null(f.Current.MagicCooldowns);
        f.Clock.Advance(TimeSpan.FromSeconds(1)); await Tick(f, 30);
    });

    [Fact]
    public Task DeclaredDeadNeptuneInputUsesTheActualDefensivePullbackWithoutInventingJumpOrACurseGrant() => Run(async f =>
    {
        Assert.Equal(0L, f.Current.Character.MinHitPoints); AssertCoherent(f, 65786);
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(f.Current.Character.Attributes, 222));
        Assert.False(f.Native.Player!.BitVector.ArrayFlags.ContainsKey(222)); // No positive status producer is claimed.
        var expected = ExpectedCoordinates(f, f.Data.Files(), (Uid, 430, 380, 420, 370));
        await f.JumpAsync(); AssertFilesAndOwner(f, expected, 65786);
        Assert.Equal(420L, f.Current.Character.X); Assert.Equal(370L, f.Current.Character.Y);
        Assert.DoesNotContain(f.Sent, bytes => MsgActionPacket.TryParse(Read(bytes), out var action) && action.Type == MsgActionType.Jump);
        Assert.Contains(f.Sent, bytes => MsgAttackPacket.TryParse(Read(bytes), out var interaction) && interaction.Query.AttackType == MsgAttackType.Death);
        Assert.Equal((ushort)0, f.Native.Player.Stamina); Assert.Null(f.Current.MagicCooldowns);
    }, new(Action: "65786", Neptune: true));

    private static byte[] WalkGolden()
    {
        var body = new List<byte>(); Field(body, 1, 6); Field(body, 2, Uid); Field(body, 3, 0);
        Field(body, 4, 123456); Field(body, 5, 1002); return Frame(2205, body);
    }
    private static byte[] JumpGolden()
    {
        var body = new List<byte>(); Field(body, 1, Uid); Field(body, 7, 432); Field(body, 8, 381);
        Field(body, 9, 123456); Field(body, 12, 137); Field(body, 13, 6); Field(body, 14, 432); Field(body, 15, 381);
        Field(body, 17, 1002); return Frame(2046, body);
    }
}
