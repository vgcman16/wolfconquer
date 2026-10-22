namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Native GameClient129/159 physical fields; no lookup, loader or queue is started here.
    public string MacAddress = "";

    internal LegacyNativePipeClientRuntime? PipeClient = null;

    // Complete native GameClient733..736; the native predicate is reference-only.
    // It deliberately does not consult PipeClient.Alive or its Socket.Alive.
    public bool IsConnectedInterServer()
    {
        return PipeClient != null;
    }
}
