using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryHandleLegacyPromotionMasterSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return npcId switch
        {
            LegacyPromotionTaoistNpcId => await TryHandleLegacyTaoistPromotionSkillAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionMonkNpcId => await TryHandleLegacyMonkPromotionSkillAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionNinjaNpcId => await TryHandleLegacyNinjaPromotionSkillAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionWarriorNpcId => await TryHandleLegacyWarriorPromotionSkillAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionArcherNpcId => await TryHandleLegacyArcherPromotionSkillAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionDragonWarriorNpcId => await TryHandleLegacyDragonWarriorPromotionSkillAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionWindWalkerNpcId => await TryHandleLegacyWindWalkerPromotionSkillAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionThunderstrikerNpcId => await TryHandleLegacyThunderstrikerPromotionSkillAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionPirateNpcId => await TryHandleLegacyPiratePromotionSkillAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask<bool> TryLearnLegacyPromotionMasterSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        ushort spellId,
        string spellName,
        long requiredLevel,
        string sourceMethod,
        string sourceRange,
        ushort avatarId,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level < requiredLevel)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["You`re not strong enough to learn this skill. Keep practicing, and come back when you`re qualified."],
                    [new LegacyNpcDialogOption("Alright.", LegacyNpcClosedOption)],
                    avatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionMaster skill " +
                    $"{FormatSession(context)} source={sourceMethod} sourceRange={sourceRange} option={optionId} " +
                    $"spellId={spellId} level={session.Character.Level} requiredLevel={requiredLevel}; source level gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.PlayerSpells.ContainsSpell(spellId))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    [$"You`ve learned {spellName}!"],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    avatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionMaster skill " +
                    $"{FormatSession(context)} source={sourceMethod} sourceRange={sourceRange} option={optionId} " +
                    $"spellId={spellId}; already learned.")
                .ConfigureAwait(false);
            return true;
        }

        var learned = CreateLegacyPromotionSpellEntry(spellId);
        var updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(session.PlayerSpells, learned);
        var updatedSession = await ApplyLegacyPromotionMasterSpellsAsync(
                context,
                session,
                updatedSpells,
                optionId,
                sourceMethod,
                sourceRange,
                spellId.ToString(CultureInfo.InvariantCulture))
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        await SendLegacyPromotionMasterLearnedSpellAsync(
                updatedContext,
                sourceMethod,
                avatarId,
                learned,
                spellName,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionMaster skill " +
                $"{FormatSession(updatedContext)} source={sourceMethod} sourceRange={sourceRange} " +
                $"option={optionId} spellId={spellId}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<WorldSession> ApplyLegacyPromotionMasterSpellsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        PlayerSpellSnapshot updatedSpells,
        byte optionId,
        string sourceMethod,
        string sourceRange,
        string learnedSpellIds)
    {
        var updatedSession = session with { PlayerSpells = updatedSpells };
        context.Sessions?.Set(context.Connection, updatedSession);

        if (!string.IsNullOrWhiteSpace(context.PlayerSpellsRoot))
        {
            new PlayerSpellSnapshotLoader(context.PlayerSpellsRoot).SaveByCharacterUid(
                updatedSession.Character.Uid,
                updatedSpells);
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc PromotionMaster skill missing PlayerSpellsRoot " +
                    $"{FormatSession(context)} source={sourceMethod} sourceRange={sourceRange} " +
                    $"option={optionId} spellIds={learnedSpellIds}; learned spell is session-only.")
                .ConfigureAwait(false);
        }

        return updatedSession;
    }

    private static async ValueTask SendLegacyPromotionMasterLearnedSpellAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        ushort avatarId,
        PlayerSpellSnapshotEntry spell,
        string spellName,
        CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(LegacySpellPacket.FromSnapshot(spell).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                [$"Congratulation! You just have learned the skill {spellName}"],
                [new LegacyNpcDialogOption("Ok , thank you.", LegacyNpcClosedOption)],
                avatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
