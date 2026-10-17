using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyAccountSecurityAddStgmaNpcId = 22_155;
    private const uint LegacyAccountSecuritySecurityAnimaNpcId = 489_123;
    private const uint LegacyAccountSecurityFullAccountNpcId = 465_123;
    private const uint LegacyAccountSecurityPkResetNpcId = 52_003;
    private const uint LegacyAccountSecurityNameRegisterNpcId = 7_935;

    private const string LegacyAccountSecurityAddStgmaSource = "NpcHandler.AddSTGMA";
    private const string LegacyAccountSecuritySecurityAnimaSource = "NpcHandler.SecurityAnima";
    private const string LegacyAccountSecurityFullAccountSource = "NpcHandler.FullAccount";
    private const string LegacyAccountSecurityPkResetSource = "NpcHandler.PkReset";
    private const string LegacyAccountSecurityNameRegisterSource = "NpcHandler.NameRegister";

    private const string LegacyAccountSecurityAddStgmaSourceLines = "4546-4570";
    private const string LegacyAccountSecuritySecurityAnimaSourceLines = "4701-4895";
    private const string LegacyAccountSecurityFullAccountSourceLines = "5607-6265";
    private const string LegacyAccountSecurityPkResetSourceLines = "26033-26122";
    private const string LegacyAccountSecurityNameRegisterSourceLines = "26523-26612";

    private static readonly LegacyNpcDialogOption[] LegacyAccountSecurityAddStgmaRootOptions =
    [
        new("Please add it.", 1),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyAccountSecuritySecurityAnimaRootOptions =
    [
        new("Create security code.", 1),
        new("Change password.", 10),
        new("Forgot password.", 5),
        new("Remove password.", 50),
        new("Activate password.", 81),
        new("No, thanks.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyAccountSecurityPkResetRootOptions =
    [
        new("Clear 100 PK points [cost: 2,000,000 CPs].", 1),
        new("Clear 500 PK points [cost: 5,000,000 CPs].", 2),
        new("Forget it.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyAccountSecurityNameRegisterRootOptions =
    [
        new("Okay, here's my CPs!", 1),
        new("Reset edit times.", 10),
        new("I'll think about it.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyAccountSecurityNameRegisterResetOptions =
    [
        new("Yes.", 11),
        new("No, thanks.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyAccountSecurityUnavailableOptions =
    [
        new("I understand.", LegacyNpcClosedOption),
    ];

    public static async ValueTask<bool> TryHandleLegacyAccountSecurityNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        string input,
        CancellationToken cancellationToken)
    {
        return npcId switch
        {
            LegacyAccountSecurityAddStgmaNpcId => await LegacyAccountSecurityHandleAddStgmaAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyAccountSecuritySecurityAnimaNpcId => await LegacyAccountSecurityHandleSecurityAnimaAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    input,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyAccountSecurityFullAccountNpcId => await LegacyAccountSecurityHandleFullAccountAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyAccountSecurityPkResetNpcId => await LegacyAccountSecurityHandlePkResetAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyAccountSecurityNameRegisterNpcId => await LegacyAccountSecurityHandleNameRegisterAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask<bool> LegacyAccountSecurityHandleAddStgmaAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyAccountSecurityAddStgmaSource,
                        ["You can spend 600,000 CPs to receive Stigma and Magic Shield for one hour."],
                        LegacyAccountSecurityAddStgmaRootOptions,
                        avatarId: 286,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                if (session.Character.ConquerPoints <= 500_000)
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            LegacyAccountSecurityAddStgmaSource,
                            ["Sorry, you do not have enough CPs."],
                            [],
                            avatarId: 286,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await ApplyLegacyAccountSecurityAddStgmaAsync(
                        context,
                        session,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> LegacyAccountSecurityHandleSecurityAnimaAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        string input,
        CancellationToken cancellationToken)
    {
        input ??= "";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyAccountSecuritySecurityAnimaSource,
                        [
                            "Hello. I can help protect your Anima with a six-character security code.",
                            "Activated = " + LegacyAccountSecurityReadAnimaActivatedStatus(session),
                        ],
                        LegacyAccountSecuritySecurityAnimaRootOptions,
                        avatarId: 286,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await LegacyAccountSecuritySendInputDialogAsync(
                        context,
                        LegacyAccountSecuritySecurityAnimaSource,
                        "Create a new PIN code.",
                        "Input:",
                        inputOptionId: 2,
                        maxLength: 6,
                        options: [],
                        avatarId: 286,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                if (input.Length != 6)
                {
                    await LegacyAccountSecuritySendInputDialogAsync(
                            context,
                            LegacyAccountSecuritySecurityAnimaSource,
                            "Please enter exactly six letters or digits.",
                            "Input:",
                            inputOptionId: 2,
                            maxLength: 6,
                            options: [],
                            avatarId: 286,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                if (!LegacyAccountSecurityTryStageAnimaPin(
                        context,
                        session,
                        input,
                        out var stagedSession))
                {
                    await LegacyAccountSecuritySendUnavailableAsync(
                            context,
                            LegacyAccountSecuritySecurityAnimaSource,
                            LegacyAccountSecuritySecurityAnimaSourceLines,
                            npcId,
                            optionId,
                            "source pending PIN requires the live session store",
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                var stagedContext = context with { Session = stagedSession };
                await SendLegacyNpcDialogAsync(
                        stagedContext,
                        LegacyAccountSecuritySecurityAnimaSource,
                        ["Is this your PIN code? " + LegacyAccountSecurityMaskTail(input, visibleTailLength: 3)],
                        [new LegacyNpcDialogOption("Yes, use this PIN code.", 3)],
                        avatarId: 286,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await ApplyLegacyAccountSecurityCreateAnimaPinAsync(
                        context,
                        session,
                        npcId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 10:
                await LegacyAccountSecuritySendInputDialogAsync(
                        context,
                        LegacyAccountSecuritySecurityAnimaSource,
                        "Please enter your old PIN code.",
                        "Old PIN:",
                        inputOptionId: 11,
                        maxLength: 6,
                        options: [new LegacyNpcDialogOption("Just looking.", 20)],
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 11:
                await HandleLegacyAccountSecurityAnimaOldPinAsync(
                        context,
                        session,
                        npcId,
                        input,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 12:
                await SendLegacyAccountSecurityAnimaNewPinPromptAsync(
                        context,
                        session,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 13:
                await ApplyLegacyAccountSecurityChangeAnimaPinAsync(
                        context,
                        session,
                        npcId,
                        input,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 5:
                await LegacyAccountSecuritySendInputDialogAsync(
                        context,
                        LegacyAccountSecuritySecurityAnimaSource,
                        "Please enter your email. If no domain is supplied, @yahoo.com is used.",
                        "Yahoo email:",
                        inputOptionId: 6,
                        maxLength: 16,
                        options: [new LegacyNpcDialogOption("Just passing by.", LegacyNpcClosedOption)],
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 6:
                await HandleLegacyAccountSecurityAnimaEmailRecoveryAsync(
                        context,
                        session,
                        input,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 50:
                await LegacyAccountSecuritySendInputDialogAsync(
                        context,
                        LegacyAccountSecuritySecurityAnimaSource,
                        "Please enter your email. If no domain is supplied, @yahoo.com is used.",
                        "Yahoo email:",
                        inputOptionId: 60,
                        maxLength: 16,
                        options: [new LegacyNpcDialogOption("Just passing by.", LegacyNpcClosedOption)],
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 60:
                await ApplyLegacyAccountSecurityAnimaEmailRemovalAsync(
                        context,
                        session,
                        npcId,
                        input,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 81:
                await LegacyAccountSecuritySendInputDialogAsync(
                        context,
                        LegacyAccountSecuritySecurityAnimaSource,
                        "Please enter your old PIN code.",
                        "PIN:",
                        inputOptionId: 80,
                        maxLength: 6,
                        options: [new LegacyNpcDialogOption("Just looking.", 20)],
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 80:
                await ApplyLegacyAccountSecurityActivateAnimaPinAsync(
                        context,
                        session,
                        npcId,
                        input,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> LegacyAccountSecurityHandleFullAccountAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyAccountSecurityFullAccountSource,
                        ["Hey, from here you can use full-account service packs."],
                        LegacyAccountSecurityCreateFullAccountRootOptions(context, session),
                        avatarId: 263,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case >= 1 and <= 11:
                await ApplyLegacyAccountSecurityFullAccountClassAsync(
                        context,
                        session,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 19:
                await ApplyLegacyAccountSecurityFullAccountInnerPowerAsync(
                        context,
                        session,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 12:
            case 13:
            case 14:
                await ApplyLegacyAccountSecurityFullAccountRunesAsync(
                        context,
                        session,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 15:
                await ApplyLegacyAccountSecurityFullAccountRandomRelicAsync(
                        context,
                        session,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 16:
                await ApplyLegacyAccountSecurityFullAccountRedBodhiBeanAsync(
                        context,
                        session,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 17:
                await ApplyLegacyAccountSecurityFullAccountP18AnimaAsync(
                        context,
                        session,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 18:
                await ApplyLegacyAccountSecurityFullAccountCpGiftAsync(
                        context,
                        session,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 20:
            case 21:
                await SendLegacyAccountSecurityFullAccountColorAsync(
                        context,
                        session,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 22:
                await ApplyLegacyAccountSecurityFullAccountSubClassesAsync(
                        context,
                        session,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> LegacyAccountSecurityHandlePkResetAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyAccountSecurityPkResetSource,
                        ["For a CP fee, I can reduce your PK points. Do you want to make this exchange?"],
                        LegacyAccountSecurityPkResetRootOptions,
                        avatarId: 264,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                if (session.Character.ConquerPoints < 2_000_000)
                {
                    await LegacyAccountSecuritySendPkResetSimpleDialogAsync(
                            context,
                            "Sorry, you do not have enough CPs.",
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                if (session.Character.PkPoints < 100)
                {
                    await LegacyAccountSecuritySendPkResetSimpleDialogAsync(
                            context,
                            "Your PK points are too low for this option.",
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await ApplyLegacyAccountSecurityPkResetAsync(
                        context,
                        session,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                if (session.Character.ConquerPoints < 5_000_000)
                {
                    await LegacyAccountSecuritySendPkResetSimpleDialogAsync(
                            context,
                            "Sorry, you do not have enough CPs.",
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                if (session.Character.PkPoints < 500)
                {
                    await LegacyAccountSecuritySendPkResetSimpleDialogAsync(
                            context,
                            "Your PK points are too low for this option.",
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await ApplyLegacyAccountSecurityPkResetAsync(
                        context,
                        session,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                if (session.Character.ConquerPoints < 5_000)
                {
                    await LegacyAccountSecuritySendPkResetSimpleDialogAsync(
                            context,
                            "Sorry, you do not have enough CPs.",
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                if (session.Character.PkPoints < 500)
                {
                    await LegacyAccountSecuritySendPkResetSimpleDialogAsync(
                            context,
                            "Your PK points are too low for this option.",
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await ApplyLegacyAccountSecurityPkResetAsync(
                        context,
                        session,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> LegacyAccountSecurityHandleNameRegisterAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyAccountSecurityNameRegisterSource,
                        [
                            "Good day! For 810 CPs, I can help you get a new name. Your new name will be registered immediately.",
                        ],
                        LegacyAccountSecurityNameRegisterRootOptions,
                        avatarId: 31,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                if (session.Character.ConquerPoints < 810)
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            LegacyAccountSecurityNameRegisterSource,
                            ["Sorry, you do not have enough CPs to rename yourself. It costs 810 CPs."],
                            [new LegacyNpcDialogOption("Rules are rules. I'll wait.", LegacyNpcClosedOption)],
                            avatarId: 31,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await LegacyAccountSecuritySendUnavailableAsync(
                        context,
                        LegacyAccountSecurityNameRegisterSource,
                        LegacyAccountSecurityNameRegisterSourceLines,
                        npcId,
                        optionId,
                        "source opens name-change UI and mutates the name registry",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 10:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyAccountSecurityNameRegisterSource,
                        ["Do you want to reset your name edit count for 10,000 CPs?"],
                        LegacyAccountSecurityNameRegisterResetOptions,
                        avatarId: 31,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 11:
                if (session.Character.ConquerPoints < 10_000)
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            LegacyAccountSecurityNameRegisterSource,
                            ["Sorry, you do not have enough CPs."],
                            [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                            avatarId: 31,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await ApplyLegacyAccountSecurityNameEditResetAsync(
                        context,
                        session,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static IReadOnlyList<LegacyNpcDialogOption> LegacyAccountSecurityCreateFullAccountRootOptions(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        var options = new List<LegacyNpcDialogOption>
        {
            new("Water-Warrior-Warrior.", 1),
            new("Water-Warrior-Trojan.", 2),
            new("Water-Warrior-Water.", 3),
            new("Water-Warrior-Fire.", 4),
            new("Water-Warrior-Archer.", 5),
            new("Water-Warrior-Ninja.", 6),
            new("Water-Warrior-Monk.", 7),
            new("Water-Warrior-Pirate.", 8),
            new("Water-Warrior-DragonWarrior.", 9),
            new("Water-Warrior-WindWalker.", 10),
            new("Water-Warrior-ThunderStrike.", 11),
            new("Full Rune [Red].", 12),
            new("Full Rune [Blue].", 13),
            new("Full Rune [Yellow].", 14),
            new("Random Relic.", 15),
            new("Red Bodhi Bean [20 items].", 16),
            new("Anima [P18].", 17),
            new("CPs [20M free].", 18),
        };

        var innerPowerUnlocked = TryReadLegacyInnerPowerFirstStageUnlocked(
                context,
                session,
                out var loadedInnerPowerUnlocked)
            ? loadedInnerPowerUnlocked
            : LegacyAccountSecurityReadBooleanAttribute(
                session,
                "InnerPowerStage0Unlocked",
                "InnerPowerUnlocked");
        if (session.Character.Level >= 100 &&
            session.Character.Reborn >= 2 &&
            !innerPowerUnlocked)
        {
            options.Add(new LegacyNpcDialogOption("Inner Power.", 19));
        }

        options.Add(new LegacyNpcDialogOption("Subclasses.", 22));
        options.Add(new LegacyNpcDialogOption("Color.", 20));
        options.Add(new LegacyNpcDialogOption("Back color.", 21));
        options.Add(new LegacyNpcDialogOption("Just passing by.", LegacyNpcClosedOption));
        return options;
    }

    private static async ValueTask LegacyAccountSecuritySendPkResetSimpleDialogAsync(
        WorldPacketHandlerContext context,
        string text,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyAccountSecurityPkResetSource,
                [text],
                [new LegacyNpcDialogOption("I understand.", LegacyNpcClosedOption)],
                avatarId: 264,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask LegacyAccountSecuritySendUnavailableAsync(
        WorldPacketHandlerContext context,
        string source,
        string sourceLines,
        uint npcId,
        byte optionId,
        string reason,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                source,
                [
                    "This account-security action is unavailable here until its legacy storage and side effects are fully modeled. No account data was changed.",
                ],
                LegacyAccountSecurityUnavailableOptions,
                avatarId: null,
                cancellationToken)
            .ConfigureAwait(false);
        await LegacyAccountSecurityLogParityGapAsync(context, source, sourceLines, npcId, optionId, reason)
            .ConfigureAwait(false);
    }

    private static async ValueTask LegacyAccountSecuritySendInputDialogAsync(
        WorldPacketHandlerContext context,
        string source,
        string text,
        string inputLabel,
        byte inputOptionId,
        ushort maxLength,
        IReadOnlyList<LegacyNpcDialogOption> options,
        ushort? avatarId,
        CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForDialog(text).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForInput(inputLabel, inputOptionId, maxLength).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var option in options)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForOption(option.Text, option.OptionId).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (avatarId.HasValue)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForAvatar(avatarId.Value).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForFinish().ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyNpc account-security input dialog " +
                $"{FormatSession(context)} source={source} option={inputOptionId.ToString(CultureInfo.InvariantCulture)} " +
                $"maxLength={maxLength.ToString(CultureInfo.InvariantCulture)}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask LegacyAccountSecurityLogParityGapAsync(
        WorldPacketHandlerContext context,
        string source,
        string sourceLines,
        uint npcId,
        byte optionId,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc AccountSecurity " +
                $"{FormatSession(context)} source={source} sourceLines={sourceLines} " +
                $"npcId={npcId.ToString(CultureInfo.InvariantCulture)} optionId={optionId.ToString(CultureInfo.InvariantCulture)}; " +
                reason + ", so WolfConquer leaves account-security state unchanged.")
            .ConfigureAwait(false);
    }

    private static string LegacyAccountSecurityReadAnimaActivatedStatus(WorldSession session)
    {
        if (LegacyAccountSecurityTryReadBooleanAttribute(session, out var active, "TREPIN2", "AnimaSecurityActive"))
        {
            return active ? "true" : "false";
        }

        return "unknown";
    }

    private static bool LegacyAccountSecurityReadBooleanAttribute(WorldSession session, params string[] keys)
    {
        return LegacyAccountSecurityTryReadBooleanAttribute(session, out var value, keys) && value;
    }

    private static bool LegacyAccountSecurityTryReadBooleanAttribute(
        WorldSession session,
        out bool value,
        params string[] keys)
    {
        value = false;
        foreach (var key in keys)
        {
            foreach (var attribute in session.Character.Attributes)
            {
                if (!string.Equals(attribute.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (bool.TryParse(attribute.Value, out value))
                {
                    return true;
                }

                if (long.TryParse(attribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
                {
                    value = numeric != 0;
                    return true;
                }

                return false;
            }
        }

        return false;
    }

    private static string LegacyAccountSecurityMaskTail(string input, int visibleTailLength)
    {
        if (input.Length <= visibleTailLength)
        {
            return input;
        }

        return new string('*', input.Length - visibleTailLength) + input[^visibleTailLength..];
    }
}
