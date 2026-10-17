namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyPromotionTrojanSuperiorSkillDialogSourceRange = "NpcHandler.cs:48213-48239";
    private const string LegacyPromotionTrojanCruelShadeSourceRange = "NpcHandler.cs:48240-48278";
    private const byte LegacyPromotionTrojanSuperiorSkillsOption = 14;
    private const byte LegacyPromotionTrojanCruelShadeOption = 32;
    private const byte LegacyPromotionTrojanCruelShadeSourceDialogOption = 31;
    private const ushort LegacyPromotionTrojanCruelShadeSpellId = 3_050;

    private static ValueTask<bool> TryHandleLegacyTrojanSuperiorSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return optionId switch
        {
            LegacyPromotionTrojanSuperiorSkillsOption => SendLegacyTrojanSuperiorSkillDialogAsync(
                context,
                session,
                optionId,
                cancellationToken),
            LegacyPromotionTrojanCruelShadeOption => TryLearnLegacyTrojanLevelGatedSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTrojanCruelShadeSpellId,
                "CruelShade",
                LegacyPromotionTrojanCruelShadeSourceRange,
                cancellationToken),
            _ => ValueTask.FromResult(false),
        };
    }

    private static async ValueTask<bool> SendLegacyTrojanSuperiorSkillDialogAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (session.Character.Reborn > 1)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionTrojanSource,
                    ["Please choose the spell you want to learn."],
                    [
                        new LegacyNpcDialogOption(
                            "CruelShade(L40).",
                            LegacyPromotionTrojanCruelShadeSourceDialogOption),
                        new LegacyNpcDialogOption("I`ll~think~about~it.", LegacyNpcClosedOption),
                    ],
                    LegacyPromotionTrojanAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyNpc PromotionTrojan superior skill dialog " +
                    $"{FormatSession(context)} source={LegacyPromotionTrojanSource} " +
                    $"sourceRange={LegacyPromotionTrojanSuperiorSkillDialogSourceRange} option={optionId}.")
                .ConfigureAwait(false);
            return true;
        }

        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionTrojanSource,
                ["Sorry, only reborn players can learn this skill."],
                [new LegacyNpcDialogOption("Okay.~I~see.", LegacyNpcClosedOption)],
                LegacyPromotionTrojanAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc PromotionTrojan superior skill dialog " +
                $"{FormatSession(context)} source={LegacyPromotionTrojanSource} " +
                $"sourceRange={LegacyPromotionTrojanSuperiorSkillDialogSourceRange} option={optionId} " +
                $"reborn={session.Character.Reborn} requiredReborn=2; source reborn gate rejected.")
            .ConfigureAwait(false);
        return true;
    }
}
