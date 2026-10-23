using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native method family. General CheckEffects/BloodSpawn/StraightLife remain held.
internal partial class LegacyNativePerfectionEffects
{
	private static void DivineGuard(LegacyNativeGameClientRuntime client)
	{
		client.Player.LastDivineGuard = DateTime.Now.AddMinutes(10.0);
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		client.Player.View.SendView(stream.MsgRefineEffectCreate(new LegacyNativeMsgRefineEffect.RefineEffectProto
		{
			Effect = LegacyNativeMsgRefineEffect.RefineEffects.DivineGuard,
			Id = client.Player.UID,
			dwParam = client.Player.UID
		}), me: true);
		if (!client.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.DivineGuard) && LegacyNativeRoleCore.Rate(5))
		{
			client.Player.AddFlag(LegacyNativeMsgUpdate.Flags.DivineGuard, 15, RemoveOnDead: true);
			client.Player.SendUpdate(stream, LegacyNativeMsgUpdate.Flags.DivineGuard, 15u, 0u, 0u, LegacyNativeMsgUpdate.DataType.AzureShield);
		}
	}
}
