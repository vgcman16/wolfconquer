namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyJailGuildServicesMountTrainerNpcId = 5_600;

    private static readonly LegacyJailGuildServicesSourceInfo LegacyJailGuildServicesMountTrainerSource =
        new("NpcHandler.MountTrainer", "41027-41203");

    private static async ValueTask<bool> TryHandleLegacyJailGuildServicesMountTrainerAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                if (session.Character.Level > 25)
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            LegacyJailGuildServicesMountTrainerSource.SourceMethod,
                            ["Hey, look here. I have good mounts and riding skills. Do you want to learn more?"],
                            [
                                new("Why do I need a mount?", 22),
                                new("I want to learn riding skills.", 10),
                            ],
                            avatarId: 15,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await SendLegacyJailGuildServicesMountTrainerMenuAsync(context, session, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 10:
                await SendLegacyJailGuildServicesMountTrainerMenuAsync(context, session, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 20:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyJailGuildServicesMountTrainerSource.SourceMethod,
                        ["A good steed can improve your movement speed and efficiency. Visit the mount dealer behind me to learn about different mounts."],
                        [new LegacyNpcDialogOption("As you wish.", 21)],
                        avatarId: 15,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 21:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyJailGuildServicesMountTrainerSource.SourceMethod,
                        ["Please visit the mount retailer."],
                        [
                            new("Yes.", 22),
                            new("No.", LegacyNpcClosedOption),
                        ],
                        avatarId: 15,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 22:
                await SendLegacyJailGuildServicesUnavailableAsync(
                        context,
                        LegacyJailGuildServicesMountTrainerSource,
                        LegacyJailGuildServicesMountTrainerNpcId,
                        optionId,
                        "mount trainer quest accept and auto-patcher state are not modeled",
                        avatarId: 15,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyJailGuildServicesMountTrainerSource.SourceMethod,
                        ["After equipping a mount, use the Riding skill from your skill list or shortcut bar."],
                        [
                            new("Learn Riding skill.", 2),
                            new("Let me think.", LegacyNpcClosedOption),
                        ],
                        avatarId: 15,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyJailGuildServicesMountTrainerSource.SourceMethod,
                        ["When mounted, Spook can force a nearby mount with lower lineage to throw its rider and lose stamina."],
                        [
                            new("Learn Spook skill.", 4),
                            new("Let me think.", LegacyNpcClosedOption),
                        ],
                        avatarId: 15,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 5:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyJailGuildServicesMountTrainerSource.SourceMethod,
                        ["WarCry can force nearby mounts with lower lineage to throw their riders and lose stamina. It has a 1 minute cooldown."],
                        [
                            new("Learn WarCry skill.", 6),
                            new("Let me think.", LegacyNpcClosedOption),
                        ],
                        avatarId: 15,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 7:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyJailGuildServicesMountTrainerSource.SourceMethod,
                        ["Charging Vortex is a mounted combat skill that attacks targets near the destination. Learning it costs 500,000 silver."],
                        [
                            new("Learn Charging Vortex.", 8),
                            new("Let me think.", LegacyNpcClosedOption),
                        ],
                        avatarId: 15,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                return await TryHandleLegacyJailGuildServicesMountTrainerLearnAsync(
                        context,
                        session,
                        optionId,
                        spellId: 7_001,
                        cost: 50_000,
                        spellName: "Riding",
                        minimumLevel: 0,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 4:
                return await TryHandleLegacyJailGuildServicesMountTrainerLearnAsync(
                        context,
                        session,
                        optionId,
                        spellId: 7_002,
                        cost: 100_000,
                        spellName: "Spook",
                        minimumLevel: 0,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 6:
                return await TryHandleLegacyJailGuildServicesMountTrainerLearnAsync(
                        context,
                        session,
                        optionId,
                        spellId: 7_003,
                        cost: 1_000_000,
                        spellName: "WarCry",
                        minimumLevel: 0,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 8:
                return await TryHandleLegacyJailGuildServicesMountTrainerLearnAsync(
                        context,
                        session,
                        optionId,
                        spellId: 11_190,
                        cost: 500_000,
                        spellName: "ChargingVortex",
                        minimumLevel: 30,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 9:
            case 11:
            case 12:
            case 13:
            case 14:
            case 15:
            case 16:
            case 17:
            case 18:
            case 19:
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask SendLegacyJailGuildServicesMountTrainerMenuAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyJailGuildServicesMountTrainerSource.SourceMethod,
                ["I am the mount trainer. I can teach riding and mount combat skills for silver."],
                [
                    new("Learn Riding " + LegacyJailGuildServicesSpellSuffix(session, 7_001, "50,000 silver"), 1),
                    new("Learn Spook " + LegacyJailGuildServicesSpellSuffix(session, 7_002, "100,000 silver"), 3),
                    new("Learn WarCry " + LegacyJailGuildServicesSpellSuffix(session, 7_003, "1,000,000 silver"), 5),
                    new("Learn Charging Vortex " + LegacyJailGuildServicesSpellSuffix(session, 11_190, "500,000 silver"), 7),
                    new("Just passing by.", LegacyNpcClosedOption),
                ],
                avatarId: 15,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyJailGuildServicesMountTrainerLearnAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        ushort spellId,
        long cost,
        string spellName,
        long minimumLevel,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level < minimumLevel)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyJailGuildServicesMountTrainerSource.SourceMethod,
                    [$"Sorry, you need to reach level {minimumLevel} first."],
                    [new LegacyNpcDialogOption("Okay, I know.", LegacyNpcClosedOption)],
                    avatarId: 15,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (session.Character.Money < cost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyJailGuildServicesMountTrainerSource.SourceMethod,
                    [LegacyJailGuildServicesMountTrainerInsufficientSilverText(spellId)],
                    [new LegacyNpcDialogOption("Oh, I will earn more silver!", LegacyNpcClosedOption)],
                    avatarId: 15,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (session.PlayerSpells.ContainsSpell(spellId))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyJailGuildServicesMountTrainerSource.SourceMethod,
                    [$"You`ve learned {spellName}!"],
                    LegacyJailGuildServicesOkayOptions,
                    avatarId: 15,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var learned = CreateLegacyPromotionSpellEntry(spellId);
        if (!TryCommitLegacyPaidSpellLearning(context, session, learned, LegacySpellLearningCurrency.Money,
                cost, minimumLevel, requiresTrojanEpicWeapon: false, cancellationToken,
                out var paid, out var reason))
        {
            await LogLegacyPaidSpellRefusalAsync(context, reason).ConfigureAwait(false);
            return true;
        }
        var updatedSession = paid!.Updated;
        var updatedContext = context with { Session = updatedSession };
        if (!await paid.PublishGrantAsync(cancellationToken).ConfigureAwait(false)) return true;
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyJailGuildServicesMountTrainerSource.SourceMethod,
                [$"Congratulation! You just have learned the skill {spellName}"],
                [new LegacyNpcDialogOption("Ok, thank you.", LegacyNpcClosedOption)],
                avatarId: 15,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static string LegacyJailGuildServicesMountTrainerInsufficientSilverText(ushort spellId)
    {
        return spellId switch
        {
            7_001 => "You don`t have 100,000 silver. You cannot learn Riding.",
            7_002 => "You don`t have 100,000 silver. You cannot learn Spook.",
            7_003 => "You don`t have 1,000,000 silver. You cannot learn WarCry.",
            11_190 => "It is really a pity. Learning ChargingVortex costs 500,000 silver, but you do not have enough silver to learn it.",
            _ => throw new ArgumentOutOfRangeException(nameof(spellId), spellId, null),
        };
    }

    private static string LegacyJailGuildServicesSpellSuffix(WorldSession session, uint spellId, string costText)
    {
        return session.PlayerSpells.ContainsSpell(spellId) ? "(learned)" : "(" + costText + ")";
    }
}
