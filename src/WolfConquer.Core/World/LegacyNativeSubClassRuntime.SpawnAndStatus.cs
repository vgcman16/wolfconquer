using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeSubClassRuntime
{
    public void CreateSpawn(LegacyNativeGameClientRuntime client)
    {
        client.Player.ActiveSublass = ActiveSubclass;
        client.Player.SubClassHasPoints = GetHashPoint();
    }

    public uint GetHashPoint()
    {
        uint num = 0u;
        foreach (LegacyNativeMsgSubClass.SubClases value in src.Values)
        {
            double term = Math.Pow(10.0, (int)(value.ID - 1)) * (double)(int)value.Phrase;
            // Observed Framework/x86 conv.u4: truncate through signed Int64,
            // retain low32 bits, and return0 at/above2^63. Modern direct uint
            // casts saturate instead. Terms here are nonnegative byte inputs.
            num += term < 9223372036854775808.0 ? unchecked((uint)(long)term) : 0u;
        }
        return num;
    }

    public void UpdateStatus(LegacyNativeGameClientRuntime client)
    {
        foreach (LegacyNativeMsgSubClass.SubClases value in src.Values)
        {
            switch (value.ID)
            {
                case LegacyNativeDbLevExp.Sort.Apothecary:
                    client.Status.Detoxication += LegacyNativePoolSubClassInfo.SubClassInfo.GetDamage(value.ID, value.Phrase)[0];
                    break;
                case LegacyNativeDbLevExp.Sort.ChiMaster:
                    client.Status.Immunity += LegacyNativePoolSubClassInfo.SubClassInfo.GetDamage(value.ID, value.Phrase)[0] * 100;
                    break;
                case LegacyNativeDbLevExp.Sort.MartialArtist:
                    client.Status.CriticalStrike += LegacyNativePoolSubClassInfo.SubClassInfo.GetDamage(value.ID, value.Phrase)[0] * 100;
                    break;
                case LegacyNativeDbLevExp.Sort.Performer:
                    client.Status.MagicAttack += LegacyNativePoolSubClassInfo.SubClassInfo.GetDamage(value.ID, value.Phrase)[0];
                    client.Status.MaxAttack += LegacyNativePoolSubClassInfo.SubClassInfo.GetDamage(value.ID, value.Phrase)[1];
                    client.Status.MinAttack += LegacyNativePoolSubClassInfo.SubClassInfo.GetDamage(value.ID, value.Phrase)[1];
                    break;
                case LegacyNativeDbLevExp.Sort.Sage:
                    client.Status.Penetration += LegacyNativePoolSubClassInfo.SubClassInfo.GetDamage(value.ID, value.Phrase)[0] * 100;
                    break;
                case LegacyNativeDbLevExp.Sort.Warlock:
                    client.Status.SkillCStrike += LegacyNativePoolSubClassInfo.SubClassInfo.GetDamage(value.ID, value.Phrase)[0] * 100;
                    break;
                case LegacyNativeDbLevExp.Sort.Wrangler:
                    client.Status.MaxHitpoints += LegacyNativePoolSubClassInfo.SubClassInfo.GetDamage(value.ID, value.Phrase)[0];
                    break;
            }
        }
    }
}
