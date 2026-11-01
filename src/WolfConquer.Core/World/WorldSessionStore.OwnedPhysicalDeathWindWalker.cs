namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed class OwnedDeathWindWalkerRegistration
    {
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly WorldPacketHandlers.OwnedPhysicalDeathPreparation preparation;
        private readonly WindWalkerOwnerSlot slot;
        private readonly JoinedWindWalker? before, after;
        private bool published;

        private OwnedDeathWindWalkerRegistration(LegacyOrdinarySpellBinding binding,
            WorldPacketHandlers.OwnedPhysicalDeathPreparation preparation, WorldSession canonical)
        {
            this.binding = binding; this.preparation = preparation;
            if (!preparation.TryReadNativeWindWalkerProposal(binding, canonical, out var vector, out var captured, out var change))
                throw InvalidSpellPublication();
            slot = WindWalkerOwners.GetValue(binding.Player, static _ => new WindWalkerOwnerSlot());
            before = slot.Owner;
            if (before is not null && (!ReferenceEquals(before.Binding, binding) ||
                !WindWalkerBindingCurrentPlain(before, binding.Player))) throw InvalidSpellPublication();
            after = before ?? (captured.Present || change?.Proposed.Present == true ? new JoinedWindWalker(binding, vector) : null);
            if (!IsCurrentPlain()) throw InvalidSpellPublication();
        }

        internal static OwnedDeathWindWalkerRegistration Prepare(LegacyOrdinarySpellBinding binding,
            WorldPacketHandlers.OwnedPhysicalDeathPreparation preparation, WorldSession canonical) =>
            new(binding, preparation, canonical);

        internal bool IsCurrentPlain() => !published && preparation.IsCurrentPlain(binding) &&
            WindWalkerOwners.TryGetValue(binding.Player, out var actual) && ReferenceEquals(actual, slot) &&
            ReferenceEquals(slot.Owner, before) && (before is null || WindWalkerBindingCurrentPlain(before, binding.Player));

        internal void PublishPrepared() { slot.Owner = after; published = true; }

        internal bool IsPublishedCurrentPlain() => published &&
            WindWalkerOwners.TryGetValue(binding.Player, out var actual) && ReferenceEquals(actual, slot) &&
            ReferenceEquals(slot.Owner, after) && (after is null || WindWalkerBindingCurrentPlain(after, binding.Player));
    }
}
