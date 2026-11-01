using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed partial class DragonhowlLevelExperienceData
    {
        // Closed receive projection DATA. Actual native/file participants are
        // prepared only after the real award has published its successor.
        internal void ValidateReceivePhase(WorldSession before, WorldSession after)
        {
            if (!IsCurrentPlain() || projectedCharacterImage is null ||
                before.AccountId != binding.AccountId || after.AccountId != binding.AccountId ||
                before.Character.Uid != binding.Uid || after.Character.Uid != binding.Uid ||
                !ReferenceEquals(before.NativeGameClient, binding.Client) ||
                !ReferenceEquals(after.NativeGameClient, binding.Client) ||
                (after with { Character = before.Character, PhysicalStatusOverlay = before.PhysicalStatusOverlay,
                    MagicCooldowns = before.MagicCooldowns, CanAttackFlashingRegistration = before.CanAttackFlashingRegistration }) != before ||
                LegacyNativeAttributeState.From(before.Character) != LegacyNativeAttributeState.From(after.Character) ||
                before.Character.Level != after.Character.Level || before.Character.Experience != after.Character.Experience ||
                before.Character.Class != after.Character.Class || before.Character.Reborn != after.Character.Reborn ||
                !BoundHitpointsInputs.PaidEqual(before.Character, after.Character)) throw Invalid();
            _ = LegacyNativeActorScalarState.RequireCurrent(before.Character);
            _ = LegacyNativeActorScalarState.RequireCurrent(after.Character);
            _ = LegacyActionState.RequireCurrent(before.Character);
            _ = LegacyActionState.RequireCurrent(after.Character);
            if (LegacyExperienceContinuationCodec.ReadCharacter(before.Character, before.AccountId) is not null ||
                LegacyExperienceContinuationCodec.ReadCharacter(after.Character, after.AccountId) is not null) throw Invalid();
            var canonical = new BoundHitpointsInputs(before.Character, after.Character).After;
            var bytes = LegacyBoundHitpointsScalarWrite.ProjectAfterImage(projectedCharacterImage, canonical);
            if (bytes.Length > LegacyTradeJournal.PhysicalDeathMaximumReferenceBytes ||
                !BoundHitpointsInputs.ScalarsEqual(CharacterSnapshotParser.ParseText(
                    CharacterSnapshotFileEncoding.Decode(bytes, out _)), canonical)) throw Invalid();
            // Keep the exact future file sequence for a subsequent actual
            // receive/death projection. This is never a live file permit.
            projectedCharacterImage = bytes;
        }
    }
}
