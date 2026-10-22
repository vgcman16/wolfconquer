using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    // All physical owners/registrations below are MODEL INPUTS, never earned Team leadership.
    private static DateTime Flag6Origin => new(2026, 10, 18, 12, 0, 0, DateTimeKind.Local);

    [Fact]
    public void NativeFlag6ModelDuplicateRetainsExactObjectVisibleBitAndDeadline()
    {
        var player = new LegacyNativeGameClientRuntime().Player;
        Assert.True(NativeFlag6TestModel.AddFlagFixture(player, Flag6Origin));
        var first = Assert.IsType<LegacyNativeStatusFlagsBigVector32.Flag>(NativeFlag6TestModel.Current(player));
        Assert.False(first.RemoveOnDead);
        Assert.Equal(2_592_000, first.Secounds);
        Assert.True(NativeFlag6TestModel.Visible(player));
        Assert.False(NativeFlag6TestModel.AddFlagFixture(player, Flag6Origin.AddDays(1)));
        Assert.Same(first, NativeFlag6TestModel.Current(player));
        Assert.Equal(Flag6Origin, first.Timer);
    }

    [Fact]
    public void NativeFlag6ModelDeathAndReviveSelectiveClearRetainEvenExpiredFalseRegistration()
    {
        var player = new LegacyNativeGameClientRuntime().Player;
        Assert.True(NativeFlag6TestModel.AddFlagFixture(player, Flag6Origin));
        var first = NativeFlag6TestModel.Current(player);
        Assert.NotNull(first);
        Assert.True(first.Expire(Flag6Origin.AddDays(31)));
        // Only the Flag6 part of both native ClearFlags calls, not full Dead/Revive execution.
        NativeFlag6TestModel.ClearDeathRemovableFixture(player);
        NativeFlag6TestModel.ClearDeathRemovableFixture(player);
        Assert.Same(first, NativeFlag6TestModel.Current(player));
        Assert.Equal(Flag6Origin, first.Timer);
        Assert.True(NativeFlag6TestModel.Visible(player));
    }

    [Fact]
    public void NativeFlag6ModelExplicitRemoveAndReAddDoNotRenewTheOldObject()
    {
        var player = new LegacyNativeGameClientRuntime().Player;
        Assert.False(NativeFlag6TestModel.RemoveFlagFixture(player));
        Assert.True(NativeFlag6TestModel.AddFlagFixture(player, Flag6Origin));
        var first = NativeFlag6TestModel.Current(player);
        Assert.True(NativeFlag6TestModel.RemoveFlagFixture(player));
        Assert.Null(NativeFlag6TestModel.Current(player));
        Assert.False(NativeFlag6TestModel.Visible(player));
        Assert.False(NativeFlag6TestModel.RemoveFlagFixture(player));
        Assert.True(NativeFlag6TestModel.AddFlagFixture(player, Flag6Origin.AddSeconds(2)));
        Assert.NotSame(first, NativeFlag6TestModel.Current(player));
        Assert.Equal(Flag6Origin.AddSeconds(2), NativeFlag6TestModel.Current(player)!.Timer);
    }

    [Fact]
    public void NativeFlag6ModelExpiryUsesInclusiveDeadlineAndDoesNotPruneOnRead()
    {
        var owner = new LegacyNativeGameClientRuntime();
        Assert.True(NativeFlag6TestModel.AddFlagFixture(owner.Player, Flag6Origin));
        var row = NativeFlag6TestModel.CaptureBuffersRegistrationFixture(owner);
        var deadline = Flag6Origin.AddSeconds(NativeFlag6TestModel.NativeSeconds);
        Assert.False(NativeFlag6TestModel.ConsumeBuffersExpiryFixture(owner, row, deadline.AddTicks(-1)));
        Assert.True(NativeFlag6TestModel.Visible(owner.Player));
        Assert.NotNull(NativeFlag6TestModel.Current(owner.Player));
        Assert.True(NativeFlag6TestModel.ConsumeBuffersExpiryFixture(owner, row, deadline));
        Assert.Null(NativeFlag6TestModel.Current(owner.Player));
        Assert.False(NativeFlag6TestModel.Visible(owner.Player));
        Assert.False(NativeFlag6TestModel.ConsumeBuffersExpiryFixture(owner, row, deadline));
    }

    [Fact]
    public void NativeFlag6ModelOldExpiredSnapshotRemovesYoungerCurrentReplacementFlag()
    {
        var owner = new LegacyNativeGameClientRuntime();
        var old = owner.Player;
        Assert.True(NativeFlag6TestModel.AddFlagFixture(old, Flag6Origin));
        var row = NativeFlag6TestModel.CaptureBuffersRegistrationFixture(owner);
        var deadline = Flag6Origin.AddDays(30);
        owner.Player = new LegacyNativePlayerRuntime(owner);
        Assert.True(NativeFlag6TestModel.AddFlagFixture(owner.Player, deadline));
        var younger = NativeFlag6TestModel.Current(owner.Player);
        Assert.NotNull(younger);
        Assert.False(younger.Expire(deadline));
        Assert.True(NativeFlag6TestModel.ConsumeBuffersExpiryFixture(owner, row, deadline));
        Assert.Null(NativeFlag6TestModel.Current(owner.Player)); // No invented generation or renewed-timer CAS.
        Assert.False(NativeFlag6TestModel.Visible(owner.Player));
        Assert.Same(row, NativeFlag6TestModel.Current(old));
        Assert.True(NativeFlag6TestModel.Visible(old));
    }

    [Fact]
    public void NativeFlag6ModelNoCapturedRowCannotExpireALaterRegistration()
    {
        var owner = new LegacyNativeGameClientRuntime();
        var row = NativeFlag6TestModel.CaptureBuffersRegistrationFixture(owner);
        Assert.Null(row);
        Assert.True(NativeFlag6TestModel.AddFlagFixture(owner.Player, Flag6Origin));
        Assert.False(NativeFlag6TestModel.ConsumeBuffersExpiryFixture(owner, row, Flag6Origin.AddDays(31)));
        Assert.True(NativeFlag6TestModel.Visible(owner.Player));
    }

    [Fact]
    public void NativeFlag6ModelProjectionCopiesWordsAndOverridesOnlyPhysicalBit6()
    {
        var owner = new LegacyNativeGameClientRuntime();
        var words = Enumerable.Range(0, 18).Select(index => 0x80000000UL + (ulong)index).ToArray();
        words[0] |= LegacyNativePlayerFlag6Lifecycle.Mask;
        var old = words.ToArray();
        var unbound = LegacyNativePlayerFlag6Lifecycle.CaptureVector(null, words);
        Assert.Equal(old, unbound);
        Assert.NotSame(words, unbound);
        var absent = LegacyNativePlayerFlag6Lifecycle.CaptureVector(owner.Player, words);
        Assert.Equal(old[0] & ~LegacyNativePlayerFlag6Lifecycle.Mask, absent[0]);
        Assert.Equal(old.Skip(1), absent.Skip(1));
        Assert.Equal(old, words);
        Assert.True(NativeFlag6TestModel.AddFlagFixture(owner.Player, Flag6Origin));
        Assert.Equal(old, LegacyNativePlayerFlag6Lifecycle.CaptureVector(owner.Player, absent));
        Assert.Equal(old[0] & ~LegacyNativePlayerFlag6Lifecycle.Mask, absent[0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => LegacyNativePlayerFlag6Lifecycle.CaptureVector(owner.Player, []));
    }

    [Fact]
    public void NativeFlag6ModelProjectionFollowsCurrentPlayerAcrossSessionCopies()
    {
        var owner = new LegacyNativeGameClientRuntime();
        var old = owner.Player;
        Assert.True(NativeFlag6TestModel.AddFlagFixture(old, Flag6Origin));
        var session = CreateSession(spellIds: []) with { NativeGameClient = owner };
        var copy = session with { Character = session.Character with { Name = "Renamed" } };
        var words = new ulong[18];
        Assert.Equal(LegacyNativePlayerFlag6Lifecycle.Mask,
            LegacyNativePlayerFlag6Lifecycle.CaptureVector(copy.NativePlayer, words)[0]);
        owner.Player = new LegacyNativePlayerRuntime(owner);
        Assert.Equal(0UL, LegacyNativePlayerFlag6Lifecycle.CaptureVector(copy.NativePlayer, words)[0]);
        Assert.True(NativeFlag6TestModel.Visible(old));
        Assert.Same(owner, copy.NativeGameClient);
        Assert.NotSame(old, copy.NativePlayer);
        Assert.Null(NativeFlag6TestModel.Current(owner.Player));
    }

    [Fact]
    public async Task NativeFlag6RevivePublicEntryPublishesRetainedModelInputRegistration()
    {
        var owner = new LegacyNativeGameClientRuntime();
        Assert.True(NativeFlag6TestModel.AddFlagFixture(owner.Player, Flag6Origin));
        var row = NativeFlag6TestModel.Current(owner.Player);
        var status = Flag6SingleStatus(await Flag6DispatchReviveAsync(owner));
        Assert.Equal(PlayerUid, status.Uid);
        var values = Assert.Single(status.Updates).Values!;
        Assert.Equal(LegacyNativePlayerFlag6Lifecycle.Mask, values[0] & LegacyNativePlayerFlag6Lifecycle.Mask);
        Assert.Equal(0UL, values[0] & ((1UL << 5) | (1UL << 10)));
        Assert.Same(row, NativeFlag6TestModel.Current(owner.Player));
        Assert.Equal(Flag6Origin, row!.Timer);
        Assert.True(NativeFlag6TestModel.Visible(owner.Player));
    }

    [Fact]
    public async Task NativeFlag6RevivePublicEntryUnboundDoesNotInventPhysicalFlag()
    {
        var status = Flag6SingleStatus(await Flag6DispatchReviveAsync(null));
        var values = Assert.Single(status.Updates).Values!;
        Assert.Equal(0UL, values[0] & (LegacyNativePlayerFlag6Lifecycle.Mask | (1UL << 5) | (1UL << 10)));
    }

    // Real public MsgAction Revive dispatch; preceding death and physical Flag6 are MODEL INPUTS.
    // These tests do not prove native Team acquisition, GameClient birth, RNG or Buffers scheduling.
    private static async Task<List<byte[]>> Flag6DispatchReviveAsync(LegacyNativeGameClientRuntime? owner)
    {
        var frames = new List<byte[]>();
        var connection = new ConquerTcpConnection(null, null,
            sendAsync: (bytes, _) => { frames.Add(bytes.ToArray()); return ValueTask.CompletedTask; },
            nativeGameClient: owner);
        var session = CreateSession(minHitPoints: 0, spellIds: [],
            attributes: new Dictionary<string, string> { ["StatusFlags"] = "1056" }) with
            { NativeGameClient = owner };
        var world = new WorldSessionStore();
        world.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, TextWriter.Null, session, Sessions: world);
        await PacketHandlers.HandleAsync(context, ReadFrame(new MsgActionPacket
            { ObjId = PlayerUid, Type = MsgActionType.Revive }.ToFrameBytes()));
        Assert.True(world.TryGet(connection, out var revived));
        Assert.NotNull(revived);
        Assert.True(revived.Character.MinHitPoints > 0);
        Assert.False(LegacyPlayerDeathState.HasStatusFlag(revived.Character.Attributes, 5));
        Assert.False(LegacyPlayerDeathState.HasStatusFlag(revived.Character.Attributes, 10));
        return frames;
    }

    private static LegacyMsgUpdatePacket Flag6SingleStatus(IEnumerable<byte[]> frames) =>
        Assert.Single(frames.Select(ReadFrame).Select(frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var update) ? update : null)
            .OfType<LegacyMsgUpdatePacket>().Where(update =>
                update.Updates.Count == 1 && update.Updates[0].Type == LegacyMsgUpdateDataType.StatusFlag));
}
