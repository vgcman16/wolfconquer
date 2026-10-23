using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMonsterDeathSupportTests
{
    [Fact]
    public Task RewardAndFurnaceSlotsAreActualIndependentUInt32PlayerState() => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets(); var player = p.Client.Player;
        Assert.Equal(0u, player.QuestMultiple); Assert.Equal(0u, player.KillersDisCity); Assert.Equal(0u, player.BattleFieldPoints);
        Assert.Equal(0u, player.DragonFurnace); Assert.Equal(0u, player.DragonFurnaceForge); Assert.Equal(0u, player.DragonFurnaceForgeCount);
        player.QuestMultiple = 2; player.KillersDisCity = uint.MaxValue; player.BattleFieldPoints = 3;
        player.DragonFurnace = 3007110; player.DragonFurnaceForge = 3007119; player.DragonFurnaceForgeCount = 7;
        var other = p.Peer(18, 64, 64, new Queue<byte[]>()).Player;
        Assert.Equal(0u, other.KillersDisCity); Assert.Equal(0u, other.DragonFurnace);
        Assert.Equal(2u, player.QuestMultiple); Assert.Equal(uint.MaxValue, player.KillersDisCity); Assert.Equal(3u, player.BattleFieldPoints);
        Assert.Equal(3007110u, player.DragonFurnace); Assert.Equal(3007119u, player.DragonFurnaceForge); Assert.Equal(7u, player.DragonFurnaceForgeCount);
        Assert.Empty(p.Queue);
    });
    [Fact]
    public Task FerentPillReadsActualPoisonedMembershipAndDoesNotExpireItOrUseBitmapAlone() => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets(); var player = p.Client.Player; int poisoned = (int)LegacyNativeMsgUpdate.Flags.Poisoned;
        Assert.False(player.OnFerentPill);
        player.BitVector.Add(poisoned); Assert.False(player.OnFerentPill); // Actual predicate uses ArrayFlags.
        player.BitVector.Remove(poisoned);
        Assert.True(player.BitVector.TryAdd(poisoned, 0, false, 0));
        player.BitVector.ArrayFlags[poisoned].Timer = DateTime.Now.AddDays(-1);
        Assert.True(player.OnFerentPill); Assert.Single(player.BitVector.ArrayFlags);
        Assert.True(player.BitVector.TryRemove(poisoned)); Assert.False(player.OnFerentPill); Assert.Empty(p.Queue);
    });
    [Fact]
    public Task FerentPillDoesNotConvertMissingPhysicalFlagOwnerIntoFalse() => NativePacketTestThread.Run(() =>
    {
        using var p = new Packets(); p.Client.Player.BitVector = null!;
        Assert.Throws<NullReferenceException>(() => _ = p.Client.Player.OnFerentPill); Assert.Empty(p.Queue);
    });
}
