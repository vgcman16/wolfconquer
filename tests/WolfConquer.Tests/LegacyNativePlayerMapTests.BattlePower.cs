using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Player = WolfConquer.Core.World.LegacyNativePlayerRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerMapTests
{
    [Theory]
    [InlineData(-1, 516)]
    [InlineData(0, 522)]
    [InlineData(2, 522)]
    [InlineData(3, 519)]
    [InlineData(16, 519)]
    [InlineData(17, 517)]
    [InlineData(49, 517)]
    [InlineData(50, 516)]
    public Task MaximumPowerReadsActualNobilityOwnerNotIndependentPlayerRankField(int position, int expected) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Player.Nobility!.Position = position;
        scope.Player.NobilityRank = LegacyNativeNobilityRank.Serf;
        Assert.Equal(expected, scope.Player.MaxBP());
        scope.Player.Nobility = null; scope.Player.NobilityRank = LegacyNativeNobilityRank.King;
        Assert.Equal(0, scope.Player.MaxBP());
    });

    [Fact]
    public Task PowerUsesLiveProfessionBenefitExtraAndEquipmentWhileRealPowerOmitsBenefitAndExtra() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); Set(scope.Player, "_class", 1005u); Set(scope.Player, "_level", (ushort)130);
        scope.Player.Reborn = 2; scope.Player.NobilityRank = LegacyNativeNobilityRank.Earl;
        scope.Client.Equipment!.BattlePower = 100; scope.Player.ExtraBattlePower = 20;
        LegacyNativeProfessionTable.Benefits[1005] = new() { BattlePower = 7 };
        int rank = (int)LegacyNativeNobilityRank.Earl;
        Assert.Equal(267 + rank, scope.Player.BattlePower); Assert.Equal(240 + rank, scope.Player.RealBattlePower);
        LegacyNativeProfessionTable.Benefits.Clear(); Assert.Equal(260 + rank, scope.Player.BattlePower);
        scope.Client.Equipment.BattlePower = 1000;
        Assert.Equal(516, scope.Player.BattlePower); Assert.Equal(516, scope.Player.RealBattlePower);
    });

    [Fact]
    public Task NegativeAndUInt32WrappedPowerAreNotClampedToZeroAndMissingOwnersStillFault() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Client.Equipment!.BattlePower = -20;
        Assert.Equal(-20, scope.Player.BattlePower); Assert.Equal(-20, scope.Player.RealBattlePower);
        scope.Client.Equipment.BattlePower = 0; scope.Player.ExtraBattlePower = uint.MaxValue;
        Assert.Equal(-1, scope.Player.BattlePower); Assert.Equal(0, scope.Player.RealBattlePower);
        scope.Client.Equipment = null;
        Assert.Throws<NullReferenceException>(() => { _ = scope.Player.BattlePower; });
        Assert.Throws<NullReferenceException>(() => { _ = scope.Player.RealBattlePower; });
    });

    [Fact]
    public Task GuildClanAndPrivateMentorSharesReplaceTheirOwnPriorContributionAndUseRealPacketQueue() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var player = scope.Player;
        player.ExtraBattlePower = 10;
        player.ClanBp = 20; Assert.Equal(30u, player.ExtraBattlePower);
        player.ClanBp = 4; Assert.Equal(14u, player.ExtraBattlePower);
        var mentor = typeof(Player).GetProperty("MentorBp", BindingFlags.Instance | BindingFlags.NonPublic)!;
        mentor.SetValue(player, 7u); Assert.Equal(21u, player.ExtraBattlePower);
        mentor.SetValue(player, 2u); Assert.Equal(16u, player.ExtraBattlePower);
        player.GuildBattlePower = 3; Assert.Equal(19u, player.ExtraBattlePower); Assert.Equal(3u, player.GuildBattlePower);
        Assert.Equal("0C00800808111204082C1003", Convert.ToHexString(Assert.Single(scope.Native.Queue)));
        scope.Native.Queue.Clear(); player.GuildBattlePower = 9;
        Assert.Equal(25u, player.ExtraBattlePower); Assert.Equal(9u, player.GuildBattlePower);
        Assert.Equal("0C00800808111204082C1009", Convert.ToHexString(Assert.Single(scope.Native.Queue)));
    });

    [Fact]
    public Task GuildShareMutatesBeforeRecycleFaultAndAllShareSubtractionKeepsUInt32Wrap() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var player = scope.Player;
        player.ExtraBattlePower = 0; Set(player, "_clanbp", 5u); player.ClanBp = 1;
        Assert.Equal(unchecked(0u - 5u + 1u), player.ExtraBattlePower);
        Set(player, "guildBP", 10u); scope.NullBin();
        Assert.Throws<NullReferenceException>(() => player.GuildBattlePower = 3);
        Assert.Equal(unchecked(0u - 5u + 1u - 10u + 3u), player.ExtraBattlePower);
        Assert.Equal(3u, player.GuildBattlePower); Assert.Empty(scope.Native.Queue);
    });
}
