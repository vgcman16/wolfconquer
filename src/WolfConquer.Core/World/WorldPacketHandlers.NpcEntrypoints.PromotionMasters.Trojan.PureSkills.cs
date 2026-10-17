namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyPromotionTrojanDragonWhirlSourceRange = "NpcHandler.cs:47713-47744";
    private const byte LegacyPromotionTrojanDragonWhirlOption = 31;
    private const ushort LegacyPromotionTrojanDragonWhirlSpellId = 10_315;

    private static async ValueTask<bool> TryHandleLegacyTrojanPureSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId != LegacyPromotionTrojanDragonWhirlOption)
        {
            return false;
        }

        if (session.Character.Reborn < 2)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionTrojanSource,
                    ["After your 2nd Rebirth, you still have to be Reincarnated and become a Pure Trojan, to be able to learn the Dragon Whirl skill."],
                    [new LegacyNpcDialogOption("Ok~I~see.", LegacyNpcClosedOption)],
                    LegacyPromotionTrojanAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionTrojan pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionTrojanSource} " +
                    $"sourceRange={LegacyPromotionTrojanDragonWhirlSourceRange} option={optionId} " +
                    $"reborn={session.Character.Reborn} requiredReborn=2; source reborn gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (!IsLegacyTrojanClass(session.Character.FirstClass) ||
            !IsLegacyTrojanClass(session.Character.SecondClass))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionTrojanSource,
                    ["You don'thave the combo ( Tro - Tro - Tro ) in your status."],
                    [new LegacyNpcDialogOption("Okay.~I~see..", LegacyNpcClosedOption)],
                    LegacyPromotionTrojanAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionTrojan pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionTrojanSource} " +
                    $"sourceRange={LegacyPromotionTrojanDragonWhirlSourceRange} option={optionId} " +
                    $"firstClass={session.Character.FirstClass} secondClass={session.Character.SecondClass}; " +
                    "source pure Trojan combo gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.Character.Level < 40 || session.Character.Class < 1_002)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionTrojanSource,
                    ["Sorry. I can`t teach you this skill until you reach level 40 and get promoted."],
                    [new LegacyNpcDialogOption("Okay.~I~see..", LegacyNpcClosedOption)],
                    LegacyPromotionTrojanAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionTrojan pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionTrojanSource} " +
                    $"sourceRange={LegacyPromotionTrojanDragonWhirlSourceRange} option={optionId} " +
                    $"level={session.Character.Level} requiredLevel=40 " +
                    $"class={session.Character.Class} requiredClass=1002; source promotion gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.PlayerSpells.ContainsSpell(LegacyPromotionTrojanDragonWhirlSpellId))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionTrojanSource,
                    ["You`ve learned DragonWhirl!"],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    LegacyPromotionTrojanAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionTrojan pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionTrojanSource} " +
                    $"sourceRange={LegacyPromotionTrojanDragonWhirlSourceRange} option={optionId} " +
                    $"spellId={LegacyPromotionTrojanDragonWhirlSpellId}; already learned.")
                .ConfigureAwait(false);
            return true;
        }

        var learned = CreateLegacyPromotionSpellEntry(LegacyPromotionTrojanDragonWhirlSpellId);
        var updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(session.PlayerSpells, learned);
        var updatedSession = await ApplyLegacyPromotionSpellsAsync(
                context,
                session,
                updatedSpells,
                optionId,
                LegacyPromotionTrojanDragonWhirlSourceRange,
                LegacyPromotionTrojanDragonWhirlSpellId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(LegacySpellPacket.FromSnapshot(learned).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPromotionTrojanSource,
                ["Congratulation! You just have learned the skill DragonWhirl"],
                [new LegacyNpcDialogOption("Ok , thank you.", LegacyNpcClosedOption)],
                LegacyPromotionTrojanAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionTrojan pure skill " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionTrojanSource} " +
                $"sourceRange={LegacyPromotionTrojanDragonWhirlSourceRange} option={optionId} " +
                $"spellId={LegacyPromotionTrojanDragonWhirlSpellId}.")
            .ConfigureAwait(false);
        return true;
    }
}
