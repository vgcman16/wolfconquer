using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    public void SageMode(uint Timer)
    {
        user.Player.AddFlag(LegacyNativeMsgUpdate.Flags.SageSpeedMode, unchecked((int)Timer), RemoveOnDead: true);
        user.Player.SendUpdate(new LegacyNativeRecycledPacket().GetStream(), LegacyNativeMsgUpdate.Flags.SageSpeedMode,
            Timer, 0u, 0u, LegacyNativeMsgUpdate.DataType.ArchiveSkill, scren: true);
    }
}
