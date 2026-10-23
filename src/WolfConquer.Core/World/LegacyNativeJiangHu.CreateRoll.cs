using System.Collections;
using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeJiangHu
{
	internal void CreateRollValue(LegacyNativePacket stream, LegacyNativeGameClientRuntime client, byte mStar, byte mStage)
	{
		Stage stage = ArrayStages[mStage - 1];
		if (mStage == 1)
		{
			stage.Activate = true;
		}
		if (stage.Activate)
		{
			Buckupstar = new BackUpStar();
			Buckupstar!.PositionStar = mStar;
			Buckupstar!.Stage = mStage;
			Buckupstar!.Star = new Stage.Star(mStar - 1);
			Buckupstar!.Star.Activate = true;
			Buckupstar!.Star.Level = CreateStarLevel(client.Player.JingTryngUltra);
			do
			{
				Buckupstar!.Star.Typ = (Stage.AtributesType)Rand.Next(1, 20);
			}
			while (!LegacyNativeJianHuTable.CultivateStatus[mStage].Contains((byte)Buckupstar!.Star.Typ));
			Buckupstar!.Star.UID = ValueToRoll(Buckupstar!.Star.Typ, Buckupstar!.Star.Level);
			client.Send(stream.JiangHuUpdatesCreate(FreeCourse, LegacyNativeMsgJiangHuUpdates.Action.CreateStar, mStar, mStage, Buckupstar!.Star.UID, (byte)FreeTimeToday, RoundBuyPoints));
		}
	}
}
