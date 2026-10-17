using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string RuntimeLegacyBombPirteAcalanathaRollAttribute = "LegacyBombPirteAcalanathaRoll";
    private const string RuntimeLegacyBombPirteNatureShieldRollAttribute = "LegacyBombPirteNatureShieldRoll";
    private const string RuntimeLegacyGaleBombAcalanathaRollAttribute = "LegacyGaleBombAcalanathaRoll";
    private const string RuntimeLegacyGaleBombNatureShieldRollAttribute = "LegacyGaleBombNatureShieldRoll";
    private const string RuntimeLegacyScurvyBombAcalanathaRollAttribute = "LegacyScurvyBombAcalanathaRoll";
    private const string RuntimeLegacyScurvyBombNatureShieldRollAttribute = "LegacyScurvyBombNatureShieldRoll";
    private const string RuntimeLegacyPirateBombAcalanathaRollAttribute = "LegacyPirateBombAcalanathaRoll";
    private const string RuntimeLegacyPirateBombNatureShieldRollAttribute = "LegacyPirateBombNatureShieldRoll";

    private enum LegacyBombPirtePlayerRuneAvoidance
    {
        None,
        SkipTarget,
        StopPlayerLoop,
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyPirateBombPlayerTargetsWithRuneAvoidance(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        ushort originX,
        ushort originY,
        int targetBudget)
    {
        if (spell.Sort is not (LegacyMagicSort.ScurvyBomb or LegacyMagicSort.BombPirte))
        {
            return SelectLegacyPirateBombPlayerTargets(
                context,
                session,
                spell,
                originX,
                originY,
                targetBudget);
        }

        var selected = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            if (selected.Count >= targetBudget)
            {
                break;
            }

            var targetSession = candidate.ActiveSession.Session;
            var character = targetSession.Character;
            if (LegacyGetDistance(originX, originY, character.X, character.Y) > LegacyPirateBombRadius)
            {
                continue;
            }

            switch (GetLegacyPirateBombPlayerRuneAvoidance(targetSession, context.WorldData, spell.Sort))
            {
                case LegacyBombPirtePlayerRuneAvoidance.StopPlayerLoop:
                    return selected;
                case LegacyBombPirtePlayerRuneAvoidance.SkipTarget:
                    continue;
            }

            selected.Add(candidate);
        }

        return selected;
    }

    private static LegacyBombPirtePlayerRuneAvoidance GetLegacyPirateBombPlayerRuneAvoidance(
        WorldSession targetSession,
        WorldDataCatalog? worldData,
        LegacyMagicSort spellSort)
    {
        if (HasLegacyEquippedRune(targetSession.PlayerItems, worldData, "Acalanatha") &&
            CheckLegacyPirateBombAcalanathaRateRoll(targetSession.Character, spellSort))
        {
            return LegacyBombPirtePlayerRuneAvoidance.StopPlayerLoop;
        }

        if (TryGetLegacyEquippedRuneLevel(targetSession.PlayerItems, worldData, "NatureShield", out var natureShieldLevel) &&
            CheckLegacyPirateBombNatureShieldRateRoll(targetSession.Character, natureShieldLevel, spellSort))
        {
            return spellSort == LegacyMagicSort.ScurvyBomb
                ? LegacyBombPirtePlayerRuneAvoidance.StopPlayerLoop
                : LegacyBombPirtePlayerRuneAvoidance.SkipTarget;
        }

        return LegacyBombPirtePlayerRuneAvoidance.None;
    }

    private static bool HasLegacyEquippedRune(
        PlayerItemSnapshot playerItems,
        WorldDataCatalog? worldData,
        string runeName)
    {
        return TryGetLegacyEquippedRuneLevel(playerItems, worldData, runeName, out _);
    }

    private static bool TryGetLegacyEquippedRuneLevel(
        PlayerItemSnapshot playerItems,
        WorldDataCatalog? worldData,
        string runeName,
        out byte level)
    {
        level = 0;
        foreach (var rune in playerItems.RuneItems)
        {
            if (!IsLegacyWornRunePosition(rune.Position) ||
                !TryGetLegacyItemType(worldData?.ItemsById, rune.ItemId, out var itemType) ||
                !ContainsLegacyItemText(itemType, runeName))
            {
                continue;
            }

            level = checked((byte)(rune.ItemId % 100));
            return true;
        }

        return false;
    }

    private static bool CheckLegacyPirateBombAcalanathaRateRoll(
        CharacterSnapshot targetCharacter,
        LegacyMagicSort spellSort)
    {
        return spellSort == LegacyMagicSort.ScurvyBomb
            ? CheckLegacyBombPirteRateRoll(
                targetCharacter,
                55,
                RuntimeLegacyScurvyBombAcalanathaRollAttribute)
            : CheckLegacyBombPirteRateRoll(
                targetCharacter,
                55,
                RuntimeLegacyBombPirteAcalanathaRollAttribute,
                RuntimeLegacyGaleBombAcalanathaRollAttribute,
                RuntimeLegacyPirateBombAcalanathaRollAttribute);
    }

    private static bool CheckLegacyPirateBombNatureShieldRateRoll(
        CharacterSnapshot targetCharacter,
        byte itemLevel,
        LegacyMagicSort spellSort)
    {
        var rate = GetLegacyBombPirteNatureShieldAvoidanceRate(itemLevel);
        if (rate == 0)
        {
            return false;
        }

        return spellSort == LegacyMagicSort.ScurvyBomb
            ? CheckLegacyBombPirteRateRoll(
                targetCharacter,
                rate,
                RuntimeLegacyScurvyBombNatureShieldRollAttribute)
            : CheckLegacyBombPirteRateRoll(
                targetCharacter,
                rate,
                RuntimeLegacyBombPirteNatureShieldRollAttribute,
                RuntimeLegacyGaleBombNatureShieldRollAttribute,
                RuntimeLegacyPirateBombNatureShieldRollAttribute);
    }

    private static bool CheckLegacyBombPirteRateRoll(
        CharacterSnapshot targetCharacter,
        uint rate,
        params string[] attributeNames)
    {
        if (rate == 0)
        {
            return false;
        }

        if (rate >= 100)
        {
            return true;
        }

        var roll = TryReadUInt32Attribute(targetCharacter.Attributes, out var forcedRoll, attributeNames)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        return roll < rate;
    }

    private static byte GetLegacyBombPirteNatureShieldAvoidanceRate(byte itemLevel)
    {
        return itemLevel switch
        {
            1 => 5,
            2 => 10,
            3 => 15,
            4 => 20,
            5 => 30,
            6 => 40,
            7 => 50,
            8 => 60,
            9 => 70,
            _ => 0,
        };
    }
}
