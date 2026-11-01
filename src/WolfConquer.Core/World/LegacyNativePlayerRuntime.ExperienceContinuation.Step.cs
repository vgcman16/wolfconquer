namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Exact, continuation-only native level/stat projection. An unregistered
    // step permit or an uncommitted DATA plan cannot publish this image.
    internal sealed class ExperienceStepNativePublication
    {
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly WorldSession before, after;
        private readonly WorldSessionStore.ExperienceContinuationExecution.StepPlan step;
        private readonly byte[] beforeImage;
        private readonly LegacyNativeActorScalarState.Values oldActor;
        private readonly LegacyNativeAttributeState oldAttributes, newAttributes;
        private readonly ushort oldLevel, newLevel;
        private readonly ulong oldExperience, newExperience;

        private ExperienceStepNativePublication(LegacyOrdinarySpellBinding binding, WorldSession before,
            WorldSession after, WorldSessionStore.ExperienceContinuationExecution.StepPlan step, byte[] beforeImage,
            LegacyNativeAttributeState newAttributes)
        {
            this.binding = binding; this.before = before; this.after = after; this.step = step;
            this.beforeImage = beforeImage.ToArray(); this.newAttributes = newAttributes;
            oldActor = binding.Player.ReadActorScalarsPlain(); oldAttributes = binding.Player.ReadAttributeStatePlain();
            oldLevel = binding.Player._level; oldExperience = binding.Player.experience;
            newLevel = checked((ushort)after.Character.Level); newExperience = checked((ulong)after.Character.Experience);
        }

        internal static ExperienceStepNativePublication Prepare(LegacyOrdinarySpellBinding binding, WorldSession before,
            WorldSessionStore.ExperienceContinuationExecution.StepPlan step, byte[] beforeImage)
        {
            var after = step.Proposed;
            var oldActor = new LegacyNativeActorScalarState.NativeValueProof(before.Character);
            var newActor = new LegacyNativeActorScalarState.NativeValueProof(after.Character);
            var oldAttributes = LegacyNativeAttributeState.From(before.Character);
            var newAttributes = LegacyNativeAttributeState.From(after.Character);
            if (!step.Covers(binding, before, after, beforeImage) || !oldActor.IsCurrentPlain() || !newActor.IsCurrentPlain() ||
                oldActor.Value != binding.Player.ReadActorScalarsPlain() || oldAttributes != binding.Player.ReadAttributeStatePlain() ||
                newActor.Value != (oldActor.Value with { Agility = newAttributes.Agility }) ||
                before.Character.Level != binding.Player._level || before.Character.Experience < 0 ||
                (ulong)before.Character.Experience != binding.Player.experience || after.Character.Level is < 1 or > 140 ||
                after.Character.Experience < 0 || after.Character.MinHitPoints != before.Character.MinHitPoints ||
                after.Character.MinMana != before.Character.MinMana || after.Character.MaxMana != before.Character.MaxMana)
                throw new InvalidOperationException("experienceStepNativeBaselineInvalid");
            return new(binding, before, after, step, beforeImage, newAttributes);
        }

        internal bool IsCurrentPlain() => step.Covers(binding, before, after, beforeImage) &&
            binding.Player.ReadActorScalarsPlain() == oldActor && binding.Player.ReadAttributeStatePlain() == oldAttributes &&
            binding.Player._level == oldLevel && binding.Player.experience == oldExperience;

        internal void PublishCommitted(WorldSessionStore.ExperienceStepCommitPermit permit)
        {
            if (!IsCurrentPlain() || !permit.Consume(step)) throw new InvalidOperationException("experienceStepCommitPermitInvalid");
            var player = binding.Player;
            player._level = newLevel; player.experience = newExperience;
            player._strength = newAttributes.Strength; player._agility = newAttributes.Agility;
            player._vitality = newAttributes.Vitality; player._spirit = newAttributes.Spirit;
            player._atributes = newAttributes.Atributes;
        }
    }
}
