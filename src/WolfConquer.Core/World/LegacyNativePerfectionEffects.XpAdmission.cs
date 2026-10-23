using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native method family. General CheckEffects/BloodSpawn/StraightLife remain held.
internal partial class LegacyNativePerfectionEffects
{
	private static void MirrorOfSin(LegacyNativePlayerRuntime Target)
	{
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		Target.View.SendView(stream.MsgRefineEffectCreate(new LegacyNativeMsgRefineEffect.RefineEffectProto
		{
			Effect = LegacyNativeMsgRefineEffect.RefineEffects.MirrorOfSin,
			Id = Target.UID,
			dwParam = Target.UID
		}), me: true);
		if (Target.OnXPSkill() == LegacyNativeMsgUpdate.Flags.Normal && !Target.ContainFlag(LegacyNativeMsgUpdate.Flags.XPList))
		{
			Target.AddFlag(LegacyNativeMsgUpdate.Flags.XPList, 20, RemoveOnDead: true);
			Target.SendUpdate(stream, 1L, LegacyNativeMsgUpdate.DataType.XPList);
		}
	}

	private static void DrainingTouch(LegacyNativeGameClientRuntime? client)
	{
	}

	private static void KillingFlash(LegacyNativeGameClientRuntime client)
	{
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		client.Player.View.SendView(stream.MsgRefineEffectCreate(new LegacyNativeMsgRefineEffect.RefineEffectProto
		{
			Effect = LegacyNativeMsgRefineEffect.RefineEffects.KillingFlash,
			Id = client.Player.UID,
			dwParam = client.Player.UID
		}), me: true);
		if (!client.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.XPList) && client.Player.OnXPSkill() == LegacyNativeMsgUpdate.Flags.Normal && LegacyNativeRoleCore.Rate(10))
		{
			client.Player.AddFlag(LegacyNativeMsgUpdate.Flags.XPList, 20, RemoveOnDead: true);
			client.Player.SendUpdate(stream, 1L, LegacyNativeMsgUpdate.DataType.XPList);
		}
	}
}
