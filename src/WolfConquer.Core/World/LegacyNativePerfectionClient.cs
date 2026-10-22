using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// SAME native live-client identity as the query's mutable MsgStatus, not a record/UID alias.
internal static class LegacyNativePerfectionClient
{
    private sealed class ClientState
    {
        internal uint PrestigeLevel;
        internal LegacyNativePerfectionSnapshot Snapshot = LegacyNativePerfectionSnapshot.Empty;
    }
    private static readonly ConditionalWeakTable<object, ClientState> Clients = new();

    private static bool WithCurrent(WorldSessionStore? world, ConquerTcpConnection connection,
        long uid, object? expectedIdentity, bool create, Action<ClientState> action)
    {
        var applied = false;
        var found = world?.WithCurrentNativeClientIdentity(connection, uid, identity =>
        {
            if (expectedIdentity is not null && !ReferenceEquals(identity, expectedIdentity)) return;
            ClientState state;
            if (create) state = Clients.GetValue(identity, static _ => new());
            else if (!Clients.TryGetValue(identity, out state!)) return;
            // Every access is synchronous inside the SAME Store gate; no publication/await here.
            action(state);
            applied = true;
        }) == true;
        return found && applied;
    }

    internal static uint Prestige(WorldSessionStore? world, ConquerTcpConnection connection,
        long uid, object? expectedIdentity = null)
    {
        uint value = 0;
        WithCurrent(world, connection, uid, expectedIdentity, false, state => value = state.PrestigeLevel);
        return value;
    }

    internal static LegacyNativePerfectionSnapshot Read(WorldSessionStore? world,
        ConquerTcpConnection connection, long uid, object? expectedIdentity = null)
    {
        var value = LegacyNativePerfectionSnapshot.Empty;
        WithCurrent(world, connection, uid, expectedIdentity, false, state => value = state.Snapshot);
        return value;
    }

    internal static bool BeginQuery(WorldSessionStore? world, ConquerTcpConnection connection,
        long uid, object? expectedIdentity) =>
        expectedIdentity is not null && WithCurrent(world, connection, uid, expectedIdentity, true,
            state => state.PrestigeLevel = 0); // Native603 retains OLD PerfectionStatus until Rebuild.

    internal static bool AddCapturedItem(WorldSessionStore? world, ConquerTcpConnection connection,
        long uid, uint perfectionLevel, object? expectedIdentity) =>
        expectedIdentity is not null && WithCurrent(world, connection, uid, expectedIdentity, false,
            state => state.PrestigeLevel = unchecked(state.PrestigeLevel + perfectionLevel));

    internal static bool Rebuild(WorldSessionStore? world, ConquerTcpConnection connection,
        long uid, Func<string, byte> rune, object? expectedIdentity) =>
        expectedIdentity is not null && WithCurrent(world, connection, uid, expectedIdentity, false,
            state => state.Snapshot = LegacyNativePerfectionSnapshot.FromPrestige(state.PrestigeLevel, rune));

    internal static bool AddLucky(WorldSessionStore? world, ConquerTcpConnection connection,
        long uid, int addition, object? expectedIdentity) =>
        expectedIdentity is not null && WithCurrent(world, connection, uid, expectedIdentity, false,
            state => state.Snapshot = state.Snapshot with
                { LuckyStrike = state.Snapshot.LuckyStrike + addition });
}
