using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgSpell
{
    // Prepared response/current-row publication only; no host dispatch adoption.
    public static void HandlerSpell(LegacyNativeGameClientRuntime client, LegacyNativePacket stream)
    {
        ArgumentNullException.ThrowIfNull(client);
        LegacyNativeSpellStorage.RequireDirectEntry(client);
        var request = ReadSoulRequest(stream);
        var joined = LegacyNativeSpellStorage.GetJoinedBindingForClientEntry(client);
        if (joined is not null) { HandleJoinedSoul(joined, stream, request); return; }
        var incoming = request.Value;
        LegacyNativePlayerRuntime player;
        LegacyNativeSpellStorage storage;
        LegacyNativeSpellStorage.DirectSpellSnapshot.DirectSpellChange change;
        byte[] frame;
        nint address;
        LegacyNativeSecuritySocket? socket;
        bool fake;
        lock (client.SpellAdmissionSyncRoot)
        {
            player = client.Player ?? throw new InvalidOperationException("Soul request requires the physical player.");
            lock (player.SpellAdmissionSyncRoot)
            {
                LegacyNativeSpellStorage.RequireDirectEntry(client);
                if (LegacyNativeSpellStorage.GetJoinedBindingForClientEntry(client) is not null)
                    throw new InvalidOperationException("Soul owner was admitted during unbound preparation.");
                storage = client.MySpells ?? throw new InvalidOperationException("Soul request requires the actual spell owner.");
                socket = client.Socket; fake = client.Fake;
                lock (storage.MutationGate)
                {
                    var selected = storage.CaptureDirectSnapshot(client, player).Find(incoming.ID);
                    if (selected is null || selected.Value.SoulLevel < incoming.UseSpellSoul)
                    { request.RequireCurrent(stream); stream.Seek(request.End); return; }
                    change = selected;
                }
                incoming.Type = 3;
                frame = LegacyNativeSpellPacket.PrepareSpellFrame(incoming, LegacyNativePreparedPacketFrames.CaptureSeal());
                LegacyNativePreparedPacketStorage.Validate(stream, frame.Length);
                address = LegacyNativePreparedPacketStorage.Address(stream);
                change.PrepareSoul(incoming.UseSoul);
                lock (storage.MutationGate)
                {
                    LegacyNativePreparedPacketStorage.Validate(stream, frame.Length);
                    if (LegacyNativePreparedPacketStorage.Address(stream) != address) throw new InvalidOperationException("Soul response storage changed.");
                    request.RequireCurrent(stream);
                    if (!ReferenceEquals(client.Socket, socket) || client.Fake != fake) throw new InvalidOperationException("Soul response owner changed.");
                    change.CommitSoul();
                }
            }
        }
        PublishSoulFrame(client, player, storage, change, stream, frame, address, socket, fake);
    }
}
