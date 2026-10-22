#nullable disable
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    public void UpdateRank()
    {
        if (LegacyNativeAtributesStatus.IsNinja(user.Player.Class))
        {
            if (Score >= 1000)
            {
                LegacyNativeNinjaRank.Entry entry = new LegacyNativeNinjaRank.Entry
                {
                    Type = LegacyNativeNinjaRank.Type.Overall,
                    TotalPoints = Score,
                    UID = user.Player.UID,
                    Name = user.Player.Name,
                    Level = unchecked((byte)user.Player.Level),
                    Class = user.Player.Class,
                    Mesh = user.Player.Mesh
                };
                entry.AddInfo(user);
                LegacyNativeNinjaRank.Ranks[LegacyNativeNinjaRank.Type.Overall].UpdateItem(entry);
                if (Fire_Score >= 1000)
                {
                    LegacyNativeNinjaRank.Entry entry2 = entry.ShallowCopy();
                    entry2.TotalPoints = Fire_Score;
                    entry2.Type = LegacyNativeNinjaRank.Type.Fire;
                    LegacyNativeNinjaRank.Ranks[LegacyNativeNinjaRank.Type.Fire].UpdateItem(entry2);
                }
                if (Water_Score >= 1000)
                {
                    LegacyNativeNinjaRank.Entry entry3 = entry.ShallowCopy();
                    entry3.TotalPoints = Water_Score;
                    entry3.Type = LegacyNativeNinjaRank.Type.Water;
                    LegacyNativeNinjaRank.Ranks[LegacyNativeNinjaRank.Type.Water].UpdateItem(entry3);
                }
                if (Earth_Score >= 1000)
                {
                    LegacyNativeNinjaRank.Entry entry4 = entry.ShallowCopy();
                    entry4.TotalPoints = Earth_Score;
                    entry4.Type = LegacyNativeNinjaRank.Type.Earth;
                    LegacyNativeNinjaRank.Ranks[LegacyNativeNinjaRank.Type.Earth].UpdateItem(entry4);
                }
                if (Wind_Score > 1000)
                {
                    LegacyNativeNinjaRank.Entry entry5 = entry.ShallowCopy();
                    entry5.TotalPoints = Wind_Score;
                    LegacyNativeNinjaRank.Ranks[LegacyNativeNinjaRank.Type.Wind].UpdateItem(entry5);
                }
                if (Lightning_Score > 1000)
                {
                    LegacyNativeNinjaRank.Entry entry6 = entry.ShallowCopy();
                    entry6.TotalPoints = Lightning_Score;
                    entry6.Type = LegacyNativeNinjaRank.Type.Lightning;
                    LegacyNativeNinjaRank.Ranks[LegacyNativeNinjaRank.Type.Lightning].UpdateItem(entry6);
                }
            }
        }
        else
        {
            LegacyNativeNinjaRank.Remove(user.Player.UID);
        }
    }
}
