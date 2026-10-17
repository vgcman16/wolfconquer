using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static readonly LegacyNpcDialogOption[] LegacyMarialDealerRootOptions =
    [
        new("Update spells.", 70),
        new("Nothing.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyMarialDealerPage1Options =
    [
        new("Viper Fang.", 1),
        new("Fire of Hell.", 2),
        new("Toxic Fog.", 3),
        new("Twofold Blades.", 4),
        new("Super Twofold Blades.", 200),
        new("Blistering Wave.", 5),
        new("Tornado.", 6),
        new("Whirlwind Kick.", 7),
        new("Next Page.", 8),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyMarialDealerPage2Options =
    [
        new("Blade Tempest.", 9),
        new("Dagger Storm.", 10),
        new("Radiant Palm.", 11),
        new("Scent Sword.", 12),
        new("Charging Vortex.", 13),
        new("Dragon Tail.", 14),
        new("Back Page.", 70),
        new("Next Page.", 16),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyMarialDealerPage3Options =
    [
        new("Snow.", 15),
        new("Bloody Scythe.", 17),
        new("Fast Blade.", 18),
        new("Phoenix.", 19),
        new("Gale Bomb.", 20),
        new("Hercules.", 21),
        new("Back Page.", 8),
    ];

    private static async ValueTask<bool> TryHandleLegacyMarialDealerAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.MarialDealer";

        if (!TryReadLegacyJiangHuGrade(session, out var grade) || grade < 9)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["Listen, you cannot acquire any Skill Souls until your Jiang Hu score reaches level 9. Keep practicing!"],
                    [new("Alright.", LegacyNpcClosedOption)],
                    null,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!grade.HasValue)
            {
                await LogLegacyEventUtilityGapAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "source checks client.Player.MyJiangHu.GetGrade; no local JiangHu grade is present")
                    .ConfigureAwait(false);
            }

            return true;
        }

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["What do you want from me?"],
                        LegacyMarialDealerRootOptions,
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 70:
                await SendLegacyMarialDealerMenuAsync(
                        context,
                        sourceMethod,
                        LegacyMarialDealerPage1Options,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 8:
                await SendLegacyMarialDealerMenuAsync(
                        context,
                        sourceMethod,
                        LegacyMarialDealerPage2Options,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 16:
                await SendLegacyMarialDealerMenuAsync(
                        context,
                        sourceMethod,
                        LegacyMarialDealerPage3Options,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 1 or 2 or 3 or 4 or 5 or 6 or 7 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 17 or 18 or 19 or 20 or 21 or 200:
                await SendLegacyEventUtilityUnavailableAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "Spell Soul updating is unavailable here.",
                        "source calls UpdateSpellSoul, spends CPs, and mutates persistent spell state",
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static ValueTask SendLegacyMarialDealerMenuAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        IReadOnlyList<LegacyNpcDialogOption> options,
        CancellationToken cancellationToken)
    {
        return SendLegacyNpcDialogAsync(
            context,
            sourceMethod,
            ["I can update some spells to souls. The source charges CPs based on your Jiang Hu grade."],
            options,
            null,
            cancellationToken);
    }

    private static bool TryReadLegacyJiangHuGrade(WorldSession session, out byte? grade)
    {
        grade = null;
        foreach (var key in new[] { "JiangHuGrade", "JiangHuScoreGrade", "MyJiangHuGrade" })
        {
            if (session.Character.Attributes.TryGetValue(key, out var raw) &&
                byte.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                grade = parsed;
                return true;
            }
        }

        return false;
    }
}
