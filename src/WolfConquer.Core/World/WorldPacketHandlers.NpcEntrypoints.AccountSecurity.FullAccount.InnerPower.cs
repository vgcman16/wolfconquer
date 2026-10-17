using System.Globalization;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyFullAccountInnerPowerCostPerGong = 10;
    private const string LegacyFullAccountInnerPowerInsufficientMessage =
        "You do not have 10 ConquerPoints with you.";

    private static async ValueTask ApplyLegacyAccountSecurityFullAccountInnerPowerAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        const byte optionId = 19;
        if (session.Character.ConquerPoints < LegacyFullAccountInnerPowerCostPerGong)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyAccountSecurityFullAccountSource,
                    [LegacyFullAccountInnerPowerInsufficientMessage],
                    [new LegacyNpcDialogOption("I'm just passing by.", LegacyNpcClosedOption)],
                    avatarId: 6,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (session.Character.ConquerPoints > uint.MaxValue)
        {
            await LogLegacyFullAccountInnerPowerFenceAsync(context, "conquer-points-out-of-range")
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await LogLegacyFullAccountInnerPowerFenceAsync(context, "session-store-missing")
                .ConfigureAwait(false);
            return;
        }

        if (context.WorldData is not { } worldData)
        {
            await LogLegacyFullAccountInnerPowerFenceAsync(context, "world-data-missing")
                .ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(session.UsersRoot))
        {
            await LogLegacyFullAccountInnerPowerFenceAsync(context, "users-root-missing")
                .ConfigureAwait(false);
            return;
        }

        if (session.Character.Uid is < 0 or > int.MaxValue)
        {
            await LogLegacyFullAccountInnerPowerFenceAsync(context, "character-uid-out-of-range")
                .ConfigureAwait(false);
            return;
        }

        var characterId = checked((int)session.Character.Uid);
        if (!worldData.InnerPowerRanksByCharacterId.TryGetValue(characterId, out var rows) ||
            rows.Count == 0)
        {
            await LogLegacyFullAccountInnerPowerFenceAsync(context, "inner-power-row-missing")
                .ConfigureAwait(false);
            return;
        }

        LegacyFullAccountInnerPowerProjection? projection = null;
        WorldSession? updatedSession = null;
        var reason = string.Empty;
        cancellationToken.ThrowIfCancellationRequested();
        lock (rows)
        {
            var row = rows[0];
            if (!TryCreateLegacyFullAccountInnerPowerProjection(
                    session.Character,
                    row,
                    worldData,
                    out projection,
                    out reason) ||
                !CanReplaceLegacyInnerPowerRow(rows, row.CharacterId, out reason))
            {
                projection = null;
            }
            else
            {
                updatedSession = session with { Character = projection.UpdatedCharacter };
                if (!TryPersistLegacyFullAccountInnerPowerMutation(
                        updatedSession,
                        projection.UpdatedRow,
                        out reason))
                {
                    projection = null;
                    updatedSession = null;
                }
                else
                {
                    ReplaceLegacyInnerPowerRow(rows, projection.UpdatedRow);
                    sessions.Set(context.Connection, updatedSession);
                }
            }
        }

        if (projection is null || updatedSession is null)
        {
            await LogLegacyFullAccountInnerPowerFenceAsync(context, reason)
                .ConfigureAwait(false);
            return;
        }

        var message =
            $"Congratulations ! [ {updatedSession.Character.Name} ] Has fixed his [InnerPower] For [ 10 ] ConquerPoints Enjoy With [ {ConquerRuntimeDefaults.ServerName} ] !.";
        var talkFrame = LegacyMsgMessagePacket.SystemWhiteTalk(message).ToFrameBytes();
        foreach (var conquerPoints in projection.ConquerPointUpdates)
        {
            var update = new LegacyMsgUpdatePacket(
                checked((uint)updatedSession.Character.Uid),
                [
                    new LegacyMsgUpdateEntry(
                        LegacyMsgUpdateDataType.ConquerPoints,
                        checked((ulong)conquerPoints)),
                ]);
            await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);

            foreach (var active in sessions.Snapshot())
            {
                await active.Connection.SendAsync(talkFrame, cancellationToken).ConfigureAwait(false);
            }
        }

        var updatedContext = context with { Session = updatedSession };
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc AccountSecurity FullAccount InnerPower " +
                $"{FormatSession(updatedContext)} source={LegacyAccountSecurityFullAccountSource} " +
                $"sourceLines={LegacyAccountSecurityFullAccountSourceLines} optionId={optionId} " +
                $"conquerPoints={session.Character.ConquerPoints}->{updatedSession.Character.ConquerPoints} " +
                $"stages={projection.StageCount} gongs={projection.GongCount} " +
                $"costPerGong={LegacyFullAccountInnerPowerCostPerGong} " +
                $"totalCost={projection.TotalCost} packetOrder=(ConquerPoints,WhiteTalk)* " +
                $"wire=7899 updateOpcode={LegacyMsgUpdatePacket.Opcode} messageOpcode={LegacyMsgMessagePacket.Opcode}.")
            .ConfigureAwait(false);
    }

    private static Task LogLegacyFullAccountInnerPowerFenceAsync(
        WorldPacketHandlerContext context,
        string reason)
    {
        return context.Log.WriteLineAsync(
            "PARITY-GAP LegacyNpc AccountSecurity FullAccount InnerPower " +
            $"{FormatSession(context)} source={LegacyAccountSecurityFullAccountSource} " +
            $"sourceLines={LegacyAccountSecurityFullAccountSourceLines} optionId=19 " +
            $"reason={reason}; no CP, InnerPower, session, or packet mutation was committed.");
    }
}
