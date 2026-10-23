using WolfConquer.Core.World;
using System;
using System.Collections.Generic;
using System.IO;

namespace WolfConquer.Core.Data;

internal class LegacyNativeDisdain
{
	public class DisdainItem
	{
		public int type;

		public int delta_lev;

		public int usr_atk_mst;

		public int mst_atk;

		public int max_atk;

		public int max_xp_atk;

		public int exp_factor;

		public int xp_exp_factor;

		public int usr_atk_usr_min;

		public int usr_atk_usr_max;

		public int usr_atk_usr_overadj;

		public int usr_atk_usrx_min;

		public int usr_atk_usrx_max;

		public int usr_atk_usrx_overadj;

		public int usrx_atk_usr_min;

		public int usrx_atk_usr_max;

		public int usrx_atk_usr_overadj;

		public int usrx_atk_usrx_min;

		public int usrx_atk_usrx_max;

		public int usrx_atk_usrx_overadj;
	}

	public static Dictionary<int, DisdainItem> DisdainRecords = new Dictionary<int, DisdainItem>();

	private static int[] NonrebornInts = new int[5] { 10, 6, 6, 6, 6 };

	private static int[] RebornInts = new int[5] { 18, 18, 14, 27, 18 };

	public static void Load()
	{
		string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "cq_disdain.txt");
		string[] array2 = array;
		foreach (string text in array2)
		{
			string[] array3 = text.Split(',');
			DisdainItem disdainItem = new DisdainItem();
			disdainItem.type = int.Parse(array3[1]);
			disdainItem.delta_lev = int.Parse(array3[2]);
			disdainItem.usr_atk_mst = int.Parse(array3[3]);
			disdainItem.mst_atk = int.Parse(array3[4]);
			disdainItem.max_atk = int.Parse(array3[5]);
			disdainItem.max_xp_atk = int.Parse(array3[6]);
			disdainItem.exp_factor = int.Parse(array3[7]);
			disdainItem.xp_exp_factor = int.Parse(array3[8]);
			disdainItem.usr_atk_usr_min = int.Parse(array3[9]);
			disdainItem.usr_atk_usr_max = int.Parse(array3[10]);
			disdainItem.usr_atk_usr_overadj = int.Parse(array3[11]);
			disdainItem.usr_atk_usrx_min = int.Parse(array3[12]);
			disdainItem.usr_atk_usrx_max = int.Parse(array3[13]);
			disdainItem.usr_atk_usrx_overadj = int.Parse(array3[14]);
			disdainItem.usrx_atk_usr_min = int.Parse(array3[15]);
			disdainItem.usrx_atk_usr_max = int.Parse(array3[16]);
			disdainItem.usrx_atk_usr_overadj = int.Parse(array3[17]);
			disdainItem.usrx_atk_usrx_min = int.Parse(array3[18]);
			disdainItem.usrx_atk_usrx_max = int.Parse(array3[19]);
			disdainItem.usrx_atk_usrx_overadj = int.Parse(array3[20]);
			DisdainRecords.Add(disdainItem.delta_lev, disdainItem);
		}
	}

	public static DisdainItem? LookupDisdain(int attacker, int target)
	{
		int num = attacker - target;
		if (num > 400)
		{
			num = 400;
		}
		else if (num < -400)
		{
			num = -400;
		}
		DisdainItem? value = null;
		DisdainRecords.TryGetValue(num, out value);
		return value;
	}

	public static int UserAttackMonster(LegacyNativePlayerRuntime attacker, LegacyNativeMonsterRole target, int damage)
	{
		DisdainItem disdainItem = LookupDisdain(attacker.BattlePower, target.BattlePower)!;
		int num = disdainItem.max_atk;
		if (attacker.OnXPSkill() != LegacyNativeMsgUpdate.Flags.Normal)
		{
			num = disdainItem.max_xp_atk;
		}
		int val = target.Family.MaxHealth * (num / 100);
		damage = Math.Min(damage, val);
		int num2 = target.BattlePower - attacker.BattlePower;
		if (num2 > 0)
		{
			num = ((num2 >= 10) ? 1 : ((num2 >= 5) ? 5 : ((num2 < 1) ? 100 : 10)));
			damage = LegacyNativeAttackBase.MulDiv(damage, num, 100);
		}
		return damage;
	}

	public static int MonsterAttackUser(LegacyNativeMonsterRole attacker, LegacyNativePlayerRuntime target, int damage)
	{
		int num = target.BattlePower - attacker.BattlePower;
		int num2 = ((num < 5) ? 100 : ((num < 10) ? 80 : ((num < 15) ? 60 : ((num >= 20) ? 30 : 40))));
		int val = LegacyNativeAttackBase.MulDiv((int)target.Owner.Status.MaxHitpoints, num2 * attacker.ExtraDamage, 1000000);
		return Math.Max(val, damage);
	}

	public static int UserAttackUser(LegacyNativePlayerRuntime attacker, LegacyNativePlayerRuntime target, int damage)
	{
		DisdainItem disdainItem = LookupDisdain(attacker.BattlePower, (attacker.ContainFlag(LegacyNativeMsgUpdate.Flags.DeathSigh) && target.BattlePower > attacker.BattlePower) ? attacker.BattlePower : target.BattlePower)!;
		int num;
		int num2;
		int numerator;
		if (attacker.Level < 110)
		{
			if (target.Level < 110)
			{
				num = disdainItem.usr_atk_usr_min;
				num2 = disdainItem.usr_atk_usr_max;
				numerator = disdainItem.usr_atk_usr_overadj;
			}
			else
			{
				num = disdainItem.usr_atk_usrx_min;
				num2 = disdainItem.usr_atk_usrx_max;
				numerator = disdainItem.usr_atk_usrx_overadj;
			}
		}
		else if (target.Level < 110)
		{
			num = disdainItem.usrx_atk_usr_min;
			num2 = disdainItem.usrx_atk_usr_max;
			numerator = disdainItem.usrx_atk_usr_overadj;
		}
		else
		{
			num = disdainItem.usrx_atk_usrx_min;
			num2 = disdainItem.usrx_atk_usrx_max;
			numerator = disdainItem.usrx_atk_usrx_overadj;
		}
		int num3 = UserAttackUserGetFactor(target);
		int level = target.Level;
		int num4 = num * level * num3 / 100;
		if (damage < num4)
		{
			return num4;
		}
		int num5 = num2 * level * num3 / 100;
		if (damage > num5)
		{
			int num6 = LegacyNativeAttackBase.MulDiv(damage - num5, numerator, 100);
			return num6 + num5;
		}
		return damage;
	}

	public static int UserAttackUser(int attacker, int target, int damage)
	{
		DisdainItem disdainItem = LookupDisdain(attacker, target)!;
		int usrx_atk_usrx_min = disdainItem.usrx_atk_usrx_min;
		int usrx_atk_usrx_max = disdainItem.usrx_atk_usrx_max;
		int usrx_atk_usrx_overadj = disdainItem.usrx_atk_usrx_overadj;
		int num = 18;
		int num2 = 140;
		int num3 = usrx_atk_usrx_min * num2 * num / 100;
		if (damage < num3)
		{
			return num3;
		}
		int num4 = usrx_atk_usrx_max * num2 * num / 100;
		if (damage > num4)
		{
			int num5 = LegacyNativeAttackBase.MulDiv(damage - num4, usrx_atk_usrx_overadj, 100);
			return num5 + num4;
		}
		return damage;
	}

	private static int UserAttackUserGetFactor(LegacyNativePlayerRuntime target)
	{
		int num = ((!LegacyNativeAtributesStatus.IsTrojan(target.Class)) ? (LegacyNativeAtributesStatus.IsWarrior(target.Class) ? 1 : (LegacyNativeAtributesStatus.IsArcher(target.Class) ? 2 : (((target.Class >= 10000 && target.Class <= 10002) || LegacyNativeAtributesStatus.IsFire(target.Class)) ? 3 : ((!LegacyNativeAtributesStatus.IsWater(target.Class)) ? 1 : 4)))) : 0);
		if (target.Reborn > 0)
		{
			return RebornInts[num];
		}
		return NonrebornInts[num];
	}
}
