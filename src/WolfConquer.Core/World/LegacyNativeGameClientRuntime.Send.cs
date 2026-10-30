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
    internal void Send(LegacyNativePacket? msg)
    {
        try
        {
            var socket = Socket;
            if (Fake || !socket!.Alive || msg == null)
            {
                return;
            }
            var description = msg.DescribeSendStorage();
            if (description.Length > 1032)
            {
                LegacyNativeConsole.WriteLine("[WARNING]: Sending packet with a huge length. ID: " +
                    description.Opcode, ConsoleColor.DarkYellow);
            }
            else
            {
                socket!.TrySend(msg, 1032);
            }
        }
        catch (Exception e) when (e is not AccessViolationException)
        {
            LegacyNativeConsole.WriteException(e);
        }
    }
}
