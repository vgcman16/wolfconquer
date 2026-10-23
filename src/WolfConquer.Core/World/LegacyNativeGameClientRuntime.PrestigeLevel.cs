using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Actual native GameClient149 physical UInt32 field, not the modern perfection snapshot.
    public uint PrestigeLevel;

    // Complete native GameClient724..731; full equipment/status rebuild remains held.
    internal void UpdatePerfectionLevel(LegacyNativePacket stream)
    {
        LegacyNativeMsgUserTotalRefineLev.TotalRefineLev totalRefineLev = new LegacyNativeMsgUserTotalRefineLev.TotalRefineLev();
        totalRefineLev.Level = PrestigeLevel;
        totalRefineLev.UID = Player.UID;
        totalRefineLev.Type = 0u;
        Send(stream.UserTotalRefineLevCreate(totalRefineLev));
    }
}
