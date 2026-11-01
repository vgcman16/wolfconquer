using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed partial class OwnedPhysicalDeathPreparation
    {
        private readonly OwnedDeathNativeStatus nativeStatus;

        internal bool TryReadNativeWindWalkerProposal(LegacyOrdinarySpellBinding binding, WorldSession canonicalTarget,
            out LegacyNativeStatusFlagsBigVector32 vector,
            out LegacyNativeStatusFlagsBigVector32.WindWalkerSnapshot before,
            out LegacyNativeStatusFlagsBigVector32.WindWalkerChange? change)
        {
            vector = nativeStatus.Vector; before = nativeStatus.Before; change = nativeStatus.Change;
            return IsCurrentPlain(binding) && CanonicalTargetMatches(canonicalTarget);
        }

        private sealed class OwnedDeathNativeStatus
        {
            private readonly LegacyOrdinarySpellBinding binding;
            private readonly ulong flag6;
            internal LegacyNativeStatusFlagsBigVector32 Vector { get; }
            internal LegacyNativeStatusFlagsBigVector32.WindWalkerSnapshot Before { get; }
            internal LegacyNativeStatusFlagsBigVector32.WindWalkerChange? Change { get; private set; }

            internal OwnedDeathNativeStatus(LegacyOrdinarySpellBinding binding)
            {
                this.binding = binding;
                Vector = binding.Player.BitVector;
                if (!Monitor.IsEntered(Vector.SyncRoot) || !Vector.TryCaptureWindWalker(out var before) || before is null ||
                    before.Present && before.RemoveOnDeath) throw InvalidOwnedPhysicalDeath();
                // The real194 producer installs RemoveOnDead=false. A changed
                // raw policy is not a new accepted native registration owner.
                Before = before;
                flag6 = LegacyNativePlayerFlag6Lifecycle.CaptureVector(binding.Player,
                    new ulong[LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength])[0] & LegacyNativePlayerFlag6Lifecycle.Mask;
                if (!IsCurrentPlain()) throw InvalidOwnedPhysicalDeath();
            }

            internal bool IsCurrentPlain() => ReferenceEquals(binding.Player.BitVector, Vector) &&
                Monitor.IsEntered(Vector.SyncRoot) && Vector.IsWindWalkerCurrentPlain(Before) &&
                (Change is null || Vector.IsOwnedPhysicalDeathWindWalkerPreparationCurrent(Change));

            internal CharacterSnapshot PrepareQuery(CharacterSnapshot character, bool desired)
            {
                if (Change is not null || !IsCurrentPlain() || !Vector.TryPrepareWindWalkerQuery(desired, out var change) ||
                    change is null) throw InvalidOwnedPhysicalDeath();
                Change = change;
                var projected = LegacyWindWalkerStatusProjection.Apply(character, change.Proposed);
                if (!IsCurrentPlain()) throw InvalidOwnedPhysicalDeath();
                return projected;
            }

            internal CharacterSnapshot ApplyFinal(CharacterSnapshot character) =>
                LegacyWindWalkerStatusProjection.Apply(character, Change?.Proposed ?? Before);

            internal ulong[] CaptureVector(CharacterSnapshot character)
            {
                var words = LegacyPlayerDeathState.CreateStatusFlagVector(character.Attributes,
                    LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength);
                words[0] = words[0] & ~LegacyNativePlayerFlag6Lifecycle.Mask | flag6;
                var physical = Change?.Proposed ?? Before;
                int word = LegacyWindWalkerStatusProjection.Word;
                ulong mask = LegacyWindWalkerStatusProjection.Mask;
                words[word] = physical.Present ? words[word] | mask : words[word] & ~mask;
                return words;
            }
        }

        private sealed partial class OwnedPhysicalDeathBuilder
        {
            private readonly OwnedDeathNativeStatus nativeStatus;

            private ulong[] CaptureCalculatedDeathVector(WorldSession phase) => nativeStatus.CaptureVector(phase.Character);

            private void PrepareWindWalkerPostlude()
            {
                bool desired = magic.RightWeaponItemId is >= 626_003 and <= 626_439 &&
                    magic.LeftWeaponItemId is >= 626_003 and <= 626_439;
                var projected = nativeStatus.PrepareQuery(current.Character, desired);
                bool visibleChange = nativeStatus.Change!.Changed ||
                    (LegacyWindWalkerStatusProjection.Read(current.Character)[LegacyWindWalkerStatusProjection.Word] &
                        LegacyWindWalkerStatusProjection.Mask) !=
                    (LegacyWindWalkerStatusProjection.Read(projected)[LegacyWindWalkerStatusProjection.Word] &
                        LegacyWindWalkerStatusProjection.Mask);
                current = current with { Character = projected };
                if (visibleChange) AppendVector();
            }
        }
    }
}
