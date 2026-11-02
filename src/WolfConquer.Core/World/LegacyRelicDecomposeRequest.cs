using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

// Immutable input DATA. Parsing does not issue an operation or reserve an item.
internal sealed class LegacyRelicDecomposeRequest
{
    internal const int MaximumSelectedCount = 49;
    private readonly byte[] originalFrame, echoFrame;
    private readonly uint[] selected;

    private LegacyRelicDecomposeRequest(MsgItemUsagePacket packet, byte[] original, uint[] selected)
    {
        Packet = packet; originalFrame = original; this.selected = selected;
        echoFrame = WorldPacketHandlers.CreateLegacyItemUsageFrameWithArgs(packet, selected);
    }

    internal MsgItemUsagePacket Packet { get; }
    internal ReadOnlySpan<uint> SelectedUids => selected;
    internal byte[] CopyOriginalFrame() => originalFrame.ToArray();
    internal byte[] CopyEchoFrame() => echoFrame.ToArray();

    internal static bool TryParse(MsgItemUsagePacket packet, PacketFrame frame,
        out LegacyRelicDecomposeRequest? request, out string reason)
    {
        request = null; reason = "relicDecomposeFrameInvalid";
        if (frame.Header.Opcode != MsgItemUsagePacket.Opcode ||
            frame.Frame.Length is < PacketHeader.Size or > ushort.MaxValue ||
            frame.Payload.Length != frame.Frame.Length - PacketHeader.Size) return false;
        var original = frame.Frame.ToArray();
        if (!PacketHeader.TryRead(original, out var header) || header != frame.Header || header.Length != original.Length ||
            !original.AsSpan(PacketHeader.Size).SequenceEqual(frame.Payload.ToArray())) return false;
        var owned = new PacketFrame(header, new ReadOnlySequence<byte>(original),
            new ReadOnlySequence<byte>(original.AsMemory(PacketHeader.Size)));
        if (!MsgItemUsagePacket.TryParse(owned, out var actual) || actual != packet ||
            actual.Action != MsgItemUsageAction.DecomposeRelics)
        { reason = "relicDecomposePacketMismatch"; return false; }
        if (packet.DwParam2 is 0 or > MaximumSelectedCount)
        { reason = "relicDecomposeCountInvalid"; return false; }
        var payload = original.AsSpan(PacketHeader.Size);
        var bytes = payload.Length - MsgItemUsagePacket.PayloadLength;
        if (bytes < 0 || bytes % sizeof(uint) != 0 || bytes / sizeof(uint) < packet.DwParam2)
        { reason = "relicDecomposeBodyInvalid"; return false; }
        var selected = new uint[checked((int)packet.DwParam2)];
        HashSet<uint> seen = [];
        for (var index = 0; index < selected.Length; index++)
        {
            var uid = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(MsgItemUsagePacket.PayloadLength + index * sizeof(uint), sizeof(uint)));
            if (uid == 0 || !seen.Add(uid))
            { reason = "relicDecomposeDistinctInputsRequired"; return false; }
            selected[index] = uid;
        }
        // Complete surplus words are harmless source input, not additional
        // selections or echo arguments. Partial words were refused above.
        request = new(packet, original, selected); reason = string.Empty; return true;
    }
}
