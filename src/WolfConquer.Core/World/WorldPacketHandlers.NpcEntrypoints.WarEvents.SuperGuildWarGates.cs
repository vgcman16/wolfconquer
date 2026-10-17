using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacySuperGuildWarCasteleLeftGateNpcId = 516_076;
    private const uint LegacySuperGuildWarCasteleRightGateNpcId = 516_077;
    private const uint LegacySuperGuildWarLeftGateNpcId = 516_078;
    private const uint LegacySuperGuildWarMidleGateNpcId = 516_079;
    private const uint LegacySuperGuildWarRightGateNpcId = 516_080;
    private const ushort LegacySuperGuildWarGateAvatarId = 6;
    private const int LegacySuperGuildWarLeftGateClosedMesh = 1_551;
    private const int LegacySuperGuildWarLeftGateOpenMesh = 1_511;
    private const int LegacySuperGuildWarRightGateClosedMesh = 1_527;
    private const int LegacySuperGuildWarRightGateOpenMesh = 1_537;
    private const int LegacySuperGuildWarGateBroadcastDistance = 19;

    private static readonly LegacySuperGuildWarStateStore LegacySuperGuildWarStates = new();

    private static readonly LegacyNpcDialogOption[] LegacySuperGuildWarGateOptions =
    [
        new("Open the gate", 1),
        new("Close gate.", 2),
        new("Get inside.", 3),
        new("Not interested.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySuperGuildWarGateOkayOptions =
    [
        new("Okay.", LegacyNpcClosedOption),
    ];

    internal static async ValueTask<bool> TryHandleLegacySuperGuildWarGateNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!TryGetLegacySuperGuildWarGate(npcId, out var gate))
        {
            return false;
        }

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        gate.SourceMethod,
                        ["Select the option you want to pursue."],
                        LegacySuperGuildWarGateOptions,
                        LegacySuperGuildWarGateAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                if (!TryGetLegacySuperGuildWarWinnerGuildId(context.WorldData?.SuperGuildWarInfo, out var openWinnerGuildId))
                {
                    await SendLegacySuperGuildWarGateGapAsync(
                            context,
                            session,
                            gate,
                            optionId,
                            "Super Guild War gate opening needs a persisted winner before it can run.",
                            "Source requires MsgSchedules.SuperGuildWar.Winner.GuildID; persisted SuperGuildWarInfo winner guild is missing or zero.",
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                if (!IsLegacySuperGuildWarGateOfficer(session, openWinnerGuildId))
                {
                    await SendLegacySuperGuildWarGateDeniedAsync(context, gate, cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await ApplyLegacySuperGuildWarGateStateAsync(
                        context,
                        session,
                        gate,
                        optionId,
                        open: true,
                        openWinnerGuildId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                if (!TryGetLegacySuperGuildWarWinnerGuildId(context.WorldData?.SuperGuildWarInfo, out var closeWinnerGuildId))
                {
                    await SendLegacySuperGuildWarGateGapAsync(
                            context,
                            session,
                            gate,
                            optionId,
                            "Super Guild War gate closing needs a persisted winner before it can run.",
                            "Source requires MsgSchedules.SuperGuildWar.Winner.GuildID; persisted SuperGuildWarInfo winner guild is missing or zero.",
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                if (!IsLegacySuperGuildWarGateOfficer(session, closeWinnerGuildId))
                {
                    await SendLegacySuperGuildWarGateDeniedAsync(context, gate, cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await ApplyLegacySuperGuildWarGateStateAsync(
                        context,
                        session,
                        gate,
                        optionId,
                        open: false,
                        closeWinnerGuildId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                if (!TryGetLegacySuperGuildWarWinnerGuildId(context.WorldData?.SuperGuildWarInfo, out var entryWinnerGuildId))
                {
                    await SendLegacySuperGuildWarGateGapAsync(
                            context,
                            session,
                            gate,
                            optionId,
                            "Super Guild War gate entry needs a persisted winner before it can run.",
                            "Source teleports MsgSchedules.SuperGuildWar.Winner guild members, but persisted SuperGuildWarInfo winner guild is missing or zero.",
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                if (!LegacySuperGuildWarStateStore.IsWinnerGuildMember(
                    ReadAttributeUInt64(session.Character.Attributes, "GuildID", "GuildId"),
                    entryWinnerGuildId))
                {
                    await SendLegacySuperGuildWarGateDeniedAsync(context, gate, cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        npcId,
                        optionId,
                        new LegacyNpcTeleportDestination(gate.MapId, gate.X, gate.Y, gate.SourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static bool TryGetLegacySuperGuildWarGate(
        uint npcId,
        out LegacySuperGuildWarGate gate)
    {
        gate = npcId switch
        {
            LegacySuperGuildWarCasteleLeftGateNpcId => new(
                LegacySuperGuildWarCasteleLeftGateNpcId,
                "NpcHandler.SuperGuildWar_CasteleLeftGate",
                3_868,
                191,
                142,
                LegacySuperGuildWarRightGateClosedMesh,
                LegacySuperGuildWarRightGateOpenMesh),
            LegacySuperGuildWarCasteleRightGateNpcId => new(
                LegacySuperGuildWarCasteleRightGateNpcId,
                "NpcHandler.SuperGuildWar_CasteleRightGate",
                3_868,
                264,
                141,
                LegacySuperGuildWarRightGateClosedMesh,
                LegacySuperGuildWarRightGateOpenMesh),
            LegacySuperGuildWarLeftGateNpcId => new(
                LegacySuperGuildWarLeftGateNpcId,
                "NpcHandler.SuperGuildWar_LeftGate",
                3_868,
                166,
                224,
                LegacySuperGuildWarLeftGateClosedMesh,
                LegacySuperGuildWarLeftGateOpenMesh),
            LegacySuperGuildWarMidleGateNpcId => new(
                LegacySuperGuildWarMidleGateNpcId,
                "NpcHandler.SuperGuildWar_MidleGate",
                3_868,
                221,
                202,
                LegacySuperGuildWarLeftGateClosedMesh,
                LegacySuperGuildWarLeftGateOpenMesh),
            LegacySuperGuildWarRightGateNpcId => new(
                LegacySuperGuildWarRightGateNpcId,
                "NpcHandler.SuperGuildWar_RightGate",
                3_868,
                287,
                223,
                LegacySuperGuildWarLeftGateClosedMesh,
                LegacySuperGuildWarLeftGateOpenMesh),
            _ => default,
        };

        return gate.SourceMethod is not null;
    }

    internal static void ResetLegacySuperGuildWarStateForTests()
    {
        LegacySuperGuildWarStates.Clear();
    }

    private static async ValueTask ApplyLegacySuperGuildWarGateStateAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacySuperGuildWarGate gate,
        byte optionId,
        bool open,
        ulong winnerGuildId,
        CancellationToken cancellationToken)
    {
        var maxHitpoints = ResolveLegacySuperGuildWarGateMaxHitpoints(context, gate);
        var mutation = LegacySuperGuildWarStates.ApplyGate(
            gate.NpcId,
            gate.ClosedMesh,
            gate.OpenMesh,
            maxHitpoints,
            open);

        if (mutation.Changed)
        {
            await SendLegacySuperGuildWarGateRuntimeUpdatesAsync(
                    context,
                    session,
                    gate,
                    mutation,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await SendLegacyNpcDialogAsync(
                context,
                gate.SourceMethod,
                [open ? "Gate is already opened." : "Gate is already closed."],
                LegacySuperGuildWarGateOkayOptions,
                LegacySuperGuildWarGateAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc SuperGuildWarGate " +
                $"{FormatSession(context)} source={gate.SourceMethod} option={optionId} npcId={gate.NpcId} " +
                $"action={(open ? "open" : "close")} changed={mutation.Changed} winnerGuildId={winnerGuildId} " +
                $"mesh={mutation.Current.Mesh} hitpoints={mutation.Current.Hitpoints} maxHitpoints={mutation.Current.MaxHitpoints}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacySuperGuildWarGateRuntimeUpdatesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacySuperGuildWarGate gate,
        LegacySuperGuildWarGateMutation mutation,
        CancellationToken cancellationToken)
    {
        var frames = new List<byte[]>();
        if (mutation.MeshChanged)
        {
            frames.Add(LegacyMsgUpdatePacket
                .CreateLegacyRuntimeMeshUpdate(gate.NpcId, (ulong)mutation.Current.Mesh)
                .ToFrameBytes());
        }

        if (mutation.HitpointsChanged)
        {
            frames.Add(new LegacyMsgUpdatePacket(
                    gate.NpcId,
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, mutation.Current.Hitpoints)])
                .ToFrameBytes());
        }

        if (frames.Count == 0)
        {
            return;
        }

        var sentCount = 0;
        if (context.Sessions is { } sessions)
        {
            foreach (var active in sessions.Snapshot())
            {
                if (!IsLegacySuperGuildWarGateUpdateRecipient(active.Session, gate))
                {
                    continue;
                }

                foreach (var frame in frames)
                {
                    await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                }

                sentCount++;
            }
        }

        if (sentCount == 0 && IsLegacySuperGuildWarGateUpdateRecipient(session, gate))
        {
            foreach (var frame in frames)
            {
                await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            }

            sentCount = 1;
        }

        await context.Log.WriteLineAsync(
                "WorldServer broadcast LegacyNpc SuperGuildWarGate runtime update " +
                $"{FormatSession(context)} source={gate.SourceMethod} npcId={gate.NpcId} recipients={sentCount} " +
                $"meshChanged={mutation.MeshChanged} hitpointsChanged={mutation.HitpointsChanged}.")
            .ConfigureAwait(false);
    }

    private static bool IsLegacySuperGuildWarGateUpdateRecipient(
        WorldSession session,
        LegacySuperGuildWarGate gate)
    {
        var character = session.Character;
        return character.MapId == gate.MapId &&
            ReadCharacterDynamicId(character) == 0 &&
            LegacyGetDistance(character.X, character.Y, gate.X, gate.Y) <= LegacySuperGuildWarGateBroadcastDistance;
    }

    private static async ValueTask SendLegacySuperGuildWarGateDeniedAsync(
        WorldPacketHandlerContext context,
        LegacySuperGuildWarGate gate,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                gate.SourceMethod,
                ["Sorry, you cannot open it. Only DLs or GLs from the guild that dominated the pole can."],
                LegacySuperGuildWarGateOkayOptions,
                LegacySuperGuildWarGateAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsLegacySuperGuildWarGateOfficer(
        WorldSession session,
        ulong winnerGuildId)
    {
        var attributes = session.Character.Attributes;
        var guildId = ReadAttributeUInt64(attributes, "GuildID", "GuildId");
        var guildRank = ReadAttributeUInt64(attributes, "GuildRank", "GuildMemberRank");
        return LegacySuperGuildWarStateStore.IsWinnerGuildMember(guildId, winnerGuildId) &&
            LegacySuperGuildWarStateStore.IsGateOfficerRank(guildRank);
    }

    private static bool TryGetLegacySuperGuildWarWinnerGuildId(
        GuildWarData? superGuildWarInfo,
        out ulong winnerGuildId)
    {
        return LegacySuperGuildWarStateStore.TryGetWinnerGuildId(superGuildWarInfo, out winnerGuildId);
    }

    private static uint ResolveLegacySuperGuildWarGateMaxHitpoints(
        WorldPacketHandlerContext context,
        LegacySuperGuildWarGate gate)
    {
        if (TryFindLegacySuperGuildWarGateSobNpc(context, gate, out var sobNpc))
        {
            var maxHitpoints = Math.Max(sobNpc.MaxLife, sobNpc.Life);
            if (maxHitpoints > 0)
            {
                return (uint)maxHitpoints;
            }
        }

        return 1;
    }

    private static bool TryFindLegacySuperGuildWarGateSobNpc(
        WorldPacketHandlerContext context,
        LegacySuperGuildWarGate gate,
        out SobNpc sobNpc)
    {
        sobNpc = default!;
        if (gate.NpcId > int.MaxValue ||
            context.WorldData is not { } worldData ||
            !worldData.SobNpcsById.TryGetValue((int)gate.NpcId, out var candidates))
        {
            return false;
        }

        foreach (var candidate in candidates)
        {
            if (candidate.MapId == gate.MapId)
            {
                sobNpc = candidate;
                return true;
            }
        }

        return false;
    }

    private static async ValueTask SendLegacySuperGuildWarGateGapAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacySuperGuildWarGate gate,
        byte optionId,
        string dialogText,
        string reason,
        CancellationToken cancellationToken)
    {
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc SuperGuildWarGate " +
                $"{FormatSession(context)} source={gate.SourceMethod} option={optionId} " +
                $"guildId={ReadAttributeUInt64(session.Character.Attributes, "GuildID", "GuildId")}; {reason}")
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                context,
                gate.SourceMethod,
                [dialogText],
                LegacySuperGuildWarGateOkayOptions,
                LegacySuperGuildWarGateAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private readonly record struct LegacySuperGuildWarGate(
        uint NpcId,
        string SourceMethod,
        int MapId,
        ushort X,
        ushort Y,
        int ClosedMesh,
        int OpenMesh);
}
