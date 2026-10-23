using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Ranking=WolfConquer.Core.Data.LegacyNativePrestigeRanking;
using Inner=WolfConquer.Core.World.LegacyNativeInnerPowerRuntime;
using InnerTable=WolfConquer.Core.Data.LegacyNativeInnerPowerTable;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeCreatePrestigeTests
{
    private sealed class Fixture:IDisposable
    {
        private readonly LegacyNativeDbSafeDictionary<Ranking.Type,Ranking.Rank> ranks=Ranking.Ranks;
        private readonly object gate=Ranking.SynRoot;
        private readonly LegacyNativeGroupServerList.Server? server=LegacyNativeGroupServerList.MyServerInfo;
        private readonly ConcurrentDictionary<uint,LegacyNativeJiangHu> jiang=LegacyNativeJiangHu.Poll;
        private readonly ConcurrentDictionary<uint,Inner> inner=Inner.InnerPowerPolle;
        private readonly uint count=InnerTable.Count;
        private readonly LegacyNativeItemTypeRuneMetadata? items=LegacyNativePoolItems.ItemsBase;
        private readonly Dictionary<uint,LegacyNativeHundredWeaponsTable.HundredWeapon> weapons=LegacyNativeHundredWeaponsTable.HundredWeaponsList;
        private readonly LegacyNativeSafeRandom random=LegacyNativePoolRandom.GetRandom;
        internal LegacyNativeGameClientRuntime Client{get;}

        internal Fixture()
        {
            Ranking.Ranks=new();Ranking.SynRoot=new();Ranking.Create();
            LegacyNativeGroupServerList.MyServerInfo=new(){ID=2};
            LegacyNativeJiangHu.Poll=new();Inner.InnerPowerPolle=new();InnerTable.Count=0;
            LegacyNativePoolItems.ItemsBase=new();
            LegacyNativeHundredWeaponsTable.HundredWeaponsList=new();
            LegacyNativePoolRandom.GetRandom=new(71);
            Client=new();
            Client.Player.UID=41;Client.Player.ServerID=3;Client.Player.Name="Hero";
            Client.HundredWeapons=new(Client);
            Client.MyWardrobe=new(Client);
            Client.Rune=new(Client);
            Client.Beasts=new(Client);
            Client.Equipment=new(Client);
        }

        public void Dispose()
        {
            Ranking.Ranks=ranks;Ranking.SynRoot=gate;
            LegacyNativeGroupServerList.MyServerInfo=server!;
            LegacyNativeJiangHu.Poll=jiang;Inner.InnerPowerPolle=inner;InnerTable.Count=count;
            LegacyNativePoolItems.ItemsBase=items;
            LegacyNativeHundredWeaponsTable.HundredWeaponsList=weapons;
            LegacyNativePoolRandom.GetRandom=random;
        }
    }

    private static void SavedField(object value,string name,object state)=>value.GetType()
        .GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(value,state);
}
