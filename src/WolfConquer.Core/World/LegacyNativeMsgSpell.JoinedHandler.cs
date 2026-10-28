using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgSpell
{
    private static void HandleJoinedSoul(LegacyOrdinarySpellBinding binding,
        LegacyNativePacket stream, SoulInput request)
    {
        var lease = binding.EnterMutation();
        byte[]? frame = null;
        try
        {
            var client = binding.Client;
            var player = binding.Player;
            var storage = binding.Storage;
            lock (client.SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
            lock (storage.MutationGate)
            lock (binding.Proficiencies.MutationGate)
            {
                if (!binding.PhysicalOwnerIsCurrentPlain()) throw new InvalidOperationException("Soul owner changed.");
                var incoming = request.Value;
                var intent = new LegacyNativeSpellMutationIntent.SetSoul(incoming.ID,
                    LegacyNativeSpellStorage.PreparedSessionSpellMap.SelectedSoul(incoming.UseSoul));
                var selectedSoul = intent.SelectedSoul;
                var current = binding.Snapshot;
                var selected = current.Spells.SingleOrDefault(row => row.Id == incoming.ID);
                if (selected is null || selected.SoulLevel < selectedSoul)
                { request.RequireCurrent(stream); stream.Seek(request.End); return; }
                var proposedRows = current.Spells.Select(row => row.Id == incoming.ID
                    ? row with { UseSpellSoul = selectedSoul }
                    : row).ToArray();
                var plan = LegacyNativeSpellStorage.PrepareSessionSnapshot(client, player,
                    current, new PlayerSpellSnapshot(proposedRows));
                incoming.Type = 3;
                frame = LegacyNativeSpellPacket.PrepareSpellFrame(incoming,
                    LegacyNativePreparedPacketFrames.CaptureSeal());
                LegacyNativePreparedPacketStorage.Validate(stream, frame.Length);
                request.RequireCurrent(stream);
                var profs = LegacyNativeProficiencyStorage.PrepareSessionSnapshot(client, player,
                    lease.ExpectedProficiencies, lease.ExpectedProficiencies);
                plan.ArmFinalProof();
                profs.ArmFinalProof();
                lease.PreparePublication(plan.FinalSnapshot, profs.FinalSnapshot);
                request.RequireCurrent(stream);
                if (!plan.ConsumeBeforeWriterPlain() || !profs.ConsumeBeforeWriterPlain() ||
                    !plan.ConsumeCurrentPlain() || !profs.ConsumeCurrentPlain() || !lease.IsCurrentPlain())
                    throw new InvalidOperationException("Soul publication owner changed.");
                plan.PublishPreparedMap();
                profs.PublishPreparedMap();
                lease.PublishPrepared();
            }
        }
        finally { lease.Dispose(); }
        if (frame is not null) LegacyOrdinarySpellNotificationPublisher.Publish(binding, [frame]);
    }
}
