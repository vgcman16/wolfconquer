using System.Buffers.Binary;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgSpell
{
    private sealed class SoulInput
    {
        internal readonly LegacyNativeMsgSpell Value;
        internal readonly int End;
        private readonly int start, size;
        private readonly nint address;
        private readonly byte[] bytes;
        internal SoulInput(LegacyNativeMsgSpell value, int start, int size, nint address, byte[] bytes)
        { Value = value; this.start = start; this.size = size; this.address = address; this.bytes = bytes; End = start + 21; }
        internal unsafe void RequireCurrent(LegacyNativePacket stream)
        {
            if ((nint)stream.Memory != address || stream.Memory == null || stream.Size != size || stream.Position != start ||
                stream.Capacity < End || !new ReadOnlySpan<byte>(stream.Memory + start, 21).SequenceEqual(bytes))
                throw new InvalidOperationException("Soul request storage changed during preparation.");
        }
    }

    private static unsafe SoulInput ReadSoulRequest(LegacyNativePacket stream)
    {
        if (stream is null || stream.Memory == null) throw new InvalidOperationException("Soul request requires live packet storage.");
        int start = stream.Position;
        long required = (long)start + 21;
        if (start < 0 || required > stream.Capacity || required > stream.Size || stream.Size > stream.Capacity)
            throw new InvalidDataException("Soul request does not contain a complete fixed spell payload.");
        var bytes = new ReadOnlySpan<byte>(stream.Memory + start, 21).ToArray();
        ReadOnlySpan<byte> payload = bytes;
        var value = new LegacyNativeMsgSpell
        {
            Experience = BinaryPrimitives.ReadInt32LittleEndian(payload), ID = BinaryPrimitives.ReadUInt16LittleEndian(payload[4..]),
            UnKnow = BinaryPrimitives.ReadUInt16LittleEndian(payload[6..]), Level = BinaryPrimitives.ReadUInt16LittleEndian(payload[8..]),
            Type = BinaryPrimitives.ReadUInt16LittleEndian(payload[10..]), UseSoul = (UseSpellSoulTyp)BinaryPrimitives.ReadUInt32LittleEndian(payload[12..]),
            Soul = payload[16], HashLevelSoul = BinaryPrimitives.ReadInt32LittleEndian(payload[17..]),
        };
        return new(value, start, stream.Size, (nint)stream.Memory, bytes);
    }

    private static void PublishSoulFrame(LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player,
        LegacyNativeSpellStorage storage, LegacyNativeSpellStorage.DirectSpellSnapshot.DirectSpellChange change,
        LegacyNativePacket stream, byte[] frame, nint address, LegacyNativeSecuritySocket? socket, bool fake)
    {
        try { LegacyNativeSpellStorage.RequireDirectEntry(client); }
        catch (InvalidOperationException) { return; }
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        {
            try { LegacyNativeSpellStorage.RequireDirectEntry(client); }
            catch (InvalidOperationException) { return; }
            lock (storage.MutationGate)
                if (!change.MatchesAfter() || !ReferenceEquals(client.Socket, socket) || client.Fake != fake) return;
        }
        // No serialization/send is inside the spell gate. Packet lifetime is
        // retained by the caller; arbitrary raw disposal is not made atomic here.
        try
        {
            if (LegacyNativePreparedPacketStorage.Address(stream) != address) return;
            LegacyNativePreparedPacketStorage.Write(stream, frame);
            client.Send(stream);
        }
        catch (InvalidOperationException) { /* Committed soul change; notification unavailable. */ }
    }
}
