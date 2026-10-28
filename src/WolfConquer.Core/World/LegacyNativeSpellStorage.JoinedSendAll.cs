using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeSpellStorage
{
    private void SendAllJoined(LegacyOrdinarySpellBinding binding, LegacyNativePacket stream)
    {
        var lease = binding.EnterMutation();
        byte[][] frames;
        try
        {
            var client = binding.Client; var player = binding.Player;
            lock (client.SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
            lock (mutationGate)
            lock (binding.Proficiencies.MutationGate)
            {
                var spells = PrepareSessionSnapshot(client, player, binding.Snapshot, binding.Snapshot);
                var profs = LegacyNativeProficiencyStorage.PrepareSessionSnapshot(client, player,
                    lease.ExpectedProficiencies, lease.ExpectedProficiencies);
                var seal = LegacyNativePreparedPacketFrames.CaptureSeal();
                frames = CaptureNinjaSpellRows(ClientSpells).Select(row =>
                    LegacyNativeSpellPacket.PrepareSpellFrame(row.Row, seal)).ToArray();
                nint address = 0;
                if (frames.Length != 0)
                {
                    LegacyNativePreparedPacketStorage.Validate(stream, frames.Max(frame => frame.Length));
                    address = LegacyNativePreparedPacketStorage.Address(stream);
                }
                spells.ArmFinalProof(); profs.ArmFinalProof();
                lease.PreparePublication(spells.FinalSnapshot, profs.FinalSnapshot);
                if (frames.Length != 0 && LegacyNativePreparedPacketStorage.Address(stream) != address ||
                    !spells.ConsumeBeforeWriterPlain() || !profs.ConsumeBeforeWriterPlain() ||
                    !spells.ConsumeCurrentPlain() || !profs.ConsumeCurrentPlain() || !lease.IsCurrentPlain())
                    throw UnavailableNinjaSpells();
                // SendAll only observes committed state: no map/holder store.
            }
        }
        finally { lease.Dispose(); }
        LegacyOrdinarySpellNotificationPublisher.Publish(binding, frames);
    }
}
