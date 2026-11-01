using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // DATA prepared only inside the real proc's permission-approved player
    // iteration. The Store's registered phase, not this projection, may publish.
    internal sealed class DragonhowlFlashingName
    {
        internal readonly WorldSession After;
        internal readonly byte[] Frame;
        internal readonly LegacyNativeStatusFlagsBigVector32.DragonhowlFlashingPreparation Native;
        private readonly TimeProvider clock;
        private readonly LegacyCanAttackFlashingRegistration registration;

        private DragonhowlFlashingName(WorldSession after, TimeProvider clock,
            LegacyCanAttackFlashingRegistration registration,
            LegacyNativeStatusFlagsBigVector32.DragonhowlFlashingPreparation native)
        {
            After = after; this.clock = clock; this.registration = registration; Native = native;
            Frame = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
                CheckedUInt32(after.Character.Uid, nameof(after.Character.Uid)),
                LegacyPlayerDeathState.CreateStatusFlagVector(after.Character.Attributes,
                    LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength)).ToFrameBytes();
        }

        internal static DragonhowlFlashingName? Prepare(WorldPacketHandlerContext context,
            LegacyOrdinarySpellBinding binding, WorldSession source, WorldSession target)
        {
            if (HasLegacyStatusFlag(source.Character, LegacyFlashingNameStatusFlag) ||
                HasLegacyPlayerMagicInterserverAttribute(source)) return null;
            var pk = ReadAttributeUInt64(source.Character.Attributes, RuntimePkModeAttribute, "PKMode") & byte.MaxValue;
            if (pk is LegacyPkModeCapture or LegacyPkModePeace ||
                !CanApplyLegacyPlayerMagicCanAttackFlashingName(target.Character)) return null;
            var clock = context.TimeProvider ?? throw new InvalidOperationException("ownedDragonhowlClockMissing");
            var registration = LegacyCanAttackFlashingRegistration.Create(source.Character.Uid, clock);
            var character = Project(source.Character);
            var after = registration.Install(source with { Character = character });
            var native = LegacyNativeStatusFlagsBigVector32.DragonhowlFlashingPreparation.Prepare(binding, registration, clock);
            return new(after, clock, registration, native);
        }

        internal bool IsCurrentPlain() => Native.IsCurrentPlain() && registration.IsValidFor(After.Character) &&
            LegacyCanAttackFlashingRegistration.LocalTime(clock, clock.GetUtcNow()) < registration.ExpiresAtLocal;

        private static CharacterSnapshot Project(CharacterSnapshot source)
        {
            var proof = LegacyNativeAttributeMapProof.CaptureRaw(source.Attributes);
            var words = LegacyWindWalkerStatusProjection.Read(source);
            if ((words[0] & 1) != 0) throw new InvalidDataException("ownedDragonhowlFlashingAlreadyPresent");
            words[0] |= 1;
            var attributes = new Dictionary<string, string>(proof.Fields, proof.CapturedComparer);
            var statusKeys = attributes.Keys.Where(key => key.Equals("StatusFlags", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("BitVector", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (statusKeys.Length == 0) statusKeys = ["StatusFlags"];
            foreach (var key in statusKeys) attributes[key] = string.Join('/', words) + "/";
            var policy = LegacyStatusFlagDeathRegistration.Key(0);
            var policyKeys = attributes.Keys.Where(key => key.Equals(policy, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (policyKeys.Length > 1) throw new InvalidDataException("ownedDragonhowlFlashingPolicyAmbiguous");
            attributes[policyKeys.Length == 0 ? policy : policyKeys[0]] = "true";
            if (!proof.IsCurrentPlain()) throw new InvalidOperationException("ownedDragonhowlFlashingInputChanged");
            return source with { Attributes = attributes };
        }
    }
}
