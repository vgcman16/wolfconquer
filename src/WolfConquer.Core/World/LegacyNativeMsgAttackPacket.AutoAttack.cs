namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgAttackPacket
{
	public static void CreateAutoAtack(LegacyNativeInteractQuery pQuery, LegacyNativeGameClientRuntime client)
	{
		ArgumentNullException.ThrowIfNull(pQuery);
		ArgumentNullException.ThrowIfNull(client);
		if (client.HasOwnedBasicPhysicalAttachment) return;
		var player = client.Player;
		if (player is null || Monitor.IsEntered(player.SpellAdmissionSyncRoot) && !Monitor.IsEntered(client.SpellAdmissionSyncRoot)) return;
		lock (client.SpellAdmissionSyncRoot)
		lock (player.SpellAdmissionSyncRoot)
		{
			if (client.HasOwnedBasicPhysicalAttachment || !ReferenceEquals(client.Player, player) || !ReferenceEquals(player.Owner, client)) return;
			var prepared = new LegacyNativeInteractQuery
			{
				AtkType = pQuery.AtkType, Damage = pQuery.Damage, Effect = pQuery.Effect, OpponentUID = pQuery.OpponentUID,
				ResponseDamage = pQuery.ResponseDamage, SpellID = pQuery.SpellID, SpellLevel = pQuery.SpellLevel,
				UID = pQuery.UID, X = pQuery.X, Y = pQuery.Y,
			};
			client.OnAutoAttack = true;
			client.AutoAttack = prepared;
		}
	}
}
