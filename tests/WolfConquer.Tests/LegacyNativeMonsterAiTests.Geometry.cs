using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;
using Angle=WolfConquer.Core.World.LegacyNativePlayerMapFlags.ConquerAngle;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMonsterAiTests
{
    [Theory]
    [InlineData(100,100,100,100,6)] [InlineData(100,100,101,100,6)] [InlineData(100,100,99,100,2)]
    [InlineData(100,100,100,101,0)] [InlineData(100,100,100,99,4)]
    [InlineData(100,100,101,101,7)] [InlineData(100,100,99,101,1)] [InlineData(100,100,99,99,3)] [InlineData(100,100,101,99,5)]
    [InlineData(0,0,65535,0,6)] [InlineData(65535,0,0,0,2)] [InlineData(0,65535,0,0,4)] [InlineData(0,0,0,65535,0)]
    public void WholeAngleUsesFrameworkObservedLiteralsNotIdealizedMovementDirections(ushort x,ushort y,ushort xx,ushort yy,byte expected)
    {
        Assert.Equal(expected,(byte)new LegacyNativeMonsterActionHandler().GetAngle(x,y,xx,yy));
    }

    [Fact]
    public void WholeModernAngleMatchesEveryOwnedFrameworkProbeVectorAndDigest()
    {
        var ai=new LegacyNativeMonsterActionHandler();uint digest=2166136261;int vectors=0;
        for(int dx=-64;dx<=64;dx++)for(int dy=-64;dy<=64;dy++)
        {
            byte value=(byte)ai.GetAngle(100,100,(ushort)(100+dx),(ushort)(100+dy));digest=unchecked((digest^value)*16777619u);vectors++;
        }
        ushort[] values=[0,1,2,32767,32768,65533,65534,65535];
        foreach(ushort x in values)foreach(ushort y in values){byte value=(byte)ai.GetAngle(x,y,y,x);digest=unchecked((digest^value)*16777619u);vectors++;}
        Assert.Equal(16705,vectors);Assert.Equal(2201143353u,digest);
    }

    [Theory]
    [InlineData(0,10,11)] [InlineData(1,9,11)] [InlineData(2,9,10)] [InlineData(3,9,9)] [InlineData(4,10,9)] [InlineData(5,11,9)] [InlineData(6,11,10)] [InlineData(7,11,11)] [InlineData(8,10,10)] [InlineData(255,10,10)]
    public void IncXYKeepsEveryOriginalIsometricDirectionAndUnknownNoMove(byte facing,ushort xx,ushort yy)
    {
        ushort x=10,y=10;LegacyNativeMonsterActionHandler.IncXY((Angle)facing,ref x,ref y);Assert.Equal(xx,x);Assert.Equal(yy,y);
    }

    [Fact]
    public void IncXYKeepsUncheckedUshortBorders()
    {
        ushort x=0,y=0;LegacyNativeMonsterActionHandler.IncXY(Angle.North,ref x,ref y);Assert.Equal(ushort.MaxValue,x);Assert.Equal(ushort.MaxValue,y);
        LegacyNativeMonsterActionHandler.IncXY(Angle.South,ref x,ref y);Assert.Equal((ushort)0,x);Assert.Equal((ushort)0,y);
    }

    [Theory]
    [InlineData(300000,false)] [InlineData(300001,true)] [InlineData(6999999,true)] [InlineData(7000000,false)] [InlineData(-1,false)]
    public Task ExtraBossDependsOnHealthIntervalNotBossByte(int maxHp,bool expected)=>NativePacketTestThread.Run(()=>
    {
        using var s=new Scope();var role=s.Monster();role.Family.MaxHealth=maxHp;role.Boss=0;Assert.Equal(expected,s.Ai.ExtraBoss(role));role.Boss=9;Assert.Equal(expected,s.Ai.ExtraBoss(role));
    });

    [Theory]
    [InlineData(1u,0u)] [InlineData(1u,41u)] [InlineData(1u,42u)] [InlineData(1u,150u)] [InlineData(7u,60u)] [InlineData(91u,4294967295u)]
    public void CheckDodgeUsesItsDistinctMsRandomAndConditionalSecondDraw(uint seed,uint dodge)
    {
        var ai=new LegacyNativeMonsterActionHandler{Random=new LegacyNativeMSRandom(seed)};uint control=seed;uint roll=(uint)MsNext(ref control)%150;if(roll>60)roll=(uint)MsNext(ref control)%150;
        Assert.Equal(roll>=dodge,ai.CheckDodge(dodge));Assert.Equal(MsNext(ref control),ai.Random.Next());
    }

    [Theory]
    [InlineData(200,15,185u)] [InlineData(15,15,1u)] [InlineData(0,15,1u)] [InlineData(-1,15,4294967280u)]
    public Task FullMonsterMagicAttackKeepsIntegralDefenseAndNegativeAttackWrap(int attack,ushort defense,uint expected)=>NativePacketTestThread.Run(()=>
    {
        using var s=new Scope();var attacker=s.Monster();var target=s.Monster();attacker.Family.MaxAttack=attack;target.Family.Defense=defense;Assert.Equal(expected,s.Ai.MagicAttack(target,attacker));
    });

    [Theory]
    [InlineData(100u,10u,90u)] [InlineData(100u,101u,4294967295u)] [InlineData(0u,4294967295u,0u)] [InlineData(4294967295u,100u,4252017624u)]
    public void DecreaseBlessPreservesUIntMultiplicationOverflowAndSubtraction(uint damage,uint bless,uint expected)
    {
        Assert.Equal(expected,new LegacyNativeMonsterActionHandler().DecreaseBless(damage,bless));
    }
}
