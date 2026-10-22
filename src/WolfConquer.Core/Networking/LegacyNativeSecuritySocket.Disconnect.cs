using System.Threading;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.Networking;

internal partial class LegacyNativeSecuritySocket
{
    // Native SecuritySocket513..556, including the real exception-file catch path.
    // Timer disposal precedes the cleanup try/finally; its faults must not be repaired.
    public void Disconnect()
    {
        lock (SynRoot)
        {
            int num = 1000;
            while (SendQueue!.Count > 0 && Alive && num-- > 0)
            {
                Thread.Sleep(1);
            }
            if (!Alive)
            {
                return;
            }
            Alive = false;
            if (TimerSubscriptions != null)
            {
                for (int i = 0; i < TimerSubscriptions.Length; i++)
                {
                    if (TimerSubscriptions[i] != null)
                    {
                        TimerSubscriptions[i]!.Dispose();
                    }
                }
            }
            try
            {
                LegacyNativeWs2_32.shutdown(Connection!.Handle, LegacyNativeWs2_32.ShutDownFlags.SD_BOTH);
                LegacyNativeWs2_32.closesocket(Connection!.Handle);
                Connection!.Dispose();
                GC.SuppressFinalize(Connection!);
            }
            catch (Exception e)
            {
                LegacyNativeConsole.SaveException(e);
            }
            finally
            {
                if (OnDisconnect != null)
                {
                    OnDisconnect(this);
                }
            }
        }
    }
}
