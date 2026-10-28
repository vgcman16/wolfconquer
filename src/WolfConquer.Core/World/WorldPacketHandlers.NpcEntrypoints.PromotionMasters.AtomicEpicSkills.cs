namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryLearnLegacyAtomicEpicSkillAsync(
        WorldPacketHandlerContext context, WorldSession session, byte optionId, ushort spellId,
        string spellName, string sourceRange, string sourceMethod, string family, ushort avatarId,
        long cost, CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions || !sessions.TryGet(context.Connection, out var current) ||
            !ReferenceEquals(current, session))
        {
            await LogLegacyPaidSpellRefusalAsync(context, "paidSpellOwnerChanged").ConfigureAwait(false);
            return true;
        }
        // Duplicate learning has no purchase to commit, even at a low balance.
        if (session.PlayerSpells.ContainsSpell(spellId))
        {
            await SendLegacyNpcDialogAsync(context, sourceMethod, [$"You`ve learned {spellName}!"],
                [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)], avatarId, cancellationToken).ConfigureAwait(false);
            await context.Log.WriteLineAsync($"WorldServer handled LegacyNpc Promotion{family} epic skill " +
                $"{FormatSession(context)} source={sourceMethod} sourceRange={sourceRange} option={optionId} spellId={spellId}; " +
                "already learned costConquerPoints=0.").ConfigureAwait(false);
            return true;
        }
        if (session.Character.ConquerPoints < cost)
        {
            await SendLegacyNpcDialogAsync(context, sourceMethod, ["Come~on,~you~don`t~have~2,150~CPs~with~you."],
                [new LegacyNpcDialogOption("Sorry...", LegacyNpcClosedOption)], 5, cancellationToken).ConfigureAwait(false);
            await context.Log.WriteLineAsync($"WorldServer skipped LegacyNpc Promotion{family} epic skill " +
                $"{FormatSession(context)} source={sourceMethod} sourceRange={sourceRange} option={optionId} spellId={spellId} " +
                $"conquerPoints={session.Character.ConquerPoints} requiredConquerPoints={cost}; insufficient CP.").ConfigureAwait(false);
            return true;
        }
        var learned = CreateLegacyPromotionSpellEntry(spellId);
        if (!TryCommitLegacyPaidSpellLearning(context, session, learned, LegacySpellLearningCurrency.ConquerPoints,
                cost, 0, requiresTrojanEpicWeapon: false, cancellationToken, out var paid, out var reason))
        {
            await LogLegacyPaidSpellRefusalAsync(context, reason).ConfigureAwait(false);
            return true;
        }
        var updatedContext = context with { Session = paid!.Updated };
        if (!await paid.PublishGrantAsync(cancellationToken).ConfigureAwait(false)) return true;
        await SendLegacyNpcDialogAsync(updatedContext, sourceMethod,
            [$"Congratulation! You just have learned the skill {spellName}"],
            [new LegacyNpcDialogOption("Ok , thank you.", LegacyNpcClosedOption)], avatarId, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync($"WorldServer handled LegacyNpc Promotion{family} epic skill " +
            $"{FormatSession(updatedContext)} source={sourceMethod} sourceRange={sourceRange} option={optionId} spellId={spellId} " +
            $"costConquerPoints={cost}.").ConfigureAwait(false);
        return true;
    }
}
