using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Whole native ShieldBlock component. Dispatcher/host acceptance is separately held.
internal class LegacyNativeShieldBlock
{
	public static void Execute(LegacyNativeGameClientRuntime user, LegacyNativeInteractQuery Attack, LegacyNativePacket stream, Dictionary<ushort, LegacyNativeMagicType.Magic> DBSpells)
	{
		if (LegacyNativeCanUseSpell.Verified(Attack, user, DBSpells, out var ClientSpell, out var Spell))
		{
			LegacyNativeMsgSpellAnimation msgSpellAnimation = new LegacyNativeMsgSpellAnimation(user.Player.UID, 0u, Attack.X, Attack.Y, ClientSpell.ID, ClientSpell.Level, ClientSpell.UseSpellSoul);
			user.Player.ShieldBlockDamage = LegacyNativeAttackConversions.UInt32(Spell.Damage);
			user.Player.AddSpellFlag(LegacyNativeMsgUpdate.Flags.ShieldBlock, (int)Spell.Duration, RemoveOnDead: true);
			user.Player.SendUpdate(stream, LegacyNativeMsgUpdate.Flags.ShieldBlock, Spell.Duration, LegacyNativeAttackConversions.UInt32(Spell.Damage), ClientSpell.Level, LegacyNativeMsgUpdate.DataType.AzureShield, scren: true);
			msgSpellAnimation.Targets.Enqueue(new LegacyNativeMsgSpellAnimation.SpellObj(user.Player.UID, 0u, LegacyNativeMsgAttackPacket.AttackEffect.None));
			msgSpellAnimation.SetStream(stream);
			msgSpellAnimation.Send(user);
		}
	}
}
