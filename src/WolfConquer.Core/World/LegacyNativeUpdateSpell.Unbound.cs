using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeUpdateSpell
{
    private sealed partial class TrainingPlan
    {
        internal void CommitUnbound(bool physical)
        {
            LegacyNativeSpellStorage.PreparedSessionSpellMap? spells = null;
            LegacyNativeProficiencyStorage.PreparedSessionProficiencyMap? profMap = null;
            byte[][] frames; nint preparedAddress;
            lock (client.SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
            {
                LegacyNativeSpellStorage.RequireDirectEntry(client);
                if (LegacyNativeSpellStorage.GetJoinedBindingForClientEntry(client) is not null)
                    throw new InvalidOperationException("Training owner was admitted during unbound preparation.");
                lock (storage?.MutationGate ?? player.SpellAdmissionSyncRoot)
                lock (profs?.MutationGate ?? player.SpellAdmissionSyncRoot)
                {
                    if (!HasBinding() || !HasMetadata() || LegacyNativeServerConfig.ExpRateSpell != spellRate) return;
                    selected?.RequireCurrent();
                    uint rate = LegacyNativeServerConfig.ExpRateProf;
                    var seal = LegacyNativePreparedPacketFrames.CaptureSeal();
                    var currentProfs = profs?.SnapshotCurrentRows();
                    var projection = currentProfs is null ? null : new LegacyNativeProficiencyStorage.ProgressProjection(
                        client, currentProfs, seal);
                    var output = new List<byte[]>();
                    if (HasSpell && projection is not null) ApplyWeaponProgression(projection, rate);
                    if (projection is not null) output.AddRange(projection.Frames);
                    int priorFrames = output.Count;
                    if (storage is not null)
                    {
                        var current = storage.CaptureSessionSnapshot(client, player);
                        var proposed = current;
                        if (HasProgress)
                        {
                            var value = selected!.Value;
                            var intent = new LegacyNativeSpellMutationIntent.SetProgress(spellId, value.Experience, value.Level);
                            proposed = new(current.Spells.Select(row => row.Id == intent.Id ? row with
                                { Experience = intent.Experience, Level = intent.Level } : row).ToArray());
                            if (leveled) output.Add(LegacyNativeProgressionFrames.SystemMessage(
                                "You increased the spell level!", seal));
                            output.Add(frame!);
                        }
                        spells = LegacyNativeSpellStorage.PrepareSessionSnapshot(client, player, current, proposed);
                    }
                    if (physical && projection is not null)
                    {
                        ApplyWeaponProgression(projection, rate);
                        output.AddRange(projection.Frames.Skip(priorFrames));
                    }
                    if (projection is not null)
                        profMap = LegacyNativeProficiencyStorage.PrepareSessionSnapshot(client, player,
                            currentProfs!, projection.FinalSnapshot);
                    frames = output.ToArray(); preparedAddress = ValidateTrainingFrames(frames);
                    spells?.ArmFinalProof(); profMap?.ArmFinalProof();
                    ValidateTrainingFrames(frames, preparedAddress);
                    if (!HasBinding() || !HasMetadata() || LegacyNativeServerConfig.ExpRateSpell != spellRate ||
                        LegacyNativeServerConfig.ExpRateProf != rate || projection is not null && !projection.InputsAreCurrentPlain() ||
                        spells is not null && !spells.ConsumeBeforeWriterPlain() || profMap is not null && !profMap.ConsumeBeforeWriterPlain() ||
                        spells is not null && !spells.ConsumeCurrentPlain() || profMap is not null && !profMap.ConsumeCurrentPlain()) return;
                    spells?.PublishPreparedMap(); profMap?.PublishPreparedMap();
                }
            }
            foreach (var output in frames)
            {
                lock (client.SpellAdmissionSyncRoot)
                lock (player.SpellAdmissionSyncRoot)
                lock (storage?.MutationGate ?? player.SpellAdmissionSyncRoot)
                lock (profs?.MutationGate ?? player.SpellAdmissionSyncRoot)
                    if (!HasBinding() || spells is not null && !spells.HasCurrentCommittedGraph() ||
                        profMap is not null && !profMap.HasCurrentCommittedGraph()) return;
                ValidateTrainingFrames([output], preparedAddress);
                LegacyNativePreparedPacketStorage.Write(stream, output);
                client.Send(stream);
            }
        }
    }
}
