using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Native PipeClient instance storage, both constructors, Send and Disconnect only.
// NOT full PipeClient: the static ExecuteLogin worker/initializer, Connect,
// CompleteConnected and incoming packet dispatch remain absent and unwired.
// The referenced SecuritySocket has the real constructor, Send and full native
// Disconnect body; this does not implement Create, actual send pump or full lifecycle.
internal partial class LegacyNativePipeClientRuntime
{
    public LegacyNativeSecuritySocket? Socket;

    public LegacyNativeGameClientRuntime? Owner;

    public uint NewUserID = 0u;

    public string ConnectToAddress = "";

    public ushort ConnectToPort = 0;

    public bool Alive { get; private set; }

    public LegacyNativePipeClientRuntime()
    {
    }

    public LegacyNativePipeClientRuntime(LegacyNativeSecuritySocket? _socket, LegacyNativeGameClientRuntime? obj)
    {
        Socket = _socket;
        Alive = true;
        Owner = obj;
        Socket!.Client = this;
    }

    // Complete native PipeClient178..184. No extra guard/catch/byte-copy/retry.
    public void Send(LegacyNativePacket msg)
    {
        if (Alive)
        {
            Socket!.Send(msg);
        }
    }

    // Complete native PipeClient186..193. Clears pipe liveness BEFORE calling
    // the genuine socket owner, including when that owner throws on bad state.
    public void Disconnect()
    {
        if (Alive)
        {
            Alive = false;
            Socket!.Disconnect();
        }
    }
}
