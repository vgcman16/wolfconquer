using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Native GameClient229/253/291 physical fields, in relative source order.
    // No socket binding or later LoadCharacter FullLoading=true phase is installed here.
    public bool FullLoading = false;

    internal LegacyNativeSecuritySocket? Socket;

    public bool Fake = false;

    // Native GameClient1450..1475. Internal only because the real packet type is internal.
    internal unsafe void Send(LegacyNativePacket? msg)
    {
        try
        {
            if (Fake || !Socket!.Alive || msg == null)
            {
                return;
            }
            if (msg.Size > 1032)
            {
                byte[] array = new byte[msg.Size];
                fixed (byte* dest = array)
                {
                    msg.memcpy(dest, msg.Memory, msg.Size);
                }
                LegacyNativeConsole.WriteLine("[WARNING]: Sending packet with a huge length. ID: " +
                    BitConverter.ToUInt16(array, 2), ConsoleColor.DarkYellow);
            }
            else
            {
                Socket.Send(msg);
            }
        }
        catch (Exception e)
        {
            LegacyNativeConsole.WriteException(e);
        }
    }
}
