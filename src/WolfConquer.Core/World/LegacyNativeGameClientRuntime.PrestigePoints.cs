using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Whole native GameClient538..722. Requires the real incoming Chi, InnerPower, Rune,
    // PrestigeRanking and JiangHu owners. No host adoption or equipment caller is installed.
	internal void CreatePrestigePoints()
	{
		PrestigePoints = new uint[PrestigePoints.Length];
		PrestigePoints[38] = HundredWeapons!.PerfectionScore;
		if (Player.MyJiangHu != null)
		{
			if (Player.MyJiangHu.Inner_Strength != 0 && Player.MyJiangHu.Inner_Strength <= 16200)
			{
				PrestigePoints[0] = Player.MyJiangHu.Inner_Strength / 100 * 30;
			}
			else if (Player.MyJiangHu.Inner_Strength >= 16201 && Player.MyJiangHu.Inner_Strength <= 40500)
			{
				PrestigePoints[0] = Player.MyJiangHu.Inner_Strength / 100 * 33;
			}
			else if (Player.MyJiangHu.Inner_Strength >= 40501 && Player.MyJiangHu.Inner_Strength <= 60750)
			{
				PrestigePoints[0] = Player.MyJiangHu.Inner_Strength / 100 * 36;
			}
			else if (Player.MyJiangHu.Inner_Strength >= 60751 && Player.MyJiangHu.Inner_Strength <= 72000)
			{
				PrestigePoints[0] = Player.MyJiangHu.Inner_Strength / 100 * 40;
			}
			else if (Player.MyJiangHu.Inner_Strength >= 72001 && Player.MyJiangHu.Inner_Strength <= 79200)
			{
				PrestigePoints[0] = Player.MyJiangHu.Inner_Strength / 100 * 45;
			}
			else if (Player.MyJiangHu.Inner_Strength >= 79201 && Player.MyJiangHu.Inner_Strength <= 80800)
			{
				PrestigePoints[0] = Player.MyJiangHu.Inner_Strength / 100 * 50;
			}
			else if (Player.MyJiangHu.Inner_Strength >= 80801 && Player.MyJiangHu.Inner_Strength <= 81000)
			{
				PrestigePoints[0] = Player.MyJiangHu.Inner_Strength / 100 * 60;
			}
		}
		PrestigePoints[1] = 0u;
		if (Player.MyChi != null)
		{
			foreach (LegacyNativeChiRuntime.ChiPower item in Player.MyChi)
			{
				PrestigePoints[1] += (uint)(item.Score * 30);
				if (item.Rank < 50)
				{
					PrestigePoints[1] += (uint)(item.Score * 10);
				}
			}
		}
		PrestigePoints[2] = 0u;
		if (Player.InnerPower != null)
		{
			LegacyNativeInnerPowerRuntime.Stage[] stages = Player.InnerPower.Stages;
			foreach (LegacyNativeInnerPowerRuntime.Stage stage in stages)
			{
				if (stage.UnLocked)
				{
					PrestigePoints[2] += 100u;
				}
			}
		}
		PrestigePoints[4] = 0u;
		if (Player.Level < 120)
		{
			PrestigePoints[4] += (uint)(Player.Level * 10);
		}
		else if (Player.Level >= 120 && Player.Level < 130)
		{
			PrestigePoints[4] += (uint)(Player.Level * 15);
		}
		else if (Player.Level >= 130 && Player.Level < 140)
		{
			PrestigePoints[4] += (uint)(Player.Level * 20);
		}
		else
		{
			PrestigePoints[4] += (uint)(Player.Level * 25);
		}
		PrestigePoints[6] = (uint)((Player.Strength + Player.Spirit + Player.Vitality + Player.Agility) * 5);
		PrestigePoints[7] = (uint)(Player.Reborn * 1000);
		PrestigePoints[15] = 0u;
		if (Player.SubClass != null)
		{
			foreach (LegacyNativeMsgSubClass.SubClases value in Player.SubClass.src.Values)
			{
				PrestigePoints[15] += (uint)(value.Level * 80);
				PrestigePoints[15] += (uint)(value.Phrase * 20);
			}
		}
		PrestigePoints[16] = 0u;
		if (MyWardrobe!.Items[1].Count > 0)
		{
			PrestigePoints[16] += 50u;
		}
		if (MyWardrobe!.Items[1].Count > 15)
		{
			PrestigePoints[16] += 50u;
		}
		if (MyWardrobe!.Items[1].Count > 30)
		{
			PrestigePoints[16] += 50u;
		}
		if (MyWardrobe!.Items[1].Count > 45)
		{
			PrestigePoints[16] += 50u;
		}
		if (MyWardrobe!.Items[1].Count > 60)
		{
			PrestigePoints[16] += 50u;
		}
		PrestigePoints[17] = 0u;
		if (MyWardrobe!.Items[1].Count > 0)
		{
			PrestigePoints[17] += 50u;
		}
		if (MyWardrobe!.Items[1].Count > 15)
		{
			PrestigePoints[17] += 50u;
		}
		if (MyWardrobe!.Items[1].Count > 30)
		{
			PrestigePoints[17] += 50u;
		}
		if (MyWardrobe!.Items[1].Count > 45)
		{
			PrestigePoints[17] += 50u;
		}
		if (MyWardrobe!.Items[1].Count > 60)
		{
			PrestigePoints[17] += 50u;
		}
		PrestigePoints[18] = (uint)Player.NobilityRank * 1000u;
		PrestigePoints[20] = Rune!.PerfectionRedRune;
		PrestigePoints[21] = Rune!.PerfectionBlueRune;
		PrestigePoints[22] = Rune!.PerfectionYellowRune;
		if (Beasts!.Activated)
		{
			PrestigePoints[37] = 300u;
			for (int j = 0; j <= Beasts!.Level; j++)
			{
				PrestigePoints[37] += (uint)((j >= 50) ? 100 : 200);
			}
		}
		LegacyNativeGameItemRuntime[] curentEquip = Equipment!.CurentEquip;
		foreach (LegacyNativeGameItemRuntime msgGameItem in curentEquip)
		{
			PrestigePoints[3] += msgGameItem.ItemPoints;
		}
		MyPrestigePoints = 0u;
		for (int l = 0; l < PrestigePoints.Length; l++)
		{
			MyPrestigePoints += PrestigePoints[l];
		}
		LegacyNativeGameItemRuntime[] curentEquip2 = Equipment!.CurentEquip;
		foreach (LegacyNativeGameItemRuntime msgGameItem2 in curentEquip2)
		{
			MyPrestigePoints += msgGameItem2.ItemPoints;
			PrestigePoints[3] += msgGameItem2.ItemPoints;
		}
		PrestrigeEntry = new LegacyNativePrestigeRanking.Entry();
		PrestrigeEntry!.type = LegacyNativePrestigeRanking.GetIndex(Player.Class);
		PrestrigeEntry!.UID = Player.UID;
		PrestrigeEntry!.Name = Player.Name;
		PrestrigeEntry!.Class = Player.Class;
		PrestrigeEntry!.TotalPoints = MyPrestigePoints;
		PrestrigeEntry!.Level = (byte)Player.Level;
		PrestrigeEntry!.Mesh = Player.Mesh;
		PrestrigeEntry!.AddInfo(this);
		PrestrigeEntry!.Points = new uint[PrestigePoints.Length];
		for (int n = 0; n < PrestigePoints.Length; n++)
		{
			PrestrigeEntry!.Points![n] = PrestigePoints[n];
		}
		if (LegacyNativeGroupServerList.MyServerInfo.ID == Player.ServerID)
		{
			LegacyNativePrestigeRanking.PublishPrestigeEntry(
				PrestrigeEntry!, Player.Class < 10000 || Player.Class > 10002);
		}
		PerfectionStatus.Update(unchecked((int)PrestigeLevel), this);
		if (Player.MyGuild != null && Player.MyGuildMember != null)
		{
			Player.MyGuildMember.PrestigePoints = PrestrigeEntry!.TotalPoints;
		}
	}
}
