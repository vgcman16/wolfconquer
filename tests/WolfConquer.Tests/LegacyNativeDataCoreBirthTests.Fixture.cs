using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeDataCoreBirthTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin=typeof(LegacyNativePacketRecycle).GetField("bin",BindingFlags.Static|BindingFlags.NonPublic)!;
        private readonly object? oldBin=Bin.GetValue(null);
        private readonly LegacyNativeSafeRandom oldRandom=LegacyNativePoolRandom.GetRandom;
        private readonly DateTime oldReset=LegacyNativePoolRandom.ResetRandom;
        private readonly ConcurrentDictionary<uint,LegacyNativeArenaUser> oldArena=LegacyNativeArenaRankings.ArenaPoll;
        private readonly ConcurrentDictionary<uint,LegacyNativeTeamArenaUser> oldTeamArena=LegacyNativeTeamArenaRankings.ArenaPoll;
        private readonly ConcurrentDictionary<uint,LegacyNativeAssociateRuntime.MyAsociats> oldAssociates=LegacyNativeAssociateRuntime.Associates;
        private readonly ConcurrentDictionary<uint,LegacyNativeFlowersRuntime> oldFlowers=LegacyNativeFlowersRuntime.ClientPoll;
        private readonly ConcurrentDictionary<uint,LegacyNativeChiRuntime> oldChi=LegacyNativeChiRuntime.ChiPool;
        private readonly List<LegacyNativePacket> packets=[];
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client=>Native.Owner;
        internal LegacyNativePlayerRuntime Player=>Client.Player;
        internal LegacyNativePacket Packet=>Native.Packet;
        internal Queue<byte[]> Queue=>Native.Queue;

        internal Scope()
        {
            LegacyNativePoolRandom.GetRandom=new LegacyNativeSafeRandom(97);
            LegacyNativePoolRandom.ResetRandom=new DateTime(2026,1,1);
            LegacyNativeArenaRankings.ArenaPoll=new();LegacyNativeTeamArenaRankings.ArenaPoll=new();
            LegacyNativeAssociateRuntime.Associates=new();LegacyNativeFlowersRuntime.ClientPoll=new();LegacyNativeChiRuntime.ChiPool=new();
            Native=new();var bin=new ConcurrentQueue<LegacyNativePacket>();
            for(int i=0;i<8;i++){var packet=new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);packets.Add(packet);bin.Enqueue(packet);}
            Bin.SetValue(null,bin);
        }
        public void Dispose()
        {
            Bin.SetValue(null,oldBin);foreach(var packet in packets)packet.Dispose();Native.Dispose();
            LegacyNativePoolRandom.GetRandom=oldRandom;LegacyNativePoolRandom.ResetRandom=oldReset;
            LegacyNativeArenaRankings.ArenaPoll=oldArena;LegacyNativeTeamArenaRankings.ArenaPoll=oldTeamArena;
            LegacyNativeAssociateRuntime.Associates=oldAssociates;LegacyNativeFlowersRuntime.ClientPoll=oldFlowers;LegacyNativeChiRuntime.ChiPool=oldChi;
        }
    }

    private static object? Owner(object owner)=>owner.GetType().GetField("Owner",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner);
    private static uint[] AchievementBits(LegacyNativeClientAchievement achievement)
    {
        var vector=(LegacyNativeBitVector32)typeof(LegacyNativeClientAchievement).GetField("BitVector32",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(achievement)!;
        return vector.bits;
    }
    private static object[] Members(LegacyNativeGameClientRuntime client)=>
        [client.Rune!,client.Inventory!,client.Equipment!,client.Warehouse!,client.MyProfs!,client.MySpells!,client.MyMail!];
    private static void AssertRealOwners(LegacyNativeGameClientRuntime client)
    {
        Assert.Same(client,client.Rune!.Owner);Assert.Same(client,client.Inventory!.Owner);Assert.Same(client,Owner(client.Equipment!));
        Assert.Same(client,client.Warehouse!.User);Assert.Same(client,Owner(client.MyProfs!));Assert.Same(client,Owner(client.MySpells!));Assert.Same(client,Owner(client.MyMail!));
        Assert.Empty(client.Rune.Objects);Assert.Empty(client.Inventory.ClientItems);Assert.Empty(client.Equipment!.ClientItems);
        Assert.Empty(client.Warehouse.ClientItems);Assert.Empty(client.MyProfs!.ClientProf);Assert.Empty(client.MySpells!.ClientSpells);Assert.Empty(client.MyMail!.Mails);
    }
    private static void SetClass(LegacyNativePlayerRuntime player,uint value)=>typeof(LegacyNativePlayerRuntime).GetField("_class",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(player,value);
}
