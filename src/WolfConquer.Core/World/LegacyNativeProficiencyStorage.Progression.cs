using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeProficiencyStorage
{
    public void CheckUpdate(uint ID, uint GetExperience, LegacyNativePacket stream)
    {
        if (GetExperience == 0 || !Enum.IsDefined(typeof(LegacyNativeMagicType.WeaponsType),
                (LegacyNativeMagicType.WeaponsType)ID)) return;
        ExecuteProgression(stream, new(ID, GetExperience, Add: false));
    }
}
