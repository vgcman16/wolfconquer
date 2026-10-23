using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using ProtoBuf;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// One real, bounded 2353 rental prepared before any slot mutation; no accepting transport callback.
internal sealed class LegacyNativeHundredWeaponsSlotPacket : IDisposable
{
    private readonly LegacyNativeRecycledPacket rental;
    private readonly LegacyNativePacket packet;

    private LegacyNativeHundredWeaponsSlotPacket(LegacyNativeRecycledPacket ownedRental, LegacyNativePacket stream)
    { rental = ownedRental; packet = stream; }

    internal static LegacyNativeHundredWeaponsSlotPacket? TryPrepare(
        LegacyNativeMsgHundredWeaponsOpt.MsgHundredWeaponsOptProto message)
    {
        try { return TryRentFrame(PrepareFrame(message)); }
        catch (Exception error)
        {
            Report("preparation", error.Message);
            return null;
        }
    }

    // Build every byte before acquiring a native rental. The global seal is read
    // ONCE; neither capacity checks nor materialization reread mutable SealSize.
    private static byte[] PrepareFrame(LegacyNativeMsgHundredWeaponsOpt.MsgHundredWeaponsOptProto message)
    {
        ArgumentNullException.ThrowIfNull(message);
        string? seal = LegacyNativePacket.SealString;
        if (seal is not null && seal.Length != 8)
            throw new InvalidDataException("HundredWeapons packet seal is invalid.");
        using var serialized = new MemoryStream();
        Serializer.Serialize(serialized, message);
        byte[] payload = serialized.ToArray();
        int sealBytes = seal is null ? 0 : 8;
        long required = 4L + payload.Length + sealBytes;
        if (required > 1032)
            throw new InvalidDataException("HundredWeapons response exceeds the native Send bound.");
        var frame = new byte[(int)required];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)(4 + payload.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2353);
        payload.CopyTo(frame, 4);
        if (seal is not null)
        {
            // Native Write(string,8) emits the first eight UTF-8 BYTES of an
            // eight-character seal, even if UTF-8 encodes to more than eight.
            Encoding.UTF8.GetBytes(seal).AsSpan(0, 8).CopyTo(frame.AsSpan(4 + payload.Length));
        }
        return frame;
    }

    private static unsafe LegacyNativeHundredWeaponsSlotPacket? TryRentFrame(byte[] frame)
    {
        LegacyNativeRecycledPacket? rental = null;
        try
        {
            if (frame.Length < 4 || frame.Length > 1032)
                throw new InvalidDataException("HundredWeapons prepared frame length is invalid.");
            rental = new LegacyNativeRecycledPacket();
            var stream = rental.GetStream();
            if (stream is null || stream.Memory == null || stream.Capacity < frame.Length)
                throw new InvalidDataException("HundredWeapons response storage is missing or undersized.");
            Marshal.Copy(frame, 0, (IntPtr)stream.Memory, frame.Length);
            stream.Size = frame.Length;
            // Native Finalize leaves its cursor after the two UInt16 header writes.
            // Do not call it: it would reread the mutable global seal and append.
            stream.Seek(4);
            return new(rental, stream);
        }
        catch (Exception error)
        {
            DisposeRental(rental);
            Report("preparation", error.Message);
            return null;
        }
    }

    internal void Publish(LegacyNativeGameClientRuntime owner)
    {
        try { owner.Send(packet); }
        catch (Exception error) { Report("publication after commit", error.Message); }
    }

    public void Dispose() => DisposeRental(rental);

    private static void DisposeRental(LegacyNativeRecycledPacket? rental)
    {
        try { rental?.Dispose(); }
        catch (Exception error) { Report("packet cleanup", error.Message); }
    }

    private static void Report(string phase, string reason)
    {
        try { Console.WriteLine($"HundredWeapons {phase}: {reason}"); }
        catch { /* Diagnostics cannot change the committed outcome. */ }
    }
}
