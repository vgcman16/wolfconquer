using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using ProtoBuf;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Genuine MsgTalkProto for the SYSTEM/ALL red Talk branch, prepared before
// potion commitment. This does not substitute a sender or acknowledge delivery.
internal sealed class LegacyNativePreparedPotionMessage : IDisposable
{
    private LegacyNativeRecycledPacket? rental;
    private byte[] frame = [];
    private IntPtr address;

    internal static LegacyNativePreparedPotionMessage Create(bool defense, int minutes,
        LegacyNativePreparedFlagPublication? status)
    {
        string attribute = defense ? "defense" : "attack";
        string duration = minutes.ToString(CultureInfo.InvariantCulture);
        var proto = new LegacyNativeMsgMessage.MsgTalkProto
        {
            Color = (uint)LegacyNativeMsgMessage.MsgColor.red,
            ChatType = (uint)LegacyNativeMsgMessage.ChatMode.Talk,
            Count = 1,
            Content = ["SYSTEM", "ALL", "", $"Your {attribute} will increase during the next {duration} minutes.", "", "", ""],
        };
        using var payload = new MemoryStream();
        Serializer.Serialize(payload, proto);
        byte[] bytes = payload.ToArray();
        string? seal = LegacyNativePacket.SealString;
        if (seal is not null && seal.Length != 8) throw new InvalidDataException("Invalid native potion packet seal.");
        int sealSize = seal is null ? 0 : 8;
        if (4L + bytes.Length + sealSize > 1032)
            throw new InvalidDataException("Potion message exceeds the native send bound.");
        var frame = new byte[4 + bytes.Length + sealSize];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)(bytes.Length + 4));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2179);
        Buffer.BlockCopy(bytes, 0, frame, 4, bytes.Length);
        if (seal is not null) Buffer.BlockCopy(Encoding.UTF8.GetBytes(seal), 0, frame, 4 + bytes.Length, 8);
        var prepared = new LegacyNativePreparedPotionMessage();
        try
        {
            prepared.rental = new LegacyNativeRecycledPacket();
            var packet = prepared.rental.GetStream();
            if (status?.ContainsPacket(packet) == true)
                throw new InvalidDataException("Potion status and message rentals cannot alias.");
            Copy(packet, frame);
            prepared.frame = frame;
            prepared.address = Address(packet);
            return prepared;
        }
        catch { prepared.Dispose(); throw; }
    }

    private static unsafe void Copy(LegacyNativePacket packet, byte[] frame)
    {
        if (packet is null || packet.Memory == null || packet.Capacity < frame.Length)
            throw new InvalidDataException("Potion message has no live bounded packet storage.");
        Marshal.Copy(frame, 0, (IntPtr)packet.Memory, frame.Length);
        packet.Size = frame.Length;
        packet.Seek(4);
    }

    internal void Publish(LegacyNativeGameClientRuntime client)
    {
        if (!HasPreparedStorage()) return;
        try { client.Send(rental!.GetStream()); }
        catch (Exception error) { LegacyNativePlayerRuntime.ReportFlagAdmission("potion publication after commit", error); }
    }

    private static unsafe IntPtr Address(LegacyNativePacket packet) => (IntPtr)packet.Memory;

    internal unsafe bool HasPreparedStorage()
    {
        var packet = rental?.GetStream();
        return packet is not null && packet.Memory != null && Address(packet) == address &&
            packet.Capacity >= frame.Length && packet.Size == frame.Length && packet.Position == 4 &&
            new ReadOnlySpan<byte>(packet.Memory, frame.Length).SequenceEqual(frame);
    }

    public void Dispose()
    {
        var owned = rental;
        rental = null;
        if (owned is null) return;
        try { owned.Dispose(); }
        catch (Exception error) { LegacyNativePlayerRuntime.ReportFlagAdmission("potion packet cleanup", error); }
    }
}
