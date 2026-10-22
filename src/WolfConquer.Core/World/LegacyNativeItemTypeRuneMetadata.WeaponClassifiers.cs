namespace WolfConquer.Core.World;

// Literal native single-ID weapons helpers on the existing owner.
internal sealed partial class LegacyNativeItemTypeRuneMetadata
{
	public static bool IsAEpicWeapon(uint ID)
	{
		return ID / 1000 == 606;
	}

	public static bool IsWarriorEpicWeapons(uint ID)
	{
		return ID / 1000 == 624;
	}

	public static bool IsPistol(uint ID)
	{
		return ID >= 612000 && ID <= 612439;
	}

	public static bool IsPickAxe(uint ID)
	{
		return ID == 562000;
	}

	public static bool IsRapier(uint ID)
	{
		return ID >= 611000 && ID <= 611439;
	}

	public static bool IsKnife(uint ID)
	{
		return ID >= 613000 && ID <= 613429;
	}

	public static bool IsDagger(uint ID)
	{
		return ID >= 490003 && ID <= 490149;
	}

	public static bool IsShield(uint ID)
	{
		return ID >= 900000 && ID <= 900309;
	}

	public static bool IsTrojanEpicWeapon(uint ID)
	{
		return ID >= 614000 && ID <= 614439;
	}

	public static bool IsPrayedBead(uint ID)
	{
		return ID >= 610000 && ID <= 610439;
	}

	public static bool IsMonkEpicWeapon(uint ID)
	{
		return ID >= 622000 && ID <= 622439;
	}

	public static bool IsPirateEpicWeapon(uint ID)
	{
		return ID / 1000 == 670 || ID / 1000 == 671;
	}

	public static bool IsWindWalkerWeapon(uint ID)
	{
		return ID >= 626003 && ID <= 626439;
	}

	public static bool IsDragonWorroir(uint ID)
	{
		return ID >= 617393 && ID <= 617439;
	}

	public static bool IsHossu(uint ID)
	{
		return ID >= 619000 && ID <= 619439;
	}

	public static bool IsTaoistEpicWeapon(uint ID)
	{
		return ID >= 620003 && ID <= 620439;
	}

	public static bool IsArrow(uint ID)
	{
		if (ID >= 1050000 && ID <= 1051000)
		{
			return true;
		}
		return false;
	}

	public static bool IsTwoHand(uint ID)
	{
		return (ID.ToString()[0] == '5' || (ID >= 421003 && ID <= 421439)) ? true : false;
	}

	public static bool IsBow(uint ID)
	{
		return ID >= 500003 && ID <= 500429;
	}

	public static bool IsBacksword(uint ID)
	{
		return ID >= 421003 && ID <= 421439;
	}

	public static bool IsNinjaEpicWeapon(uint ID)
	{
		if (ID == 616)
		{
			return true;
		}
		return ID >= 616000 && ID <= 616439;
	}

	public static bool IsKatana(uint ID)
	{
		if (ID >= 601000 && ID <= 601439)
		{
			return true;
		}
		return false;
	}

	public static bool IsScythe(uint ID)
	{
		if (ID == 511)
		{
			return true;
		}
		if (ID >= 511000 && ID <= 511439)
		{
			return true;
		}
		return false;
	}

	public static bool IsMonkWeapon(uint ID)
	{
		return ID >= 610000 && ID <= 610439;
	}
}
