using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeCloneTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private static readonly FieldInfo Generators = typeof(LegacyNativeMonsterFamily).GetField("ItemGeneratorLinker", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? oldBin = Bin.GetValue(null), oldGenerators = Generators.GetValue(null);
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime oldReset = LegacyNativePoolRandom.ResetRandom;
        private readonly Random oldFamilyRandom = LegacyNativeMonsterFamily.Random;
        private readonly LegacyNativeCounter oldCloneUid = LegacyNativeClone.CounterUID;
        private readonly LegacyNativeCounter oldMonsterUid = LegacyNativeMobCollection.GenerateUid;
        private readonly LegacyNativeMapDictionary<uint, LegacyNativeGameMap>? oldMaps = LegacyNativePoolMaps.ServerMaps;
        private readonly Dictionary<int, string> oldMapContents = LegacyNativeGameMap.MapContents;
        private readonly Dictionary<uint, LegacyNativeMonsterFamily> oldFamilies = LegacyNativePoolMonsters.MonsterFamilies;
        private readonly ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? oldClients = LegacyNativePoolClients.GamePoll;
        private readonly LegacyNativeMagicType oldMagic = LegacyNativePoolMagic.Magic;
        private readonly Dictionary<ushort, List<ushort>> oldWeapons = LegacyNativePoolMagic.WeaponSpells;
        private readonly LegacyNativeItemTypeRuneMetadata? oldItems = LegacyNativePoolItems.ItemsBase;
        private readonly Dictionary<uint, LegacyNativeNinjaFile.NinjaSprintItem>? oldNinja = LegacyNativeNinjaFile.gouyu_type;
        private readonly List<LegacyNativePacket> packets = [];
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePlayerRuntime Player => Client.Player;
        internal LegacyNativePacket Packet => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;

        internal Scope()
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026,1,1);
            LegacyNativeMonsterFamily.Random = new Random(91);
            Generators.SetValue(null, new Dictionary<uint, LegacyNativeMobItemGenerator>());
            LegacyNativeClone.CounterUID = new(700100); LegacyNativeMobCollection.GenerateUid = new(400000);
            LegacyNativePoolMaps.ServerMaps = new(); LegacyNativeGameMap.MapContents = new();
            LegacyNativePoolMonsters.MonsterFamilies = new(); LegacyNativePoolClients.GamePoll = new();
            LegacyNativePoolMagic.Magic = new(); LegacyNativePoolMagic.WeaponSpells = new();
            LegacyNativePoolItems.ItemsBase = new(); LegacyNativeNinjaFile.gouyu_type = new();
            Native = new(); Client.Equipment = new(Client); Client.Rune = new(Client); Client.MyNinja = new(Client); Client.MyProfs = new(Client);
            Player.Name = "OwnedWolf"; Player.Stamina = 100; Player.Mana = 100; Queue.Clear();
            var bin = new ConcurrentQueue<LegacyNativePacket>();
            for (int i = 0; i < 16; i++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE); packets.Add(packet); bin.Enqueue(packet);
            }
            Bin.SetValue(null, bin);
        }

        internal LegacyNativeGameMap Map()
        {
            var map = new LegacyNativeGameMap(80,80,700) { View = new LegacyNativeMapView(80,80) };
            for (int x=0;x<80;x++) for(int y=0;y<80;y++) map.cells[x,y]=LegacyNativeMapFlagType.Valid;
            LegacyNativePoolMaps.ServerMaps!.Add(700,map); Player.Map=700;Player.X=10;Player.Y=10;map.Enquer(Client);
            return map;
        }

        internal LegacyNativeGameClientRuntime Peer(LegacyNativeGameMap map, Queue<byte[]> queue)
        {
            var peer = NativeSpellTestFixture.CreateOwner(queue); peer.Player.UID=99;peer.Player.Map=700;peer.Player.X=11;peer.Player.Y=10;
            map.Enquer(peer);return peer;
        }

        internal (LegacyNativeInteractQuery Attack, LegacyNativeMsgSpell Spell,
            Dictionary<ushort, LegacyNativeMagicType.Magic> Rows) Spell(ushort id, ushort level)
        {
            var spell=new LegacyNativeMsgSpell { ID=id,Level=level };
            Native.Storage.ClientSpells[id]=spell;
            var rows=new Dictionary<ushort,LegacyNativeMagicType.Magic>();
            for(ushort i=0;i<=level;i++) rows[i]=new(){ID=id,Level=checked((byte)i),Type=LegacyNativeMagicType.MagicSort.CallPet};
            return (new(){UID=Player.UID,SpellID=id,AtkType=24,X=12,Y=13},spell,rows);
        }

        internal void ThunderFamily()
        {
            LegacyNativePoolMonsters.MonsterFamilies[1]=new(){ID=1,Name="Pheasant",MaxAttack=30,MinAttack=10,MaxHealth=500,
                Mesh=100,Level=15,AttackRange=2,DropSpecials=[]};
            LegacyNativePoolMagic.Magic[13190]=new(){{0,new(){ID=13190,Level=0}}};
        }

        internal void DoubleThunderRune()
        {
            Set(Player,"_class",1000u);Set(Player,"_level",(ushort)15);Player.Reborn=2;
            const uint id=4030001;LegacyNativePoolItems.ItemsBase![id]=new(){ID=id,Name="DoubleThunder",RuneSpellID=id};
            var item=new LegacyNativeGameItemRuntime{Uid=id,ITEM_ID=id,Position=101};Assert.True(Client.Rune!.Add(item));
        }

        public void Dispose()
        {
            Bin.SetValue(null,oldBin);foreach(var packet in packets)packet.Dispose();Native.Dispose();
            Generators.SetValue(null,oldGenerators);LegacyNativeMonsterFamily.Random=oldFamilyRandom;
            LegacyNativePoolRandom.GetRandom=oldRandom;LegacyNativePoolRandom.ResetRandom=oldReset;
            LegacyNativeClone.CounterUID=oldCloneUid;LegacyNativeMobCollection.GenerateUid=oldMonsterUid;
            LegacyNativePoolMaps.ServerMaps=oldMaps;LegacyNativeGameMap.MapContents=oldMapContents;
            LegacyNativePoolMonsters.MonsterFamilies=oldFamilies;LegacyNativePoolClients.GamePoll=oldClients;
            LegacyNativePoolMagic.Magic=oldMagic;LegacyNativePoolMagic.WeaponSpells=oldWeapons;
            LegacyNativePoolItems.ItemsBase=oldItems;LegacyNativeNinjaFile.gouyu_type=oldNinja!;
        }
    }

    private static void Set(object owner,string field,object value)=>owner.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,value);
    private static byte[] Bytes(LegacyNativePacket packet)=>NativeSpellTestFixture.Bytes(packet);
    private static ushort Opcode(byte[] frame)=>BitConverter.ToUInt16(frame,2);

    private static Dictionary<int,List<ulong>> Scalars(byte[] frame)
    {
        int offset=4;
        ulong Read(){ulong result=0;int shift=0;while(true){byte value=frame[offset++];result|=(ulong)(value&127)<<shift;if((value&128)==0)return result;shift+=7;Assert.True(shift<=63);}}
        var fields=new Dictionary<int,List<ulong>>();
        while(offset<frame.Length)
        {
            ulong key=Read();int tag=(int)(key>>3),wire=(int)(key&7);
            if(wire==0){if(!fields.TryGetValue(tag,out var values))fields[tag]=values=[];values.Add(Read());}
            else{Assert.Equal(2,wire);int size=checked((int)Read());offset+=size;Assert.InRange(offset,0,frame.Length);}
        }
        return fields;
    }
}
