using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native method family. General CheckEffects/BloodSpawn/StraightLife remain held.
internal partial class LegacyNativePerfectionEffects
{
	public static bool ToxinEraser(LegacyNativePlayerRuntime Target)
	{
		if (Target.Owner.PerfectionStatus.ToxinEraser > 0.0 && LegacyNativeAttackBase.Rate(Target.Owner.PerfectionStatus.ToxinEraser))
		{
			using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
			{
				LegacyNativePacket stream = recycledPacket.GetStream();
				Target.View.SendView(stream.MsgRefineEffectCreate(new LegacyNativeMsgRefineEffect.RefineEffectProto
				{
					Effect = LegacyNativeMsgRefineEffect.RefineEffects.ToxinEraserLevel,
					Id = Target.UID,
					dwParam = Target.UID
				}), me: true);
			}
			return true;
		}
		return false;
	}

	private static void CalmWind(LegacyNativeGameClientRuntime client)
	{
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		client.Player.View.SendView(stream.MsgRefineEffectCreate(new LegacyNativeMsgRefineEffect.RefineEffectProto
		{
			Effect = LegacyNativeMsgRefineEffect.RefineEffects.CalmWind,
			Id = client.Player.UID,
			dwParam = client.Player.UID
		}), me: true);
	}

	public static void InvisibleArrow(LegacyNativePlayerRuntime player, LegacyNativePlayerRuntime target, ref LegacyNativeMsgSpellAnimation.SpellObj SpellObj)
	{
		if (player.Owner.PerfectionStatus.InvisibleArrow > 0.0 && LegacyNativeAttackBase.Rate(player.Owner.PerfectionStatus.InvisibleArrow))
		{
			using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
			{
				LegacyNativePacket stream = recycledPacket.GetStream();
				target.View.SendView(stream.MsgRefineEffectCreate(new LegacyNativeMsgRefineEffect.RefineEffectProto
				{
					Effect = LegacyNativeMsgRefineEffect.RefineEffects.InvisbleArrow,
					Id = player.UID,
					dwParam = player.UID
				}), me: true);
			}
		}
	}
}
