using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Jiang = WolfConquer.Core.World.LegacyNativeJiangHu;
using Table = WolfConquer.Core.Data.LegacyNativeJianHuTable;
using Attr = WolfConquer.Core.World.LegacyNativeJiangHu.Stage.AtributesType;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeJiangHuTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Top = typeof(Jiang.JiangHuRanking).GetField("TopRank",BindingFlags.Static|BindingFlags.NonPublic)!;
        private static readonly FieldInfo Gate = typeof(Jiang.JiangHuRanking).GetField("SyncRoot",BindingFlags.Static|BindingFlags.NonPublic)!;
        private readonly object? top = Top.GetValue(null), gate = Gate.GetValue(null);
        private readonly Jiang[]? published = Jiang.JiangHuRanking.TopRank100;
        private readonly ConcurrentDictionary<uint,Jiang> poll = Jiang.Poll;
        private readonly Dictionary<ushort,Table.Atribut> attributes = Table.Atributes;
        private readonly Dictionary<byte,List<byte>> cultivate = Table.CultivateStatus;
        private readonly bool interserver = LegacyNativeServerConfig.IsInterServer;
        private readonly LegacyNativeSafeRandom random = LegacyNativePoolRandom.GetRandom;
        private readonly CultureInfo culture=CultureInfo.CurrentCulture,ui=CultureInfo.CurrentUICulture;

        internal Scope()
        {
            Jiang.Poll=new();
            Table.Atributes=new();
            Table.CultivateStatus=new();
            Top.SetValue(null,new ConcurrentDictionary<uint,Jiang>());
            Gate.SetValue(null,new object());
            Jiang.JiangHuRanking.TopRank100=null;
            LegacyNativeServerConfig.IsInterServer=false;
            LegacyNativePoolRandom.GetRandom=new(71);
            CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture=CultureInfo.InvariantCulture;
        }

        public void Dispose()
        {
            Jiang.Poll=poll;
            Table.Atributes=attributes;
            Table.CultivateStatus=cultivate;
            Top.SetValue(null,top);
            Gate.SetValue(null,gate);
            Jiang.JiangHuRanking.TopRank100=published;
            LegacyNativeServerConfig.IsInterServer=interserver;
            LegacyNativePoolRandom.GetRandom=random;
            CultureInfo.CurrentCulture=culture;
            CultureInfo.CurrentUICulture=ui;
        }
    }

    private static Jiang New(uint uid=41) => new(uid){Name="Hero",CustomizedName="Cultivation"};

    private static uint[] Status(LegacyNativeMsgStatus value) =>
    [value.MaxHitpoints,value.MaxMana,value.MaxAttack,value.MinAttack,value.Defence,value.MagicAttack,
     value.MagicDefence,value.PhysicalDamageIncrease,value.MagicDamageIncrease,value.PhysicalDamageDecrease,
     value.MagicDamageDecrease,value.CriticalStrike,value.SkillCStrike,value.Immunity,value.Breakthrough,value.Counteraction];

    private static Jiang.Stage.Star Star(Jiang value,int stage,int position,Attr type,byte level,ushort power,bool active=false)
    {
        value.ArrayStages[stage].Activate=true;
        var star=value.ArrayStages[stage].ArrayStars[position];
        star.Typ=type;
        star.Level=level;
        star.UID=Table.ValueToRoll((byte)type,level);
        star.Activate=active;
        Table.Atributes[star.UID]=new(){Type=(byte)type,Level=level,Power=power};
        return star;
    }

    private static uint[] SingleStatus(Attr type,uint power)
    {
        var values=new uint[16];
        int[] positions=type switch
        {
            Attr.MaxLife=>[0],Attr.MaxMana=>[1],Attr.PAttack=>[2,3],Attr.PDefense=>[4],
            Attr.MAttack=>[5],Attr.Mdefense=>[6],Attr.FinalAttack=>[7],Attr.FinalMagicAttack=>[8],
            Attr.FinalDefense=>[9],Attr.FinalMagicDefense=>[10],Attr.CriticalStrike=>[11],
            Attr.SkillCriticalStrike=>[12],Attr.Immunity=>[13],Attr.Breakthrough=>[14],Attr.Counteraction=>[15],
            _=>[],
        };
        foreach(int index in positions) values[index]=power;
        return values;
    }

    private static void CheckMode(Jiang value,bool flag) => typeof(Jiang)
        .GetField("CheckMode",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(value,flag);
}
