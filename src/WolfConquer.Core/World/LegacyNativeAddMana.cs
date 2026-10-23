using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Whole native AddMana component. Dispatcher/host acceptance is separately held.
internal class LegacyNativeAddMana
{
	public static void Execute(LegacyNativeGameClientRuntime user, LegacyNativeInteractQuery Attack, LegacyNativePacket stream, Dictionary<ushort, LegacyNativeMagicType.Magic> DBSpells)
	{
		if (!LegacyNativeCanUseSpell.Verified(Attack, user, DBSpells, out var ClientSpell, out var Spell))
		{
			return;
		}
		ushort iD = ClientSpell.ID;
		ushort num = iD;
		if (num == 1195)
		{
			LegacyNativeMsgSpellAnimation msgSpellAnimation = new LegacyNativeMsgSpellAnimation(user.Player.UID, 0u, Attack.X, Attack.Y, ClientSpell.ID, ClientSpell.Level, ClientSpell.UseSpellSoul);
			if (ClientSpell.Level == 2)
			{
				Spell.Damage = 1020f;
			}
			uint num2 = 0u;
			if (user.Player.UID == Attack.OpponentUID)
			{
				num2 = LegacyNativeAttackBase.CalculateHealtDmg(LegacyNativeAttackConversions.UInt32(Spell.Damage), user.Status.MaxMana, user.Player.Mana);
				msgSpellAnimation.Targets.Enqueue(new LegacyNativeMsgSpellAnimation.SpellObj(user.Player.UID, num2, LegacyNativeMsgAttackPacket.AttackEffect.None));
				user.Player.Mana += (ushort)num2;
			}
			LegacyNativeUpdateSpell.CheckUpdate(stream, user, Attack, LegacyNativeAttackConversions.UInt32(Spell.Damage), DBSpells);
			msgSpellAnimation.SetStream(stream);
			msgSpellAnimation.Send(user);
		}
	}
}
