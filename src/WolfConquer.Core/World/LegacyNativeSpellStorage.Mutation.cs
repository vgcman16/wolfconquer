using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeSpellStorage
{
    private enum SpellMutationKind { Add, Remove, Reborn, Clear }
    private readonly record struct SpellNotice(LegacyNativeMsgSpell? Added, ushort Removed);

    private bool ExecuteTypedMutation(LegacyNativePacket stream, LegacyNativeSpellMutationIntent intent)
    {
        if (Monitor.IsEntered(mutationGate)) throw UnavailableNinjaSpells();
        var binding = GetJoinedBindingForEntry();
        if (binding is not null) return ExecuteJoinedTypedMutation(binding, stream, intent);
        var client = Owner ?? throw UnavailableNinjaSpells();
        RefuseTypedMutationEntry(client);
        var player = client.Player ?? throw UnavailableNinjaSpells();
        PreparedSpellMutation prepared;
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        {
            RefuseTypedMutationEntry(client);
            if (GetJoinedBindingForEntry() is not null) throw UnavailableNinjaSpells();
            lock (mutationGate) prepared = new(this, client, player, intent);
            // Serializers and native storage checks are outside the spell gate.
            prepared.PrepareFrames(stream);
            lock (mutationGate)
            {
                prepared.RequireCurrent();
                prepared.RequireStorage(stream);
                prepared.PublishMap();
            }
        }
        // All gates acquired by this operation have released before publication.
        prepared.Publish(stream);
        return prepared.RemovedExisting;
    }

    private bool ExecuteJoinedTypedMutation(LegacyOrdinarySpellBinding binding, LegacyNativePacket stream,
        LegacyNativeSpellMutationIntent intent)
    {
        var lease = binding.EnterMutation(); // Store/package/connection gates precede all actor gates.
        PreparedSpellMutation? prepared = null;
        try
        {
            var client = binding.Client;
            var player = binding.Player;
            lock (client.SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
            {
                if (!ReferenceEquals(client.MySpells, this) || !ReferenceEquals(binding.Storage, this))
                    throw UnavailableNinjaSpells();
                lock (mutationGate) prepared = new(this, client, player, intent);
                prepared.PrepareFrames(stream);
                lock (mutationGate)
                lock (binding.Proficiencies.MutationGate)
                {
                    prepared.RequireCurrent();
                    prepared.RequireStorage(stream);
                    var profs = LegacyNativeProficiencyStorage.PrepareSessionSnapshot(client, player,
                        lease.ExpectedProficiencies, lease.ExpectedProficiencies);
                    prepared.ArmFinalProof();
                    profs.ArmFinalProof();
                    lease.PreparePublication(prepared.FinalSnapshot, profs.FinalSnapshot);
                    if (!prepared.ConsumeBeforeWriterPlain() || !profs.ConsumeBeforeWriterPlain() ||
                        !prepared.ConsumeCurrentPlain() || !profs.ConsumeCurrentPlain() ||
                        !lease.IsCurrentPlain()) throw UnavailableNinjaSpells();
                    prepared.PublishMap();
                    profs.PublishPreparedMap();
                    lease.PublishPrepared();
                }
            }
        }
        finally { lease.Dispose(); }
        prepared!.PublishJoined(binding.Connection);
        return prepared.RemovedExisting;
    }

    private void RefuseTypedMutationEntry(LegacyNativeGameClientRuntime client)
    {
        if (Monitor.IsEntered(mutationGate) || LegacyNativeNinja.HasActiveOwnedPreparation(client) ||
            client.MyProfs is { } profs && Monitor.IsEntered(profs.MutationGate) ||
            client.OrdinarySpellBinding is { } pin && Monitor.IsEntered(pin.Proficiencies.MutationGate))
            throw UnavailableNinjaSpells();
    }

    private static LegacyNativeMsgSpell CreateTypedSpell(ushort id, ushort level, byte soul,
        byte previous, int experience) => new()
        { ID = id, Level = level, Experience = experience, SoulLevel = soul, PreviousLevel = previous };

    private void RequireTypedMutationOwner(LegacyNativeGameClientRuntime client,
        LegacyNativePlayerRuntime player, uint uid)
    {
        if (uid == 0 || !IsOwnedBy(client) || !ReferenceEquals(client.Player, player) ||
            !ReferenceEquals(player.Owner, client) || player.UID != uid ||
            !Monitor.IsEntered(client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(player.SpellAdmissionSyncRoot) ||
            !Monitor.IsEntered(mutationGate)) throw UnavailableNinjaSpells();
    }
}
