using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Whole native UpdateSpell component. Dispatcher/host acceptance is separately held.
internal class LegacyNativeUpdateSpell
{
	public static void CheckUpdate(LegacyNativePacket stream, LegacyNativeGameClientRuntime client, LegacyNativeInteractQuery Attack, uint Damage, Dictionary<ushort, LegacyNativeMagicType.Magic> DBSpells)
	{
		if (Damage == 0)
		{
			return;
		}
		if (DBSpells != null && client.MySpells.ClientSpells.TryGetValue(Attack.SpellID, out var value))
		{
			uint num = client.Equipment.RightWeapon / 1000;
			uint num2 = client.Equipment.LeftWeapon / 1000;
			if (num != 0)
			{
				client.MyProfs.CheckUpdate(num, Damage * LegacyNativeServerConfig.ExpRateProf, stream);
			}
			if (num2 != 0)
			{
				client.MyProfs.CheckUpdate(num2, Damage * LegacyNativeServerConfig.ExpRateProf, stream);
			}
			ushort level = value.Level;
			if (value.Level < DBSpells.Count - 1 && !DBSpells[value.Level].isRuneSkill && client.Player.Level >= DBSpells[value.Level].NeedLevel)
			{
				value.Experience += (int)(Damage * LegacyNativeServerConfig.ExpRateSpell);
				if (value.Experience > DBSpells[value.Level].Experience)
				{
					value.Level++;
					value.Experience = 0;
				}
				if (value.PreviousLevel != 0 && value.PreviousLevel >= value.Level / 2)
				{
					value.Level = value.PreviousLevel;
				}
				try
				{
					if (value.Level > level)
					{
						client.SendSysMesage("You increased the spell level!");
					}
				}
				catch (Exception ex)
				{
					LegacyNativeConsole.WriteLine(ex.ToString());
				}
				client.Send(stream.SpellCreate(value));
			}
		}
		if (Attack.AtkType == 2 || Attack.AtkType == 28 || Attack.AtkType == 24)
		{
			uint num3 = client.Equipment.RightWeapon / 1000;
			uint num4 = client.Equipment.LeftWeapon / 1000;
			if (num3 != 0)
			{
				client.MyProfs.CheckUpdate(num3, Damage * LegacyNativeServerConfig.ExpRateProf, stream);
			}
			if (num4 != 0)
			{
				client.MyProfs.CheckUpdate(num4, Damage * LegacyNativeServerConfig.ExpRateProf, stream);
			}
		}
	}
}
