using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native method family. General CheckEffects/BloodSpawn/StraightLife remain held.
internal partial class LegacyNativePerfectionEffects
{
	private static void LightOfStamina(LegacyNativeGameClientRuntime Attacker)
	{
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		if (Attacker.Player.Stamina < 100)
		{
			Attacker.Player.View.SendView(stream.MsgRefineEffectCreate(new LegacyNativeMsgRefineEffect.RefineEffectProto
			{
				Effect = LegacyNativeMsgRefineEffect.RefineEffects.LightOfStamina,
				Id = Attacker.Player.UID,
				dwParam = Attacker.Player.UID
			}), me: true);
			Attacker.Player.Stamina = 100;
			Attacker.Player.SendUpdate(stream, Attacker.Player.Stamina, LegacyNativeMsgUpdate.DataType.Stamina);
		}
	}
}
