namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyPromotionTrojanSuperCycloneSourceRange = "NpcHandler.cs:48279-48317";
    private const string LegacyPromotionTrojanGolemSourceRange = "NpcHandler.cs:48318-48356";
    private const string LegacyPromotionTrojanHerculesSourceRange = "NpcHandler.cs:48357-48395";
    private const string LegacyPromotionTrojanSpiritHealingSourceRange = "NpcHandler.cs:48396-48434";
    private const string LegacyPromotionTrojanBreathFocusSourceRange = "NpcHandler.cs:48435-48473";
    private const byte LegacyPromotionTrojanSuperCycloneOption = 8;
    private const byte LegacyPromotionTrojanGolemOption = 10;
    private const byte LegacyPromotionTrojanHerculesOption = 11;
    private const byte LegacyPromotionTrojanSpiritHealingOption = 12;
    private const byte LegacyPromotionTrojanBreathFocusOption = 13;
    private const ushort LegacyPromotionTrojanSuperCycloneSpellId = 11_970;
    private const ushort LegacyPromotionTrojanGolemSpellId = 1_270;
    private const ushort LegacyPromotionTrojanHerculesSpellId = 1_115;
    private const ushort LegacyPromotionTrojanSpiritHealingSpellId = 1_190;
    private const ushort LegacyPromotionTrojanBreathFocusSpellId = 11_960;

    private static ValueTask<bool> TryHandleLegacyTrojanSimpleSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return optionId switch
        {
            LegacyPromotionTrojanSuperCycloneOption => TryLearnLegacyTrojanLevelGatedSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTrojanSuperCycloneSpellId,
                "SuperCyclone",
                LegacyPromotionTrojanSuperCycloneSourceRange,
                cancellationToken),
            LegacyPromotionTrojanGolemOption => TryLearnLegacyTrojanLevelGatedSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTrojanGolemSpellId,
                "Golem",
                LegacyPromotionTrojanGolemSourceRange,
                cancellationToken),
            LegacyPromotionTrojanHerculesOption => TryLearnLegacyTrojanLevelGatedSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTrojanHerculesSpellId,
                "Hercules",
                LegacyPromotionTrojanHerculesSourceRange,
                cancellationToken),
            LegacyPromotionTrojanSpiritHealingOption => TryLearnLegacyTrojanLevelGatedSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTrojanSpiritHealingSpellId,
                "SpiritHealing",
                LegacyPromotionTrojanSpiritHealingSourceRange,
                cancellationToken),
            LegacyPromotionTrojanBreathFocusOption => TryLearnLegacyTrojanLevelGatedSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTrojanBreathFocusSpellId,
                "BreathFocus",
                LegacyPromotionTrojanBreathFocusSourceRange,
                cancellationToken),
            _ => ValueTask.FromResult(false),
        };
    }
}
