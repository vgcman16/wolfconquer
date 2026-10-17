namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyPromotionTrojanMortalStrikeSourceRange = "NpcHandler.cs:47985-48042";
    private const string LegacyPromotionTrojanFatalCrossSourceRange = "NpcHandler.cs:48043-48100";
    private const byte LegacyPromotionTrojanFatalCrossOption = 16;
    private const byte LegacyPromotionTrojanMortalStrikeOption = 17;
    private const ushort LegacyPromotionTrojanFatalCrossSpellId = 11_980;
    private const ushort LegacyPromotionTrojanMortalStrikeSpellId = 11_990;
    private const long LegacyPromotionTrojanEpicSkillCost = 2_150;

    private static ValueTask<bool> TryHandleLegacyTrojanEpicSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return optionId switch
        {
            LegacyPromotionTrojanMortalStrikeOption => TryLearnLegacyTrojanEpicSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTrojanMortalStrikeSpellId,
                "MortalStrike",
                LegacyPromotionTrojanMortalStrikeSourceRange,
                cancellationToken),
            LegacyPromotionTrojanFatalCrossOption => TryLearnLegacyTrojanEpicSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTrojanFatalCrossSpellId,
                "FatalCross",
                LegacyPromotionTrojanFatalCrossSourceRange,
                cancellationToken),
            _ => ValueTask.FromResult(false),
        };
    }

    private static async ValueTask<bool> TryLearnLegacyTrojanEpicSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        ushort spellId,
        string spellName,
        string sourceRange,
        CancellationToken cancellationToken)
    {
        if (!HasLegacyEquippedTrojanEpicWeapon(session.PlayerItems))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionTrojanSource,
                    [$"You need to equip a Trojan`s Epic Weapon to learn [{spellName}]."],
                    [new LegacyNpcDialogOption("Ah ok.", LegacyNpcClosedOption)],
                    LegacyPromotionTrojanAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionTrojan epic skill " +
                    $"{FormatSession(context)} source={LegacyPromotionTrojanSource} " +
                    $"sourceRange={sourceRange} option={optionId} spellId={spellId}; " +
                    "missing equipped Trojan epic weapon.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.PlayerSpells.ContainsSpell(spellId))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionTrojanSource,
                    [$"You`ve learned {spellName}!"],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    LegacyPromotionTrojanAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionTrojan epic skill " +
                    $"{FormatSession(context)} source={LegacyPromotionTrojanSource} " +
                    $"sourceRange={sourceRange} option={optionId} spellId={spellId}; already learned.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.Character.ConquerPoints < LegacyPromotionTrojanEpicSkillCost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionTrojanSource,
                    ["Come~on,~you~don`t~have~2,150~CPs~with~you."],
                    [new LegacyNpcDialogOption("Sorry...", LegacyNpcClosedOption)],
                    LegacyPromotionTrojanAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionTrojan epic skill " +
                    $"{FormatSession(context)} source={LegacyPromotionTrojanSource} " +
                    $"sourceRange={sourceRange} option={optionId} spellId={spellId} " +
                    $"conquerPoints={session.Character.ConquerPoints} " +
                    $"requiredConquerPoints={LegacyPromotionTrojanEpicSkillCost}; insufficient CP.")
                .ConfigureAwait(false);
            return true;
        }

        var debitedSession = await DebitLegacyPromotionConquerPointsAsync(
                context,
                session,
                LegacyPromotionTrojanEpicSkillCost,
                optionId,
                sourceRange,
                cancellationToken)
            .ConfigureAwait(false);
        var learned = CreateLegacyPromotionSpellEntry(spellId);
        var updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(debitedSession.PlayerSpells, learned);
        var updatedSession = await ApplyLegacyPromotionSpellsAsync(
                context with { Session = debitedSession },
                debitedSession,
                updatedSpells,
                optionId,
                sourceRange,
                spellId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        await updatedContext.Connection.SendAsync(
                LegacySpellPacket.FromSnapshot(learned).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPromotionTrojanSource,
                [
                    "You~successfully~acquired~the~skill,~[Mortal~Strike].~Equip~2~Trojan`s~Epic~Weapons~to~cast.~While~attacking,~the~caster~has~a~chance~to~cause~severe~damage~on~human~targets,",
                    "~and~more~violent~to~monsters,~within~a~certain~range.",
                ],
                [new LegacyNpcDialogOption("Thanks!", LegacyNpcClosedOption)],
                LegacyPromotionTrojanAvatarId,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionTrojan epic skill " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionTrojanSource} " +
                $"sourceRange={sourceRange} option={optionId} spellId={spellId} " +
                $"costConquerPoints={LegacyPromotionTrojanEpicSkillCost}.")
            .ConfigureAwait(false);
        return true;
    }
}
