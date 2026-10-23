using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
internal void AddHeavenBlessing(LegacyNativePacket stream, int Time)
	{
		if (!ContainFlag(LegacyNativeMsgUpdate.Flags.HeavenBlessing))
		{
			HeavenBlessTime = DateTime.Now;
		}
		if (Time > 86400)
		{
			Owner.SendSysMesage("You`ve received " + Time / 86400 + " days` blessing time.", LegacyNativeMsgMessage.ChatMode.Talk);
		}
		else
		{
			Owner.SendSysMesage("You`ve received " + Time / 60 / 60 + " hours` blessing time.", LegacyNativeMsgMessage.ChatMode.Talk);
		}
		bool resetOnlineTraining = HeavenBlessing == 0;
		HeavenBlessTime = HeavenBlessTime.AddSeconds(Time);
		HeavenBlessing += Time;
		CreateHeavenBlessPacket(stream, resetOnlineTraining);
		if (MyMentor != null)
		{
			MyMentor.Mentor_Blessing += (uint)(Time / 10000);
			if (MyMentor.Associat.ContainsKey(5) && MyMentor.Associat[5].TryGetValue(UID, out var value))
			{
				value.Blessing += (uint)(Time / 10000);
			}
		}
	}
}
