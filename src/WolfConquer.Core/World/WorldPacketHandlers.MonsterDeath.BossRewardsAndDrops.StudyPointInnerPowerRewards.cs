using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyBossDeathStudyPointInnerPowerRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession killerSession,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monsterFamily.Id < 0 ||
            !LegacyBossDeathRewardPolicy.TryGetStudyPointInnerPowerTeamReward(
                checked((uint)monsterFamily.Id),
                monster.Boss,
                out var reward))
        {
            return killerSession;
        }

        var killerPotencyApplied = await TryApplyLegacyInnerPowerPotencyAwardAsync(
                context,
                context.Connection,
                killerSession,
                reward.KillerInnerPowerPotency,
                cancellationToken)
            .ConfigureAwait(false);

        var teammateStudyCount = 0;
        var teammatePotencyCount = 0;
        if (context.TeamStates is { } teamStates &&
            context.Sessions is { } sessions)
        {
            foreach (var connection in teamStates.GetConnections(context.Connection))
            {
                if (ReferenceEquals(connection, context.Connection))
                {
                    continue;
                }

                if (!sessions.TryGet(connection, out var teammateSession) ||
                    teammateSession is null)
                {
                    continue;
                }

                var updatedTeammateSession = await ApplyLegacyStudyPointAwardAsync(
                        context,
                        connection,
                        teammateSession,
                        reward.TeammateStudyPoints,
                        cancellationToken)
                    .ConfigureAwait(false);
                teammateStudyCount++;

                if (await TryApplyLegacyInnerPowerPotencyAwardAsync(
                        context,
                        connection,
                        updatedTeammateSession,
                        reward.TeammateInnerPowerPotency,
                        cancellationToken)
                    .ConfigureAwait(false))
                {
                    teammatePotencyCount++;
                }
            }
        }

        await BroadcastLegacyBossStudyPointMessageAsync(
                context,
                reward.FormatSystemMessage(monster.Name, killerSession.Character.Name),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy boss study-point/InnerPower reward " +
                $"{FormatSession(context with { Session = killerSession })} " +
                $"monsterUid={monster.Uid} familyId={monsterFamily.Id} teammateStudyRecipients={teammateStudyCount} " +
                $"killerPotencyApplied={killerPotencyApplied} teammatePotencyRecipients={teammatePotencyCount}; " +
                "source=MonsterRole Family.ID 4171/20060/8311 AddPotency/AddStudyPoints and SendBossSysMesage.")
            .ConfigureAwait(false);
        return killerSession;
    }

    private static async ValueTask<bool> TryApplyLegacyInnerPowerPotencyAwardAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        WorldSession session,
        int potency,
        CancellationToken cancellationToken)
    {
        if (potency <= 0)
        {
            return false;
        }

        if (context.WorldData is null)
        {
            await LogLegacyInnerPowerPotencyRewardDeferredAsync(
                    context,
                    session,
                    "InnerPower.txt rows are not loaded")
                .ConfigureAwait(false);
            return false;
        }

        if (session.Character.Uid is < 0 or > int.MaxValue)
        {
            await LogLegacyInnerPowerPotencyRewardDeferredAsync(
                    context,
                    session,
                    $"InnerPower.txt row key {session.Character.Uid} exceeds source row key range")
                .ConfigureAwait(false);
            return false;
        }

        var characterId = checked((int)session.Character.Uid);
        if (!context.WorldData.InnerPowerRanksByCharacterId.TryGetValue(characterId, out var rows) ||
            rows.Count == 0)
        {
            await LogLegacyInnerPowerPotencyRewardDeferredAsync(
                    context,
                    session,
                    $"InnerPower.txt row for character {characterId} is not loaded")
                .ConfigureAwait(false);
            return false;
        }

        var updatedPotency = 0;
        var applied = false;
        var deferReason = string.Empty;
        lock (rows)
        {
            var row = rows[0];
            if (TryReadLegacyInnerPowerState(row, out var state, out deferReason))
            {
                updatedPotency = checked(state.Potency + potency);
                var updatedRow = CreateLegacyInnerPowerRankEntry(
                    row,
                    state with { Potency = updatedPotency });
                applied = TryReplaceLegacyInnerPowerRow(rows, updatedRow, out deferReason);
            }
        }

        if (!applied)
        {
            await LogLegacyInnerPowerPotencyRewardDeferredAsync(context, session, deferReason).ConfigureAwait(false);
            return false;
        }

        var packet = new LegacyMsgUpdatePacket(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            [
                new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.InnerPowerPotency,
                    checked((ulong)updatedPotency)),
            ]);
        await connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static async ValueTask LogLegacyInnerPowerPotencyRewardDeferredAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "WorldServer deferred legacy boss InnerPower potency reward " +
                $"{FormatSession(context with { Session = session })}; {reason}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession> TryApplyLegacyBossDeathStudyPointRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession killerSession,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monsterFamily.Id < 0 ||
            !LegacyBossDeathRewardPolicy.TryGetStudyPointTeamReward(
                checked((uint)monsterFamily.Id),
                monster.Boss,
                out var reward))
        {
            return killerSession;
        }

        var updatedKillerSession = killerSession;
        var awardedCount = 0;
        if (context.TeamStates is { } teamStates &&
            context.Sessions is { } sessions)
        {
            foreach (var connection in teamStates.GetConnections(context.Connection))
            {
                if (ReferenceEquals(connection, context.Connection))
                {
                    continue;
                }

                if (!sessions.TryGet(connection, out var teammateSession) ||
                    teammateSession is null)
                {
                    continue;
                }

                _ = await ApplyLegacyStudyPointAwardAsync(
                        context,
                        connection,
                        teammateSession,
                        reward.TeammateStudyPoints,
                        cancellationToken)
                    .ConfigureAwait(false);
                awardedCount++;
            }
        }

        updatedKillerSession = await ApplyLegacyStudyPointAwardAsync(
                context,
                context.Connection,
                updatedKillerSession,
                reward.KillerStudyPoints,
                cancellationToken)
            .ConfigureAwait(false);
        awardedCount++;

        await BroadcastLegacyBossStudyPointMessageAsync(
                context,
                reward.FormatSystemMessage(monster.Name, updatedKillerSession.Character.Name),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy boss study-point reward " +
                $"{FormatSession(context with { Session = updatedKillerSession })} " +
                $"monsterUid={monster.Uid} familyId={monsterFamily.Id} points={reward.KillerStudyPoints} recipients={awardedCount}; " +
                "source=MonsterRole Family.ID == 20055 AddStudyPoints and SendBossSysMesage.")
            .ConfigureAwait(false);
        return updatedKillerSession;
    }

    private static async ValueTask<WorldSession> ApplyLegacyStudyPointAwardAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        WorldSession session,
        ushort points,
        CancellationToken cancellationToken)
    {
        var updatedCharacter = AddLegacyStudyPoints(session.Character, points);
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(connection, updatedSession);

        var state = updatedCharacter.Attributes.TryGetValue("SubProfInfo", out var rawState) &&
            LegacySubClassHitpointsCalculator.TryParseLegacyState(rawState, out var parsed)
                ? parsed
                : new LegacySubClassState(StudyPoints: 0, ActiveSubClass: 0, Entries: []);
        var uid = CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid));

        await connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk($"You received {points.ToString(CultureInfo.InvariantCulture)} StudyPoints.").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await connection.SendAsync(
                LegacyStringPacket.PlayerEffect(uid, "zf2-e300").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await connection.SendAsync(
                new LegacySubClassGuiPacket(
                        LegacySubClassAction.Animation,
                        state.StudyPoints,
                        StudyReceive: 0,
                        Entries: [])
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        return updatedSession;
    }

    private static async ValueTask BroadcastLegacyBossStudyPointMessageAsync(
        WorldPacketHandlerContext context,
        string message,
        CancellationToken cancellationToken)
    {
        await BroadcastLegacySystemRedCenterMessageAsync(context, message, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask BroadcastLegacySystemRedCenterMessageAsync(
        WorldPacketHandlerContext context,
        string message,
        CancellationToken cancellationToken)
    {
        var frame = LegacyMsgMessagePacket.SystemRedCenter(message).ToFrameBytes();
        var activeSessions = context.Sessions?.Snapshot();
        if (activeSessions is { Count: > 0 })
        {
            foreach (var active in activeSessions)
            {
                await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
    }
}
