using System.Net.Sockets;

namespace WolfConquer.Core.Networking;

// Only native SecuritySocket's two-callback constructor, Send and Disconnect prefix.
// Create, host binding, receive dispatch and scheduling/send-pump ownership are absent.
internal partial class LegacyNativeSecuritySocket
{
    private Action<LegacyNativeSecuritySocket>? OnDisconnect;

    private Action<LegacyNativeSecuritySocket, LegacyNativePacket>? OnReceiveHandler;

    public Socket? Connection;

    private IDisposable?[]? TimerSubscriptions = null;

    public bool Alive = false;

    // Native Create owns this assignment; construction deliberately leaves it null.
#pragma warning disable CS0649
    private Queue<byte[]>? SendQueue;
#pragma warning restore CS0649

    public LegacyNativeTQCast5? Crypto;

    private object locckss = new object();

    public LegacyNativeReceiveBuffer DHKeyBuffer = new LegacyNativeReceiveBuffer(1024, changesize: true);

    public LegacyNativeReceiveBuffer EncryptedDHKeyBuffer = new LegacyNativeReceiveBuffer(1024, changesize: true);

    public object SynRoot = new object();

    public LegacyNativeSecuritySocket(
        Action<LegacyNativeSecuritySocket> _OnDisconnect,
        Action<LegacyNativeSecuritySocket, LegacyNativePacket> _OnReceiveHandler)
    {
        OnReceiveHandler = _OnReceiveHandler;
        OnDisconnect = _OnDisconnect;
    }
}
