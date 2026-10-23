using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
internal void CreateHeavenBlessPacket(LegacyNativePacket stream, bool ResetOnlineTraining)
	{
		if (HeavenBlessing > 0)
		{
			if (ResetOnlineTraining)
			{
				ReceivePointsOnlineTraining = DateTime.Now.AddMinutes(1.0);
				OnlineTrainingTime = DateTime.Now.AddMinutes(10.0);
			}
			AddFlag(LegacyNativeMsgUpdate.Flags.HeavenBlessing, 2592000, RemoveOnDead: false);
			SendUpdate(stream, HeavenBlessing, LegacyNativeMsgUpdate.DataType.HeavensBlessing);
			SendUpdate(stream, 0L, LegacyNativeMsgUpdate.DataType.OnlineTraining);
			if (Map == 601 || Map == 1039)
			{
				SendUpdate(stream, 1L, LegacyNativeMsgUpdate.DataType.OnlineTraining);
			}
			SendString(stream, LegacyNativeMsgStringPacket.StringID.Effect, true, "bless");
		}
	}
}
