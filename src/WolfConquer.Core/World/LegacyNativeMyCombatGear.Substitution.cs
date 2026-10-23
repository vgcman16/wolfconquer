using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMyCombatGear
{
public void Substitution(ref LegacyNativeMsgSpellAnimation.SpellObj SpellObj, LegacyNativePlayerRuntime Target)
	{
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		LegacyNativeMsgUpdate.Flags flag = LegacyNativeMsgUpdate.Flags.FireAura | LegacyNativeMsgUpdate.Flags.SongofPhoenix;
		if (!user.Player.ContainFlag(flag))
		{
			return;
		}
		LegacyNativeMsgUpdate.Flags flag2 = LegacyNativeMsgUpdate.Flags.EartAura | LegacyNativeMsgUpdate.Flags.HawksAmbition;
		uint num = NativeGearShieldUInt32(GetValue("Substitution")[0]);
		if (num != 0)
		{
			if (SpellObj.Damage * 20 / 100 > num)
			{
				user.Player.SendUpdate(stream, flag, 0u, 0u, 0u, LegacyNativeMsgUpdate.DataType.AzureShield, scren: true);
				user.Player.AddSpellFlag(flag2, 10, RemoveOnDead: true);
				LegacyNativeAzureShield.CreateDmg(Target, user.Player, num);
				SpellObj.Damage -= num;
			}
			else
			{
				num -= SpellObj.Damage * 20 / 100;
				SetValue("Substitution", num);
				user.Player.SendUpdate(stream, flag, 0u, num, 0u, LegacyNativeMsgUpdate.DataType.AzureShield, scren: true);
				LegacyNativeAzureShield.CreateDmg(Target, user.Player, SpellObj.Damage * 20 / 100);
				SpellObj.Damage -= SpellObj.Damage * 20 / 100;
			}
		}
	}
}
