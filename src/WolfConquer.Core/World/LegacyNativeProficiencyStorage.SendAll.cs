#nullable disable
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeProficiencyStorage
{
    public void SendAll(LegacyNativePacket stream)
    {
        foreach (LegacyNativeMsgProficiency value in ClientProf.Values)
        {
            Owner.Send(stream.ProficiencyCreate(value.ID, value.Level, value.Experience, value.NeededExperience));
        }
    }
}
