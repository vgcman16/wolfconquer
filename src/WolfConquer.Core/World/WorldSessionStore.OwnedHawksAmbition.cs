using System.Globalization;
using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private static readonly ConditionalWeakTable<LegacyOrdinarySpellBinding, OwnedHawksSlot> OwnedHawks = new();
    private sealed class OwnedHawksSlot
    {
        internal OwnedHawksLease? Lease;
        internal int Retired;
    }
    private sealed record OwnedHawksLease(uint Power, DateTimeOffset ExpiresAtUtc, TimeProvider Clock,
        WorldPacketHandlers.OwnedHawksAmbitionCast SourceCast);
    private static readonly ConditionalWeakTable<OwnedHawksStatusProof, OwnedHawksCapture> OwnedHawksProofs = new();

    internal sealed class OwnedHawksStatusProof
    {
        internal uint Power => OwnedHawksProofs.TryGetValue(this, out var captured) ? captured.Power :
            throw new InvalidOperationException("ownedHawksStatusProofNotIssued");
        internal bool IsCurrentPlain() => OwnedHawksProofs.TryGetValue(this, out var captured) && captured.IsCurrentPlain();
    }

    private sealed class OwnedHawksCapture
    {
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly WorldSession current;
        private readonly OwnedHawksSlot slot;
        private readonly OwnedHawksLease lease;
        private readonly LegacyNativeAttributeMapProof attributes;
        private readonly DateTimeOffset observedNow;
        internal OwnedHawksCapture(LegacyOrdinarySpellBinding binding, WorldSession current,
            OwnedHawksSlot slot, OwnedHawksLease lease, LegacyNativeAttributeMapProof attributes, DateTimeOffset observedNow)
        { this.binding = binding; this.current = current; this.slot = slot; this.lease = lease; this.attributes = attributes; this.observedNow = observedNow; }
        internal uint Power => lease.Power;
        internal bool IsCurrentPlain() => HawksOwnerIsCurrentPlain(binding, current) &&
            OwnedHawks.TryGetValue(binding, out var actual) && ReferenceEquals(actual, slot) && Volatile.Read(ref slot.Retired) == 0 &&
            ReferenceEquals(slot.Lease, lease) && attributes.IsCurrentPlain() && observedNow < lease.ExpiresAtUtc &&
            binding.Store.IsOwnedHawksAmbitionClockCurrentPlain(binding, lease.Clock);
    }

    internal sealed class OwnedHawksPowerProof
    {
        private readonly OwnedHawksStatusProof status;
        internal OwnedHawksPowerProof(OwnedHawksStatusProof status) => this.status = status;
        internal uint Power => status.Power;
        internal bool IsCurrentPlain() => status.IsCurrentPlain();
    }

    internal static bool TryCaptureOwnedHawksStatus(LegacyOrdinarySpellBinding binding, WorldSession current,
        out OwnedHawksStatusProof? proof)
    {
        proof = null;
        if (!HawksOwnerIsCurrentPlain(binding, current) || !OwnedHawks.TryGetValue(binding, out var slot) ||
            Volatile.Read(ref slot.Retired) != 0 || slot.Lease is not { } lease) return false;
        try
        {
            if (!binding.Store.IsOwnedHawksAmbitionClockCurrentPlain(binding, lease.Clock)) return false;
            var now = lease.Clock.GetUtcNow();
            var attributes = new LegacyNativeAttributeMapProof(current.Character.Attributes);
            if (!HawksLeaseMatches(current.Character, attributes, lease) || now >= lease.ExpiresAtUtc) return false;
            var captured = new OwnedHawksStatusProof();
            OwnedHawksProofs.Add(captured, new(binding, current, slot, lease, attributes, now));
            if (!captured.IsCurrentPlain()) return false;
            proof = captured; return true;
        }
        catch (Exception error) when (error is InvalidOperationException or InvalidDataException or ArgumentException or OverflowException)
        { return false; }
    }

    internal static bool TryCaptureOwnedHawksPower(LegacyOrdinarySpellBinding binding, WorldSession current,
        out OwnedHawksPowerProof? proof)
    {
        proof = null;
        if (!TryCaptureOwnedHawksStatus(binding, current, out var status) || status is null) return false;
        proof = new(status); return proof.IsCurrentPlain();
    }

    internal void RetireOwnedHawksAmbition(LegacyOrdinarySpellBinding binding)
    {
        if (!ReferenceEquals(binding.Store, this) || !OwnedHawks.TryGetValue(binding, out var slot)) return;
        Interlocked.Exchange(ref slot.Retired, 1);
        Volatile.Write(ref slot.Lease, null);
    }

    private static bool HawksOwnerIsCurrentPlain(LegacyOrdinarySpellBinding binding, WorldSession current) =>
        IsCurrentThreadInsideAnyConnectionGate && Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) &&
        Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) &&
        binding.Store.IsSpellFileWriteOwnerCurrent(binding.Connection, current, binding.NativeIdentity, binding);

    private static bool HawksLeaseMatches(CharacterSnapshot character, LegacyNativeAttributeMapProof attributes, OwnedHawksLease lease)
    {
        var fields = attributes.Fields;
        var flags = LegacyWindWalkerStatusProjection.Read(character with { Attributes = fields });
        return character.MinHitPoints > 0 && (flags[0] & ((1UL << 5) | (1UL << 10))) == 0 && (flags[8] & 1UL) != 0 &&
            fields.TryGetValue("HawksAmbitionPower", out var power) &&
            uint.TryParse(power, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedPower) && parsedPower == lease.Power &&
            fields.TryGetValue("LegacyHawksAmbitionExpiresAtUtc", out var expiry) &&
            DateTimeOffset.TryParseExact(expiry, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedExpiry) &&
            parsedExpiry == lease.ExpiresAtUtc && fields.TryGetValue("LegacyHawksAmbitionRemoveOnDead", out var dead) && dead == "true";
    }
}
