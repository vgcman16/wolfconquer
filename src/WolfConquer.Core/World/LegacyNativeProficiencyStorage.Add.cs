#nullable disable
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeProficiencyStorage
{
    public void Add(LegacyNativePacket stream, uint ID, uint Level = 0u,
        uint Experience = 0u, byte PreviousLevel = 0, bool ClearExp = false)
    {
        if (ClientProf.TryGetValue(ID, out var value))
        {
            value.NeededExperience = LegacyNativeBaseFunc.ProficiencyLevelExperience(unchecked((byte)Level));
            value.Level = Level;
            value.Experience = Experience;
            value.PreviouseLevel = PreviousLevel;
            if (value.Level == 20 || ClearExp)
            {
                value.Experience = 0u;
            }
        }
        else
        {
            value = new LegacyNativeMsgProficiency();
            value.ID = ID;
            value.NeededExperience = LegacyNativeBaseFunc.ProficiencyLevelExperience(unchecked((byte)Level));
            value.Level = Level;
            value.Experience = Experience;
            if (value.Level == 20 || ClearExp)
            {
                value.Experience = 0u;
            }
            value.PreviouseLevel = PreviousLevel;
            ClientProf.TryAdd(value.ID, value);
        }
        Owner.Send(stream.ProficiencyCreate(value.ID, value.Level, value.Experience, value.NeededExperience));
        UpdSpell(value.ID, value.Level, value.Experience, stream);
    }

    private void UpdSpell(uint ID, uint level, uint Experience, LegacyNativePacket stream)
    {
        Owner.Send(stream.UpdateProfExperienceCreate(Experience,
            LegacyNativeBaseFunc.ProficiencyLevelExperience(unchecked((byte)level)), ID));
    }
}
