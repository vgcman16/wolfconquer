using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed record LegacyTeleportStatusCleanupResult(
        WorldSession CleanedSession,
        IReadOnlyList<LegacyTeleportStatusRemoval> Removals,
        bool ClearedInUseIntensify,
        bool RemovedCastPrayBookkeeping)
    {
        public bool HasChanges =>
            ClearedInUseIntensify ||
            Removals.Count > 0 ||
            RemovedCastPrayBookkeeping;
    }

    internal sealed record LegacyTeleportStatusRemoval(
        int Flag,
        WorldSession Session)
    {
        public bool RequiresAzureShieldReset => Flag == LegacyMagicDefenderFlag;
    }

    internal static LegacyTeleportStatusCleanupResult CreateLegacyTeleportStatusCleanup(
        WorldSession session)
    {
        var character = session.Character;
        var removals = new List<LegacyTeleportStatusRemoval>(capacity: 3);
        var clearedInUseIntensify = TryCancelLegacyIntensifyArm(
            character.Attributes,
            out var intensifyAttributes);
        if (clearedInUseIntensify)
        {
            character = character with { Attributes = intensifyAttributes };
        }

        var removedCastPrayBookkeeping = false;
        RemoveStatusFlag(LegacyPrayingFlag);
        RemoveStatusFlag(
            LegacyCastPrayFlag,
            RuntimeLegacyCastPrayExpiresAtUtcAttribute,
            RuntimeLegacyCastPrayRemoveOnDeadAttribute);
        RemoveStatusFlag(
            LegacyMagicDefenderFlag,
            RuntimeLegacyAurasMagicDefenderExpiresAtUtcAttribute,
            RuntimeLegacyAurasMagicDefenderRemoveOnDeadAttribute);

        return new LegacyTeleportStatusCleanupResult(
            session with { Character = character },
            removals,
            clearedInUseIntensify,
            removedCastPrayBookkeeping);

        void RemoveStatusFlag(int flag, params string[] bookkeepingAttributes)
        {
            var removedFlag = HasLegacyStatusFlag(character, flag);
            if (removedFlag)
            {
                character = WithoutLegacyStatusFlag(character, flag);
            }

            foreach (var attribute in bookkeepingAttributes)
            {
                if (!character.Attributes.ContainsKey(attribute))
                {
                    continue;
                }

                character = WithoutCharacterAttribute(character, attribute);
                removedCastPrayBookkeeping = true;
            }

            if (removedFlag)
            {
                removals.Add(new LegacyTeleportStatusRemoval(
                    flag,
                    session with { Character = character }));
            }
        }
    }

    private static async ValueTask SendLegacyTeleportStatusCleanupFramesAsync(
        WorldPacketHandlerContext context,
        WorldSession sourceSession,
        CancellationToken cancellationToken)
    {
        var cleanup = CreateLegacyTeleportStatusCleanup(sourceSession);
        foreach (var removal in cleanup.Removals)
        {
            var removalContext = context with { Session = removal.Session };
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    removalContext,
                    removal.Session,
                    cancellationToken)
                .ConfigureAwait(false);
            if (removal.Flag == LegacyMagicDefenderFlag)
            {
                await SendLegacyTeleportMagicDefenderResetAsync(
                        removalContext,
                        removal.Session,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (cleanup.Removals.Count > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer sent legacy GameClient.Teleport RemoveBuffersMovements status cleanup " +
                    $"{FormatSession(context with { Session = sourceSession })} sourceMap={sourceSession.Character.MapId} " +
                    $"removedFlags=[{string.Join(',', cleanup.Removals.Select(removal => $"0x{removal.Flag:X}"))}]; " +
                    "sent one full StatusFlag vector per removal before Player.View.Clear.")
                .ConfigureAwait(false);
        }
    }

    private static async ValueTask<WorldSession?> TryApplyLegacyTeleportStatusCleanupAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        long sourceMapId,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy GameClient.Teleport transition " +
                    $"{FormatSession(context)} sourceMap={sourceMapId} " +
                    $"destinationMap={session.Character.MapId}; WorldSessionStore is unavailable, " +
                    "so RemoveBuffersMovements cleanup cannot be compare-and-set.")
                .ConfigureAwait(false);
            return null;
        }

        var cleanup = CreateLegacyTeleportStatusCleanup(session);
        if (!cleanup.HasChanges)
        {
            return session;
        }

        if (!TryCommitOwnedLegacyTeleport(context, session, cleanup.CleanedSession, out var committedSession))
        {
            sessions.TryGet(context.Connection, out var currentSession);
            var currentUid = currentSession?.Character.Uid.ToString() ?? "missing";
            var currentMap = currentSession?.Character.MapId.ToString() ?? "missing";
            await context.Log.WriteLineAsync(
                    "WorldServer skipped stale legacy GameClient.Teleport transition cleanup " +
                    $"expectedUid={session.Character.Uid} expectedSourceMap={sourceMapId} " +
                    $"expectedDestinationMap={session.Character.MapId} currentUid={currentUid} currentMap={currentMap}; " +
                    "WorldSessionStore.TryCompareAndSet rejected the transition session, so destination cleanup was not " +
                    "published and the Teleport transition was skipped.")
                .ConfigureAwait(false);
            return null;
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy GameClient.Teleport RemoveBuffersMovements cleanup " +
                $"{FormatSession(context with { Session = committedSession })} " +
                $"clearedInUseIntensify={cleanup.ClearedInUseIntensify} " +
                $"removedFlags=[{string.Join(',', cleanup.Removals.Select(removal => $"0x{removal.Flag:X}"))}] " +
                $"removedCastPrayBookkeeping={cleanup.RemovedCastPrayBookkeeping}; " +
                "destination state was compare-and-set without resending source cleanup frames.")
            .ConfigureAwait(false);

        return committedSession;
    }

    private static async ValueTask SendLegacyTeleportMagicDefenderResetAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var packet = new LegacyMsgUpdatePacket(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            [
                new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.AzureShield,
                    Value: 0,
                    Flag: LegacyMagicDefenderFlag),
            ]);
        await SendFrameToPlayerAndVisiblePeersAsync(
                context,
                context.Connection,
                session,
                packet.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
