using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed class LegacyNativePreparedFlagPublication : IDisposable
{
    private LegacyNativeRecycledPacket? status;
    private LegacyNativeRecycledPacket? timed;
    private byte[] statusFrame = [];
    private byte[]? timedFrame;
    private IntPtr statusAddress, timedAddress;

    internal static LegacyNativePreparedFlagPublication Create(uint uid, uint[] words,
        LegacyNativeMsgUpdate.Flags flag, int seconds, uint shown, uint amount)
    {
        var publication = new LegacyNativePreparedFlagPublication();
        try
        {
            string? seal = LegacyNativePacket.SealString;
            byte[] statusFrame = Frame(2176, LegacyNativeMsgUpdate.PrepareStatusFlagPayload(uid, words), seal);
            byte[]? timedFrame = flag >= LegacyNativeMsgUpdate.Flags.Accelerated && flag <= LegacyNativeMsgUpdate.Flags.Confused
                ? Frame(2215, TimedPayload(uid, flag, seconds, shown, amount), seal) : null;
            publication.statusFrame = statusFrame;
            publication.timedFrame = timedFrame;
            publication.status = new();
            var statusPacket = publication.status.GetStream();
            ValidateStorage(statusPacket, statusFrame.Length);
            if (timedFrame is not null)
            {
                publication.timed = new();
                var timedPacket = publication.timed.GetStream();
                if (ReferenceEquals(statusPacket, timedPacket))
                    throw new InvalidDataException("Flag update rentals cannot alias one packet.");
                ValidateStorage(timedPacket, timedFrame.Length);
                CopyFrame(timedPacket, timedFrame);
                publication.timedAddress = Address(timedPacket);
            }
            CopyFrame(statusPacket, statusFrame);
            publication.statusAddress = Address(statusPacket);
            return publication;
        }
        catch { publication.Dispose(); throw; }
    }

    private static byte[] Frame(ushort opcode, byte[] payload, string? seal)
    {
        if (seal is not null && seal.Length != 8) throw new InvalidDataException("Invalid native flag packet seal.");
        int sealSize = seal is null ? 0 : 8;
        if (4L + payload.Length + sealSize > 1032) throw new InvalidDataException("Flag update exceeds native send bound.");
        var bytes = new byte[4 + payload.Length + sealSize];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)(payload.Length + 4));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), opcode);
        Buffer.BlockCopy(payload, 0, bytes, 4, payload.Length);
        if (seal is not null) Buffer.BlockCopy(Encoding.UTF8.GetBytes(seal), 0, bytes, 4 + payload.Length, 8);
        return bytes;
    }

    private static byte[] TimedPayload(uint uid, LegacyNativeMsgUpdate.Flags flag, int seconds, uint shown, uint amount)
    {
        // Entire genuine MsgBuilder GameUpdateCreate1134..1155: seven UInt32s,
        // active256, unsigned duration bits, and the Decelerated inversion only.
        var bytes = new byte[28];
        uint[] fields = [uid, 1, (uint)flag, 256, shown, unchecked((uint)seconds),
            flag == LegacyNativeMsgUpdate.Flags.Deceleration ? uint.MaxValue - amount : amount];
        for (int index = 0; index < fields.Length; index++)
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), fields[index]);
        return bytes;
    }

    private static unsafe void ValidateStorage(LegacyNativePacket packet, int required)
    {
        if (packet is null || packet.Memory == null || packet.Capacity < required)
            throw new InvalidDataException("Flag packet has no live bounded storage.");
    }

    private static unsafe void CopyFrame(LegacyNativePacket packet, byte[] frame)
    {
        Marshal.Copy(frame, 0, (IntPtr)packet.Memory, frame.Length);
        packet.Size = frame.Length;
        packet.Seek(4);
    }

    internal bool ContainsPacket(LegacyNativePacket packet) =>
        ReferenceEquals(status?.GetStream(), packet) || ReferenceEquals(timed?.GetStream(), packet);

    internal bool HasPreparedStorage() => Matches(status?.GetStream(), statusFrame, statusAddress) &&
        (timedFrame is null || Matches(timed?.GetStream(), timedFrame, timedAddress));

    private static unsafe IntPtr Address(LegacyNativePacket packet) => (IntPtr)packet.Memory;

    private static unsafe bool Matches(LegacyNativePacket? packet, byte[] frame, IntPtr address) =>
        packet is not null && packet.Memory != null && Address(packet) == address &&
        packet.Capacity >= frame.Length && packet.Size == frame.Length && packet.Position == 4 &&
        new ReadOnlySpan<byte>(packet.Memory, frame.Length).SequenceEqual(frame);

    internal void Publish(LegacyNativePlayerRuntime player, LegacyNativeGameClientRuntime client,
        LegacyNativeRoleView view, uint uid)
    {
        if (!IsCurrent(player, client, view, uid) || !HasPreparedStorage()) return;
        PublishOwner(client, status!);
        if (!IsCurrent(player, client, view, uid) || !HasPreparedStorage()) return;
        PublishView(view, status!, false);
        if (!IsCurrent(player, client, view, uid) || !HasPreparedStorage() || timed is null) return;
        PublishView(view, timed, true);
    }

    private static bool IsCurrent(LegacyNativePlayerRuntime player, LegacyNativeGameClientRuntime client,
        LegacyNativeRoleView view, uint uid) => ReferenceEquals(client.Player, player) &&
        ReferenceEquals(player.View, view) && ReferenceEquals(view.Owner, client) && player.UID == uid;

    private static void PublishOwner(LegacyNativeGameClientRuntime client, LegacyNativeRecycledPacket rental)
    {
        try { client.Send(rental.GetStream()); }
        catch (Exception error) { LegacyNativePlayerRuntime.ReportFlagAdmission("publication after commit", error); }
    }

    private static void PublishView(LegacyNativeRoleView view, LegacyNativeRecycledPacket rental, bool me)
    {
        try { view.SendView(rental.GetStream(), me); }
        catch (Exception error) { LegacyNativePlayerRuntime.ReportFlagAdmission("publication after commit", error); }
    }

    public void Dispose()
    {
        DisposeOne(ref status); DisposeOne(ref timed);
    }

    private static void DisposeOne(ref LegacyNativeRecycledPacket? rental)
    {
        var owned = rental; rental = null;
        if (owned is null) return;
        try { owned.Dispose(); }
        catch (Exception error) { LegacyNativePlayerRuntime.ReportFlagAdmission("packet cleanup", error); }
    }
}
