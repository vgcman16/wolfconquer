using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Entire native EffectStigma owner, no host or attack-dispatch stand-in.
internal class LegacyNativeEffectStigma
{
	public static void Execute(LegacyNativeInteractQuery? Attack, LegacyNativeGameClientRuntime user, LegacyNativePacket stream, Dictionary<ushort, LegacyNativeMagicType.Magic>? DBSpells)
	{
		LegacyNativeMsgSpellAnimation msgSpellAnimation = new LegacyNativeMsgSpellAnimation(user.Player.UID, 0u, user.Player.X, user.Player.Y, 1095, 4, 0);
		if (!user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.Stigma))
		{
			// This path observed absence. Refusal must not become a claimed-success animation.
			if (!user.Player.AddFlag(LegacyNativeMsgUpdate.Flags.Stigma, 90, RemoveOnDead: true)) return;
			msgSpellAnimation.Targets.Enqueue(new LegacyNativeMsgSpellAnimation.SpellObj(user.Player.UID, 0u, LegacyNativeMsgAttackPacket.AttackEffect.None));
		}
		msgSpellAnimation.SetStream(stream);
		msgSpellAnimation.Send(user);
	}
}
