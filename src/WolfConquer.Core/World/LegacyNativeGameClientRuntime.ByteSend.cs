using System.Buffers.Binary;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Preserve native byte transformation and1032 frontier, preparing an owned
    // copy before queue admission. This owns only its temporary Packet, not a connection.
    internal void Send(byte[] buffer)
    {
        try
        {
            var socket = Socket;
            if (Fake || !socket!.Alive)
            {
                return;
            }
            ArgumentNullException.ThrowIfNull(buffer);
            if (buffer.Length > 1032)
            {
                LegacyNativeConsole.WriteLine("[WARNING]: Sending packet with a huge length. ID: " +
                    BitConverter.ToUInt16(buffer, 2), ConsoleColor.DarkYellow);
                return;
            }
            if (buffer.Length < 12)
                throw new ArgumentOutOfRangeException(nameof(buffer), "Byte sends require the four-byte header and eight-byte seal.");
            byte[] prepared = (byte[])buffer.Clone();
            ushort declaredLength = BinaryPrimitives.ReadUInt16LittleEndian(prepared);
            ushort actualLength = checked((ushort)(prepared.Length - 8));
            if (declaredLength != 0 && declaredLength != actualLength)
                throw new InvalidDataException("Byte-send declared length does not match its actual frame excluding the seal.");
            if (declaredLength == 0)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(prepared, actualLength);
            }
            "TQServer"u8.CopyTo(prepared.AsSpan(prepared.Length - 8));
            using var msg = new LegacyNativePacket(prepared);
            if (socket!.TrySend(msg, 1032) == LegacyNativeSecuritySocket.SendResult.Queued)
                prepared.CopyTo(buffer, 0);
        }
        catch (Exception e) when (e is not AccessViolationException)
        {
            LegacyNativeConsole.WriteException(e);
        }
    }
}
