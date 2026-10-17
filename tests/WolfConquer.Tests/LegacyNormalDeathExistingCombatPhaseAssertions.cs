using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private static int[] AssertExistingNormalDeathCombatPhases(
        IReadOnlyList<byte[]> frames,
        uint uid,
        params (uint Maximum, uint Hitpoints)[] phases)
    {
        // Only these neutral, nontransformed fixtures: three real query pairs,
        // then the separate HP0 setter. Any earlier ordinary receives stay explicit.
        Assert.True(phases.Length >= 4);
        var hp = Enumerable.Range(0, frames.Count).Where(index =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var update) &&
            update.Uid == uid && update.Updates.Any(entry => entry.Type is
                LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints)).ToArray();
        Assert.Equal(phases.Length * 2, hp.Length);
        for (var phase = 0; phase < phases.Length; phase++)
        {
            Assert.Equal((ushort)2176, ReadFrame(frames[hp[phase * 2]]).Header.Opcode);
            Assert.Equal((ushort)2176, ReadFrame(frames[hp[phase * 2 + 1]]).Header.Opcode);
            Assert.Equal(hp[phase * 2] + 1, hp[phase * 2 + 1]);
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[hp[phase * 2]]), out var maximum));
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[hp[phase * 2 + 1]]), out var current));
            Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, phases[phase].Maximum)],
                maximum.Updates);
            Assert.Equal(
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, phases[phase].Maximum),
                 new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, phases[phase].Hitpoints)],
                current.Updates);
        }

        var queryFirstPair = phases.Length - 4;
        var query = SingleFrameIndex(frames, frame => LegacyShowEquipmentPacket.TryParse(frame, out _));
        var status = SingleFrameIndex(frames, frame =>
            LegacyPlayerStatusPacket.TryParse(frame, out var packet) && packet.Uid == uid);
        Assert.True(query < hp[queryFirstPair * 2]);
        Assert.True(hp[queryFirstPair * 2 + 1] < status && status < hp[queryFirstPair * 2 + 2]);
        if (queryFirstPair > 0) Assert.True(hp[queryFirstPair * 2 - 1] < query);

        var talk = SingleFrameIndex(frames, frame =>
            LegacyMsgMessagePacket.TryParse(frame, out var message) &&
            message.Content.Contains(LegacyPlayerDeathState.DeadTalkText));
        var vectors = Enumerable.Range(talk + 1, frames.Count - talk - 1).Where(index =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var update) && update.Uid == uid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag)).ToArray();
        Assert.Equal(2, vectors.Length); // ClearFlags, then AddFlag(Dead); never one final-only vector.
        for (var phase = 0; phase < vectors.Length; phase++)
        {
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[vectors[phase]]), out var vector));
            var entry = Assert.Single(vector.Updates);
            Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, entry.Type);
            Assert.NotNull(entry.Values);
            Assert.Equal(phase == 1, IsLegacyStatusFlagSet(entry.Values!, LegacyPlayerDeathState.DeadStatusFlag));
        }
        Assert.True(talk < vectors[0] && vectors[0] < query);
        Assert.True(hp[^1] < vectors[1]);
        return hp;
    }
}
