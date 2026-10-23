using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;
using Effect = WolfConquer.Core.World.LegacyNativeMsgAttackPacket.AttackEffect;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    [Theory]
    [InlineData(true, true, 1100u, true)] [InlineData(false, true, 1000u, false)]
    [InlineData(true, false, 1000u, false)]
    public Task DragonhowlUsesActualEquippedGearFlagAndCriticalEffect(bool critical, bool flag, uint expected, bool destroy)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Gear(Gear.TypeID.Dragonhowl, 2000); if (flag) s.Flag(Flag.DragonPierceCrit);
        var obj = new LegacyNativeMsgSpellAnimation.SpellObj(55, 1000, critical ? Effect.CriticalStrike : Effect.None);
        uint independent = 700; s.Client.CombatGear!.AttackEffect(ref obj, null, null, ref independent);
        Assert.Equal(expected, obj.Damage); Assert.Equal(destroy, (obj.Effect & Effect.Destroy) != 0);
        Assert.Equal(700u, independent); Assert.Empty(s.Queue);
    });

    [Theory]
    [InlineData(0, 1450u)] [InlineData(1, 1120u)] [InlineData(2, 1120u)]
    public Task WildPhoenixUsesRealTargetKindAndSeparateDamageReference(int kind, uint expected) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Gear(Gear.TypeID.Taoist5, 14002); s.Flag(Flag.Oblivion | Flag.HawksAmbition);
        s.Client.CombatGear!.SetValue("WildPhoenix", 12345.75);
        ILegacyNativeMapObj target = kind switch { 0 => s.Player, 1 => s.Monster(10),
            _ => new LegacyNativeSobNpcRuntime { ObjType = LegacyNativeMapObjectType.SobNpc } };
        var obj = new LegacyNativeMsgSpellAnimation.SpellObj(55, 1000, Effect.None); uint independent = 1000;
        s.Client.CombatGear.AttackEffect(ref obj, new LegacyNativeMagicType.Magic { ID = 1165 }, target, ref independent);
        Assert.Equal(expected, independent); Assert.Equal(1000u, obj.Damage); Assert.Empty(s.Queue);
    });

    [Theory]
    [InlineData(1165)] [InlineData(1001)] [InlineData(1002)]
    public Task WildPhoenixSupportsEveryExactSpellGateAndAliasedObjectDamage(ushort spell) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Gear(Gear.TypeID.Taoist5, 14002); s.Flag(Flag.Oblivion | Flag.HawksAmbition);
        s.Client.CombatGear!.SetValue("WildPhoenix", -12345.75);
        var obj = new LegacyNativeMsgSpellAnimation.SpellObj(55, 1000, Effect.None);
        s.Client.CombatGear.AttackEffect(ref obj, new LegacyNativeMagicType.Magic { ID = spell }, s.Player, ref obj.Damage);
        Assert.Equal(550u, obj.Damage);
    });

    [Fact]
    public Task WildPhoenixUnsupportedSpellDoesNotReadNullTargetOrMissingValueButAcceptedSpellFaults() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Gear(Gear.TypeID.Taoist5, 14002); s.Flag(Flag.Oblivion | Flag.HawksAmbition);
        var obj = new LegacyNativeMsgSpellAnimation.SpellObj(55, 1000, Effect.None); uint damage = 1000;
        s.Client.CombatGear!.AttackEffect(ref obj, new LegacyNativeMagicType.Magic { ID = 1164 }, null, ref damage);
        Assert.Equal(1000u, damage);
        Assert.Throws<NullReferenceException>(() => s.Client.CombatGear.AttackEffect(ref obj,
            new LegacyNativeMagicType.Magic { ID = 1165 }, null, ref damage));
        Assert.Throws<IndexOutOfRangeException>(() => s.Client.CombatGear.AttackEffect(ref obj,
            new LegacyNativeMagicType.Magic { ID = 1165 }, s.Player, ref damage));
        Assert.Equal(1000u, damage); Assert.Empty(s.Queue);
    });

    [Theory]
    [InlineData(50.0, 950u, 50.0, 3, true)]
    [InlineData(200.0, 800u, 0.0, 2, false)]
    [InlineData(500.0, 800u, 300.0, 2, false)]
    public Task SubstitutionKeepsBothRealPacketBranchesAndNativeStalePoolOnDepletion(double pool, uint expected,
        double remaining, int packets, bool addedFlag) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Flag(Flag.FireAura | Flag.SongofPhoenix);
        s.Client.CombatGear!.SetValue("Substitution", pool);
        var obj = new LegacyNativeMsgSpellAnimation.SpellObj(55, 1000, Effect.None);
        var attacker = s.Peer(100, 100, 2000, 0); attacker.UID = 91;
        s.Client.CombatGear.Substitution(ref obj, attacker);
        Assert.Equal(expected, obj.Damage); Assert.Equal(remaining, s.Client.CombatGear.GetValue("Substitution")[0]);
        Assert.Equal(addedFlag, s.Player.ContainFlag(Flag.EartAura | Flag.HawksAmbition));
        Assert.Equal(packets, s.Queue.Count);
        var frames = s.Queue.ToArray(); Assert.Equal((ushort)2176, BitConverter.ToUInt16(frames[0], 2));
        Assert.Equal((ushort)2135, BitConverter.ToUInt16(frames[^1], 2));
        if (addedFlag)
        {
            var flag = s.Player.BitVector.ArrayFlags[(int)(Flag.EartAura | Flag.HawksAmbition)];
            Assert.Equal(10, flag.Secounds); Assert.True(flag.RemoveOnDead);
        }
        using var packet = new WolfConquer.Core.Networking.LegacyNativePacket(frames[^1]); packet.Seek(4);
        var query = packet.ProtoBufferDeserialize(new LegacyNativeInteractQuery());
        Assert.Equal((ushort)55, query.AtkType); Assert.Equal(91u, query.UID); Assert.Equal(s.Player.UID, query.OpponentUID);
        Assert.Equal(addedFlag ? 50 : 200, query.Damage);
    });

    [Fact]
    public Task SubstitutionRecyclesBeforeFlagGateAndPreservesPostValueFaultPrefix() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var obj = new LegacyNativeMsgSpellAnimation.SpellObj(55, 1000, Effect.None);
        s.Client.CombatGear!.Substitution(ref obj, null!); Assert.Equal(1000u, obj.Damage); Assert.Empty(s.Queue);
        s.Flag(Flag.FireAura | Flag.SongofPhoenix);
        Assert.Throws<IndexOutOfRangeException>(() => s.Client.CombatGear.Substitution(ref obj, null!));
        s.Client.CombatGear.SetValue("Substitution", 500);
        Assert.Throws<NullReferenceException>(() => s.Client.CombatGear.Substitution(ref obj, null!));
        Assert.Equal(300.0, s.Client.CombatGear.GetValue("Substitution")[0]); Assert.Equal(1000u, obj.Damage);
        Assert.Single(s.Queue);
        s.NullBin(); s.Player.BitVector.TryRemove((int)(Flag.FireAura | Flag.SongofPhoenix));
        Assert.Throws<NullReferenceException>(() => s.Client.CombatGear.Substitution(ref obj, null!));
    });
}
