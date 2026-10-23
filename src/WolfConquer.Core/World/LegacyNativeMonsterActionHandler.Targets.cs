using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMonsterActionHandler
{
	public void CheckTarget(LegacyNativePlayerRuntime client, LegacyNativeMonsterRole? monster)
	{
		if (monster == null || !monster.Alive)
		{
			return;
		}
		if (monster.Target == null && !client.ContainFlag(LegacyNativeMsgUpdate.Flags.Fly))
		{
			short distance = LegacyNativeMonsterView.GetDistance(client.X, client.Y, monster.X, monster.Y);
			if (distance > monster.Family.ViewRange || !client.Alive)
			{
				return;
			}
			ILegacyNativeMapObj? target = monster.View.GetTarget(client.Owner.Map!, LegacyNativeMapObjectType.Player);
			if (target == null)
			{
				return;
			}
			monster.Target = target as LegacyNativePlayerRuntime;
			if (monster.Target == null)
			{
				return;
			}
			if (!monster.Target!.ContainFlag(LegacyNativeMsgUpdate.Flags.Fly))
			{
				monster.State = LegacyNativeMobStatus.SearchTarget;
				return;
			}
			{
				foreach (ILegacyNativeMapObj item in monster.View.Roles(client.Owner.Map!, LegacyNativeMapObjectType.Player))
				{
					LegacyNativePlayerRuntime? player = item as LegacyNativePlayerRuntime;
					if (!player!.ContainFlag(LegacyNativeMsgUpdate.Flags.Fly))
					{
						monster.Target = player;
						monster.State = LegacyNativeMobStatus.SearchTarget;
					}
				}
				return;
			}
		}
		short distance2 = LegacyNativeMonsterView.GetDistance(client.X, client.Y, monster.X, monster.Y);
		if (monster.Target == null)
		{
			ILegacyNativeMapObj? target2 = monster.View.GetTarget(client.Owner.Map!, LegacyNativeMapObjectType.Player);
			if (target2 != null)
			{
				monster.Target = target2 as LegacyNativePlayerRuntime;
				if (!monster.Target!.ContainFlag(LegacyNativeMsgUpdate.Flags.Fly) || monster.Boss == 1)
				{
					monster.State = LegacyNativeMobStatus.SearchTarget;
				}
				else
				{
					foreach (ILegacyNativeMapObj item2 in monster.View.Roles(client.Owner.Map!, LegacyNativeMapObjectType.Player))
					{
						LegacyNativePlayerRuntime? player2 = item2 as LegacyNativePlayerRuntime;
						if (!player2!.ContainFlag(LegacyNativeMsgUpdate.Flags.Fly))
						{
							monster.Target = player2;
							monster.State = LegacyNativeMobStatus.SearchTarget;
						}
					}
				}
			}
		}
		if (monster.Target != null && (distance2 > monster.Family.ViewRange || monster.Target!.ContainFlag(LegacyNativeMsgUpdate.Flags.Fly) || monster.Target!.Owner.Socket == null || (monster.Target!.Owner.Socket != null && !monster.Target!.Owner.Socket.Alive)))
		{
			monster.Target = null;
		}
		else if (monster.Target == null || monster.Target!.Alive)
		{
			monster.State = LegacyNativeMobStatus.SearchTarget;
		}
	}
}
