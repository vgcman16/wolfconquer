#nullable disable
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeHundredWeapons
{
    public void UpdateRank()
    {
        if (Unlocked && Valid())
        {
            foreach (LegacyNativeHundredWeaponInfo value in Objects.Values)
            {
                if (value.Level >= 4)
                {
                    LegacyNativeHWRank.Entry entry = new LegacyNativeHWRank.Entry
                    {
                        Type = LegacyNativeHWRank.GetIndex(value.WeaponSubtype),
                        TotalPoints = value.Score,
                        UID = Owner.Player.UID,
                        Name = Owner.Player.Name,
                        Level = unchecked((byte)Owner.Player.Level),
                        Class = Owner.Player.Class,
                        Mesh = Owner.Player.Mesh
                    };
                    entry.AddInfo(Owner);
                    LegacyNativeHWRank.Ranks[LegacyNativeHWRank.GetIndex(value.WeaponSubtype)].UpdateItem(entry);
                    LegacyNativeHWRank.Entry entry2 = entry.ShallowCopy();
                    entry2.TotalPoints = TotalScore;
                    LegacyNativeHWRank.Ranks[LegacyNativeHWRank.Type.Main].UpdateItem(entry2);
                }
            }
            return;
        }
        LegacyNativeHWRank.Remove(Owner.Player.UID);
    }
}
