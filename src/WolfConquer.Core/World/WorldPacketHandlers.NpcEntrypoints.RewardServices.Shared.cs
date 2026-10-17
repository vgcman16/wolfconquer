namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyShopRilecNpcId = 456_456;
    private const uint LegacyCliamCupNpcId = 54_613;
    private const uint LegacyRelicNpcId = 111_235;
    private const uint LegacyHosuuNpcId = 25_415;
    private const uint LegacyRewardPointsNpcId = 301_856;
    private const uint LegacyJiangMakerNpcId = 15_532;
    private const ushort LegacyRewardServicesAvatarId = 263;
    private const ushort LegacyHosuuAvatarId = 260;
    private const ushort LegacyJiangMakerAvatarId = 258;

    private static readonly LegacyNpcDialogOption[] LegacyShopRilecOptions =
    [
        new("Relic[Random]", 1),
        new("Just~passing~by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyCliamCupOptions =
    [
        new("Yes,~claim~it.", 1),
        new("Just~passing~by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRelicOptions =
    [
        new("Yes,~I~want~one.", 1),
        new("Just~passing~by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyHosuuOptions =
    [
        new("Yes,~please.", 1),
        new("Just~passing~by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRewardPointsOptions =
    [
        new("MysteryFruit[1~point]", 1),
        new("VIP6~1~Hour[10~points]", 2),
        new("LifeFruitBasket[5~points]", 3),
        new("Just~passing~by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRewardServicesCloseOptions =
    [
        new("I~see.", LegacyNpcClosedOption),
    ];

    private static async ValueTask SendLegacyRewardServicesNoticeAsync(
        WorldPacketHandlerContext context,
        string source,
        string text,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                source,
                [text],
                LegacyRewardServicesCloseOptions,
                LegacyRewardServicesAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyRewardServicesGapAsync(
        WorldPacketHandlerContext context,
        string source,
        string sourceLines,
        byte optionId,
        string effect,
        string text,
        CancellationToken cancellationToken)
    {
        await SendLegacyRewardServicesNoticeAsync(context, source, text, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                LegacyParityGapDiagnostics.Create(
                        LegacyParityGapCategory.Npc,
                        "LegacyNpc RewardServices",
                        text,
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("source", source),
                            LegacyParityGapDiagnostics.Fact("sourceLines", sourceLines),
                            LegacyParityGapDiagnostics.Fact("optionId", optionId),
                            LegacyParityGapDiagnostics.Fact("effect", effect),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("source", source),
                            LegacyParityGapDiagnostics.Fact("optionId", optionId),
                        ])
                    .FormatLogMessage())
            .ConfigureAwait(false);
    }
}
