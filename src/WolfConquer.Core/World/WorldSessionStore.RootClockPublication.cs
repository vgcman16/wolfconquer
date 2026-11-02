using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed class RootClockPublication { }
    private sealed record RootClockPublicationData(WorldSessionStore Store, ConquerTcpConnection Connection,
        SessionHolder Holder, WorldSession? Expected, WorldSession Final, object Identity, object Plan);
    private static readonly ConditionalWeakTable<RootClockPublication, RootClockPublicationData> RootClockPublications = new();
    private static RootClockPublication RootClockPermit(WorldSessionStore store, ConquerTcpConnection connection,
        SessionHolder holder, WorldSession? expected, WorldSession final, object identity, object plan)
    {
        var token = new RootClockPublication();
        RootClockPublications.Add(token, new(store, connection, holder, expected, final, identity, plan));
        return token;
    }
    private static bool RootClockGateHeld(RootClockPublicationData data) =>
        data.Store._connectionGates.TryGetValue(data.Connection, out var gate) && Monitor.IsEntered(gate) &&
        data.Store._sessions.TryGetValue(data.Connection, out var holder) && ReferenceEquals(holder, data.Holder);
    private static bool RootClockPermitBefore(RootClockPublication permit) =>
        RootClockPublications.TryGetValue(permit, out var data) && IsCurrentThreadInsideAnyConnectionGate && RootClockGateHeld(data) &&
        ReferenceEquals(data.Holder.Read(), data.Expected) && ReferenceEquals(data.Holder.NativeClientIdentity, data.Identity) &&
        data.Plan is LegacyRootItemEffectFlagClock.PreparedSuccession succession && succession.IsCurrentPlain();
    internal static bool ConsumeRootClockPermit(RootClockPublication permit, object plan) =>
        RootClockPublications.TryGetValue(permit, out var data) && IsCurrentThreadInsideAnyConnectionGate && RootClockGateHeld(data) &&
        ReferenceEquals(data.Plan, plan) && ReferenceEquals(data.Holder.Read(), data.Final) &&
        ReferenceEquals(data.Holder.NativeClientIdentity, data.Identity) && RootClockPublications.Remove(permit);
}
