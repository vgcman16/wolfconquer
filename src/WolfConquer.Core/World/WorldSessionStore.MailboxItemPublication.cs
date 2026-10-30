using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Issued only under the actual Store's current holder gate. This capability
    // prepares native state; it cannot accept a saver, reserve a UID or commit IO.
    internal sealed class PreparedMailboxItemPublication : IDisposable
    {
        private readonly WorldSessionStore store;
        private readonly ConquerTcpConnection connection;
        private readonly LegacyMailboxPackageProducer producer;
        private readonly WorldSession expected;
        private readonly SessionHolder holder;
        private readonly object gate, identity;
        private readonly PreparedSessionSpellPublication publication;
        private LegacyOwnedNativeItemImage.CatalogEvidence? catalog;
        private bool active = true, requirementsValidated, prepared, armed;

        internal PreparedMailboxItemPublication(WorldSessionStore store, ConquerTcpConnection connection,
            LegacyMailboxPackageProducer producer, WorldSession expected)
        {
            this.store = store; this.connection = connection; this.producer = producer; this.expected = expected;
            if (!ReferenceEquals(store.MailboxPackages, producer) ||
                !store._connectionGates.TryGetValue(connection, out gate!) || !Monitor.IsEntered(gate) ||
                !store._sessions.TryGetValue(connection, out holder!) || !ReferenceEquals(holder.Read(), expected))
                throw InvalidSpellPublication();
            identity = holder.NativeClientIdentity;
            publication = new(store, connection, holder, expected, expected, deferItems: true);
        }

        internal void ValidateItemIds(IEnumerable<uint> actualItemIds)
        {
            RequireLease();
            ArgumentNullException.ThrowIfNull(actualItemIds);
            if (prepared || requirementsValidated) throw InvalidSpellPublication();
            requirementsValidated = true;
            if (publication.Binding is not { } binding) return; // Wholly unbound compatibility.
            binding.Items.RequireCurrentBeforePreparation();
            catalog = LegacyOwnedNativeItemImage.ValidateItemIds(binding.ItemCatalog, actualItemIds, binding.ItemProfile);
            catalog.RequireCurrent();
        }

        internal WorldSession Prepare(WorldSession proposed)
        {
            RequireLease();
            if (prepared || !requirementsValidated || publication.Binding is not null && catalog is null) throw InvalidSpellPublication();
            catalog?.RequireCurrent();
            var final = publication.PrepareDeferredItems(proposed);
            prepared = true;
            return final;
        }

        internal void ArmBeforeCommit()
        {
            RequireLease();
            if (!prepared || armed) throw InvalidSpellPublication();
            catalog?.RequireCurrent();
            publication.RequireCurrentBeforeWriter();
            if (catalog is not null && !catalog.IsCurrentPlain()) throw InvalidSpellPublication();
            publication.ArmFinalProof();
            if (catalog is not null && !catalog.IsCurrentPlain() || !publication.ConsumeBeforeWriterPlain())
                throw InvalidSpellPublication();
            armed = true;
        }

        internal bool IsCurrentPlain(WorldSession returned) => active && prepared && armed &&
            Monitor.IsEntered(gate) && ReferenceEquals(store.MailboxPackages, producer) &&
            store._sessions.TryGetValue(connection, out var current) && ReferenceEquals(current, holder) &&
            ReferenceEquals(holder.Read(), expected) && ReferenceEquals(holder.NativeClientIdentity, identity) &&
            ReferenceEquals(returned, publication.FinalSession) &&
            (catalog is null || catalog.IsCurrentPlain()) && publication.IsCurrentPlain();

        internal LegacyOrdinarySpellBinding? Binding => publication.Binding;
        internal void PublishNative() => publication.PublishNative();
        internal void PublishHolder() => publication.PublishHolder();

        private void RequireLease()
        {
            if (!active || !Monitor.IsEntered(gate) || !ReferenceEquals(store.MailboxPackages, producer) ||
                !store._sessions.TryGetValue(connection, out var current) || !ReferenceEquals(current, holder) ||
                !ReferenceEquals(holder.Read(), expected) || !ReferenceEquals(holder.NativeClientIdentity, identity))
                throw InvalidSpellPublication();
        }

        public void Dispose()
        {
            if (!active) return;
            active = false;
            publication.Dispose();
        }
    }
}
