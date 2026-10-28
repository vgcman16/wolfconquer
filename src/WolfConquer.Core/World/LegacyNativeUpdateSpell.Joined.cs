using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeUpdateSpell
{
    private static void CheckUpdateJoined(LegacyNativePacket stream, LegacyNativeGameClientRuntime client,
        LegacyNativeInteractQuery attack, uint damage, Dictionary<ushort, LegacyNativeMagicType.Magic>? metadata,
        bool physical, LegacyOrdinarySpellBinding binding)
    {
        var lease = binding.EnterMutation();
        byte[][]? notifications = null;
        try
        {
            var plan = TrainingPlan.TryPrepare(stream, client, attack, damage, metadata, physical);
            if (plan is null) return;
            notifications = plan.CommitJoined(binding, lease, physical);
        }
        finally { lease.Dispose(); }
        if (notifications is not null) LegacyOrdinarySpellNotificationPublisher.Publish(binding, notifications);
    }

    private sealed partial class TrainingPlan
    {
        internal byte[][]? CommitJoined(LegacyOrdinarySpellBinding binding,
            WorldSessionStore.NativeSpellMutationLease lease, bool physical)
        {
            lock (client.SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
            lock (binding.Storage.MutationGate)
            lock (binding.Proficiencies.MutationGate)
            {
                if (!binding.PhysicalOwnerIsCurrentPlain() || !HasBinding() || !HasMetadata() ||
                    LegacyNativeServerConfig.ExpRateSpell != spellRate) return null;
                selected?.RequireCurrent();
                uint proficiencyRate = LegacyNativeServerConfig.ExpRateProf;
                var seal = LegacyNativePreparedPacketFrames.CaptureSeal();
                var projection = new LegacyNativeProficiencyStorage.ProgressProjection(client,
                    lease.ExpectedProficiencies, seal);
                var frames = new List<byte[]>();
                // Preserve the source's two distinct passes: spell-associated
                // proficiency first, spell notice, then physical proficiency.
                if (HasSpell) ApplyWeaponProgression(projection, proficiencyRate);
                frames.AddRange(projection.Frames);
                int priorFrames = frames.Count;
                var proposed = binding.Snapshot;
                if (HasProgress)
                {
                    var value = selected!.Value;
                    var intent = new LegacyNativeSpellMutationIntent.SetProgress(spellId, value.Experience, value.Level);
                    if (!proposed.Spells.Any(entry => entry.Id == intent.Id)) return null;
                    proposed = new(proposed.Spells.Select(entry => entry.Id == intent.Id
                        ? entry with { Experience = intent.Experience, Level = intent.Level } : entry).ToArray());
                    if (leveled) frames.Add(LegacyNativeProgressionFrames.SystemMessage(
                        "You increased the spell level!", seal));
                    frames.Add(frame!);
                }
                if (physical)
                {
                    ApplyWeaponProgression(projection, proficiencyRate);
                    frames.AddRange(projection.Frames.Skip(priorFrames));
                }
                var spells = LegacyNativeSpellStorage.PrepareSessionSnapshot(client, player,
                    binding.Snapshot, proposed);
                var profMap = LegacyNativeProficiencyStorage.PrepareSessionSnapshot(client, player,
                    lease.ExpectedProficiencies, projection.FinalSnapshot);
                var preparedFrames = frames.ToArray();
                nint preparedAddress = ValidateTrainingFrames(preparedFrames);
                spells.ArmFinalProof(); profMap.ArmFinalProof();
                lease.PreparePublication(spells.FinalSnapshot, profMap.FinalSnapshot);
                ValidateTrainingFrames(preparedFrames, preparedAddress);
                if (!HasBinding() || !HasMetadata() || LegacyNativeServerConfig.ExpRateSpell != spellRate ||
                    LegacyNativeServerConfig.ExpRateProf != proficiencyRate || !projection.InputsAreCurrentPlain() ||
                    !spells.ConsumeBeforeWriterPlain() || !profMap.ConsumeBeforeWriterPlain() ||
                    !spells.ConsumeCurrentPlain() || !profMap.ConsumeCurrentPlain() || !lease.IsCurrentPlain()) return null;
                spells.PublishPreparedMap(); profMap.PublishPreparedMap(); lease.PublishPrepared();
                return preparedFrames;
            }
        }

        private void ApplyWeaponProgression(LegacyNativeProficiencyStorage.ProgressProjection projection, uint rate)
        {
            ulong experience = (ulong)damage * rate;
            if (right != 0) projection.Apply(new(right, experience, Add: false));
            if (left != 0) projection.Apply(new(left, experience, Add: false));
        }

        private nint ValidateTrainingFrames(byte[][] frames, nint expected = 0)
        {
            if (frames.Length == 0) return 0;
            LegacyNativePreparedPacketStorage.Validate(stream, frames.Max(value => value.Length));
            nint actual = LegacyNativePreparedPacketStorage.Address(stream);
            if (expected != 0 && actual != expected) throw new InvalidOperationException("Training packet storage changed.");
            return actual;
        }
    }
}
