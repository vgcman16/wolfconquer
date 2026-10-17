namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyPromotionWindWalkerStomperSkillAvatarId = 8;
    private const long LegacyPromotionWindWalkerSkillLevelGate = 100;

    private const string LegacyPromotionWindWalkerJusticeChantSourceRange = "NpcHandler.cs:46649-46687";
    private const string LegacyPromotionWindWalkerOmnipotenceSourceRange = "NpcHandler.cs:46688-46726";
    private const string LegacyPromotionWindWalkerFreezingPelterSourceRange = "NpcHandler.cs:47001-47039";
    private const string LegacyPromotionWindWalkerRevengeTailSourceRange = "NpcHandler.cs:47040-47078";
    private const string LegacyPromotionWindWalkerPeaceOfStomperSourceRange = "NpcHandler.cs:47079-47117";
    private const string LegacyPromotionWindWalkerChillingSnowSourceRange = "NpcHandler.cs:47118-47156";
    private const string LegacyPromotionWindWalkerHorrorOfStomperSourceRange = "NpcHandler.cs:47157-47195";
    private const string LegacyPromotionWindWalkerRageOfWarSourceRange = "NpcHandler.cs:47196-47234";
    private const string LegacyPromotionWindWalkerHealingSnowSourceRange = "NpcHandler.cs:47235-47273";
    private const string LegacyPromotionWindWalkerBurntFrostSourceRange = "NpcHandler.cs:47274-47312";
    private const string LegacyPromotionWindWalkerAngerOfStomperSourceRange = "NpcHandler.cs:47313-47351";

    private const byte LegacyPromotionWindWalkerAngerOfStomperOption = 20;
    private const byte LegacyPromotionWindWalkerBurntFrostOption = 21;
    private const byte LegacyPromotionWindWalkerHealingSnowOption = 22;
    private const byte LegacyPromotionWindWalkerRageOfWarOption = 23;
    private const byte LegacyPromotionWindWalkerHorrorOfStomperOption = 24;
    private const byte LegacyPromotionWindWalkerChillingSnowOption = 25;
    private const byte LegacyPromotionWindWalkerPeaceOfStomperOption = 26;
    private const byte LegacyPromotionWindWalkerRevengeTailOption = 27;
    private const byte LegacyPromotionWindWalkerFreezingPelterOption = 28;
    private const byte LegacyPromotionWindWalkerOmnipotenceOption = 35;
    private const byte LegacyPromotionWindWalkerJusticeChantOption = 36;

    private const ushort LegacyPromotionWindWalkerOmnipotenceSpellId = 12_860;
    private const ushort LegacyPromotionWindWalkerJusticeChantSpellId = 12_870;
    private const ushort LegacyPromotionWindWalkerRageOfWarSpellId = 12_930;
    private const ushort LegacyPromotionWindWalkerBurntFrostSpellId = 12_940;
    private const ushort LegacyPromotionWindWalkerHealingSnowSpellId = 12_950;
    private const ushort LegacyPromotionWindWalkerChillingSnowSpellId = 12_960;
    private const ushort LegacyPromotionWindWalkerAngerOfStomperSpellId = 12_980;
    private const ushort LegacyPromotionWindWalkerHorrorOfStomperSpellId = 12_990;
    private const ushort LegacyPromotionWindWalkerPeaceOfStomperSpellId = 13_000;
    private const ushort LegacyPromotionWindWalkerFreezingPelterSpellId = 13_020;
    private const ushort LegacyPromotionWindWalkerRevengeTailSpellId = 13_030;

    private static ValueTask<bool> TryHandleLegacyWindWalkerStomperPromotionSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return optionId switch
        {
            LegacyPromotionWindWalkerAngerOfStomperOption => TryLearnLegacyWindWalkerStomperPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionWindWalkerAngerOfStomperSpellId,
                "AngerofStomper",
                LegacyPromotionWindWalkerAngerOfStomperSourceRange,
                cancellationToken),
            LegacyPromotionWindWalkerBurntFrostOption => TryLearnLegacyWindWalkerStomperPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionWindWalkerBurntFrostSpellId,
                "BurntFrost",
                LegacyPromotionWindWalkerBurntFrostSourceRange,
                cancellationToken),
            LegacyPromotionWindWalkerHealingSnowOption => TryLearnLegacyWindWalkerStomperPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionWindWalkerHealingSnowSpellId,
                "HealingSnow",
                LegacyPromotionWindWalkerHealingSnowSourceRange,
                cancellationToken),
            LegacyPromotionWindWalkerRageOfWarOption => TryLearnLegacyWindWalkerStomperPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionWindWalkerRageOfWarSpellId,
                "RageofWar",
                LegacyPromotionWindWalkerRageOfWarSourceRange,
                cancellationToken),
            LegacyPromotionWindWalkerHorrorOfStomperOption => TryLearnLegacyWindWalkerStomperPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionWindWalkerHorrorOfStomperSpellId,
                "HorrorofStomper",
                LegacyPromotionWindWalkerHorrorOfStomperSourceRange,
                cancellationToken),
            LegacyPromotionWindWalkerChillingSnowOption => TryLearnLegacyWindWalkerStomperPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionWindWalkerChillingSnowSpellId,
                "ChillingSnow",
                LegacyPromotionWindWalkerChillingSnowSourceRange,
                cancellationToken),
            LegacyPromotionWindWalkerPeaceOfStomperOption => TryLearnLegacyWindWalkerStomperPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionWindWalkerPeaceOfStomperSpellId,
                "PeaceofStomper",
                LegacyPromotionWindWalkerPeaceOfStomperSourceRange,
                cancellationToken),
            LegacyPromotionWindWalkerRevengeTailOption => TryLearnLegacyWindWalkerStomperPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionWindWalkerRevengeTailSpellId,
                "RevengeTail",
                LegacyPromotionWindWalkerRevengeTailSourceRange,
                cancellationToken),
            LegacyPromotionWindWalkerFreezingPelterOption => TryLearnLegacyWindWalkerStomperPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionWindWalkerFreezingPelterSpellId,
                "FreezingPelter",
                LegacyPromotionWindWalkerFreezingPelterSourceRange,
                cancellationToken),
            LegacyPromotionWindWalkerOmnipotenceOption => TryLearnLegacyWindWalkerStomperPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionWindWalkerOmnipotenceSpellId,
                "Omnipotence",
                LegacyPromotionWindWalkerOmnipotenceSourceRange,
                cancellationToken),
            LegacyPromotionWindWalkerJusticeChantOption => TryLearnLegacyWindWalkerStomperPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionWindWalkerJusticeChantSpellId,
                "JusticeChant",
                LegacyPromotionWindWalkerJusticeChantSourceRange,
                cancellationToken),
            _ => ValueTask.FromResult(false),
        };
    }

    private static ValueTask<bool> TryLearnLegacyWindWalkerStomperPromotionSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        ushort spellId,
        string spellName,
        string sourceRange,
        CancellationToken cancellationToken)
    {
        return TryLearnLegacyPromotionMasterSkillAsync(
            context,
            session,
            optionId,
            spellId,
            spellName,
            LegacyPromotionWindWalkerSkillLevelGate,
            LegacyPromotionWindWalkerSource,
            sourceRange,
            LegacyPromotionWindWalkerStomperSkillAvatarId,
            cancellationToken);
    }
}
