using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMonsterHitSitInterruptionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint damage,
        bool killedPlayer,
        CancellationToken cancellationToken)
    {
        return await TryApplyLegacyMonsterHitSitInterruptionAsync(
                context,
                context.Connection,
                session,
                damage,
                killedPlayer,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMonsterHitSitInterruptionAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        WorldSession session,
        uint damage,
        bool killedPlayer,
        CancellationToken cancellationToken)
    {
        if (killedPlayer || !IsLegacyConquerActionSit(session.Character))
        {
            return session;
        }

        var stamina = ReadLegacyRuntimeStamina(session.Character);
        var remainingStamina = stamina >= 20 ? stamina - 20 : 0;
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(session.Character, "Stamina", remainingStamina),
            "Action",
            LegacyConquerActionNone);
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(connection, updatedSession);

        await connection.SendAsync(
                LegacyMsgUpdatePacket
                    .CreateLegacyRuntimeStaminaTickUpdate(
                        CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid)),
                        remainingStamina)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy monster hit sit interruption " +
                $"{FormatSession(context with { Session = updatedSession })} damage={damage} " +
                $"stamina={stamina} remainingStamina={remainingStamina}; " +
                "source=MsgMonster.ActionHandler.CheckForOponnentDead Sit.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMonsterHitActivePickRemovalAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint damage,
        CancellationToken cancellationToken)
    {
        return await TryApplyLegacyMonsterHitActivePickRemovalAsync(
                context,
                context.Connection,
                session,
                damage,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMonsterHitActivePickRemovalAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        WorldSession session,
        uint damage,
        CancellationToken cancellationToken)
    {
        if (!HasActivePick(session.Character))
        {
            return session;
        }

        var updatedCharacter = WithoutCharacterAttribute(session.Character, "ActivePick");
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(connection, updatedSession);

        var action = new MsgActionPacket
        {
            ObjId = CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid)),
            DwParam = LegacyActionPickDwParam,
            Timestamp = unchecked((uint)Environment.TickCount),
            Type = MsgActionType.ActionPick,
            Facing = 2,
            RemainTime = 0,
            Strings = ["Interrupted"],
        };

        await connection.SendAsync(action.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent legacy monster hit ActivePick interruption " +
                $"{FormatSession(context with { Session = updatedSession })} damage={damage}; " +
                "source=MsgMonster.ActionHandler.CheckForOponnentDead ActivePick.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool IsLegacyConquerActionSit(CharacterSnapshot character)
    {
        if (!character.Attributes.TryGetValue("Action", out var rawAction) ||
            string.IsNullOrWhiteSpace(rawAction))
        {
            return false;
        }

        return rawAction.Equals("Sit", StringComparison.OrdinalIgnoreCase) ||
            (ulong.TryParse(rawAction, NumberStyles.Integer, CultureInfo.InvariantCulture, out var action) &&
                action == LegacyConquerActionSit);
    }
}
