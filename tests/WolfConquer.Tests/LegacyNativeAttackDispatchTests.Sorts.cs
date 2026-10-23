using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackDispatchTests
{
    [Theory]
    [InlineData(100, 1000u, 50f, 150, 50u)]
    [InlineData(100, 1000u, -1f, 1000, 900u)]
    [InlineData(100, 100000u, 65536f, 100, 65536u)]
    [InlineData(65530, 100000u, 10f, 4, 10u)]
    [InlineData(500, 400u, 999f, 500, 0u)]
    [InlineData(400, 400u, 999f, 400, 0u)]
    [InlineData(100, 1000u, float.NaN, 100, 0u)]
    [InlineData(100, 1000u, 4294967808f, 612, 512u)]
    public Task AddManaUsesNativeScalarConversionHealthClampAndUInt16PlayerWrap(
        ushort before, uint maximum, float damage, ushort after, uint animationDamage) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.Mana = before; s.Client.Status.MaxMana = maximum; s.Row.Damage = damage;
        LegacyNativeAddMana.Execute(s.Client, s.Attack, s.Packet, s.Rows);
        Assert.Equal(after, s.Player.Mana); Assert.Equal(damage, s.Row.Damage);
        Assert.Equal(Animation(s, 1195, 0, 30, 40, Target(17, animationDamage)), Assert.Single(s.Queue));
        Assert.Equal(4, s.Packet.Position); Assert.Equal(0, s.ClientSpell.Experience);
    });

    [Fact]
    public Task AddManaLevelTwoMutatesSharedDatabaseDamageBeforeTargetAndProgressionChecks() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Skill(1195, 2); s.Row.Damage = 1; s.Player.Mana = 100; s.Attack.OpponentUID = 999;
        LegacyNativeAddMana.Execute(s.Client, s.Attack, s.Packet, s.Rows);
        Assert.Equal(1020f, s.Row.Damage); Assert.Equal((ushort)100, s.Player.Mana);
        Assert.Equal(Animation(s, 1195, 2, 30, 40), Assert.Single(s.Queue));
    });

    [Fact]
    public Task AddManaFullLoadingSendsManaBeforeActualSpellProgressThenAnimation() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Row.Damage = 50; s.NextLevel(1000); s.Client.FullLoading = true;
        LegacyNativeAddMana.Execute(s.Client, s.Attack, s.Packet, s.Rows);
        byte[][] frames = s.Queue.ToArray(); Assert.Equal(new ushort[] { 2176, 2101, 2239 }, frames.Select(Opcode));
        Assert.Equal((ushort)150, s.Player.Mana); Assert.Equal(50, s.ClientSpell.Experience);
        Assert.Equal(50, NativeSpellTestFixture.ReadSpell(frames[1]).Experience);
        Assert.Equal(Animation(s, 1195, 0, 30, 40, Target(17, 50)), frames[2]);
    });

    [Fact]
    public Task AddManaNonMatchingSkillStillUsesCompleteAdmissionButDoesNotProduceASubstituteAnimation() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Skill(999); s.Row.UseMana = 10;
        LegacyNativeAddMana.Execute(s.Client, s.Attack, s.Packet, s.Rows);
        Assert.Equal((ushort)90, s.Player.Mana); Assert.Empty(s.Queue); Assert.Equal(237, s.Packet.Position);
        s.Native.Storage.ClientSpells.Clear(); s.Row.UseMana = 90;
        LegacyNativeAddMana.Execute(s.Client, s.Attack, s.Packet, s.Rows);
        Assert.Equal((ushort)90, s.Player.Mana); Assert.Empty(s.Queue);
    });

    [Fact]
    public Task AddManaKeepsManaCommitWhenAnimationSerializationFailsAfterWholeRealUpdate() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Row.Damage = 50; LegacyNativePoolClients.GamePoll = null;
        Assert.Throws<NullReferenceException>(() => LegacyNativeAddMana.Execute(s.Client, s.Attack, s.Packet, s.Rows));
        Assert.Equal((ushort)150, s.Player.Mana); Assert.Empty(s.Queue); Assert.Equal(4, s.Packet.Position);
    });

    [Theory]
    [InlineData(100, 1000u, 400, 300u)]
    [InlineData(900, 1000u, 1000, 100u)]
    [InlineData(1000, 1000u, 1000, 0u)]
    [InlineData(2000, 1000u, 2000, 0u)]
    [InlineData(65530, 100000u, 294, 300u)]
    public Task EffectMpDoesNotVerifyOrReadAttackDictionaryAndAlwaysUsesPlayerCoordinates(
        ushort before, uint maximum, ushort after, uint damage) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.Mana = before; s.Client.Status.MaxMana = maximum;
        LegacyNativeEffectMP.Execute(null!, s.Client, s.Packet, null!);
        Assert.Equal(after, s.Player.Mana); Assert.Empty(s.Client.MyProfs!.ClientProf);
        Assert.Equal(Animation(s, 1175, 0, 32, 42, Target(17, damage)), Assert.Single(s.Queue));
    });

    [Theory]
    [InlineData(30f, 30u)] [InlineData(-1f, 4294967295u)]
    [InlineData(4294967808f, 512u)] [InlineData(float.NaN, 0u)] [InlineData(float.PositiveInfinity, 0u)]
    public Task ShieldBlockSetsActualDamageReplacesRealFlagAndPublishesStatusShieldThenAnimation(float damage, uint expected) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Skill(12000, 2); s.Row.Damage = damage; s.Row.Duration = uint.MaxValue;
        Assert.True(s.Player.BitVector.TryAdd((int)Flag.ShieldBlock, 9, false, 0));
        var previous = s.Player.BitVector.ArrayFlags[(int)Flag.ShieldBlock];
        s.Packet.InitWriter();
        LegacyNativeShieldBlock.Execute(s.Client, s.Attack, s.Packet, s.Rows);
        Assert.Equal(expected, s.Player.ShieldBlockDamage);
        var current = s.Player.BitVector.ArrayFlags[(int)Flag.ShieldBlock]; Assert.NotSame(previous, current);
        Assert.Equal(-1, current.Secounds); Assert.True(current.RemoveOnDead); Assert.Equal(9, previous.Secounds);
        byte[][] frames = s.Queue.ToArray(); Assert.Equal(new ushort[] { 2176, 2176, 2239 }, frames.Select(Opcode));
        Assert.Equal(NativeAddFlagTestFixture.Status(Flag.ShieldBlock), frames[0]);
        Assert.Equal(ShieldFrame((uint)Flag.ShieldBlock, uint.MaxValue, expected, 2), frames[1]);
        Assert.Equal(Animation(s, 12000, 2, 30, 40, Target(17, 0)), frames[2]);
        Assert.Equal(0, s.ClientSpell.Experience); Assert.Empty(s.Client.MyProfs!.ClientProf);
    });

    [Fact]
    public Task ShieldStatusPreservesCurrentCallerPrefixBecauseNativeSendUpdateDoesNotResetWriter() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Skill(12000); s.Row.Damage = 7; s.Row.Duration = 9;
        s.Packet.InitWriter(); s.Packet.Write(0x11223344u); s.Packet.Write((ushort)0x5566);
        LegacyNativeShieldBlock.Execute(s.Client, s.Attack, s.Packet, s.Rows);
        byte[][] frames = s.Queue.ToArray(); Assert.Equal(3, frames.Length);
        byte[] clean = ShieldFrame((uint)Flag.ShieldBlock, 9, 7, 0);
        byte[] prefixed = new byte[clean.Length + 6]; clean.CopyTo(prefixed, 0);
        BitConverter.GetBytes((ushort)prefixed.Length).CopyTo(prefixed, 0);
        new byte[] { 0x44, 0x33, 0x22, 0x11, 0x66, 0x55 }.CopyTo(prefixed, 4);
        clean.AsSpan(4).CopyTo(prefixed.AsSpan(10));
        Assert.Equal(prefixed, frames[1]); Assert.Equal(Animation(s, 12000, 0, 30, 40, Target(17, 0)), frames[2]);
    });

    [Fact]
    public Task ShieldAdmissionAndCaughtNullFlagOwnerFailureDoNotCommitDamageOrPublish() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Skill(12000); s.Row.Damage = 7; s.Player.ShieldBlockDamage = 99;
        s.Row.UseMana = 101;
        LegacyNativeShieldBlock.Execute(s.Client, s.Attack, s.Packet, s.Rows);
        Assert.Equal(99u, s.Player.ShieldBlockDamage); Assert.False(s.Player.ContainFlag(Flag.ShieldBlock)); Assert.Empty(s.Queue);
        s.Row.UseMana = 0; s.Player.BitVector = null!;
        // The whole admission method reads the current flag owner first, catches this null
        // owner fault and rejects. ShieldBlock's later mutation is never reached.
        LegacyNativeShieldBlock.Execute(s.Client, s.Attack, s.Packet, s.Rows);
        Assert.Equal(99u, s.Player.ShieldBlockDamage); Assert.Empty(s.Queue);
        Assert.Null(s.Player.BitVector);
    });

    [Theory]
    [InlineData(90, 5.9f, 95)] [InlineData(90, 30f, 100)] [InlineData(90, -1f, 100)]
    [InlineData(90, 256f, 90)] [InlineData(90, 257.75f, 91)] [InlineData(90, float.NaN, 90)]
    [InlineData(90, float.PositiveInfinity, 90)] [InlineData(100, 50f, 100)] [InlineData(150, 50f, 150)]
    public Task BreathFocusUsesVerifiedNativeByteDamageAndCapsOnlyInsideBelowHundredBranch(
        ushort before, float damage, ushort after) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Skill(11960); s.Player.Stamina = before; s.Row.Damage = damage;
        LegacyNativeBreathFocus.Execute(s.Attack, s.Client, s.Packet, s.Rows);
        Assert.Equal(after, s.Player.Stamina); byte[][] frames = s.Queue.ToArray();
        Assert.Equal(before < 100 ? new ushort[] { 2176, 2239 } : new ushort[] { 2239 }, frames.Select(Opcode));
        Assert.Equal(Animation(s, 11960, 0, 32, 42), frames.Last());
    });

    [Fact]
    public Task BreathFocusAlwaysUsesLiteral11960AnimationEvenForOtherAdmittedSkillAndRunsRealTraining() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Skill(999); s.NextLevel(10000); s.Player.Stamina = 100;
        LegacyNativeBreathFocus.Execute(s.Attack, s.Client, s.Packet, s.Rows);
        byte[][] frames = s.Queue.ToArray(); Assert.Equal(new ushort[] { 2101, 2239 }, frames.Select(Opcode));
        Assert.Equal(1000, s.ClientSpell.Experience); Assert.Equal((ushort)999, NativeSpellTestFixture.ReadSpell(frames[0]).ID);
        Assert.Equal(Animation(s, 11960, 0, 32, 42), frames[1]);
    });
}
