using System.Collections;
using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeJiangHu
{
	internal void CreateTime()
	{
		StartCountDwon = DateTime.Now;
		CountDownEnd = DateTime.Now.AddMinutes((int)LegacyNativeJianHuTable.GetMinutesOnTalent(Talent));
	}

	internal void DisableJiang(LegacyNativeGameClientRuntime user)
	{
		CheckMode = false;
		OnJiangMode = false;
		user.Player.JiangHuActive = 0;
		user.Player.JiangHuTalent = 0;
		SendInfo(user, LegacyNativeMsgJiangHuInfo.JiangMode.InfoStauts, true, user.Player.UID.ToString(), Talent.ToString(), OnJiangMode ? "1" : "2");
	}

	internal void CheckStatus(LegacyNativeGameClientRuntime user)
	{
		if (!(DateTime.Now > ThreadStamp.AddMinutes(1.0)))
		{
			return;
		}
		if (!CheckMode && user.Player.PkMode != LegacyNativePkMode.Jiang)
		{
			CheckMode = true;
			CountDownMode = DateTime.Now.AddMinutes(10.0);
		}
		if (OnJiangMode && user.Player.PkMode != LegacyNativePkMode.Jiang && DateTime.Now > CountDownMode)
		{
			OnJiangMode = false;
			CheckMode = false;
			user.Player.JiangHuActive = 0;
			user.Player.JiangHuTalent = Talent;
			SendInfo(user, LegacyNativeMsgJiangHuInfo.JiangMode.InfoStauts, true, user.Player.UID.ToString(), Talent.ToString(), OnJiangMode ? "1" : "2");
		}
		if (FreeCourse < 1000000)
		{
			if (InTwinCastle(user.Player) && user.Player.PkMode == LegacyNativePkMode.Jiang)
			{
				StartCountDwon = StartCountDwon.AddMinutes(LegacyNativeJianHuTable.GetMinutesInCastle(Talent));
				FreeCourse += LegacyNativeJianHuTable.GetFreeCourseInCastle(Talent);
			}
			else
			{
				FreeCourse += LegacyNativeJianHuTable.GetFreeCourse(Talent);
				StartCountDwon = StartCountDwon.AddMinutes(1.0);
			}
			if (StartCountDwon > CountDownEnd)
			{
				GetReward(user);
			}
			SendInfo(user, LegacyNativeMsgJiangHuInfo.JiangMode.UpdateDateTime, false, FreeCourse.ToString(), Time.ToString());
			if (FreeCourse % 10000 == 0)
			{
				GetReward(user);
			}
		}
		else
		{
			FreeCourse = 1000000u;
		}
		ThreadStamp = DateTime.Now;
	}

	internal void GetReward(LegacyNativeGameClientRuntime user)
	{
		do
		{
			FreeCourse++;
		}
		while (FreeCourse % 1000 != 0);
		SendInfo(user, LegacyNativeMsgJiangHuInfo.JiangMode.UpdateDateTime, false, FreeCourse.ToString(), Time.ToString());
		CreateTime();
	}

	internal void Kill(LegacyNativeGameClientRuntime user, LegacyNativeGameClientRuntime opponent)
	{
		if (opponent.Player.MyJiangHu != null)
		{
			uint num = opponent.Player.MyJiangHu.FreeCourse % 10000;
			if (num != 0)
			{
				uint val = num * 30 / 100;
				FreeCourse = Math.Min(1000000u, val);
				SendInfo(user, LegacyNativeMsgJiangHuInfo.JiangMode.UpdateDateTime, false, FreeCourse.ToString(), Time.ToString());
			}
			if (opponent.Player.MyJiangHu.Talent > 1)
			{
				opponent.Player.MyJiangHu.Talent--;
				Talent = (byte)Math.Min(5, Talent + 1);
				user.Player.MyJiangHu!.SendInfo(user, LegacyNativeMsgJiangHuInfo.JiangMode.UpdateTalent, true, user.Player.UID.ToString(), user.Player.MyJiangHu!.Talent.ToString());
			}
		}
	}

	internal bool InTwinCastle(LegacyNativePlayerRuntime location)
	{
		ushort x = location.X;
		ushort y = location.Y;
		if (location.Map != 1002)
		{
			return false;
		}
		if (x <= 357 && x >= 274 && y <= 277 && y >= 110)
		{
			return true;
		}
		if (x >= 335 && x <= 370 && y <= 235 && y >= 232)
		{
			return true;
		}
		if (x >= 281 && x <= 350 && y >= 268 && y <= 322)
		{
			return true;
		}
		if (x >= 247 && x <= 291 && y >= 200 && y <= 243)
		{
			return true;
		}
		return false;
	}
}
