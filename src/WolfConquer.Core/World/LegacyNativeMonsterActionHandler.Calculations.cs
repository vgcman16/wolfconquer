using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMonsterActionHandler
{
	public bool ExtraBoss(LegacyNativeMonsterRole monster)
	{
		return monster.Family.MaxHealth > 300000 && monster.Family.MaxHealth < 7000000;
	}

	public bool CheckDodge(uint Dodge)
	{
		bool result = true;
		uint num = (uint)Random.Next() % 150u;
		if (num > 60)
		{
			num = (uint)Random.Next() % 150u;
		}
		if (num < Dodge)
		{
			result = false;
		}
		return result;
	}

	public uint MagicAttack(LegacyNativeMonsterRole attacked, LegacyNativeMonsterRole monster)
	{
		uint maxAttack = (uint)monster.Family.MaxAttack;
		if (maxAttack > attacked.Family.Defense)
		{
			return maxAttack - attacked.Family.Defense;
		}
		return 1u;
	}

	public uint DecreaseBless(uint Damage, uint bless)
	{
		uint num = Damage;
		num = num * bless / 100;
		return Damage - num;
	}
}
