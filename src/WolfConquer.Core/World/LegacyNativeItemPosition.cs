namespace WolfConquer.Core.World;

// Complete native ItemType.ItemPosition and its eight pure predicates.
// No data-table, wardrobe-only shortcut, ID normalization or packet route.
internal static class LegacyNativeItemPosition
{
	internal static ushort ItemPosition(uint ID)
		{
			if (ID >= 203003 && ID <= 203009)
			{
				return 18;
			}
			if ((ID >= 200731 && ID <= 200770) || ID == 200771 || ID == 200772 || ID == 205007 || ID == 205005 || (ID >= 200000 && ID <= 200726) || ID == 200696 || ID == 200699 || ID == 200579 || ID == 200726 || ID == 205004 || ID == 200697 || ID == 205000 || ID == 200705 || ID == 205902 || ID == 200677 || ID == 200688 || ID == 200682 || ID == 200647 || ID == 200691 || ID == 205901 || ID == 205003 || ID == 200670 || ID == 205003)
			{
				return 17;
			}
			if ((ID >= 360323 && ID <= 360401) || (ID >= 350001 && ID <= 360291) || ID == 205003 || ID == 350174 || ID == 350175 || ID == 360311 || ID == 360312 || ID == 360319 || ID == 360320 || ID == 360321 || ID == 360322 || ID == 360318 || ID == 360305 || ID == 360309 || ID == 360310 || ID == 360297)
			{
				return 15;
			}
			if ((ID >= 360323 && ID <= 360401) || (ID >= 370001 && ID <= 380046) || ID == 205003 || ID == 350174 || ID == 350175 || ID == 360311 || ID == 360312 || ID == 360319 || ID == 360320 || ID == 360321 || ID == 360322 || ID == 360318 || ID == 360305 || ID == 360309 || ID == 360310 || ID == 360297)
			{
				return 16;
			}
			if ((ID >= 199185 && ID <= 199255) || ID == 199325 || ID == 196885 || ID == 196865 || ID == 196845 || ID == 196815 || ID == 196705 || ID == 196755 || ID == 196785 || ID == 196345 || ID == 196305 || ID == 196275 || ID == 196225 || ID == 196595 || ID == 196675 || ID == 196635 || ID == 196625 || ID == 196615 || ID == 196365 || ID == 134155 || ID == 131155 || ID == 133155 || ID == 130155 || ID == 195795 || (ID == 195845 && ID >= 195845 && ID <= 196225))
			{
				return 9;
			}
			if ((ID >= 111003 && ID <= 118309) || (ID >= 123000 && ID <= 123309) || (ID >= 141003 && ID <= 145309) || (ID >= 146000 && ID <= 148309) || (ID >= 170000 && ID <= 170309))
			{
				return 1;
			}
			if (ID >= 120001 && ID <= 121269)
			{
				return 2;
			}
			if ((ID >= 130003 && ID <= 139309) || (ID >= 101000 && ID <= 102309))
			{
				return 3;
			}
			if (ID >= 150000 && ID <= 152279)
			{
				return 6;
			}
			if (ID >= 160013 && ID <= 160249)
			{
				return 8;
			}
			if (ID >= 181305 && ID <= 196785)
			{
				return 9;
			}
			if (ID >= 201003 && ID <= 201009)
			{
				return 10;
			}
			if (ID >= 202003 && ID <= 202009)
			{
				return 11;
			}
			if (ID == 300000)
			{
				return 12;
			}
			if ((ID >= 410003 && ID <= 613429) || IsTrojanEpicWeapon(ID) || IsMonkEpicWeapon(ID) || IsNinjaEpicWeapon(ID) || (ID >= 617000 && ID <= 617439) || IsTaoistEpicWeapon(ID) || IsWarriorEpicWeapons(ID) || IsWindWalkerWeapon(ID) || IsPirateEpicWeapon(ID) || IsMonkEpicWeapon(ID) || ID / 1000 == 681)
			{
				return 4;
			}
			if ((ID >= 900000 && ID <= 900309) || (ID >= 1050000 && ID <= 1051000) || (ID >= 612000 && ID <= 612439) || (ID >= 612000 && ID <= 613429) || IsWarriorEpicWeapons(ID) || IsWindWalkerWeapon(ID) || (ID >= 611000 && ID <= 611439) || IsTrojanEpicWeapon(ID) || IsNinjaEpicWeapon(ID) || IsMonkEpicWeapon(ID) || (ID >= 617000 && ID <= 617439) || IsHossu(ID) || ID / 1000 == 680)
			{
				return 5;
			}
			if ((ID >= 2100025 && ID <= 2100125) || ID == 2100165 || ID == 2100175 || ID == 2100185 || ID == 2100195 || ID == 2100205 || ID == 2100005 || ID == 2100245 || ID == 2168735 || ID == 2168896 || ID == 2168725 || ID == 2168715 || ID == 2168705)
			{
				return 7;
			}
			if (ID >= 204003 && ID <= 204009)
			{
				return 19;
			}
			if (ID / 10000 == 401)
			{
				return 101;
			}
			if (ID / 10000 == 402)
			{
				return 102;
			}
			if (ID / 10000 == 403)
			{
				return 103;
			}
			if (ID / 10000 == 410)
			{
				return 13;
			}
			return 0;
		}

	private static bool IsTrojanEpicWeapon(uint ID)
		{
			return ID >= 614000 && ID <= 614439;
		}

	private static bool IsMonkEpicWeapon(uint ID)
		{
			return ID >= 622000 && ID <= 622439;
		}

	private static bool IsNinjaEpicWeapon(uint ID)
		{
			if (ID == 616)
			{
				return true;
			}
			return ID >= 616000 && ID <= 616439;
		}

	private static bool IsTaoistEpicWeapon(uint ID)
		{
			return ID >= 620003 && ID <= 620439;
		}

	private static bool IsWarriorEpicWeapons(uint ID)
		{
			return ID / 1000 == 624;
		}

	private static bool IsWindWalkerWeapon(uint ID)
		{
			return ID >= 626003 && ID <= 626439;
		}

	private static bool IsPirateEpicWeapon(uint ID)
		{
			return ID / 1000 == 670 || ID / 1000 == 671;
		}

	private static bool IsHossu(uint ID)
		{
			return ID >= 619000 && ID <= 619439;
		}
}
