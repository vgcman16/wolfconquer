using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeProficiencyStorage
{
    private LegacyOrdinarySpellBinding? JoinedBindingForEntry()
    {
        var binding = LegacyNativeSpellStorage.GetJoinedBindingForClientEntry(Owner);
        if (binding is not null && (!ReferenceEquals(binding.Proficiencies, this) ||
            !ReferenceEquals(Owner.MyProfs, this))) throw UnavailableProficiencies();
        return binding;
    }

    private void RequireEntry()
    {
        if (Monitor.IsEntered(mutationGate) || LegacyNativeNinja.HasActiveOwnedPreparation(Owner) ||
            Owner.MySpells is { } storage && Monitor.IsEntered(storage.MutationGate) ||
            Owner.OrdinarySpellBinding is { } pin && (Monitor.IsEntered(pin.Storage.MutationGate) ||
                Monitor.IsEntered(pin.Proficiencies.MutationGate)))
            throw UnavailableProficiencies();
    }

    internal PlayerProficiencySnapshot SnapshotCurrentRows()
    {
        if (Owner.Player is not { } player || player.UID == 0 || !IsOwnedBy(Owner) ||
            !ReferenceEquals(player.Owner, Owner) || !Monitor.IsEntered(Owner.SpellAdmissionSyncRoot) ||
            !Monitor.IsEntered(player.SpellAdmissionSyncRoot) || !Monitor.IsEntered(mutationGate))
            throw UnavailableProficiencies();
        var rows = CaptureRows(ClientProf);
        return new(new CanonicalRows(rows.Select(row => new PlayerProficiencySnapshotEntry(row.Value.Id,
            row.Value.Level, row.Value.Previous, row.Value.Experience, row.Value.Needed)).ToArray()));
    }

    private void ExecuteProgression(LegacyNativePacket stream, ProgressInstruction instruction)
    {
        RequireEntry();
        var binding = JoinedBindingForEntry();
        if (binding is not null) { ExecuteJoinedProgression(binding, stream, instruction); return; }
        var client = Owner;
        var player = client.Player ?? throw UnavailableProficiencies();
        PreparedSessionProficiencyMap map;
        byte[][] frames;
        nint address;
        var socket = client.Socket; bool fake = client.Fake;
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        {
            RequireEntry();
            if (JoinedBindingForEntry() is not null || !IsOwnedBy(client)) throw UnavailableProficiencies();
            lock (mutationGate)
            {
                var current = SnapshotCurrentRows();
                var projection = new ProgressProjection(client, current, LegacyNativePreparedPacketFrames.CaptureSeal());
                projection.Apply(instruction);
                map = PrepareSessionSnapshot(client, player, current, projection.FinalSnapshot);
                frames = projection.Frames;
                address = RequireNotificationStorage(stream, frames);
                map.ArmFinalProof();
                if (!projection.InputsAreCurrentPlain() || !map.ConsumeBeforeWriterPlain() ||
                    !map.ConsumeCurrentPlain() || !ReferenceEquals(client.Socket, socket) || client.Fake != fake)
                    throw UnavailableProficiencies();
                RequireNotificationStorage(stream, frames, address);
                map.PublishPreparedMap();
            }
        }
        PublishNativeFrames(client, player, map, frames, stream, address, socket, fake);
    }

    private void ExecuteJoinedProgression(LegacyOrdinarySpellBinding binding, LegacyNativePacket stream,
        ProgressInstruction instruction)
    {
        var lease = binding.EnterMutation();
        byte[][] frames;
        try
        {
            var client = binding.Client; var player = binding.Player;
            lock (client.SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
            lock (binding.Storage.MutationGate)
            lock (mutationGate)
            {
                if (!binding.PhysicalOwnerIsCurrentPlain()) throw UnavailableProficiencies();
                var projection = new ProgressProjection(client, lease.ExpectedProficiencies,
                    LegacyNativePreparedPacketFrames.CaptureSeal());
                projection.Apply(instruction);
                var spells = LegacyNativeSpellStorage.PrepareSessionSnapshot(client, player,
                    binding.Snapshot, binding.Snapshot);
                var profs = PrepareSessionSnapshot(client, player, lease.ExpectedProficiencies, projection.FinalSnapshot);
                frames = projection.Frames;
                nint address = RequireNotificationStorage(stream, frames);
                spells.ArmFinalProof(); profs.ArmFinalProof();
                lease.PreparePublication(spells.FinalSnapshot, profs.FinalSnapshot);
                RequireNotificationStorage(stream, frames, address);
                if (!projection.InputsAreCurrentPlain() || !spells.ConsumeBeforeWriterPlain() ||
                    !profs.ConsumeBeforeWriterPlain() || !spells.ConsumeCurrentPlain() ||
                    !profs.ConsumeCurrentPlain() || !lease.IsCurrentPlain()) throw UnavailableProficiencies();
                spells.PublishPreparedMap(); profs.PublishPreparedMap(); lease.PublishPrepared();
            }
        }
        finally { lease.Dispose(); }
        LegacyOrdinarySpellNotificationPublisher.Publish(binding, frames);
    }

    private static nint RequireNotificationStorage(LegacyNativePacket stream, byte[][] frames, nint expected = 0)
    {
        if (frames.Length == 0) return 0;
        LegacyNativePreparedPacketStorage.Validate(stream, frames.Max(frame => frame.Length));
        nint address = LegacyNativePreparedPacketStorage.Address(stream);
        if (expected != 0 && address != expected) throw UnavailableProficiencies();
        return address;
    }

    private void PublishNativeFrames(LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player,
        PreparedSessionProficiencyMap map, byte[][] frames, LegacyNativePacket stream, nint address,
        LegacyNativeSecuritySocket? socket, bool fake)
    {
        foreach (var frame in frames)
        {
            lock (client.SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
            lock (mutationGate)
                if (!map.HasCurrentCommittedGraph() || !ReferenceEquals(client.Socket, socket) || client.Fake != fake) return;
            RequireNotificationStorage(stream, [frame], address);
            LegacyNativePreparedPacketStorage.Write(stream, frame);
            client.Send(stream);
        }
    }
}
