using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMonsterActionHandler
{
	public void SearchTarget(LegacyNativePlayerRuntime? client, LegacyNativeMonsterRole? monster)
	{
		if (monster == null || !monster.Alive)
		{
			return;
		}
		try
		{
			if (monster.Target != null && (!monster.Target!.Alive || monster.Target!.ContainFlag(LegacyNativeMsgUpdate.Flags.Fly) || !monster.Target!.Owner.Socket!.Alive))
			{
				monster.State = LegacyNativeMobStatus.Idle;
			}
			else if (monster.Target == null)
			{
				monster.State = LegacyNativeMobStatus.Idle;
			}
			else
			{
				if (monster.Family == null)
				{
					return;
				}
				short distance = LegacyNativeMonsterView.GetDistance(monster.Target!.X, monster.Target!.Y, monster.X, monster.Y);
				if (distance <= monster.Family.AttackRange || (monster.Boss == 1 && monster.HitPoints > 2000000 && distance <= 18))
				{
					monster.State = LegacyNativeMobStatus.Attacking;
				}
				else
				{
					monster.State = LegacyNativeMobStatus.Idle;
				}
				if (distance <= monster.Family.ViewRange && monster.Target != null && monster.Target!.Alive)
				{
					try
					{
						if (DateTime.Now > monster.MoveStamp.AddMilliseconds(monster.Family.MoveSpeed))
						{
							monster.MoveStamp = DateTime.Now;
							if (Random.Next() % 100 < 70)
							{
								LegacyNativePlayerMapFlags.ConquerAngle angle = GetAngle(monster.X, monster.Y, monster.Target!.X, monster.Target!.Y);
								ushort x = monster.X;
								ushort y = monster.Y;
								IncXY(angle, ref x, ref y);
								LegacyNativeGameMap? map = monster.Target!.Owner.Map;
								if (map != null)
								{
									if (map.ValidLocation(x, y) && !map.MonsterOnTile(x, y))
									{
										map.SetMonsterOnTile(monster.X, monster.Y, Value: false);
										map.SetMonsterOnTile(x, y, Value: true);
										map.View!.MoveTo((ILegacyNativeMapObj)monster, (int)x, (int)y);
										monster.X = x;
										monster.Y = y;
										LegacyNativeWalkQuery pQuery = new LegacyNativeWalkQuery
										{
											Direction = (uint)angle,
											Running = 1u,
											UID = monster.UID,
											MapID = monster.Map
										};
										using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
										LegacyNativePacket stream = recycledPacket.GetStream();
										monster.Send(stream.MovementCreate(pQuery));
										return;
									}
									angle = (LegacyNativePlayerMapFlags.ConquerAngle)(Random.Next() % 8);
									x = monster.X;
									y = monster.Y;
									IncXY(angle, ref x, ref y);
									if (map.ValidLocation(x, y) && !map.MonsterOnTile(x, y))
									{
										map.SetMonsterOnTile(monster.X, monster.Y, Value: false);
										map.SetMonsterOnTile(x, y, Value: true);
										map.View!.MoveTo((ILegacyNativeMapObj)monster, (int)x, (int)y);
										monster.X = x;
										monster.Y = y;
										LegacyNativeWalkQuery pQuery2 = new LegacyNativeWalkQuery
										{
											Direction = (uint)angle,
											Running = 1u,
											UID = monster.UID,
											MapID = monster.Map
										};
										using LegacyNativeRecycledPacket recycledPacket2 = new LegacyNativeRecycledPacket();
										LegacyNativePacket stream2 = recycledPacket2.GetStream();
										monster.Send(stream2.MovementCreate(pQuery2));
										monster.UpdateMonsterView(monster.Target!.View, stream2);
										return;
									}
								}
							}
						}
						return;
					}
					catch
					{
						monster.State = LegacyNativeMobStatus.Idle;
						return;
					}
				}
				monster.State = LegacyNativeMobStatus.Idle;
			}
		}
		catch (Exception ex)
		{
			LegacyNativeConsole.WriteLine(ex.ToString());
		}
	}
}
