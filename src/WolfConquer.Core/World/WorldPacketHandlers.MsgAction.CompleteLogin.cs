namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyCompleteLoginAttribute = "CompleteLogin";
    private const string LegacyCompleteLoginGuildPrompt =
        "Why not join a guild. and find some companions in this turbulent world!";

    private static async ValueTask HandleCompleteLoginAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var session = context.Session;
        if (context.Sessions is { } currentSessions)
        {
            // The supplied context can predate another ordinary handler's
            // publication. A removed current session must never be resurrected.
            if (!currentSessions.TryGet(context.Connection, out session)) session = null;
        }
        if (session is null)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped MsgAction CompleteLogin {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }
        context = context with { Session = session };

        LegacyCompleteLoginRuntime.Owner? owner = null;
        if (context.Connection.NativeGameClient is not null || session.NativeGameClient is not null)
        {
            if (context.Sessions is null ||
                !LegacyCompleteLoginRuntime.TryCapture(context.Connection, session, out owner))
            {
                await context.Log.WriteLineAsync(
                        $"WorldServer skipped MsgAction CompleteLogin {FormatSession(context)}; current physical Player owner is unavailable.")
                    .ConfigureAwait(false);
                return;
            }
        }

        if (HasExplicitLegacyCompleteLoginInterserverAttribute(session))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP MsgAction CompleteLogin interserver no-op " +
                    $"{FormatSession(context)} typeId={(ushort)packet.Type} " +
                    $"initTransfer={ReadAttributeUInt64(session.Character.Attributes, "InitTransfer")}; " +
                    "source=MsgDataPacket.CompleteLogin [Data(132)] OnInterServer branch queries alternate equipment, " +
                    "advances InitTransfer 1->2, sends InterServerCheckTransferCreate(3, uid), and resolves name collision; " +
                    "no local interserver transfer helper exists for this bounded handler.")
                .ConfigureAwait(false);
            return;
        }

        await context.Connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyVipLevelStartupUpdate(session.Character).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyVipStatusPacket.FromSnapshot(session.Character).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var jiangHuSent = false;
        if (LegacyJiangHuInfoPacket.TryCreateStartup(session.Character, out var jiangHu))
        {
            await context.Connection.SendAsync(jiangHu.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
            jiangHuSent = true;
        }

        var coatStorageCount = 0;
        foreach (var coatStorage in LegacyCoatStoragePacket.CreateLegacyStartup(session.PlayerItems))
        {
            await context.Connection.SendAsync(coatStorage.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
            coatStorageCount++;
        }

        var guildPromptSent = false;
        if (ReadAttributeUInt64(session.Character.Attributes, "GuildID", "GuildId") == 0 &&
            session.Character.Level > 20)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk(LegacyCompleteLoginGuildPrompt).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            guildPromptSent = true;
        }

        var abilityScore = CreateLegacyUserAbilityScoreResponse(session, context.WorldData);
        await context.Connection.SendAsync(abilityScore.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);

        WorldSession? updatedSession;
        if (context.Sessions is { } sessions)
        {
            // Native Data132 writes the physical flag only AFTER the final send.
            // Merge its compatibility projection into the latest same-owner
            // session; an awaited startup must not overwrite a newer wallet.
            if (!sessions.TryCompleteLegacyLogin(context.Connection, session, owner, out updatedSession))
            {
                await context.Log.WriteLineAsync(
                        $"WorldServer skipped MsgAction CompleteLogin publication {FormatSession(context)}; current owner changed or session ended during startup.")
                    .ConfigureAwait(false);
                return;
            }
        }
        else
        {
            // Preserve existing wholly unbound managed fixtures, without
            // constructing a client, Player, or private completion issuer.
            updatedSession = session with
            {
                Character = WithCharacterAttribute(session.Character, LegacyCompleteLoginAttribute, 1UL),
            };
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgAction CompleteLogin " +
                $"{FormatSession(context with { Session = updatedSession })} typeId={(ushort)packet.Type} " +
                $"jiangHuSent={jiangHuSent} coatStorageCount={coatStorageCount} " +
                $"guildPromptSent={guildPromptSent} abilityScoreItems={abilityScore.Items.Count} " +
                $"physicalPlayerCompleted={owner is not null}; " +
                "source=MsgDataPacket.CompleteLogin [Data(132)] local branch sends VIP startup, JiangHu startup, " +
                "wardrobe startup, optional guild prompt, prestige response, then sets Player.CompleteLogin=true.")
            .ConfigureAwait(false);
    }

    private static bool HasExplicitLegacyCompleteLoginInterserverAttribute(WorldSession session)
    {
        return ReadAttributeBool(
            session.Character.Attributes,
            "OnInterServer",
            "OnInterserver",
            "InterServer",
            "Interserver",
            "InterServerActive",
            "RuntimeOnInterServer",
            "RuntimeInterServer");
    }
}
