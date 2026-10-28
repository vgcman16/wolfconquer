using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeProficiencyStorage
{
    public void Add(LegacyNativePacket stream, uint ID, uint Level = 0,
        uint Experience = 0, byte PreviousLevel = 0, bool ClearExp = false) =>
        ExecuteProgression(stream, new(ID, 0, Add: true, Level: Level, InitialExperience: Experience,
            PreviousLevel: PreviousLevel, ClearExperience: ClearExp));
}
