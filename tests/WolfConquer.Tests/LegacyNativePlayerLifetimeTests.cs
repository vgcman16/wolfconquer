using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Xunit;

namespace WolfConquer.Tests;

// MODEL/producer-support controls only. No public Team Create, handshake, RNG or tick parity proof.
public sealed class LegacyNativePlayerLifetimeTests
{
    private static readonly DateTime Now = new(2026, 10, 18, 10, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public void TransportAndUnboundSessionDoNotInventNativePlayerBirth()
    {
        var connection = new ConquerTcpConnection(null, null);
        Assert.Null(connection.NativeGameClient);
        Assert.Null(Session().NativePlayer);
    }

    [Fact]
    public void RecordCopiesStorePublishesAndUidMutationDoNotAllocatePlayer()
    {
        var client = new LegacyNativeGameClientRuntime();
        var connection = new ConquerTcpConnection(null, null, nativeGameClient: client);
        var original = Session() with { NativeGameClient = client };
        var player = client.Player;
        Assert.True(NativeFlag6TestModel.Seed(player, Now));
        var registration = Assert.IsType<LegacyNativeStatusFlagsBigVector32.Flag>(NativeFlag6TestModel.Current(player));
        var world = new WorldSessionStore();
        world.Set(connection, original);
        var copied = original with { Character = original.Character with { Money = 123, Uid = 2 } };
        world.Set(connection, copied);
        Assert.Same(client, copied.NativeGameClient);
        Assert.Same(player, copied.NativePlayer);
        Assert.Same(player, original.NativePlayer);
        Assert.Same(registration, NativeFlag6TestModel.Current(copied.NativePlayer!));
        Assert.Equal(123, copied.Character.Money);
    }

    [Fact]
    public void NewPhysicalPlayerDoesNotReplaceCurrentUntilExplicitNativeAssignment()
    {
        var client = new LegacyNativeGameClientRuntime();
        var session = Session() with { NativeGameClient = client };
        var oldPlayer = client.Player;
        Assert.True(NativeFlag6TestModel.Seed(oldPlayer, Now));
        var oldRegistration = NativeFlag6TestModel.Current(oldPlayer);
        var imported = new LegacyNativePlayerRuntime(client);
        Assert.Same(client, imported.Owner);
        Assert.NotSame(oldPlayer.BitVector, imported.BitVector);
        Assert.Null(NativeFlag6TestModel.Current(imported));
        Assert.Same(oldPlayer, session.NativePlayer); // Allocation precedes parser/adoption.
        client.Player = imported;
        Assert.Same(imported, session.NativePlayer); // Even a stale record dereferences CURRENT Player.
        Assert.Null(NativeFlag6TestModel.Current(session.NativePlayer!));
        Assert.Same(oldRegistration, NativeFlag6TestModel.Current(oldPlayer)); // Replacement is not erasure/rollback.
    }

    [Fact]
    public void DuplicateDeathAndRemoveReaddPreserveNativeFlag6RegistrationRules()
    {
        var flags = new LegacyNativeGameClientRuntime().Player.BitVector;
        Assert.True(NativeFlag6TestModel.Seed(flags, Now));
        var first = Assert.IsType<LegacyNativeStatusFlagsBigVector32.Flag>(NativeFlag6TestModel.Current(flags));
        Assert.False(NativeFlag6TestModel.Seed(flags, Now.AddDays(1)));
        flags.GetClear();
        Assert.Same(first, NativeFlag6TestModel.Current(flags));
        Assert.Equal(Now, first.Timer);
        Assert.Equal(2_592_000, first.Secounds);
        Assert.False(first.RemoveOnDead);
        Assert.True(flags.TryRemove(NativeFlag6TestModel.Flag));
        Assert.False(flags.TryRemove(NativeFlag6TestModel.Flag));
        Assert.True(NativeFlag6TestModel.Seed(flags, Now.AddDays(2)));
        Assert.NotSame(first, NativeFlag6TestModel.Current(flags));
        Assert.Equal(Now.AddDays(2), NativeFlag6TestModel.Current(flags)!.Timer);
    }

    [Fact]
    public void InclusiveExpiryIsQueryOnlyUntilNativeRemoveConsumerRuns()
    {
        var flags = new LegacyNativeGameClientRuntime().Player.BitVector;
        Assert.True(NativeFlag6TestModel.Seed(flags, Now));
        var flag = Assert.IsType<LegacyNativeStatusFlagsBigVector32.Flag>(NativeFlag6TestModel.Current(flags));
        var deadline = Now.AddSeconds(flag.Secounds);
        Assert.False(flag.Expire(deadline.AddTicks(-1)));
        Assert.True(flag.Expire(deadline));
        Assert.True(flag.Expire(deadline.AddTicks(1)));
        Assert.Same(flag, NativeFlag6TestModel.Current(flags)); // Query does not invent a tick or publication.
    }

    [Theory]
    [InlineData(true, 100, 100)] // SetNewTimer: replace timer and seconds.
    [InlineData(false, 100, 100)] // Cap: old deadline is greater than now+MaxSeconds.
    [InlineData(false, 3_000_000, 2_592_100)] // Else: extend seconds without resetting Timer.
    public void ExplicitUpdateKeepsTheSameRegistrationAndNativeBranchOrder(
        bool setNewTimer, int maxSeconds, int expectedSeconds)
    {
        var flags = new LegacyNativeGameClientRuntime().Player.BitVector;
        Assert.False(flags.UpdateFlag(NativeFlag6TestModel.Flag, 100, setNewTimer, maxSeconds));
        // Explicit public Timer fixture prestate makes cap/extension branches deterministic.
        // The native method itself still samples the actual DateTime.Now, never a test clock.
        var modelTimer = DateTime.Now;
        Assert.True(NativeFlag6TestModel.Seed(flags, modelTimer));
        var flag = NativeFlag6TestModel.Current(flags);
        var before = DateTime.Now;
        Assert.True(flags.UpdateFlag(NativeFlag6TestModel.Flag, 100, setNewTimer, maxSeconds));
        var after = DateTime.Now;
        Assert.Same(flag, NativeFlag6TestModel.Current(flags));
        Assert.Equal(expectedSeconds, flag!.Secounds);
        if (setNewTimer || maxSeconds == 100)
            Assert.InRange(flag.Timer, before, after);
        else
            Assert.Equal(modelTimer, flag.Timer);
    }

    private static WorldSession Session() => new(
        1, "native-player-model",
        new CharacterSnapshot(
            Uid: 1, Name: "NativePlayerModel", Body: 1003, Face: 0, Hair: 0,
            Class: 10, FirstClass: 0, SecondClass: 0, Reborn: 0, Level: 1, Experience: 0,
            MapId: 1002, X: 400, Y: 400, PreviousMapId: 1002, PreviousMapX: 400, PreviousMapY: 400,
            Strength: 1, Agility: 1, Vitality: 1, Spirit: 1, AttributePoints: 0,
            MinHitPoints: 100, MinMana: 100, Money: 0, ConquerPoints: 0,
            BoundConquerPoints: 0, BankConquerPoints: 0, PkPoints: 0, Online: 1,
            SourceFile: "declared-model-input", Attributes: new Dictionary<string, string>(),
            PaidNobility: new Dictionary<string, string>()),
        PlayerItemSnapshot.Empty, PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty);
}
