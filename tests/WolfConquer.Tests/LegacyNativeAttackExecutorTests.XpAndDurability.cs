using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackExecutorTests
{
    private static readonly Flag[] NativeXpOrder =
    [
        Flag.Cyclone, Flag.Superman, Flag.Oblivion, Flag.FatalStrike, Flag.ShurikenVortex, Flag.ChaintBolt,
        Flag.BlackbeardsRage, Flag.CannonBarrage, Flag.BladeFlurry, Flag.SuperCyclone, Flag.DragonCyclone,
        Flag.Omnipotence, Flag.ManiacDance, Flag.ThunderRampage
    ];

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)] [InlineData(12)] [InlineData(13)]
    public Task WholeXpSkillGetterRetainsAllFourteenNativePrioritiesOnTheActualVector(int first) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        for (int index = first; index < NativeXpOrder.Length; index++) s.Player.BitVector.TryAdd((int)NativeXpOrder[index], 9, false, 0);
        Assert.Equal(NativeXpOrder[first], s.Player.OnXPSkill());
        Assert.Equal(14 - first, s.Player.BitVector.GetFlags().Length); Assert.Empty(s.Queue); Assert.Equal(0, s.Player.CompletedRateDraws);
    });

    [Fact]
    public Task WholeXpGetterDoesNotTreatXpListOrUnrelatedBuffAsAnActiveXpSkillOrRepairNullVector() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); Assert.Equal(Flag.Normal, s.Player.OnXPSkill());
        s.Player.BitVector.TryAdd((int)Flag.XPList, 20, true, 0); s.Player.BitVector.TryAdd((int)Flag.Stigma, 90, true, 0);
        Assert.Equal(Flag.Normal, s.Player.OnXPSkill()); Assert.Equal(2, s.Player.BitVector.GetFlags().Length); Assert.Empty(s.Queue);
        s.Player.BitVector = null!; Assert.Throws<NullReferenceException>(() => s.Player.OnXPSkill());
    });

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public Task SourceDurabilityMethodsConsumeExactlyOnePlayerRateButDoNotInventItemWearOrPackets(bool response, bool rateWins) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); int seed = SeedFor(4, rateWins); s.Player.MyRandom = new Random(seed); var reference = new Random(seed); reference.Next();
        var item = new LegacyNativeGameItemRuntime { Uid = 31, ITEM_ID = 410009, Durability = 999, MaximDurability = 1000 };
        s.Client.Equipment!.ClientItems[31] = item; long before = s.Player.CompletedRateDraws;
        if (response) LegacyNativeCheckItems.RespouseDurability(s.Client); else LegacyNativeCheckItems.AttackDurability(s.Client, null);
        Assert.Equal(before + 1, s.Player.CompletedRateDraws); Assert.Equal(reference.Next(), s.Player.MyRandom.Next());
        Assert.Equal((ushort)999, item.Durability); Assert.Equal((ushort)1000, item.MaximDurability);
        Assert.Same(item, s.Client.Equipment.ClientItems[31]); Assert.Empty(s.Queue);
    });
}
