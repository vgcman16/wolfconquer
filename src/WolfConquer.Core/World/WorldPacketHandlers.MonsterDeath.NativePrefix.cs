using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask PublishLegacyFamily8418DeathPrefixAsync(
        WorldPacketHandlerContext context, WorldSession session,
        LegacyMonsterInstance monster, CancellationToken cancellationToken)
    {
        var states = context.MonsterStates ??
            throw new InvalidOperationException("The native death prefix requires its real monster state owner.");
        var now = (context.TimeProvider ?? TimeProvider.System).GetUtcNow();
        if (states.BeginLegacyFamily8418DeathPrefix(monster, now))
        {
            await SendLegacyFamily8418DeathPrefixFrameAsync(
                context, session, monster, LegacyBlackspotPacket.CreateInactive(monster.Uid).ToFrameBytes(),
                cancellationToken).ConfigureAwait(false);
        }

        // ClearFlags() has SendScreem=false by default: clear the owner, emit NO clear packet.
        // Native StatusFlagsBigVector32(320) owns TEN UInt32 words, widened by MsgUpdate.
        if (states.CommitLegacyFamily8418DeathHitpointsAndFlags(monster,
                (context.TimeProvider ?? TimeProvider.System).GetUtcNow(), out var statusFlags))
        {
            await SendLegacyFamily8418DeathPrefixFrameAsync(
                context, session, monster,
                new LegacyMsgUpdatePacket(monster.Uid,
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.StatusFlag, 0, Values: statusFlags)])
                    .ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        }
        // A non-removable existing Dead flag survives ClearFlags: native AddFlag returns false,
        // sends nothing, preserves its metadata, and execution still reaches DeadStamp.

        states.StampLegacyFamily8418Death(monster,
            (context.TimeProvider ?? TimeProvider.System).GetUtcNow());

    }

    private static WorldSession IncrementLegacyFamily8418Kills(
        WorldPacketHandlerContext context, WorldSession session)
    {
        // Native holds killer.Player in a local then dereferences it: no lazy birth or
        // snapshot Kills fallback may replace a missing physical receiver at this phase.
        var nativePlayer = session.NativePlayer!;
        nativePlayer.Kills = unchecked(nativePlayer.Kills + 1u);
        var kills = nativePlayer.Kills;

        var updated = session with { Character = WithCharacterAttribute(session.Character, "Kills", kills) };
        context.Sessions?.Set(context.Connection, updated);
        return updated;
    }

    private static async ValueTask SendLegacyFamily8418DeathPrefixFrameAsync(
        WorldPacketHandlerContext context, WorldSession session,
        LegacyMonsterInstance monster, byte[] frame, CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        if (context.Sessions is not { } sessions) return;
        foreach (var active in sessions.Snapshot())
        {
            if (ReferenceEquals(active.Connection, context.Connection) ||
                active.Session.Character.Uid == session.Character.Uid ||
                !CanSeeMonster(active.Session.Character, monster))
                continue;
            await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }
}
