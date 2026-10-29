using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Whole native producer, including Thundercloud and existing-clone removal branches.
internal class LegacyNativeCallPet
{
	public static void Execute(LegacyNativeGameClientRuntime user, LegacyNativeInteractQuery Attack, LegacyNativePacket stream, Dictionary<ushort, LegacyNativeMagicType.Magic> DBSpells)
	{
		// Clone removal precedes Verified and is an effect too.
		if (!LegacyNativeUnboundSpellScope.TryEnter(user, out var unbound)) return;
		using var unboundExecution = unbound;
		if (user.Player.MyClones.Count != 0)
		{
			LegacyNativeClone[] array = user.Player.MyClones.ToArray();
			LegacyNativeClone[] array2 = array;
			foreach (LegacyNativeClone clone in array2)
			{
				clone.RemoveThat(user);
			}
			user.Player.MyClones.Clear();
		}
		else
		{
			if (!LegacyNativeCanUseSpell.Verified(Attack, user, DBSpells, out var ClientSpell, out var Spell))
			{
				return;
			}
			switch (ClientSpell.ID)
			{
			case 12840:
			{
				LegacyNativeMsgSpellAnimation msgSpellAnimation2 = new LegacyNativeMsgSpellAnimation(user.Player.UID, 0u, Attack.X, Attack.Y, ClientSpell.ID, ClientSpell.Level, ClientSpell.UseSpellSoul);
				msgSpellAnimation2.SetStream(stream);
				msgSpellAnimation2.Send(user);
				int num = 1;
				byte itemLevel = 0;
				if (user.Rune!.IsEquipped("DoubleThunder", ref itemLevel))
				{
					itemLevel = (byte)(98 + itemLevel * 2);
					num = 2;
				}
				for (int k = 0; k < num; k++)
				{
					if (LegacyNativePoolMonsters.MonsterFamilies.TryGetValue(1u, out var value))
					{
						LegacyNativeMonsterFamily monsterFamily = value.Copy();
						monsterFamily.SpawnX = Attack.X;
						monsterFamily.SpawnY = Attack.Y;
						monsterFamily.MaxSpawnX = (ushort)(Attack.X + 1);
						monsterFamily.MaxSpawnY = (ushort)(Attack.Y + 1);
						monsterFamily.MapID = user.Player.Map;
						monsterFamily.SpawnCount = 1;
						LegacyNativeMonsterRole? monsterRole = user.Map!.MonstersColletion.Add(monsterFamily, RemoveOnDead: true, user.Player.DynamicID, justone: true);
						if (monsterRole == null)
						{
							return;
						}
						monsterRole.Family.ID = 999996641u;
						monsterRole.IsFloor = true;
						monsterRole.FloorStampTimer = DateTime.Now.AddSeconds(7.0);
						monsterRole.Family.SpellId = 13190u;
						monsterRole.Family.AttackSpeed = 1000;
						monsterRole.Family.Settings = LegacyNativeMonsterSettings.Lottus;
						monsterRole.FloorPacket = new LegacyNativeMsgItemPacket();
						monsterRole.FloorPacket.m_UID = monsterRole.UID;
						monsterRole.FloorPacket.m_ID = 3843u;
						monsterRole.FloorPacket.m_X = Attack.X;
						monsterRole.FloorPacket.m_Y = Attack.Y;
						monsterRole.FloorPacket.MaxLife = 2;
						monsterRole.FloorPacket.Life = 14u;
						monsterRole.FloorPacket.DropType = LegacyNativeMsgDropID.Effect;
						monsterRole.FloorPacket.m_Color = 13;
						monsterRole.FloorPacket.ItemOwnerUID = user.Player.UID;
						monsterRole.FloorPacket.GuildID = user.Player.GuildID;
						monsterRole.FloorPacket.FlowerType = 2;
						monsterRole.FloorPacket.Timer = LegacyNativeRoleCore.TqTimer(monsterRole.FloorStampTimer);
						monsterRole.PetFlag = 3u;
						if (Spell.Level >= 5)
						{
							monsterRole.PetFlag = 9u;
						}
						monsterRole.HitPoints = 14u;
						monsterRole.Family.MaxHealth = 14;
						monsterRole.Boss = 1;
						monsterRole.Level = 140;
						monsterRole.Name = "Thundercloud";
						monsterRole.Mesh = 980u;
						monsterRole.StampFloorSecounds = 10000;
						monsterRole.FloorStampTimer = DateTime.Now.AddSeconds(1.0);
						monsterRole.RemoveFloor = DateTime.Now.AddSeconds(25.0);
						monsterRole.DBSpell = LegacyNativePoolMagic.Magic[13190][0];
						monsterRole.OwnerFloor = user;
						user.Map!.View!.EnterMap((ILegacyNativeMapObj)monsterRole);
						user.Send(stream.CreatePetInfo(monsterRole.UID, 4264u, monsterRole.PetFlag, monsterRole.Mesh, 14, monsterRole.X, monsterRole.Y, monsterRole.Name!));
						monsterRole.Send(monsterRole.GetArray(stream, view: false, st: false));
						LegacyNativeActionQuery pQuery2 = new LegacyNativeActionQuery
						{
							ObjId = monsterRole.UID,
							Type = 134,
							PositionX = monsterRole.X,
							PositionY = monsterRole.Y
						};
						user.Player.View!.SendView(stream.ActionCreate(pQuery2), me: true);
						user.Map!.GetRandCoord(ref Attack.X, ref Attack.Y, 5);
					}
				}
				LegacyNativeUpdateSpell.CheckUpdate(stream, user, Attack, 1000u, DBSpells);
				break;
			}
			case 12090:
			{
				LegacyNativeMsgSpellAnimation msgSpellAnimation = new LegacyNativeMsgSpellAnimation(user.Player.UID, 0u, Attack.X, Attack.Y, ClientSpell.ID, ClientSpell.Level, ClientSpell.UseSpellSoul);
				if (ClientSpell.Level >= 4)
				{
					LegacyNativeClone.CreateShadowClone(user.Player, stream);
				}
				else
				{
					LegacyNativeClone.CreateShadowClone(user.Player, 3, stream);
				}
				LegacyNativeClone[] array3 = user.Player.MyClones.ToArray();
				LegacyNativeClone[] array4 = array3;
				foreach (LegacyNativeClone clone2 in array4)
				{
					LegacyNativeActionQuery pQuery = new LegacyNativeActionQuery
					{
						ObjId = clone2.UID,
						Type = 134,
						PositionX = user.Player.X,
						PositionY = user.Player.Y
					};
					user.Player.View!.SendView(stream.ActionCreate(pQuery), me: true);
				}
				LegacyNativeUpdateSpell.CheckUpdate(stream, user, Attack, 1000u, DBSpells);
				msgSpellAnimation.SetStream(stream);
				msgSpellAnimation.Send(user);
				break;
			}
			}
		}
	}
}
