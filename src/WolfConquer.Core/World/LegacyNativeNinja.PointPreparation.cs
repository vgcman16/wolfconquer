using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    private void ExecutePreparedPointCredit(uint points)
    {
        var client = user ?? throw InvalidPointCreditOwner();
        RefuseNinjaPublicEntry(client);
        LegacyNativeRecycledPacket? rental = null;
        LegacyNativePacket stream = null!;
        try
        {
            lock (client.SpellAdmissionSyncRoot)
            {
                var player = client.Player ?? throw InvalidPointCreditOwner();
                lock (player.SpellAdmissionSyncRoot)
                {
                    RefuseNinjaPublicEntry(client);
                    lock (persistenceLoadGate)
                    {
                        uint uid = player.UID;
                        RequirePointCreditActor(client, player, uid);
                        var original = ReadPersistenceState();
                        uint updated = checked(original.Exp + points);
                        var prepared = original with { Exp = updated };
                        var frame = LegacyNativeMsgNinja.PrepareNinjaUserFrame(
                            new() { Type = 2, Points = updated }, LegacyNativePreparedPacketFrames.CaptureSeal());
                        try { rental = new(); }
                        catch (NullReferenceException error)
                        { throw new InvalidOperationException("Ninja point notification requires real packet storage.", error); }
                        stream = rental.GetStream();
                        LegacyNativePreparedPacketStorage.Validate(stream, frame.Length);
                        RequirePointCreditActor(client, player, uid);
                        if (!ReferenceEquals(ReadPersistenceState(), original)) throw InvalidPointCreditOwner();
                        // The sum and replacement were prepared under the same
                        // actual gate. Only packet copy and one state store remain.
                        LegacyNativePreparedPacketStorage.Write(stream, frame);
                        Volatile.Write(ref persistenceState, prepared);
                    }
                }
            }
            client.Send(stream); // Postcommit notification outside all held gates.
        }
        finally { rental?.Dispose(); }
    }

    private void RequirePointCreditActor(LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player, uint uid)
    {
        if (uid == 0 || !IsCombatOwnedBy(client) || !ReferenceEquals(client.Player, player) ||
            !ReferenceEquals(player.Owner, client) || player.UID != uid ||
            !Monitor.IsEntered(client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(player.SpellAdmissionSyncRoot) ||
            !Monitor.IsEntered(persistenceLoadGate)) throw InvalidPointCreditOwner();
    }

    private static InvalidOperationException InvalidPointCreditOwner() =>
        new("Ninja point credit requires its current physical actor and prepared state.");
}
