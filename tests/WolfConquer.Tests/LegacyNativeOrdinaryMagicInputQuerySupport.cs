using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private static async Task<WorldSession> PrepareNativeOrdinaryMagicInputQueryAsync(
        WorldPacketHandlerContext context, Func<byte[][]> frames,
        uint expectedMagicAttack, uint expectedMaximumHitpoints, uint expectedMaximumMana,
        uint expectedHitpointIncrease = 0)
    {
        var sessions = Assert.IsType<WorldSessionStore>(context.Sessions);
        Assert.True(sessions.TryGet(context.Connection, out var before));
        Assert.NotNull(before);
        var uid = before.Character.Uid;
        var hitpoints = before.Character.MinHitPoints;
        var mana = before.Character.MinMana;
        var begin = frames().Length;
        await PacketHandlers.HandleAsync(context with { Session = before },
            ReadFrame(new MsgActionPacket { ObjId = checked((uint)uid),
                Type = (MsgActionType)251 }.ToFrameBytes()));
        var bytes = Assert.Single(frames().Skip(begin),
            frame => LegacyPlayerStatusPacket.TryParse(ReadFrame(frame), out _));
        Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var status));
        Assert.Equal(checked((uint)uid), status.Uid);
        Assert.Equal(expectedMagicAttack, status.MagicAttack);
        Assert.Equal(expectedMaximumHitpoints, status.MaxHP);
        Assert.Equal(expectedMaximumMana, status.MaxMana);
        Assert.True(sessions.TryGet(context.Connection, out var current));
        Assert.NotNull(current);
        Assert.Equal(uid, current.Character.Uid);
        Assert.Equal(checked(hitpoints + expectedHitpointIncrease), current.Character.MinHitPoints);
        Assert.Equal(mana, current.Character.MinMana);
        return current;
    }
}
