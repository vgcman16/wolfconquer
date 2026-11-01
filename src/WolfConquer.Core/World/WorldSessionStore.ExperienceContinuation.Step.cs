using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed partial class ExperienceContinuationExecution
    {
        // A step plan is DATA prepared from this actual execution while the
        // original owner/file gates are held. It has no commit or query power.
        // Activation still requires a continuation-specific native publication
        // and real level-query preflight, neither of which is supplied here.
        internal sealed class StepPlan
        {
            private readonly ExperienceContinuationExecution execution;
            private readonly LegacyCombatExperienceMetadataWitness metadata;
            private readonly LegacyOwnedPhysicalMetadataWitness physical;
            private readonly byte[] before;
            internal WorldSession Proposed { get; }
            internal LegacyExperienceContinuation Next { get; }
            internal LegacyClassLevelStatProgressionChange Progression { get; }
            internal byte[] CopyBeforeImage() => before.ToArray();

            private StepPlan(ExperienceContinuationExecution execution, LegacyCombatExperienceMetadataWitness metadata,
                LegacyOwnedPhysicalMetadataWitness physical, byte[] before, WorldSession proposed,
                LegacyExperienceContinuation next, LegacyClassLevelStatProgressionChange progression)
            {
                this.execution = execution; this.metadata = metadata; this.physical = physical; this.before = before;
                Proposed = proposed; Next = next; Progression = progression;
            }

            internal bool IsHeldCurrentPlain(byte[] currentImage) => currentImage.AsSpan().SequenceEqual(before) &&
                execution.IsHeldCurrentPlain(currentImage) && metadata.IsCurrentPlain() && physical.IsCurrentPlain();

            internal bool Covers(LegacyOrdinarySpellBinding binding, WorldSession expected, WorldSession proposed,
                byte[] currentImage) => ReferenceEquals(execution.binding, binding) &&
                ReferenceEquals(execution.current, expected) && ReferenceEquals(Proposed, proposed) &&
                IsHeldCurrentPlain(currentImage);

            internal byte[] ProjectCharacterImage()
            {
                var scalars = LegacyBoundHitpointsScalarWrite.ProjectAfterImage(before, Proposed.Character);
                return LegacyExperienceContinuationCodec.ProjectCharacterImage(before, scalars,
                    execution.current.AccountId, checked((uint)execution.current.Character.Uid), execution.pending, Next);
            }

            internal static bool TryPrepare(ExperienceContinuationExecution execution, byte[] currentImage,
                out StepPlan? plan)
            {
                plan = null;
                try
                {
                    if (!execution.IsHeldCurrentPlain(currentImage) || execution.pending.Phase != LegacyExperienceContinuationPhase.Ready ||
                        execution.current.Character.Level >= 140 || execution.context.WorldData is not { } catalog) return false;
                    var metadata = new LegacyCombatExperienceMetadataWitness(catalog);
                    var physical = LegacyOwnedPhysicalMetadataWitness.CaptureCombatExperience(execution.binding, catalog, execution.current);
                    if (!catalog.LevelExperienceByTypeAndLevel.TryGetValue((0, checked((int)execution.current.Character.Level)), out var threshold) ||
                        threshold.Experience <= 0 || execution.pending.RemainingExperience < checked((ulong)threshold.Experience)) return false;
                    var next = execution.pending.AdvanceLevel(checked((ulong)threshold.Experience));
                    var leveled = execution.current.Character with
                    { Level = next.CurrentLevel, Experience = checked((long)next.RemainingExperience) };
                    var character = LegacyClassLevelStatProgression.Apply(leveled, catalog, execution.current.Character.Level, out var progression);
                    if (execution.current.Character.Reborn == 0 && character.Level <=
                            (character.Class is >= 13002 and <= 13057 ? 110 : 120) && !progression.AppliedSourceStats) return false;
                    character = LegacyExperienceContinuationCodec.WithState(character, execution.current.AccountId, next);
                    character = new WorldPacketHandlers.BoundHitpointsInputs(execution.current.Character, character).After;
                    if (character.MinHitPoints != execution.current.Character.MinHitPoints ||
                        character.MinMana != execution.current.Character.MinMana || character.MaxMana != execution.current.Character.MaxMana) return false;
                    var proposed = execution.current with { Character = character };
                    physical.Extend(proposed);
                    var prepared = new StepPlan(execution, metadata, physical, currentImage.ToArray(), proposed, next, progression);
                    if (!prepared.IsHeldCurrentPlain(currentImage)) return false;
                    plan = prepared; return true;
                }
                catch (Exception error) when (error is InvalidOperationException or InvalidDataException or
                    ArgumentException or OverflowException or NotSupportedException)
                { return false; }
            }
        }
    }
}
