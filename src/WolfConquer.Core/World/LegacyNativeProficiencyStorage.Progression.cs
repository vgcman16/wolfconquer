#nullable disable
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeProficiencyStorage
{
    public void CheckUpdate(uint ID, uint GetExperience, LegacyNativePacket stream)
    {
        unchecked
        {
            if (GetExperience == 0 || !Enum.IsDefined(typeof(LegacyNativeMagicType.WeaponsType), (LegacyNativeMagicType.WeaponsType)ID))
            {
                return;
            }
            if (ClientProf.TryGetValue(ID, out var value))
            {
                if (value.Level >= 20)
                {
                    return;
                }
                if (value.Level < 5)
                {
                    GetExperience *= 100;
                }
                else if (value.Level > 13)
                {
                    GetExperience /= 100;
                }
                GetExperience /= value.Level + 1;
                value.Experience += GetExperience;
                value.Experience += GetExperience * Owner.GemValues(LegacyNativeGameItemFlags.Gem.NormalVioletGem) / 100;
                bool flag = false;
                while (value.Experience >= LegacyNativeBaseFunc.ProficiencyLevelExperience((byte)value.Level))
                {
                    value.Experience -= LegacyNativeBaseFunc.ProficiencyLevelExperience((byte)value.Level);
                    value.Level++;
                    if (value.Level == 20)
                    {
                        value.Experience = 0u;
                        Owner.Send(stream.ProficiencyCreate(value.ID, value.Level, value.Experience, Owner.Player.UID));
                        Owner.SendSysMesage("You increased the weapon proficiency!");
                        UpdSpell(value.ID, value.Level, value.Experience, stream);
                        return;
                    }
                    flag = true;
                    Owner.SendSysMesage("You increased the weapon proficiency!");
                    if (value.PreviouseLevel != 0 && value.Level >= value.PreviouseLevel / 2 && value.Level < value.PreviouseLevel)
                    {
                        value.Level = value.PreviouseLevel;
                        value.Experience = 0u;
                    }
                }
                if (flag)
                {
                    Owner.Send(stream.ProficiencyCreate(value.ID, value.Level, value.Experience, value.NeededExperience));
                }
                UpdSpell(value.ID, value.Level, value.Experience, stream);
            }
            else
            {
                Add(stream, ID, 0u, 0u, 0);
            }
        }
    }
}
