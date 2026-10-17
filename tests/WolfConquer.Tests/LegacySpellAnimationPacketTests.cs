using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySpellAnimationPacketTests
{
    [Fact]
    public void MonsterMagicSpellAnimationRoundTripsLegacyOpcodeAndCoordinates()
    {
        var packet = LegacySpellAnimationPacket.ForMonsterMagic(
            monsterUid: 408_014_000,
            targetUid: 1_293_600,
            targetX: 672,
            targetY: 720,
            spellId: 3_013,
            damage: 780);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal((ushort)2239, frame.Header.Opcode);
        Assert.Equal(WorldPacketOpcodes.LegacySpellUse, frame.Header.Opcode);
        Assert.True(LegacySpellAnimationPacket.TryParse(frame, out var parsed));
        Assert.Equal(408_014_000U, parsed.AttackerUid);
        Assert.Equal(0U, parsed.AttackedUid);
        Assert.Equal(672, parsed.X);
        Assert.Equal(720, parsed.Y);
        Assert.Equal(3_013, parsed.SpellId);
        Assert.Equal(0, parsed.SpellLevel);
        Assert.Equal(0, parsed.NextSpell);
        Assert.Equal(0, parsed.LevelHu);
        Assert.Equal(0, parsed.SpellEffect);
        Assert.Equal(0, parsed.BombFlag);

        var target = Assert.Single(parsed.Targets);
        Assert.Equal(1_293_600U, target.Uid);
        Assert.Equal(780U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
        Assert.Equal(0, target.NewX);
        Assert.Equal(0, target.NewY);
        Assert.Equal(0U, target.Unknown);
        Assert.Equal(0U, target.TailedBeast);
    }

    [Fact]
    public void TargetedMonsterMagicSpellAnimationRoundTripsLegacyOpcodeAndAttackedUid()
    {
        var packet = LegacySpellAnimationPacket.ForTargetedMonsterMagic(
            monsterUid: 408_014_001,
            targetUid: 1_293_601,
            spellId: 3_014,
            damage: 1_230);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal((ushort)2239, frame.Header.Opcode);
        Assert.Equal(WorldPacketOpcodes.LegacySpellUse, frame.Header.Opcode);
        Assert.True(LegacySpellAnimationPacket.TryParse(frame, out var parsed));
        Assert.Equal(408_014_001U, parsed.AttackerUid);
        Assert.Equal(1_293_601U, parsed.AttackedUid);
        Assert.Equal(0, parsed.X);
        Assert.Equal(0, parsed.Y);
        Assert.Equal(3_014, parsed.SpellId);
        Assert.Equal(0, parsed.SpellLevel);
        Assert.Equal(0, parsed.NextSpell);
        Assert.Equal(0, parsed.LevelHu);
        Assert.Equal(0, parsed.SpellEffect);
        Assert.Equal(0, parsed.BombFlag);

        var target = Assert.Single(parsed.Targets);
        Assert.Equal(1_293_601U, target.Uid);
        Assert.Equal(1_230U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
        Assert.Equal(0, target.NewX);
        Assert.Equal(0, target.NewY);
        Assert.Equal(0U, target.Unknown);
        Assert.Equal(0U, target.TailedBeast);
    }

    [Fact]
    public void SelfTargetedFloorSpellAnimationRoundTripsAttackedUidAndCoordinates()
    {
        var packet = new LegacySpellAnimationPacket(
            AttackerUid: 1_293_600,
            AttackedUid: 1_293_600,
            X: 110,
            Y: 200,
            SpellId: 12_070,
            SpellLevel: 4,
            NextSpell: 0,
            LevelHu: 2,
            SpellEffect: 0,
            Targets: []);

        var parsed = ParseSpellAnimationFrame(packet.ToFrameBytes());

        Assert.Equal(1_293_600U, parsed.AttackerUid);
        Assert.Equal(1_293_600U, parsed.AttackedUid);
        Assert.Equal(110, parsed.X);
        Assert.Equal(200, parsed.Y);
        Assert.Equal(12_070, parsed.SpellId);
        Assert.Equal(4, parsed.SpellLevel);
        Assert.Equal(2, parsed.LevelHu);
        Assert.Empty(parsed.Targets);
    }

    [Fact]
    public void ToFramesKeepsThirtyTargetsInOneLegacyOpcodeFrame()
    {
        var packet = CreateSpellAnimationWithTargets(targetCount: 30);

        var frames = packet.ToFrames();

        var onlyFrameBytes = Assert.Single(frames);
        Assert.Equal(packet.ToFrameBytes(), onlyFrameBytes);

        var parsed = ParseSpellAnimationFrame(onlyFrameBytes);
        AssertCommonChunkFields(parsed);
        Assert.Equal(30, parsed.Targets.Count);
        AssertTargetRange(parsed.Targets, startIndex: 0);
    }

    [Fact]
    public void ToFramesChunksThirtyOneTargetsIntoThirtyAndOneLegacyOpcodeFrames()
    {
        var packet = CreateSpellAnimationWithTargets(targetCount: 31);

        var frames = packet.ToFrames();

        Assert.Equal(2, frames.Count);

        var first = ParseSpellAnimationFrame(frames[0]);
        AssertCommonChunkFields(first);
        Assert.Equal(30, first.Targets.Count);
        AssertTargetRange(first.Targets, startIndex: 0);

        var second = ParseSpellAnimationFrame(frames[1]);
        AssertCommonChunkFields(second);
        Assert.Single(second.Targets);
        AssertTargetRange(second.Targets, startIndex: 30);
    }

    [Fact]
    public void TideTrapMoveLineBombFlagRoundTripsThroughLegacySpellEffectSlot()
    {
        var target = new LegacySpellAnimationTarget(
            Uid: 1_293_650,
            Damage: 2_345,
            Hit: true,
            Effect: MsgAttackEffect.None,
            NewX: 680,
            NewY: 721,
            Unknown: 0,
            TailedBeast: 0);
        var packet = LegacySpellAnimationPacket.ForPlayerMagic(
            playerUid: 100_004,
            x: 680,
            y: 721,
            spellId: 14_250,
            spellLevel: 0,
            levelHu: 0,
            targets: [target],
            bombFlag: 1);

        var parsed = ParseSpellAnimationFrame(packet.ToFrameBytes());

        Assert.Equal(1, parsed.BombFlag);
        Assert.Equal(1, parsed.SpellEffect);
        Assert.Equal(target, Assert.Single(parsed.Targets));
    }

    private static LegacySpellAnimationPacket CreateSpellAnimationWithTargets(int targetCount)
    {
        var targets = Enumerable.Range(0, targetCount)
            .Select(CreateTarget)
            .ToArray();

        return new LegacySpellAnimationPacket(
            AttackerUid: 99_001,
            AttackedUid: 0,
            X: 420,
            Y: 421,
            SpellId: 3_999,
            SpellLevel: 4,
            NextSpell: 22,
            LevelHu: 5,
            SpellEffect: 6,
            targets);
    }

    private static LegacySpellAnimationTarget CreateTarget(int index)
    {
        return new LegacySpellAnimationTarget(
            Uid: checked((uint)(10_000 + index)),
            Damage: checked((uint)(1_000 + index)),
            Hit: index % 2 == 0,
            Effect: index % 2 == 0 ? MsgAttackEffect.None : MsgAttackEffect.Block,
            NewX: checked((ushort)(100 + index)),
            NewY: checked((ushort)(200 + index)),
            Unknown: checked((uint)(300 + index)),
            TailedBeast: checked((uint)(400 + index)));
    }

    private static LegacySpellAnimationPacket ParseSpellAnimationFrame(byte[] bytes)
    {
        var frame = ReadFrame(bytes);
        Assert.Equal((ushort)2239, frame.Header.Opcode);
        Assert.Equal(WorldPacketOpcodes.LegacySpellUse, frame.Header.Opcode);
        Assert.True(LegacySpellAnimationPacket.TryParse(frame, out var parsed));
        return parsed;
    }

    private static void AssertCommonChunkFields(LegacySpellAnimationPacket parsed)
    {
        Assert.Equal(99_001U, parsed.AttackerUid);
        Assert.Equal(0U, parsed.AttackedUid);
        Assert.Equal(420, parsed.X);
        Assert.Equal(421, parsed.Y);
        Assert.Equal(3_999, parsed.SpellId);
        Assert.Equal(4, parsed.SpellLevel);
        Assert.Equal(22, parsed.NextSpell);
        Assert.Equal(5, parsed.LevelHu);
        Assert.Equal(6, parsed.SpellEffect);
    }

    private static void AssertTargetRange(
        IReadOnlyList<LegacySpellAnimationTarget> targets,
        int startIndex)
    {
        for (var index = 0; index < targets.Count; index++)
        {
            Assert.Equal(CreateTarget(startIndex + index), targets[index]);
        }
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
