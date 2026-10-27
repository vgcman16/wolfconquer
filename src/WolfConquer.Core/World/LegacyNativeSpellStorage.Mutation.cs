using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeSpellStorage
{
    private enum SpellMutationKind { Add, Remove, Reborn, Clear }
    private readonly record struct SpellNotice(LegacyNativeMsgSpell? Added, ushort Removed);

    private bool ExecuteTypedMutation(LegacyNativePacket stream, SpellMutationKind kind, ushort id,
        LegacyNativeMsgSpell? row, HashSet<ushort>? retained)
    {
        if (Monitor.IsEntered(mutationGate)) throw UnavailableNinjaSpells();
        var client = Owner ?? throw UnavailableNinjaSpells();
        RefuseTypedMutationEntry(client);
        var player = client.Player ?? throw UnavailableNinjaSpells();
        PreparedSpellMutation prepared;
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        {
            RefuseTypedMutationEntry(client);
            lock (mutationGate) prepared = new(this, client, player, kind, id, row, retained);
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

    private void RefuseTypedMutationEntry(LegacyNativeGameClientRuntime client)
    {
        if (Monitor.IsEntered(mutationGate) || LegacyNativeNinja.HasActiveOwnedPreparation(client))
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
