using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyPromotionTrojanWeaponSkillSourceRange = "NpcHandler.cs:48557-48632";
    private const byte LegacyPromotionTrojanWeaponSkillOption = 15;
    private const ushort LegacyPromotionTrojanWeaponFastBladeSpellId = 1_045;
    private const ushort LegacyPromotionTrojanWeaponScentSwordSpellId = 1_046;
    private const string LegacyPromotionTrojanFastBladeSourceName = "FastBlader";
    private const string LegacyPromotionTrojanScentSwordSourceName = "ScrenSword";

    private static async ValueTask<bool> TryHandleLegacyTrojanWeaponSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId != LegacyPromotionTrojanWeaponSkillOption)
        {
            return false;
        }

        if (session.PlayerSpells.ContainsSpell(LegacyPromotionTrojanWeaponFastBladeSpellId))
        {
            await SendLegacyTrojanWeaponSkillAlreadyLearnedAsync(
                    context,
                    optionId,
                    LegacyPromotionTrojanWeaponFastBladeSpellId,
                    LegacyPromotionTrojanFastBladeSourceName,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var learnedFastBlade = CreateLegacyPromotionSpellEntry(LegacyPromotionTrojanWeaponFastBladeSpellId);
        var updatedSession = await LearnLegacyTrojanWeaponSkillAsync(
                context,
                session,
                optionId,
                learnedFastBlade,
                LegacyPromotionTrojanFastBladeSourceName,
                cancellationToken)
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedSession };

        if (updatedSession.PlayerSpells.ContainsSpell(LegacyPromotionTrojanWeaponScentSwordSpellId))
        {
            await SendLegacyTrojanWeaponSkillAlreadyLearnedAsync(
                    updatedContext,
                    optionId,
                    LegacyPromotionTrojanWeaponScentSwordSpellId,
                    LegacyPromotionTrojanScentSwordSourceName,
                    cancellationToken)
                .ConfigureAwait(false);
            await LogLegacyTrojanWeaponSkillHandledAsync(
                    context,
                    updatedContext,
                    optionId,
                    LegacyPromotionTrojanWeaponFastBladeSpellId.ToString(CultureInfo.InvariantCulture))
                .ConfigureAwait(false);
            return true;
        }

        var learnedScentSword = CreateLegacyPromotionSpellEntry(LegacyPromotionTrojanWeaponScentSwordSpellId);
        updatedSession = await LearnLegacyTrojanWeaponSkillAsync(
                updatedContext,
                updatedSession,
                optionId,
                learnedScentSword,
                LegacyPromotionTrojanScentSwordSourceName,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        await LogLegacyTrojanWeaponSkillHandledAsync(
                context,
                updatedContext,
                optionId,
                LegacyPromotionTrojanWeaponFastBladeSpellId.ToString(CultureInfo.InvariantCulture) + "," +
                LegacyPromotionTrojanWeaponScentSwordSpellId.ToString(CultureInfo.InvariantCulture))
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<WorldSession> LearnLegacyTrojanWeaponSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        PlayerSpellSnapshotEntry learned,
        string spellName,
        CancellationToken cancellationToken)
    {
        var updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(session.PlayerSpells, learned);
        var updatedSession = await ApplyLegacyPromotionSpellsAsync(
                context,
                session,
                updatedSpells,
                optionId,
                LegacyPromotionTrojanWeaponSkillSourceRange,
                learned.Id.ToString(CultureInfo.InvariantCulture))
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        await SendLegacyPromotionLearnedSpellAsync(updatedContext, learned, spellName, cancellationToken)
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask SendLegacyTrojanWeaponSkillAlreadyLearnedAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        ushort spellId,
        string spellName,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionTrojanSource,
                [$"You`ve learned {spellName}!"],
                [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                LegacyTrojanSkillAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc PromotionTrojan skill " +
                $"{FormatSession(context)} source={LegacyPromotionTrojanSource} " +
                $"sourceRange={LegacyPromotionTrojanWeaponSkillSourceRange} option={optionId} " +
                $"spellId={spellId}; already learned.")
            .ConfigureAwait(false);
    }

    private static async ValueTask LogLegacyTrojanWeaponSkillHandledAsync(
        WorldPacketHandlerContext originalContext,
        WorldPacketHandlerContext updatedContext,
        byte optionId,
        string learnedSpellIds)
    {
        await originalContext.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionTrojan skill " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionTrojanSource} " +
                $"sourceRange={LegacyPromotionTrojanWeaponSkillSourceRange} option={optionId} " +
                $"spellIds={learnedSpellIds}.")
            .ConfigureAwait(false);
    }
}
