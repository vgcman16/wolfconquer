using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Effect = WolfConquer.Core.World.LegacyNativeMsgAttackPacket.AttackEffect;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    [Theory]
    [InlineData(0, 1200u)]
    [InlineData(1, 1560u)]
    [InlineData(2, 18000u)]
    [InlineData(3, 5400u)]
    public Task ValidEmptyAndNonemptyStatusGraphsRetainWholePhysicalFormulaAndRngOrder(int mode, uint expected)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s);
        var spell = new LegacyNativeMagicType.Magic { ID = 500, Damage = 100, Passive = false };
        if (mode == 1) Assert.True(s.Player.BitVector.TryAdd((int)Flag.Stigma, 30, true, 0));
        if (mode == 2)
        {
            Assert.True(s.Player.BitVector.TryAdd((int)Flag.Superman, 30, true, 0));
            Assert.True(s.Player.BitVector.TryAdd((int)Flag.FatalStrike, 30, true, 0));
        }
        if (mode == 3)
        {
            Assert.True(s.Player.BitVector.TryAdd((int)Flag.DeathSigh, 30, true, 0));
            Assert.True(s.Player.BitVector.TryAdd((int)Flag.Infinity, 30, true, 0));
            s.Player.DeathSighActive = 125; s.Player.InfinitySDamage = 200;
        }
        var vector = new PhysicalAdmissionVectorSnapshot(s.Player); uint hp = monster.HitPoints;
        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, spell, out var result);
        Assert.Equal(monster.UID, result.UID); Assert.Equal(expected, result.Damage);
        Assert.Equal(Effect.None, result.Effect); Assert.Equal(1u, result.Hit);
        Assert.Equal(hp, monster.HitPoints); Assert.Equal(100f, spell.Damage); Assert.Empty(s.Queue);
        vector.AssertUnchanged(); AssertPhysicalAdmissionFourDraws();
    });

    [Fact]
    public Task ValidAdmissionKeepsLocalSpellFloorSharedRowAndExactLuckyStrikeWire()
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var monster = CombatMonster(s);
        var spell = new LegacyNativeMagicType.Magic { ID = 500, Damage = 5 };
        var rows = new Dictionary<ushort, LegacyNativeMagicType.Magic> { [500] = spell };
        s.Client.PerfectionStatus.LuckyStrike = 100;
        LegacyNativeCalculatePhysical.OnMonster(s.Player, monster, spell, out var result);
        Assert.Equal(240u, result.Damage); Assert.Equal(Effect.LuckyStrike, result.Effect);
        Assert.Same(spell, rows[500]); Assert.Equal(5f, rows[500].Damage);
        // Header10/opcode2319; required protobuf fields Id17, dwParam0, LuckyStrike3.
        Assert.Equal(Convert.FromHexString("0A000F09081110001803"), Assert.Single(s.Queue));
        AssertPhysicalAdmissionFourDraws();
    });

    private static void AssertPhysicalAdmissionFourDraws()
    {
        var expected = new Random(1);
        for (int draw = 0; draw < 4; draw++) _ = expected.Next();
        Assert.Equal(expected.Next(), LegacyNativeAttackBase.MyRandom.Next());
    }
}
