using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Only the real private item-proc handler associates this inert token.
    internal sealed class RootManualItemEffectAdmission { }
    private sealed record RootManualItemEffectAdmissionData(WorldSessionStore Store, ConquerTcpConnection Connection,
        WorldSession Expected, object Identity, int Flag, int Seconds, string RemoveOnDead, TimeProvider Provider);
    private static readonly ConditionalWeakTable<RootManualItemEffectAdmission, RootManualItemEffectAdmissionData> RootManualItemEffectAdmissions = new();

    internal static bool RootManualItemEffectAdmissionIsCurrent(RootManualItemEffectAdmission admission,
        WorldSessionStore store, ConquerTcpConnection connection, WorldSession expected, object identity,
        int flag, int seconds, string removeOnDead, TimeProvider provider) =>
        WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate &&
        RootManualItemEffectAdmissions.TryGetValue(admission, out var data) &&
        ReferenceEquals(data.Store, store) && ReferenceEquals(data.Connection, connection) &&
        ReferenceEquals(data.Expected, expected) && ReferenceEquals(data.Identity, identity) &&
        data.Flag == flag && data.Seconds == seconds && data.RemoveOnDead == removeOnDead &&
        ReferenceEquals(data.Provider, provider);

    internal static bool ConsumeRootManualItemEffectAdmission(RootManualItemEffectAdmission admission,
        WorldSessionStore store, ConquerTcpConnection connection, WorldSession expected, object identity,
        int flag, int seconds, string removeOnDead, TimeProvider provider) =>
        RootManualItemEffectAdmissionIsCurrent(admission, store, connection, expected, identity, flag, seconds, removeOnDead, provider) &&
        RootManualItemEffectAdmissions.Remove(admission);
}
