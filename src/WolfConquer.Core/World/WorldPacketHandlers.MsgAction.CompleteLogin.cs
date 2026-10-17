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

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped MsgAction CompleteLogin {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
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

        var updatedCharacter = WithCharacterAttribute(session.Character, LegacyCompleteLoginAttribute, 1UL);
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgAction CompleteLogin " +
                $"{FormatSession(context with { Session = updatedSession })} typeId={(ushort)packet.Type} " +
                $"jiangHuSent={jiangHuSent} coatStorageCount={coatStorageCount} " +
                $"guildPromptSent={guildPromptSent} abilityScoreItems={abilityScore.Items.Count}; " +
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
