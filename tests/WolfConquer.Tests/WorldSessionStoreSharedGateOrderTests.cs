using System.Reflection;
using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class WorldSessionStoreSharedGateOrderTests
{
    [Fact]
    public void NativeTeamAndPersistedPairUseSameGateOrderWithReversedParticipants()
    {
        var world = new WorldSessionStore();
        var low = new ConquerTcpConnection(null, null);
        var high = new ConquerTcpConnection(null, null);
        var lowSession = CreateSession(1_294_041);
        var highSession = CreateSession(1_294_042);
        world.Set(low, lowSession);
        world.Set(high, highSession);

        // Seed the shared order opposite to the participant order used below.
        // A separate Team-only registry would instead assign high before low.
        Assert.True(WorldSessionStore.GetConnectionGateOrder(low) < WorldSessionStore.GetConnectionGateOrder(high));
        var holders = new LegacyTournamentWatcherHolders(world);
        var teams = new TeamSessionStateStore(world, holders);
        Assert.True(holders.TryCaptureClient(low, out var lowClient));
        Assert.True(holders.TryCaptureClient(high, out var highClient));
        Assert.NotNull(lowClient);
        Assert.NotNull(highClient);
        Assert.True(teams.TryCreateNativeTeam(highClient, "ReversedLeader"));
        var lowGate = GetConnectionGate(world, low);
        var highGate = GetConnectionGate(world, high);

        AssertWaitsForLowGateWithoutHoldingHigh(lowGate, highGate,
            () => teams.TryAddNativeMember(highClient, lowClient, requireLeader: true));
        var persistenceCalls = 0;
        AssertWaitsForLowGateWithoutHoldingHigh(lowGate, highGate,
            () => world.TryCommitPair(high, highSession, highSession, low, lowSession, lowSession,
                () => { Interlocked.Increment(ref persistenceCalls); return true; }));

        Assert.Equal(1, Volatile.Read(ref persistenceCalls));
        Assert.True(teams.TryCaptureTournamentTeam(highClient, out var team));
        Assert.True(teams.IsTournamentTeamMember(team!, lowClient));
        Assert.True(world.TryGet(low, out var currentLow));
        Assert.True(world.TryGet(high, out var currentHigh));
        Assert.Same(lowSession, currentLow);
        Assert.Same(highSession, currentHigh);
    }

    private static void AssertWaitsForLowGateWithoutHoldingHigh(object lowGate, object highGate, Func<bool> action)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new Thread(() =>
        {
            try { completion.SetResult(action()); }
            catch (Exception error) { completion.SetException(error); }
        }) { IsBackground = true };
        var highAvailable = false;
        Monitor.Enter(lowGate);
        try
        {
            worker.Start();
            Assert.True(SpinWait.SpinUntil(
                () => worker.ThreadState.HasFlag(ThreadState.WaitSleepJoin), TimeSpan.FromSeconds(5)),
                "The reversed participant operation did not wait on its first connection gate.");
            highAvailable = Monitor.TryEnter(highGate);
            if (highAvailable) Monitor.Exit(highGate);
        }
        finally
        {
            Monitor.Exit(lowGate);
            Assert.True(worker.Join(TimeSpan.FromSeconds(5)), "The operation did not finish after its first gate was released.");
        }

        Assert.True(highAvailable, "A second owner acquired the higher gate before the shared lower gate, allowing ABBA deadlock.");
        Assert.True(completion.Task.GetAwaiter().GetResult());
    }

    private static object GetConnectionGate(WorldSessionStore world, ConquerTcpConnection connection)
    {
        var field = typeof(WorldSessionStore).GetField("_connectionGates", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var gates = Assert.IsType<ConditionalWeakTable<ConquerTcpConnection, object>>(field.GetValue(world));
        Assert.True(gates.TryGetValue(connection, out var gate));
        return Assert.IsType<object>(gate);
    }

    private static WorldSession CreateSession(uint uid)
    {
        var character = new CharacterSnapshot(
            Uid: uid, Name: "GateTrader" + uid, Body: 1003, Face: 0, Hair: 0,
            Class: 10, FirstClass: 10, SecondClass: 0, Reborn: 0, Level: 1,
            Experience: 0, MapId: 1002, X: 300, Y: 278,
            PreviousMapId: 1002, PreviousMapX: 300, PreviousMapY: 278,
            Strength: 0, Agility: 0, Vitality: 0, Spirit: 0, AttributePoints: 0,
            MinHitPoints: 1, MinMana: 0, Money: 0, ConquerPoints: 0,
            BoundConquerPoints: 0, BankConquerPoints: 0, PkPoints: 0, Online: 1,
            SourceFile: uid + ".ini", Attributes: new Dictionary<string, string>(),
            PaidNobility: new Dictionary<string, string>());
        return new(uid, character.Name, character, PlayerItemSnapshot.Empty, PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty);
    }
}
