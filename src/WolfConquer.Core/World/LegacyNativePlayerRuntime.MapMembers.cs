using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Whole Player1403 auto-property,3193..3196 and4588..4591. No IMapObj claim yet.
    public uint IndexInScreen { get; set; }

    public bool IsTrap()
    {
        return false;
    }

    internal void Send(LegacyNativePacket msg)
    {
        Owner.Send(msg);
    }
}
