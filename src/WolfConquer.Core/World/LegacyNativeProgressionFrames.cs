using System.Buffers.Binary;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Prepared equivalents of the existing native 2041/2199 primitives and genuine
// system-message serializer. Nothing here sends or writes a live owner.
internal static class LegacyNativeProgressionFrames
{
    internal static byte[] Proficiency(uint id, uint level, uint experience, string? seal)
    {
        var payload = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, id);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), level);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8), experience);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(12), Threshold(level));
        return LegacyNativePreparedPacketFrames.Frame(2041, payload, seal);
    }

    internal static byte[] Experience(uint id, uint level, uint experience, string? seal)
    {
        var payload = new byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, experience);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), Threshold(level));
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8), id);
        return LegacyNativePreparedPacketFrames.Frame(2199, payload, seal);
    }

    internal static uint Threshold(uint level) =>
        LegacyNativeBaseFunc.ProficiencyLevelExperience(unchecked((byte)level));

    internal static byte[] SystemMessage(string message, string? seal)
    {
        using var recycled = new LegacyNativeRecycledPacket();
        var stream = recycled.GetStream();
        LegacyNativePreparedPacketStorage.Validate(stream, 1032);
        var native = new LegacyNativeMsgMessage(message, LegacyNativeMsgMessage.MsgColor.red,
            LegacyNativeMsgMessage.ChatMode.TopLeftSystem);
        native.GetArray(stream);
        LegacyNativePreparedPacketStorage.Validate(stream, stream.Size);
        if (!string.Equals(seal, LegacyNativePreparedPacketFrames.CaptureSeal(), StringComparison.Ordinal))
            throw new InvalidOperationException("Progression notification seal changed during preparation.");
        var frame = new byte[stream.Size];
        Marshal.Copy(LegacyNativePreparedPacketStorage.Address(stream), frame, 0, frame.Length);
        int payloadSize = BinaryPrimitives.ReadUInt16LittleEndian(frame);
        if (payloadSize != frame.Length - (seal is null ? 0 : 8))
            throw new InvalidDataException("Progression notification framing changed during preparation.");
        if (seal is not null && !frame.AsSpan(payloadSize, 8).SequenceEqual(
                System.Text.Encoding.UTF8.GetBytes(seal).AsSpan(0, 8)))
            throw new InvalidDataException("Progression notification seal is inconsistent.");
        return frame;
    }
}
