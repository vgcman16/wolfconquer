using System.Net.Sockets;

namespace WolfConquer.Core.Networking;

internal partial class LegacyNativeSecuritySocket
{
    // Native SecuritySocket206..238. This queues bytes; it does not send on Connection.
    public unsafe void Send(LegacyNativePacket msg)
    {
        try
        {
            if (!Alive)
            {
                return;
            }
            lock (locckss)
            {
                byte[] array = new byte[msg.Size];
                if (Crypto != null)
                {
                    Crypto.Encrypt(msg.Memory, 0, array, 0, msg.Size);
                }
                else
                {
                    fixed (byte* dest = array)
                    {
                        msg.memcpy(dest, msg.Memory, msg.Size);
                    }
                }
                SendQueue!.Enqueue(array);
            }
        }
        catch (SocketException)
        {
            Disconnect();
        }
        catch (Exception)
        {
        }
    }
}
