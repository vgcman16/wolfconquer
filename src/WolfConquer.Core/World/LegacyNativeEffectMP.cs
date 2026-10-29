using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Whole native EffectMP component. Dispatcher/host acceptance is separately held.
internal class LegacyNativeEffectMP
{
	public static void Execute(LegacyNativeInteractQuery Attack, LegacyNativeGameClientRuntime user, LegacyNativePacket stream, Dictionary<ushort, LegacyNativeMagicType.Magic> DBSpells)
	{
		// This whole component has no owned executor/notification plan. The
		// active managed manual-MP path prepares its own Store/CAS transaction.
		if (!LegacyNativeUnboundSpellScope.TryEnter(user, out var unbound)) return;
		using var unboundExecution = unbound;
		LegacyNativeMsgSpellAnimation msgSpellAnimation = new LegacyNativeMsgSpellAnimation(user.Player.UID, 0u, user.Player.X, user.Player.Y, 1175, 0, 0);
		uint num = LegacyNativeAttackBase.CalculateHealtDmg(300u, user.Status.MaxMana, user.Player.Mana);
		user.Player.Mana = checked(user.Player.Mana + num);
		msgSpellAnimation.Targets.Enqueue(new LegacyNativeMsgSpellAnimation.SpellObj(user.Player.UID, num, LegacyNativeMsgAttackPacket.AttackEffect.None));
		msgSpellAnimation.SetStream(stream);
		msgSpellAnimation.Send(user);
	}
}
