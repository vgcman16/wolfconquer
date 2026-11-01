namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Every allocation/metadata read occurs during preparation. The generic
    // Store's final owner proof surrounds this participant with all its others.
    private sealed class OwnedHawksAmbitionPublication
    {
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly WorldSession expected;
        private readonly OwnedHawksSlot slot;
        private readonly OwnedHawksLease? before, after;
        private readonly LegacyNativeAttributeMapProof beforeAttributes, afterAttributes;
        private readonly WorldPacketHandlers.OwnedHawksAmbitionCast? cast;
        private readonly TimeProvider clock;

        private OwnedHawksAmbitionPublication(LegacyOrdinarySpellBinding binding, WorldSession expected,
            WorldSession proposed, OwnedHawksSlot slot, WorldPacketHandlers.OwnedHawksAmbitionCast? cast)
        {
            this.binding = binding; this.expected = expected; this.slot = slot; this.cast = cast;
            before = slot.Lease;
            clock = binding.Store.RequireOwnedHawksAmbitionClock(binding);
            var now = clock.GetUtcNow();
            beforeAttributes = new(expected.Character.Attributes); afterAttributes = new(proposed.Character.Attributes);
            if (cast is not null)
            {
                if (!cast.TryReadPreparedProposal(binding, expected, out _, out var power, out var expiry) ||
                    !cast.MatchesPreparedProjection(binding, expected, proposed)) throw InvalidSpellPublication();
                after = new(power, expiry, clock, cast);
                if (!HawksLeaseMatches(proposed.Character, afterAttributes, after)) throw InvalidSpellPublication();
            }
            else if (before is not null && HawksLeaseMatches(expected.Character, beforeAttributes, before) &&
                HawksLeaseMatches(proposed.Character, afterAttributes, before) && ReferenceEquals(before.Clock, clock) && now < before.ExpiresAtUtc)
                after = before;
            if (!IsCurrentPlain()) throw InvalidSpellPublication();
        }

        internal static OwnedHawksAmbitionPublication? PrepareBound(LegacyOrdinarySpellBinding binding,
            WorldSession expected, WorldSession proposed, WorldPacketHandlers.OwnedHawksAmbitionCast? cast)
        {
            if (!HawksOwnerIsCurrentPlain(binding, expected)) throw InvalidSpellPublication();
            if (!OwnedHawks.TryGetValue(binding, out var slot))
            {
                if (cast is null) return null;
                slot = OwnedHawks.GetValue(binding, static _ => new OwnedHawksSlot());
            }
            if (cast is null && slot.Lease is null) return null; // No active optional dependency remains.
            return new(binding, expected, proposed, slot, cast);
        }

        internal bool IsCurrentPlain() => HawksOwnerIsCurrentPlain(binding, expected) &&
            OwnedHawks.TryGetValue(binding, out var actual) && ReferenceEquals(actual, slot) &&
            Volatile.Read(ref slot.Retired) == 0 && ReferenceEquals(slot.Lease, before) &&
            beforeAttributes.IsCurrentPlain() && afterAttributes.IsCurrentPlain() &&
            binding.Store.IsOwnedHawksAmbitionClockCurrentPlain(binding, clock) &&
            (cast is null || cast.IsCurrentPlain(binding, expected));

        internal void PublishPrepared()
        {
            slot.Lease = after;
            cast?.PublishConsumedPlain();
        }
    }
}
