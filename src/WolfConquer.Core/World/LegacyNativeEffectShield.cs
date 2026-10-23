using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Entire native EffectShield owner, no host or attack-dispatch stand-in.
internal class LegacyNativeEffectShield
{
	public static void Execute(LegacyNativeInteractQuery? Attack, LegacyNativeGameClientRuntime user, LegacyNativePacket stream, Dictionary<ushort, LegacyNativeMagicType.Magic>? DBSpells)
	{
		LegacyNativeMsgSpellAnimation msgSpellAnimation = new LegacyNativeMsgSpellAnimation(user.Player.UID, 0u, user.Player.X, user.Player.Y, 1020, 4, 0);
		if (!user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.MagicShield))
		{
			user.Player.AddSpellFlag(LegacyNativeMsgUpdate.Flags.MagicShield, 40, RemoveOnDead: true);
			msgSpellAnimation.Targets.Enqueue(new LegacyNativeMsgSpellAnimation.SpellObj(user.Player.UID, 0u, LegacyNativeMsgAttackPacket.AttackEffect.None));
		}
		msgSpellAnimation.SetStream(stream);
		msgSpellAnimation.Send(user);
	}
}
