using System;

namespace WolfConquer.Core.World;

internal class LegacyNativeRoleStatus
{
	public enum StatuTyp : byte
	{
		IncreaseMStrike = 60,
		IncreasePStrike = 59,
		IncreaseImunity = 61,
		IncreaseBreack = 62,
		IncreaseAntiBreack = 63,
		IncreaseMaxHp = 64,
		IncreasePAttack = 65,
		IncreaseMAttack = 66,
		IncreaseFinalPDamage = 67,
		IncreaseFinalMDamage = 68,
		IncreaseFinalPAttack = 69,
		IncreaseFinalMAttack = 70
	}

	private uint Power = 0u;

	public DateTime Stamp = default(DateTime);

	public uint GetPower => Power;

	public LegacyNativeRoleStatus(uint _Power, int Secounds)
	{
		Power = _Power;
		Stamp = DateTime.Now.AddSeconds(Secounds);
	}

	public static implicit operator bool(LegacyNativeRoleStatus big)
	{
		if (DateTime.Now > big.Stamp)
		{
			return true;
		}
		return false;
	}

	public static implicit operator uint(LegacyNativeRoleStatus big)
	{
		return big.GetPower;
	}
}
