using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    private void ExecutePreparedFlagChange(ulong flag, bool remove)
    {
        // Keep genuinely unused owners/packet storage outside no-op branches.
        if (!NeedsFlagNotification(ReadPersistenceState().Flag, flag, remove)) return;
        var client = user ?? throw InvalidFlagOwner();
        RefuseNinjaPublicEntry(client);
        LegacyNativeRecycledPacket? rental = null;
        LegacyNativePacket stream = null!;
        try
        {
            lock (client.SpellAdmissionSyncRoot)
            {
                var player = client.Player ?? throw InvalidFlagOwner();
                lock (player.SpellAdmissionSyncRoot)
                {
                    RefuseNinjaPublicEntry(client);
                    lock (persistenceLoadGate)
                    {
                        uint uid = player.UID;
                        RequireFlagActor(client, player, uid);
                        var original = ReadPersistenceState();
                        if (!NeedsFlagNotification(original.Flag, flag, remove)) return;
                        ulong updated = remove ? original.Flag & ~flag : original.Flag | flag;
                        var prepared = original with { Flag = updated };
                        var frame = LegacyNativeMsgNinja.PrepareNinjaUserFrame(
                            new() { Type = 1, Flag = updated }, LegacyNativePreparedPacketFrames.CaptureSeal());
                        try { rental = new(); }
                        catch (NullReferenceException error)
                        { throw new InvalidOperationException("Ninja flag notification requires real packet storage.", error); }
                        stream = rental.GetStream();
                        LegacyNativePreparedPacketStorage.Validate(stream, frame.Length);
                        RequireFlagActor(client, player, uid);
                        if (!ReferenceEquals(ReadPersistenceState(), original)) throw InvalidFlagOwner();
                        // No callback, lookup or state allocation after the copy:
                        // the packet loan is retained and the next operation is a store.
                        LegacyNativePreparedPacketStorage.Write(stream, frame);
                        Volatile.Write(ref persistenceState, prepared);
                    }
                }
            }
            client.Send(stream); // Postcommit notification, outside all held gates.
        }
        finally { rental?.Dispose(); }
    }

    private static bool NeedsFlagNotification(ulong current, ulong requested, bool remove) =>
        remove ? (current & requested) != 0 : (current & requested) == 0;

    private void RequireFlagActor(LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player, uint uid)
    {
        if (uid == 0 || !IsCombatOwnedBy(client) || !ReferenceEquals(client.Player, player) ||
            !ReferenceEquals(player.Owner, client) || player.UID != uid ||
            !Monitor.IsEntered(client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(player.SpellAdmissionSyncRoot) ||
            !Monitor.IsEntered(persistenceLoadGate)) throw InvalidFlagOwner();
    }

    private static InvalidOperationException InvalidFlagOwner() =>
        new("Ninja flag mutation requires its current physical actor and prepared state.");
}
