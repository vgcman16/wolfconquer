namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyPromotionTrojanMonsterHunterSourceRange = "NpcHandler.cs:48101-48139";
    private const string LegacyPromotionTrojanUndyingWillSourceRange = "NpcHandler.cs:48140-48178";
    private const byte LegacyPromotionTrojanMonsterHunterOption = 251;
    private const byte LegacyPromotionTrojanUndyingWillOption = 252;
    private const ushort LegacyPromotionTrojanMonsterHunterSpellId = 15_950;
    private const ushort LegacyPromotionTrojanUndyingWillSpellId = 15_960;

    private static ValueTask<bool> TryHandleLegacyTrojanNewSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return optionId switch
        {
            LegacyPromotionTrojanMonsterHunterOption => TryLearnLegacyTrojanLevelGatedSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTrojanMonsterHunterSpellId,
                "MonsterHunter",
                LegacyPromotionTrojanMonsterHunterSourceRange,
                cancellationToken),
            LegacyPromotionTrojanUndyingWillOption => TryLearnLegacyTrojanLevelGatedSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTrojanUndyingWillSpellId,
                "UndyingWill",
                LegacyPromotionTrojanUndyingWillSourceRange,
                cancellationToken),
            _ => ValueTask.FromResult(false),
        };
    }
}
