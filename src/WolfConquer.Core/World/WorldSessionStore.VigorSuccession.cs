using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // This token is associated only by an actual prepared Store publication.
    // A constructed token, equal map, UID or caller callback grants nothing.
    internal sealed class OwnedVigorPublication { }
    private sealed record VigorPublicationData(WorldSessionStore Store, ConquerTcpConnection Connection,
        SessionHolder Holder, LegacyOrdinarySpellBinding Binding, WorldSession Expected, WorldSession Final,
        object Identity, LegacyOrdinarySpellBinding.PreparedVigorRetention? Retention);
    private static readonly ConditionalWeakTable<OwnedVigorPublication, VigorPublicationData> VigorPublications = new();

    private static OwnedVigorPublication PrepareVigorPublication(WorldSessionStore store, ConquerTcpConnection connection,
        SessionHolder holder, LegacyOrdinarySpellBinding binding, WorldSession expected, WorldSession final, object identity)
    {
        var retention = binding.PrepareVigorRetention(expected, final);
        var permit = new OwnedVigorPublication();
        VigorPublications.Add(permit, new(store, connection, holder, binding, expected, final, identity, retention));
        return permit;
    }

    private static bool VigorPublicationReady(OwnedVigorPublication permit)
    {
        if (!VigorPublications.TryGetValue(permit, out var data) ||
            !IsCurrentThreadInsideAnyConnectionGate || !ReferenceEquals(data.Holder.Read(), data.Expected) ||
            !ReferenceEquals(data.Holder.SpellBinding, data.Binding) ||
            !ReferenceEquals(data.Holder.NativeClientIdentity, data.Identity)) return false;
        try { return data.Retention is null || data.Retention.IsCurrentPlain(); }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException)
        { return false; }
    }

    internal static bool ConsumeVigorPublication(OwnedVigorPublication permit, LegacyOrdinarySpellBinding binding,
        LegacyOrdinarySpellBinding.PreparedVigorRetention? retention)
    {
        if (!VigorPublications.TryGetValue(permit, out var data) || !IsCurrentThreadInsideAnyConnectionGate ||
            !ReferenceEquals(data.Binding, binding) || !ReferenceEquals(data.Retention, retention) ||
            !ReferenceEquals(data.Store, binding.Store) || !ReferenceEquals(data.Connection, binding.Connection) ||
            !ReferenceEquals(data.Holder.Read(), data.Final) || !ReferenceEquals(data.Holder.SpellBinding, binding) ||
            !ReferenceEquals(data.Holder.NativeClientIdentity, data.Identity)) return false;
        return VigorPublications.Remove(permit);
    }

    private static LegacyOrdinarySpellBinding.PreparedVigorRetention? VigorRetention(OwnedVigorPublication permit) =>
        VigorPublications.TryGetValue(permit, out var data) ? data.Retention : throw InvalidSpellPublication();
}
