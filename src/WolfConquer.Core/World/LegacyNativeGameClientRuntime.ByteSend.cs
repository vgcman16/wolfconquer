using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Native GameClient1423..1448. Mutates the caller's byte[] before Packet copies it;
    // does not own socket creation, a send pump, host binding, or Packet disposal.
    internal void Send(byte[] buffer)
    {
        try
        {
            if (Fake || !Socket!.Alive)
            {
                return;
            }
            if (buffer.Length > 1032)
            {
                LegacyNativeConsole.WriteLine("[WARNING]: Sending packet with a huge length. ID: " +
                    BitConverter.ToUInt16(buffer, 2), ConsoleColor.DarkYellow);
                return;
            }
            if (BitConverter.ToUInt16(buffer, 0) == 0)
            {
                // Literal reachable MsgServer.Packet.WriteUInt16 (32..39), offset 0.
                ushort arg = (ushort)(buffer.Length - 8);
                int offset = 0;
                if (buffer != null && offset <= buffer.Length - 1 && buffer.Length >= offset + 2)
                {
                    buffer[offset] = (byte)arg;
                    buffer[offset + 1] = (byte)(arg >> 8);
                }
            }
            // Literal reachable MsgServer.Packet.WriteString (5..14). In particular,
            // the native guard does NOT reject a negative offset on short arrays.
            string text = "TQServer";
            int sealOffset = buffer!.Length - 8;
            if (buffer != null && sealOffset <= buffer.Length - 1 && buffer.Length >= sealOffset + text.Length)
            {
                for (ushort num = 0; num < text.Length; num++)
                {
                    buffer[(ushort)(num + sealOffset)] = (byte)text[num];
                }
            }
            LegacyNativePacket msg = new LegacyNativePacket(buffer!);
            Socket.Send(msg);
        }
        catch (Exception e)
        {
            LegacyNativeConsole.WriteException(e);
        }
    }
}
