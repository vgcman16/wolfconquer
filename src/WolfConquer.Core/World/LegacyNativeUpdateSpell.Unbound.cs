namespace WolfConquer.Core.World;

internal partial class LegacyNativeUpdateSpell
{
    private sealed partial class TrainingPlan
    {
        internal void CommitUnbound(bool physical)
        {
            LegacyNativeProficiencyStorage.PreparedUnboundTraining? profMap = null;
            // No weapon pass consumes the optional proficiency slot. Do not
            // inspect or lock an absent/foreign owner for unrelated spell XP.
            var usedProfs = (HasSpell || physical) && (right != 0 || left != 0) ? profs : null;
            byte[][] frames; nint preparedAddress;
            lock (client.SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
            {
                LegacyNativeSpellStorage.RequireDirectEntry(client);
                if (LegacyNativeSpellStorage.GetJoinedBindingForClientEntry(client) is not null)
                    throw new InvalidOperationException("Training owner was admitted during unbound preparation.");
                lock (storage?.MutationGate ?? player.SpellAdmissionSyncRoot)
                lock (usedProfs?.MutationGate ?? player.SpellAdmissionSyncRoot)
                {
                    if (!HasBinding() || !HasMetadata() || LegacyNativeServerConfig.ExpRateSpell != spellRate) return;
                    selected?.RequireCurrent();
                    selected?.ArmUnboundProgressProof();
                    uint rate = LegacyNativeServerConfig.ExpRateProf;
                    var seal = LegacyNativePreparedPacketFrames.CaptureSeal();
                    var currentProfs = usedProfs?.SnapshotCurrentRows();
                    var projection = currentProfs is null ? null : new LegacyNativeProficiencyStorage.ProgressProjection(
                        client, currentProfs, seal);
                    var output = new List<byte[]>();
                    if (HasSpell && projection is not null) ApplyWeaponProgression(projection, rate);
                    if (projection is not null) output.AddRange(projection.Frames);
                    int priorFrames = output.Count;
                    if (HasProgress)
                    {
                        if (leveled) output.Add(LegacyNativeProgressionFrames.SystemMessage(
                            "You increased the spell level!", seal));
                        output.Add(frame!);
                    }
                    if (physical && projection is not null)
                    {
                        ApplyWeaponProgression(projection, rate);
                        output.AddRange(projection.Frames.Skip(priorFrames));
                    }
                    if (projection is not null)
                        profMap = new(usedProfs!, client, player,
                            currentProfs!, projection.FinalSnapshot);
                    frames = output.ToArray(); preparedAddress = ValidateTrainingFrames(frames);
                    ValidateTrainingFrames(frames, preparedAddress);
                    if (!HasBinding() || !HasMetadata() || LegacyNativeServerConfig.ExpRateSpell != spellRate ||
                        LegacyNativeServerConfig.ExpRateProf != rate || projection is not null && !projection.InputsAreCurrentPlain() ||
                        selected is not null && !selected.ConsumeUnboundProgressProof() ||
                        profMap is not null && !profMap.ConsumeCurrentPlain()) return;
                    if (HasProgress) selected!.PublishPreparedUnboundProgress();
                    profMap?.PublishPrepared();
                }
            }
            foreach (var output in frames)
            {
                lock (client.SpellAdmissionSyncRoot)
                lock (player.SpellAdmissionSyncRoot)
                lock (storage?.MutationGate ?? player.SpellAdmissionSyncRoot)
                lock (usedProfs?.MutationGate ?? player.SpellAdmissionSyncRoot)
                    if (!HasBinding() || selected is not null && !selected.MatchesAfter() ||
                        profMap is not null && !profMap.HasCurrentCommittedGraph()) return;
                ValidateTrainingFrames([output], preparedAddress);
                LegacyNativePreparedPacketStorage.Write(stream, output);
                client.Send(stream);
            }
        }
    }
}
