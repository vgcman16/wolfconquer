using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeExistingCharacterPrefix
{
    // This capability carries the actual prepared prefix, its original physical
    // state and planned UID. No saved UID alone can issue it or hydrate a player.
    internal SpellAdmissionOwner PrepareSpellAdmissionOwner(ConquerTcpConnection expected, WorldSession proposed)
    {
        if (!ReferenceEquals(expected, connection) || !ReferenceEquals(proposed.NativeGameClient, client) ||
            proposed.Character.Uid != updated.Uid || playerLoad.PreparedUid != updated.Uid ||
            !Monitor.IsEntered(client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(player.SpellAdmissionSyncRoot) || !IsCurrent())
            throw new InvalidOperationException("Ordinary spell admission requires its actual prepared native prefix.");
        return SpellAdmissionOwner.Capture(this);
    }

    internal sealed class SpellAdmissionOwner
    {
        private readonly LegacyNativeExistingCharacterPrefix prefix;
        private bool armed;
        internal uint PreparedUid => prefix.playerLoad.PreparedUid;
        internal LegacyNativeBeastsRuntime PreparedBeastsForBinding(LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player)
        {
            if (!BeforeOwnerIsCurrentPlain(client, player))
                throw new InvalidOperationException("Rune Beast binding requires the actual prepared admission prefix.");
            return prefix.beasts.Target;
        }
        private SpellAdmissionOwner(LegacyNativeExistingCharacterPrefix prefix) => this.prefix = prefix;
        internal static SpellAdmissionOwner Capture(LegacyNativeExistingCharacterPrefix prefix)
        {
            if (prefix is null || !prefix.IsCurrent() || prefix.playerLoad.PreparedUid == 0 ||
                prefix.playerLoad.PreparedUid != prefix.updated.Uid ||
                !Monitor.IsEntered(prefix.client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(prefix.player.SpellAdmissionSyncRoot))
                throw new InvalidOperationException("A detached admission owner requires the genuine current prefix.");
            return new(prefix);
        }

        internal bool BeforeOwnerIsCurrentPlain(LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player) =>
            ReferenceEquals(prefix.client, client) && ReferenceEquals(prefix.player, player) && prefix.NativeIdentityCurrent() &&
            Monitor.IsEntered(client.SpellAdmissionSyncRoot) && Monitor.IsEntered(player.SpellAdmissionSyncRoot) &&
            prefix.playerLoad.IsCurrentPlain() && prefix.playerBefore.IsCurrent() && prefix.beastsBefore.IsCurrent() &&
            prefix.hairfaceBefore.IsCurrent() && prefix.beasts.IsOwnerCurrent() && prefix.hairface.IsCurrent();

        internal bool PublishedOwnerIsCurrentPlain(LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player) =>
            ReferenceEquals(prefix.client, client) && ReferenceEquals(prefix.player, player) && prefix.published &&
            ReferenceEquals(prefix.connection.NativeGameClient, client) && ReferenceEquals(client.Player, player) &&
            ReferenceEquals(player.Owner, client) && !client.FullLoading && !client.Fake &&
            prefix.playerLoad.IsPublishedCurrentPlain() && ReferenceEquals(client.Beasts, prefix.beasts.Target) &&
            ReferenceEquals(client.HairfaceStorage, prefix.hairface.Target);

        internal void ArmAdmissionProof()
        {
            if (armed || !prefix.IsCurrent()) throw new InvalidOperationException("Native prefix changed before admission proof.");
            prefix.beasts.ArmAdmissionProof(); armed = true;
        }
        internal bool ConsumeBeforeWriterPlain() => armed && BeforeOwnerIsCurrentPlain(prefix.client, prefix.player) &&
            prefix.beasts.ConsumeBeforeAdmissionPlain();
        internal bool ConsumeCurrentPlain() => armed && BeforeOwnerIsCurrentPlain(prefix.client, prefix.player) &&
            prefix.beasts.ConsumeFinalAdmissionPlain();
    }
}
