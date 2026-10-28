using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeProficiencyStorage
{
    public void SendAll(LegacyNativePacket stream)
    {
        RequireEntry();
        var binding = JoinedBindingForEntry();
        if (binding is not null) { SendAllJoined(binding, stream); return; }
        var client = Owner; var player = client.Player ?? throw UnavailableProficiencies();
        var socket = client.Socket; bool fake = client.Fake;
        PreparedSessionProficiencyMap map; byte[][] frames; nint address;
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        {
            RequireEntry();
            if (JoinedBindingForEntry() is not null || !IsOwnedBy(client)) throw UnavailableProficiencies();
            lock (mutationGate)
            {
                var current = SnapshotCurrentRows();
                map = PrepareSessionSnapshot(client, player, current, current);
                frames = PrepareAllFrames(current);
                address = RequireNotificationStorage(stream, frames);
                map.ArmFinalProof();
                if (!map.ConsumeBeforeWriterPlain() || !map.ConsumeCurrentPlain()) throw UnavailableProficiencies();
            }
        }
        PublishNativeFrames(client, player, map, frames, stream, address, socket, fake);
    }

    private static byte[][] PrepareAllFrames(WolfConquer.Core.Data.PlayerProficiencySnapshot snapshot)
    {
        var seal = LegacyNativePreparedPacketFrames.CaptureSeal();
        return snapshot.Proficiencies.Select(row =>
            LegacyNativeProgressionFrames.Proficiency(row.Id, row.Level, row.Experience, seal)).ToArray();
    }

    private void SendAllJoined(LegacyOrdinarySpellBinding binding, LegacyNativePacket stream)
    {
        var lease = binding.EnterMutation(); byte[][] frames;
        try
        {
            var client = binding.Client; var player = binding.Player;
            lock (client.SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
            lock (binding.Storage.MutationGate)
            lock (mutationGate)
            {
                var spells = LegacyNativeSpellStorage.PrepareSessionSnapshot(client, player,
                    binding.Snapshot, binding.Snapshot);
                var profs = PrepareSessionSnapshot(client, player, lease.ExpectedProficiencies, lease.ExpectedProficiencies);
                frames = PrepareAllFrames(SnapshotCurrentRows());
                nint address = RequireNotificationStorage(stream, frames);
                spells.ArmFinalProof(); profs.ArmFinalProof();
                lease.PreparePublication(spells.FinalSnapshot, profs.FinalSnapshot);
                RequireNotificationStorage(stream, frames, address);
                if (!spells.ConsumeBeforeWriterPlain() || !profs.ConsumeBeforeWriterPlain() ||
                    !spells.ConsumeCurrentPlain() || !profs.ConsumeCurrentPlain() || !lease.IsCurrentPlain())
                    throw UnavailableProficiencies();
            }
        }
        finally { lease.Dispose(); }
        LegacyOrdinarySpellNotificationPublisher.Publish(binding, frames);
    }
}
