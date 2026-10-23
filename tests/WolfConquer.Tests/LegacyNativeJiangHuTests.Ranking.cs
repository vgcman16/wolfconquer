using Jiang = WolfConquer.Core.World.LegacyNativeJiangHu;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeJiangHuTests
{
    [Fact]
    public void ActualRankOwnerSortsUnsignedStrengthTrimsToHundredAndResetsDroppedPhysicalRanks()
    {
        using var scope=new Scope();
        var all=new List<Jiang>();
        for(uint uid=1;uid<=110;uid++)
        {
            var value=New(uid);
            value.Inner_Strength=uid*100;
            all.Add(value);
            Jiang.JiangHuRanking.UpdateRank(value);
        }
        var rank=Jiang.JiangHuRanking.TopRank100!;
        Assert.Equal(100,rank.Length);
        Assert.Equal(Enumerable.Range(11,100).Reverse().Select(i=>(uint)i),rank.Select(value=>value.UID));
        for(int i=0;i<100;i++) Assert.Equal((byte)(i+1),rank[i].Rank);
        Assert.All(all.Take(10),value=>Assert.Equal((byte)0,value.Rank));
        Assert.Equal(110,Jiang.Poll.Count); // Ranking truncation does not remove general owner pool.
        Assert.Same(all[109],rank[0]);
        rank[0].Inner_Strength=0;
        Jiang.JiangHuRanking.UpdateRank(all[10]);
        Assert.Equal(109u,Jiang.JiangHuRanking.TopRank100![0].UID);
        Assert.Equal(110u,Jiang.JiangHuRanking.TopRank100![^1].UID);
    }

    [Fact]
    public void DuplicateUidUpdateDoesNotReplacePhysicalRankedOwnerAndPublishedArrayIsNotBackendStorage()
    {
        using var scope=new Scope();
        var first=New(41);
        first.Inner_Strength=100;
        Jiang.JiangHuRanking.UpdateRank(first);
        var second=New(42);
        second.UID=41;
        second.Inner_Strength=uint.MaxValue;
        second.Rank=77;
        Jiang.JiangHuRanking.UpdateRank(second);
        Assert.Same(first,Assert.Single(Jiang.JiangHuRanking.TopRank100!));
        Assert.Equal((byte)1,first.Rank);
        Assert.Equal((byte)77,second.Rank);
        Jiang.JiangHuRanking.TopRank100![0]=second;
        Jiang.JiangHuRanking.UpdateRank(first);
        Assert.Same(first,Assert.Single(Jiang.JiangHuRanking.TopRank100!));
        Assert.Same(second,Jiang.Poll[42]);
        var published=Jiang.JiangHuRanking.TopRank100;
        Assert.Throws<NullReferenceException>(()=>Jiang.JiangHuRanking.UpdateRank(null!));
        Assert.Same(published,Jiang.JiangHuRanking.TopRank100);
    }
}
