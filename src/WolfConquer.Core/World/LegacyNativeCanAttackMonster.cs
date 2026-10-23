using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal class LegacyNativeCanAttackMonster
{
	public static bool Verified(LegacyNativeGameClientRuntime client, LegacyNativeMonsterRole attacked, LegacyNativeMagicType.Magic? DBSpell)
	{
		if ((attacked.Family.Settings & LegacyNativeMonsterSettings.Reviver) == LegacyNativeMonsterSettings.Reviver)
		{
			return false;
		}
		if ((attacked.Family.Settings & LegacyNativeMonsterSettings.Lottus) == LegacyNativeMonsterSettings.Lottus && attacked.OwnerFloor != null && client.Player.UID == attacked.OwnerFloor.Player.UID)
		{
			return false;
		}
		if (!attacked.Alive)
		{
			return false;
		}
		if (attacked.Name!.Contains("[GM]") || attacked.Name!.Contains("[PM]"))
		{
			return false;
		}
		if ((attacked.Family.Settings & LegacyNativeMonsterSettings.Guard) == LegacyNativeMonsterSettings.Guard)
		{
			if (client.Player.PkMode != LegacyNativePkMode.PK)
			{
				return false;
			}
			client.Player.AddFlag(LegacyNativeMsgUpdate.Flags.FlashingName, 30, RemoveOnDead: true);
		}
		return true;
	}
}
