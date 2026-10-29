using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Whole native BreathFocus component. Dispatcher/host acceptance is separately held.
internal class LegacyNativeBreathFocus
{
	public static void Execute(LegacyNativeInteractQuery Attack, LegacyNativeGameClientRuntime user, LegacyNativePacket stream, Dictionary<ushort, LegacyNativeMagicType.Magic> DBSpells)
	{
		if (!LegacyNativeUnboundSpellScope.TryEnter(user, out var unbound)) return;
		using var unboundExecution = unbound;
		if (LegacyNativeCanUseSpell.Verified(Attack, user, DBSpells, out var ClientSpell, out var Spell))
		{
			LegacyNativeMsgSpellAnimation msgSpellAnimation = new LegacyNativeMsgSpellAnimation(user.Player.UID, 0u, user.Player.X, user.Player.Y, 11960, ClientSpell.Level, ClientSpell.UseSpellSoul);
			if (user.Player.Stamina < 100)
			{
				LegacyNativePlayerRuntime player = user.Player;
				player.Stamina += LegacyNativeAttackConversions.Byte(Spell.Damage);
				user.Player.Stamina = (ushort)Math.Min((int)user.Player.Stamina, 100);
				user.Player.SendUpdate(stream, user.Player.Stamina, LegacyNativeMsgUpdate.DataType.Stamina);
			}
			LegacyNativeUpdateSpell.CheckUpdate(stream, user, Attack, 1000u, DBSpells);
			msgSpellAnimation.SetStream(stream);
			msgSpellAnimation.Send(user);
		}
	}
}
