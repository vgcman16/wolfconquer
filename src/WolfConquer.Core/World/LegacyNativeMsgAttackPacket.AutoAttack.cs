namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgAttackPacket
{
	public static void CreateAutoAtack(LegacyNativeInteractQuery pQuery, LegacyNativeGameClientRuntime client)
	{
		client.OnAutoAttack = true;
		client.AutoAttack = new LegacyNativeInteractQuery();
		client.AutoAttack.AtkType = pQuery.AtkType;
		client.AutoAttack.Damage = pQuery.Damage;
		client.AutoAttack.Effect = pQuery.Effect;
		client.AutoAttack.OpponentUID = pQuery.OpponentUID;
		client.AutoAttack.ResponseDamage = pQuery.ResponseDamage;
		client.AutoAttack.SpellID = pQuery.SpellID;
		client.AutoAttack.SpellLevel = pQuery.SpellLevel;
		client.AutoAttack.UID = pQuery.UID;
		client.AutoAttack.X = pQuery.X;
		client.AutoAttack.Y = pQuery.Y;
	}
}
