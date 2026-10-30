using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool TryCommitMagicCosts(ConquerTcpConnection connection, WorldSession expected,
        object identity, WorldSession candidate, out MagicCostOutputReceipt? receipt, LegacyRuneCastEligibility? runeEligibility = null)
    {
        if (!ReferenceEquals(expected, candidate))
            return TryCompareAndSetCore(connection, expected, candidate, identity,
                out _, captureMagicCosts: true, out receipt, runeEligibility);

        // A genuinely unchanged admission needs a current holder proof, not a
        // redundant writer/observer merely to manufacture publication history.
        receipt = null;
        RefuseBoundSpellReentry(connection);
        using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
        if (!_connectionGates.TryGetValue(connection, out var gate)) return false;
        lock (gate)
        {
            using var scope = EnterNativeConnectionGate();
            if (!_sessions.TryGetValue(connection, out var holder) ||
                !ReferenceEquals(holder.Read(), expected) || !ReferenceEquals(holder.NativeClientIdentity, identity) ||
                !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, expected)) return false;
            if (holder.SpellBinding is { } binding
                ? !BindingIsCurrentPlain(binding, holder, expected)
                : connection.NativeGameClient?.OrdinarySpellBinding is not null ||
                  connection.NativeGameClient?.Player?.HasOwnedVitalsAttachment == true) return false;
            if (runeEligibility is not null && !runeEligibility.IsCurrentPlain()) return false;
            receipt = new(this, connection, identity, holder.SpellBinding, expected, expected);
            return true;
        }
    }
}
