using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Entire native ElTabakh.MsgGuild.AddPet; Death is jointly held on RemoveFlag.
internal static partial class LegacyNativeMsgGuild
{
    public static uint UID = 700010u;

    public static void AddPet(LegacyNativeGameClientRuntime user, uint id, uint Flag, ushort X, ushort Y, LegacyNativePacket stream)
    {
        if (!LegacyNativePoolMonsters.MonsterFamilies.TryGetValue(1u, out var value))
        {
            return;
        }
        LegacyNativeMonsterFamily monsterFamily = value.Copy();
        monsterFamily.SpawnX = X;
        monsterFamily.SpawnY = Y;
        monsterFamily.MaxSpawnX = X;
        monsterFamily.MaxSpawnY = Y;
        monsterFamily.MapID = user.Player.Map;
        monsterFamily.SpawnCount = 1;
        LegacyNativeMonsterRole? monsterRole = user.Map!.MonstersColletion.Add(monsterFamily, RemoveOnDead: true, user.Player.DynamicID, justone: true);
        if (monsterRole != null)
        {
            monsterRole.UID = UID;
            UID++;
            monsterRole.Family.ID = id;
            monsterRole.FloorStampTimer = DateTime.Now.AddSeconds(7.0);
            monsterRole.Family.SpellId = 13190u;
            monsterRole.Family.AttackSpeed = 1000;
            monsterRole.Family.Settings = LegacyNativeMonsterSettings.Lottus;
            monsterRole.PetFlag = Flag;
            monsterRole.Boss = 0;
            monsterRole.Level = 140;
            monsterRole.Name = "";
            if (id >= 6639 && id <= 6658)
            {
                monsterRole.Mesh = 138u;
                monsterRole.HitPoints = 10u;
                monsterRole.Family.MaxHealth = 10;
            }
            if (id >= 6659 && id <= 6678)
            {
                monsterRole.Mesh = 137u;
                monsterRole.HitPoints = 10u;
                monsterRole.Family.MaxHealth = 10;
            }
            if (id >= 6679 && id <= 6698)
            {
                monsterRole.Mesh = 848u;
                monsterRole.HitPoints = 10u;
                monsterRole.Family.MaxHealth = 10;
            }
            monsterRole.StampFloorSecounds = 10000;
            monsterRole.FloorStampTimer = DateTime.Now.AddSeconds(1.0);
            monsterRole.OwnerFloor = user;
            user.Map!.View!.EnterMap((ILegacyNativeMapObj)monsterRole);
            user.Send(stream.CreatePetInfo(monsterRole.UID, id, monsterRole.PetFlag, monsterRole.Mesh, 14, monsterRole.X, monsterRole.Y, monsterRole.Name));
            monsterRole.Send(monsterRole.GetArray(stream, view: false, st: false));
            LegacyNativeActionQuery pQuery = new LegacyNativeActionQuery
            {
                ObjId = monsterRole.UID,
                Type = 134,
                PositionX = monsterRole.X,
                PositionY = monsterRole.Y
            };
            user.Player.ListPets.Add(monsterRole.UID, monsterRole);
            user.Player.View!.SendView(stream.ActionCreate(pQuery), me: true);
            user.Map!.GetRandCoord(ref X, ref Y, 5);
        }
    }
}
