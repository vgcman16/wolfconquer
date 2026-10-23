using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
public void IncreaseXPDuration(short seconds)
	{
		if (BitVector.ArrayFlags.ContainsKey((int)OnXPSkill()))
		{
			BitVector.ArrayFlags[(int)OnXPSkill()].Secounds += seconds;
		}
	}

public void UpdateXpSkill()
	{
		if ((UseXPSpell == LegacyNativeMsgUpdate.Flags.Cyclone || UseXPSpell == LegacyNativeMsgUpdate.Flags.Superman) && ContainFlag(UseXPSpell))
		{
			UpdateFlag(UseXPSpell, 1, SetNewTimer: false, 20);
		}
	}

internal void SendScrennXPSkill(ILegacyNativeMapObj obj)
	{
		if (OnXPSkill() != LegacyNativeMsgUpdate.Flags.Normal)
		{
			using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
			{
				LegacyNativePacket stream = recycledPacket.GetStream();
				LegacyNativeInteractQuery pQuery = new LegacyNativeInteractQuery
				{
					UID = UID,
					KilledMonster = 1,
					X = X,
					Y = Y,
					AtkType = 14,
					SpellLevel = KillCounter
				};
				obj.Send(stream.InteractionCreate(pQuery));
			}
		}
	}
}
