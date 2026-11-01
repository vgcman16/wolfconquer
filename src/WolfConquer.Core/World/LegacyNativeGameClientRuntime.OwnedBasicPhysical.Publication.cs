using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    internal sealed class OwnedBasicPhysicalPublication
    {
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly WorldSessionStore.BasicPhysicalNativeChange change;
        private readonly BasicPhysicalNativeSnapshot before;
        private readonly BasicSnapshotData after;
        private readonly BasicPhysicalNativeKind kind;
        private readonly bool retirement;
        private readonly LegacyNativePlayerRuntime.BasicPhysicalStampPublication? stamp;
        internal BasicPhysicalNativeSnapshot After { get; }

        private OwnedBasicPhysicalPublication(LegacyOrdinarySpellBinding binding, WorldSessionStore.BasicPhysicalNativeChange change,
            BasicPhysicalNativeSnapshot before, BasicSnapshotData after, BasicPhysicalNativeKind kind, bool retirement,
            LegacyNativePlayerRuntime.BasicPhysicalStampPublication? stamp)
        {
            this.binding = binding; this.change = change; this.before = before; this.after = after;
            this.kind = kind; this.retirement = retirement; this.stamp = stamp;
            After = RegisterBasicSnapshot(after);
        }

        internal static OwnedBasicPhysicalPublication Prepare(LegacyOrdinarySpellBinding binding,
            WorldSessionStore.BasicPhysicalNativeChange change)
        {
            ArgumentNullException.ThrowIfNull(binding);
            ArgumentNullException.ThrowIfNull(change);
            if (!change.TryReadPreparedProposal(binding, out var before, out var kind, out var request, out var at) ||
                before is null || !BasicSnapshots.TryGetValue(before, out var old) || !old.Committed ||
                !ReferenceEquals(old.Binding, binding)) throw InvalidBasicNative();
            bool retirement = change.IsRetirementCancellationPlain(binding);
            if (retirement ? !before.IsCurrentForRetirementPlain(binding) : !change.IsCurrentPlain(binding) || !before.IsCurrentPlain(binding))
                throw InvalidBasicNative();
            if (retirement && (kind != BasicPhysicalNativeKind.CancelRepeat || request is not null || at is not null || !old.On))
                throw InvalidBasicNative();
            bool on = old.On; var descriptor = old.Descriptor; long bits = old.StampBits;
            LegacyNativePlayerRuntime.BasicPhysicalStampPublication? stamp = null;
            switch (kind)
            {
                case BasicPhysicalNativeKind.CommittedHit when request is null && at is { } value && !retirement:
                    on = false; // Accepted native attack prefix; never dirty rejected entry state.
                    bits = Unsafe.BitCast<DateTime, long>(value);
                    stamp = LegacyNativePlayerRuntime.BasicPhysicalStampPublication.Prepare(binding, change, before);
                    break;
                case BasicPhysicalNativeKind.ArmRepeat when request is { } query && at is null && !retirement:
                    descriptor = new LegacyNativeInteractQuery
                    {
                        AtkType = checked((ushort)query.AttackType), Damage = query.Damage, Effect = (uint)query.Effect,
                        OpponentUID = query.OpponentUid, ResponseDamage = query.ResponseDamage, SpellID = query.SpellId,
                        SpellLevel = query.SpellLevel, UID = query.Uid, X = query.X, Y = query.Y,
                    };
                    on = true;
                    break;
                case BasicPhysicalNativeKind.CancelRepeat when request is null && at is null:
                    on = false;
                    break;
                default: throw InvalidBasicNative();
            }
            var prepared = new OwnedBasicPhysicalPublication(binding, change, before,
                new(binding, on, descriptor, ReadBasicDescriptor(descriptor), bits, retirement, false), kind, retirement, stamp);
            if (!prepared.IsCurrentPlain()) throw InvalidBasicNative();
            return prepared;
        }

        internal bool IsCurrentPlain() => !after.Committed &&
            (retirement ? change.IsRetirementCancellationPlain(binding) && before.IsCurrentForRetirementPlain(binding) :
                change.IsCurrentPlain(binding) && before.IsCurrentPlain(binding)) &&
            BasicDescriptorCurrent(after) && (stamp is null || stamp.IsCurrentPlain());

        internal bool IsPreparedFor(LegacyOrdinarySpellBinding actualBinding, WorldSessionStore.BasicPhysicalNativeChange actualChange) =>
            ReferenceEquals(binding, actualBinding) && ReferenceEquals(change, actualChange) && IsCurrentPlain();

        // The real Store consumes final typed owner proofs before these stores.
        // No allocation, CWT lookup, callback or unrelated field write follows.
        internal void PublishPrepared()
        {
            switch (kind)
            {
                case BasicPhysicalNativeKind.CommittedHit:
                    Volatile.Write(ref binding.Client.onAutoAttack, false);
                    stamp!.PublishPrepared();
                    break;
                case BasicPhysicalNativeKind.ArmRepeat:
                    Volatile.Write(ref binding.Client.onAutoAttack, true);
                    Volatile.Write(ref binding.Client.autoAttack, after.Descriptor);
                    break;
                case BasicPhysicalNativeKind.CancelRepeat: Volatile.Write(ref binding.Client.onAutoAttack, false); break;
            }
            after.Committed = true;
        }
    }
}

public sealed partial class LegacyNativePlayerRuntime
{
    // Private physical stamp access stays in Player's actual containing type;
    // no detached DateTime/internal numeric setter bypass is introduced.
    internal sealed class BasicPhysicalStampPublication
    {
        private readonly LegacyNativePlayerRuntime player;
        private readonly long before, after;
        private BasicPhysicalStampPublication(LegacyNativePlayerRuntime player, long after)
        { this.player = player; before = Volatile.Read(ref player.attackStampBits); this.after = after; }
        internal static BasicPhysicalStampPublication Prepare(LegacyOrdinarySpellBinding binding,
            WorldSessionStore.BasicPhysicalNativeChange change, LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot expectedBefore)
        {
            if (!change.TryReadPreparedProposal(binding, out var before, out var kind, out var request, out var at) ||
                !ReferenceEquals(before, expectedBefore) || kind != LegacyNativeGameClientRuntime.BasicPhysicalNativeKind.CommittedHit ||
                request is not null || at is null || !change.IsCurrentPlain(binding) || !expectedBefore.IsCurrentPlain(binding))
                throw new InvalidOperationException("ordinaryBasicPhysicalStampOwnerChanged");
            return new(binding.Player, Unsafe.BitCast<DateTime, long>(at.Value));
        }
        internal bool IsCurrentPlain() => Volatile.Read(ref player.attackStampBits) == before;
        internal void PublishPrepared() => Volatile.Write(ref player.attackStampBits, after);
    }
}
