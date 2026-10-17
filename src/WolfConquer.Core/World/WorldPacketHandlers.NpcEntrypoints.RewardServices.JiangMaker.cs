using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryHandleLegacyJiangMakerAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.JiangMaker";
        if (optionId == 0)
        {
            var activeStages = ReadAttributeIntList(session.Character.Attributes, "JiangHuActiveStages");
            if (activeStages is null)
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        ["You have not created a Jiang Hu skill yet. Please learn one before returning."],
                        LegacyRewardServicesCloseOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            var options = activeStages
                .Where(stage => stage is >= 1 and <= 9)
                .Distinct()
                .Order()
                .Select(stage => new LegacyNpcDialogOption($"Stage~[{stage.ToString(CultureInfo.InvariantCulture)}]", (byte)stage))
                .Append(new LegacyNpcDialogOption("I~will~think~about~it.", LegacyNpcClosedOption))
                .ToArray();
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    ["I am the martial master. Do you need my help?"],
                    options,
                    LegacyJiangMakerAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId is >= 1 and <= 9)
        {
            var options = GetLegacyJiangMakerAttributeOptions(context, optionId);
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    [$"Choose the attribute to apply to Stage {optionId.ToString(CultureInfo.InvariantCulture)}."],
                    options,
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId >= 10)
        {
            if (session.Character.ConquerPoints < 10_000)
            {
                await SendLegacyRewardServicesNoticeAsync(
                        context,
                        source,
                        "You do not have 10,000 CPs.",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            await SendLegacyRewardServicesGapAsync(
                    context,
                    source,
                    "7907-8008",
                    optionId,
                    "spend 10,000 CPs and rewrite all stars in the selected Jiang Hu stage",
                    "Jiang Hu star rewriting is unavailable here until Jiang Hu stage mutation and CP persistence are modeled safely.",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static IReadOnlyList<LegacyNpcDialogOption> GetLegacyJiangMakerAttributeOptions(
        WorldPacketHandlerContext context,
        byte stage)
    {
        if (context.WorldData?.JiangHuCultivateStatusesById.TryGetValue(stage, out var status) == true)
        {
            return status.Values
                .Where(value => value is >= 1 and <= 15)
                .Select(value => new LegacyNpcDialogOption(GetLegacyJiangMakerAttributeName(value), checked((byte)(value * 10 + stage))))
                .ToArray();
        }

        return
        [
            new("Max~Hitpoints", checked((byte)(1 * 10 + stage))),
            new("Physical~Attack", checked((byte)(2 * 10 + stage))),
            new("Magic~Attack", checked((byte)(3 * 10 + stage))),
        ];
    }

    private static string GetLegacyJiangMakerAttributeName(int value)
    {
        return value switch
        {
            1 => "Max~Hitpoints",
            2 => "Physical~Attack",
            3 => "Magic~Attack",
            4 => "Magic~Defense",
            5 => "Critical~Strike",
            6 => "Skill~Critical~Strike",
            7 => "Immunity",
            8 => "Breakthrough",
            9 => "Counteraction",
            10 => "Physical~Damage~Increase",
            11 => "Physical~Damage~Decrease",
            12 => "Magic~Damage~Increase",
            13 => "Magic~Damage~Decrease",
            14 => "Final~Physical~Damage",
            15 => "Final~Magic~Damage",
            _ => $"Attribute~{value.ToString(CultureInfo.InvariantCulture)}",
        };
    }
}
