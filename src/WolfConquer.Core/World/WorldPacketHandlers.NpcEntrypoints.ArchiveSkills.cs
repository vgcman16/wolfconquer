using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyArchiveWarriorNpcId = 21_010;
    private const uint LegacyArchivePirateNpcId = 74_746;
    private const uint LegacyArchiveArcherNpcId = 21_011;
    private const uint LegacyArchiveFireNpcId = 5_541;
    private const uint LegacyAllNinjaSkillNpcId = 1_021;
    private const uint LegacySwordMasterNpcId = 24_312;
    private const uint LegacySupClassNpcId = 78_964;
    private const uint LegacyNewJobCenterNpcId = 8_591;
    private const uint LegacyAllNinjaRandomSkillBookItemId = 729_464;
    private const long LegacyArchiveUnlockCost = 100_000;
    private const long LegacyAllNinjaRandomSkillBookCost = 1_000_000;
    private const long LegacySupClassCost = 10_000_000;
    private const ushort LegacyArchiveSkillsAvatarId = 286;
    private const ushort LegacySupClassAvatarId = 265;
    private const ushort LegacyNewJobCenterAvatarId = 76;
    private const ushort LegacySwordMasterAvatarId = 215;

    private static readonly LegacyNpcDialogOption[] LegacyArchiveWarriorOptions =
    [
        new("Unlock~Dragonhowl.", 1),
        new("Unlock~Bloodlust.", 2),
        new("Unlock~Redcurse.", 3),
        new("Maybe~later.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyArchivePirateOptions =
    [
        new("First~ability.", 1),
        new("Second~ability.", 2),
        new("Third~ability.", 3),
        new("Maybe~later.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyArchiveNinjaOptions =
    [
        new("Teach~me~all~archive~skills.", 1),
        new("Buy~a~random~skill~book.", 2),
        new("Maybe~later.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySwordMasterOptions =
    [
        new("Awaken~weapon.", 2),
        new("Unlock~awakening~skill.", 3),
        new("View~awakening~status.", 1),
        new("Maybe~later.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySupClassOptions =
    [
        new("Fix~Supclass.", 1),
        new("No~thanks.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyNewJobCenterOptions =
    [
        new("Yes,~please.", 1),
        new("See~you~later.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyArchiveSingleCloseOptions =
    [
        new("I~see.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyArchiveCombatGearOption[] LegacyArchiveWarriorGearOptions =
    [
        new(1, LegacyCombatGearDragonhowlItemId, "Dragonhowl"),
        new(2, LegacyCombatGearBloodlustItemId, "Bloodlust"),
        new(3, LegacyCombatGearRedcurseItemId, "Redcurse"),
    ];

    private static readonly LegacyArchiveCombatGearOption[] LegacyArchivePirateGearOptions =
    [
        new(1, 701, "Pirate first ability"),
        new(2, 702, "Pirate second ability"),
        new(3, 703, "Pirate third ability"),
    ];

    private static readonly LegacyArchiveCombatGearOption[] LegacyArchiveArcherGearOptions =
    [
        new(1, LegacyCombatGearStoneCrackerItemId, "Stone Cracker"),
        new(2, LegacyCombatGearColdMoonItemId, "Cold Moon"),
        new(3, LegacyCombatGearTharnCutterItemId, "Tharn Cutter"),
    ];

    private static readonly LegacyArchiveCombatGearOption[] LegacyArchiveFireGearOptions =
    [
        new(1, LegacyCombatGearTaoist1ItemId, "Withered Bloom"),
        new(2, LegacyCombatGearTaoist2ItemId, "Supreme Kindness"),
        new(3, LegacyCombatGearTaoist3ItemId, "Transformation"),
        new(4, LegacyCombatGearTaoist4ItemId, "Soul Shock"),
        new(5, LegacyCombatGearTaoist5ItemId, "Life and Death"),
    ];

    private static readonly byte[] LegacySupClassIds =
    [
        1,
        2,
        3,
        4,
        5,
        9,
        6,
    ];

    internal static async ValueTask<bool> TryHandleLegacyArchiveSkillsNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return npcId switch
        {
            LegacyArchiveWarriorNpcId => await TryHandleLegacyArchiveWarriorAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyArchivePirateNpcId => await TryHandleLegacyArchivePirateAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyArchiveArcherNpcId => await TryHandleLegacyArchiveArcherAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyArchiveFireNpcId => await TryHandleLegacyArchiveFireAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyAllNinjaSkillNpcId => await TryHandleLegacyAllNinjaSkillAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacySwordMasterNpcId => await TryHandleLegacySwordMasterAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacySupClassNpcId => await TryHandleLegacySupClassAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyNewJobCenterNpcId => await TryHandleLegacyNewJobCenterAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask<bool> TryHandleLegacyArchiveWarriorAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.ArchiveWarrior",
                    ["Your martial training can awaken further. Second-reborn heroes can unlock these archive abilities for 100,000 CPs each."],
                    LegacyArchiveWarriorOptions,
                    LegacyArchiveSkillsAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return await TryHandleLegacyArchiveCombatGearUnlockAsync(
                context,
                session,
                "NpcHandler.ArchiveWarrior",
                optionId,
                LegacyArchiveWarriorGearOptions,
                classAllowed: true,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyArchivePirateAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            if (!IsLegacyArchivePirateClass(session.Character.Class))
            {
                await SendLegacyClassMismatchDialogAsync(context, "NpcHandler.ArciveParite", cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.ArciveParite",
                    ["If you are second reborn and carry 100,000 CPs, I can help you study an archive ability."],
                    LegacyArchivePirateOptions,
                    LegacyArchiveSkillsAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return await TryHandleLegacyArchiveCombatGearUnlockAsync(
                context,
                session,
                "NpcHandler.ArciveParite",
                optionId,
                LegacyArchivePirateGearOptions,
                IsLegacyArchivePirateClass(session.Character.Class),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyArchiveArcherAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            if (!IsLegacyArcherClass(session.Character.Class))
            {
                await SendLegacyClassMismatchDialogAsync(context, "NpcHandler.ArchiveArcher", cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            if (session.Character.Reborn < 2)
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.ArchiveArcher",
                        ["You have not reached second rebirth yet."],
                        LegacyArchiveSingleCloseOptions,
                        LegacyArchiveSkillsAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            if (session.Character.ConquerPoints < LegacyArchiveUnlockCost)
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.ArchiveArcher",
                        ["You need 100,000 CPs before I can help you study an archive ability."],
                        LegacyArchiveSingleCloseOptions,
                        LegacyArchiveSkillsAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            var next = LegacyArchiveArcherGearOptions.FirstOrDefault(option =>
                !HasLegacyCombatGearItem(session.CombatGear ?? CombatGearSnapshot.Empty, option.ItemId));
            if (next.OptionId == 0)
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.ArchiveArcher",
                        ["You have already learned these archive abilities."],
                        LegacyArchiveSingleCloseOptions,
                        LegacyArchiveSkillsAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.ArchiveArcher",
                    ["If you are second reborn and carry 100,000 CPs, I can help you study your next archive ability."],
                    [new LegacyNpcDialogOption("Unlock~" + next.Name.Replace(' ', '~') + ".", next.OptionId), new LegacyNpcDialogOption("Maybe~later.", LegacyNpcClosedOption)],
                    LegacyArchiveSkillsAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return await TryHandleLegacyArchiveCombatGearUnlockAsync(
                context,
                session,
                "NpcHandler.ArchiveArcher",
                optionId,
                LegacyArchiveArcherGearOptions,
                IsLegacyArcherClass(session.Character.Class),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyArchiveFireAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var sourceClassGate = IsLegacyWaterTaoistClass(session.Character.Class) &&
            IsLegacyFireTaoistClass(session.Character.Class) &&
            IsLegacyTaoistClass(session.Character.Class);
        if (optionId == 0)
        {
            if (!sourceClassGate)
            {
                await SendLegacyClassMismatchDialogAsync(context, "NpcHandler.ArchiveFire", cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.ArchiveFire",
                    ["Taoist archive artifacts require second rebirth and 100,000 CPs for each study step."],
                    [new LegacyNpcDialogOption("Withered~Bloom.", 1), new LegacyNpcDialogOption("Maybe~later.", LegacyNpcClosedOption)],
                    LegacyArchiveSkillsAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return await TryHandleLegacyArchiveCombatGearUnlockAsync(
                context,
                session,
                "NpcHandler.ArchiveFire",
                optionId,
                LegacyArchiveFireGearOptions,
                sourceClassGate,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyAllNinjaSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            if (!IsLegacyNinjaClass(session.Character.Class))
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.AllNinjaSKill",
                        ["You are not a Ninja."],
                        LegacyArchiveSingleCloseOptions,
                        LegacyArchiveSkillsAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.AllNinjaSKill",
                    ["I have mastered the secret Ninja archive skills. I can teach the archive path for 100,000 CPs, or sell a random Ninja skill book for 1,000,000 CPs."],
                    LegacyArchiveNinjaOptions,
                    LegacyArchiveSkillsAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!IsLegacyNinjaClass(session.Character.Class))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc AllNinjaSKill " +
                    $"{FormatSession(context)} option={optionId} class={session.Character.Class}; " +
                    "source menu only offers this branch to Ninja classes.")
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 1)
        {
            if (session.Character.ConquerPoints < LegacyArchiveUnlockCost || session.Character.Reborn < 2)
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.AllNinjaSKill",
                        ["You need 100,000 CPs and second rebirth before I can teach those archive skills."],
                        LegacyArchiveSingleCloseOptions,
                        LegacyArchiveSkillsAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            await SendUnsupportedArchiveStateDialogAsync(
                    context,
                    "NpcHandler.AllNinjaSKill",
                    optionId,
                    "MyNinja.AddFlag archive flag storage and MsgNinjaUser packets are not modeled",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 2)
        {
            return await TryBuyLegacyAllNinjaRandomSkillBookAsync(context, session, cancellationToken)
                .ConfigureAwait(false);
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacySwordMasterAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.SwordMaster",
                    [
                        "Weapon awakening can unlock surprising power.",
                        "Guardian space is the most impressive awakening state. You can enter weapon awakening and preview its guardian states here.",
                    ],
                    LegacySwordMasterOptions,
                    LegacySwordMasterAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId is 1 or 2 or 3 or 4 || optionId is >= 10 and <= 90)
        {
            await SendUnsupportedArchiveStateDialogAsync(
                    context,
                    "NpcHandler.SwordMaster",
                    optionId,
                    "HundredWeapons archive state and client window actions are not modeled in this slice",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacySupClassAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            if (session.Character.Level >= 100 && session.Character.Reborn >= 2)
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.SUpClass",
                        [$"Hello, {session.Character.Name}. I can fix your Supclass for 10,000,000 CPs."],
                        LegacySupClassOptions,
                        LegacySupClassAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.SUpClass",
                    ["Please reach level 100 and second rebirth first."],
                    LegacyArchiveSingleCloseOptions,
                    261,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId != 1)
        {
            return false;
        }

        if (session.Character.Level < 100 || session.Character.Reborn < 2)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.SUpClass",
                    ["Please reach level 100 and second rebirth first."],
                    LegacyArchiveSingleCloseOptions,
                    261,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var currentState = ReadLegacySubClassState(session.Character);
        if (currentState.Entries.FirstOrDefault(entry => entry.Id == 5)?.Level >= 1)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.SUpClass",
                    ["You have already fixed your Supclass."],
                    LegacyArchiveSingleCloseOptions,
                    284,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (session.Character.ConquerPoints < LegacySupClassCost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.SUpClass",
                    ["You do not have 10,000,000 CPs."],
                    LegacyArchiveSingleCloseOptions,
                    LegacySupClassAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        await ApplyLegacySupClassFixAsync(context, session, currentState, cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyNewJobCenterAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.NewJopCenter",
                    [$"Hello, {session.Character.Name}. Do you want to enter the new job center to promote and get your skills?"],
                    LegacyNewJobCenterOptions,
                    LegacyNewJobCenterAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId != 1)
        {
            return false;
        }

        if (TryGetLegacyNewJobCenterTeleportDestination(session.Character.Class, out var destination))
        {
            await SendLegacyNpcTeleportAsync(
                    context,
                    session,
                    LegacyNewJobCenterNpcId,
                    optionId,
                    destination,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        await context.Log.WriteLineAsync(
                "PARITY-GAP skipped LegacyNpc NewJopCenter teleport " +
                $"{FormatSession(context)} npcId={LegacyNewJobCenterNpcId} option={optionId} " +
                $"class={session.Character.Class}; source has no matching class range.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyArchiveCombatGearUnlockAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        byte optionId,
        IReadOnlyList<LegacyArchiveCombatGearOption> options,
        bool classAllowed,
        CancellationToken cancellationToken)
    {
        var selected = options.FirstOrDefault(option => option.OptionId == optionId);
        if (selected.OptionId == 0)
        {
            return false;
        }

        if (!classAllowed)
        {
            await SendLegacyClassMismatchDialogAsync(context, sourceMethod, cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (session.Character.ConquerPoints < LegacyArchiveUnlockCost || session.Character.Reborn < 2)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["You need second rebirth and 100,000 CPs for this archive ability."],
                    LegacyArchiveSingleCloseOptions,
                    LegacyArchiveSkillsAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (HasLegacyCombatGearItem(session.CombatGear ?? CombatGearSnapshot.Empty, selected.ItemId))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["You have already learned that archive ability."],
                    LegacyArchiveSingleCloseOptions,
                    LegacyArchiveSkillsAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        await SendUnsupportedArchiveStateDialogAsync(
                context,
                sourceMethod,
                optionId,
                "CombatGear archive item mutation is not modeled because CombatGearSnapshot is load-only",
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryBuyLegacyAllNinjaRandomSkillBookAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (session.Character.ConquerPoints < LegacyAllNinjaRandomSkillBookCost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.AllNinjaSKill",
                    ["You do not have 1,000,000 CPs."],
                    LegacyArchiveSingleCloseOptions,
                    LegacyArchiveSkillsAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.AllNinjaSKill",
                    ["Please make one more space in your inventory."],
                    LegacyArchiveSingleCloseOptions,
                    LegacyArchiveSkillsAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var conquerPointsBefore = session.Character.ConquerPoints;
        var conquerPointsAfter = checked(conquerPointsBefore - LegacyAllNinjaRandomSkillBookCost);
        var conquerPointsDelta = conquerPointsAfter - conquerPointsBefore;
        var updatedCharacter = session.Character with
        {
            ConquerPoints = conquerPointsAfter,
        };
        var grantedItem = CreateLegacyNpcItemGrant(context, session.PlayerItems, LegacyAllNinjaRandomSkillBookItemId);
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat([grantedItem]).OrderBy(item => item.Uid).ToArray(),
        };
        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendScalarUpdateAsync(
                updatedContext,
                updatedCharacter,
                LegacyMsgUpdateDataType.ConquerPoints,
                checked((ulong)updatedCharacter.ConquerPoints),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(grantedItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyItemFollowupsAsync(updatedContext, grantedItem, cancellationToken).ConfigureAwait(false);
        await PersistLegacyArchiveCharacterAttributesAsync(
                updatedContext,
                updatedSession,
                "NpcHandler.AllNinjaSKill",
                "ConquerPoints")
            .ConfigureAwait(false);
        await PersistLegacyNpcItemsAsync(updatedContext, grantedItem.ItemId, "NpcHandler.AllNinjaSKill")
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                updatedContext,
                "NpcHandler.AllNinjaSKill",
                ["You received a Ninja skill book."],
                LegacyArchiveSingleCloseOptions,
                LegacyArchiveSkillsAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc AllNinjaSKill random skill book " +
                $"{FormatSession(updatedContext)} source=NpcHandler.AllNinjaSKill " +
                $"economy=ConquerPoints itemId={LegacyAllNinjaRandomSkillBookItemId} " +
                $"cost={LegacyAllNinjaRandomSkillBookCost.ToString(CultureInfo.InvariantCulture)} " +
                $"conquerPointsBefore={conquerPointsBefore.ToString(CultureInfo.InvariantCulture)} " +
                $"conquerPointsAfter={conquerPointsAfter.ToString(CultureInfo.InvariantCulture)} " +
                $"conquerPointsDelta={conquerPointsDelta.ToString(CultureInfo.InvariantCulture)}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask ApplyLegacySupClassFixAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacySubClassState currentState,
        CancellationToken cancellationToken)
    {
        var entries = currentState.Entries.ToList();
        var learned = new List<LegacySubClassEntry>();
        foreach (var subclassId in LegacySupClassIds)
        {
            var existing = entries.FirstOrDefault(entry => entry.Id == subclassId);
            if (existing is not null)
            {
                continue;
            }

            var entry = new LegacySubClassEntry(subclassId, Level: 1, Phrase: 1);
            entries.Add(entry);
            learned.Add(entry);
        }

        var updatedState = currentState with
        {
            Entries = entries.ToArray(),
        };
        var updatedSubProfInfo = FormatLegacySubClassState(updatedState);
        var updatedHash = CalculateLegacySubClassHashPoint(updatedState);
        var conquerPointsBefore = session.Character.ConquerPoints;
        var conquerPointsAfter = checked(conquerPointsBefore - LegacySupClassCost);
        var conquerPointsDelta = conquerPointsAfter - conquerPointsBefore;
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithCharacterAttribute(
                    session.Character with
                    {
                        ConquerPoints = conquerPointsAfter,
                    },
                    "SubProfInfo",
                    updatedSubProfInfo),
                "SubClassHashPoint",
                updatedHash.ToString(CultureInfo.InvariantCulture)),
            "ConquerPoints",
            conquerPointsAfter.ToString(CultureInfo.InvariantCulture));
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendScalarUpdateAsync(
                updatedContext,
                updatedCharacter,
                LegacyMsgUpdateDataType.ConquerPoints,
                checked((ulong)updatedCharacter.ConquerPoints),
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var entry in learned)
        {
            await context.Connection.SendAsync(
                    new LegacySubClassUpdatePacket(
                        LegacySubClassAction.LearnSubClass,
                        entry.Id,
                        entry.Level,
                        StudyReceive: 0).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await PersistLegacyArchiveCharacterAttributesAsync(
                updatedContext,
                updatedSession,
                "NpcHandler.SUpClass",
                "ConquerPoints",
                "SubProfInfo",
                "SubClassHashPoint")
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                updatedContext,
                "NpcHandler.SUpClass",
                ["Your Supclass has been fixed."],
                LegacyArchiveSingleCloseOptions,
                LegacySupClassAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc SUpClass fix " +
                $"{FormatSession(updatedContext)} source=NpcHandler.SUpClass economy=ConquerPoints " +
                $"cost={LegacySupClassCost.ToString(CultureInfo.InvariantCulture)} " +
                $"conquerPointsBefore={conquerPointsBefore.ToString(CultureInfo.InvariantCulture)} " +
                $"conquerPointsAfter={conquerPointsAfter.ToString(CultureInfo.InvariantCulture)} " +
                $"conquerPointsDelta={conquerPointsDelta.ToString(CultureInfo.InvariantCulture)} " +
                $"learned={string.Join(',', learned.Select(entry => entry.Id.ToString(CultureInfo.InvariantCulture)))}.")
            .ConfigureAwait(false);
    }

    private static LegacySubClassState ReadLegacySubClassState(CharacterSnapshot character)
    {
        return character.Attributes.TryGetValue("SubProfInfo", out var rawState) &&
            LegacySubClassHitpointsCalculator.TryParseLegacyState(rawState, out var state)
                ? state
                : new LegacySubClassState(StudyPoints: 0, ActiveSubClass: 0, Entries: []);
    }

    private static async ValueTask PersistLegacyArchiveCharacterAttributesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        params string[] keys)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            if (string.Equals(key, "ConquerPoints", StringComparison.OrdinalIgnoreCase))
            {
                values[key] = session.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture);
                continue;
            }

            if (session.Character.Attributes.TryGetValue(key, out var value))
            {
                values[key] = value;
            }
        }

        if (values.Count == 0)
        {
            return;
        }

        if (TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                session.Character.Uid,
                values,
                out var path,
                out var reason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer persisted LegacyNpc archive attributes " +
                    $"{FormatSession(context)} source={sourceMethod} path={Path.GetFileName(path)} " +
                    $"keys={string.Join(',', values.Keys)}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc archive attribute persistence " +
                $"{FormatSession(context)} source={sourceMethod} reason={reason}; mutation is session-only.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyClassMismatchDialogAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                ["Your class does not match this archive trainer."],
                LegacyArchiveSingleCloseOptions,
                LegacyArchiveSkillsAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendUnsupportedArchiveStateDialogAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        string reason,
        CancellationToken cancellationToken)
    {
        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                    "ArchiveSkills",
                    sourceMethod,
                    reason,
                    facts:
                    [
                        LegacyParityGapDiagnostics.Fact("optionId", optionId),
                        LegacyParityGapDiagnostics.Fact("source", sourceMethod),
                    ],
                    dedupeFacts:
                    [
                        LegacyParityGapDiagnostics.Fact("source", sourceMethod),
                        LegacyParityGapDiagnostics.Fact("optionId", optionId),
                    ]))
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc archive skill branch " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; {reason}.")
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                ["That archive state is not available yet. Your CPs were not spent."],
                LegacyArchiveSingleCloseOptions,
                LegacyArchiveSkillsAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool HasLegacyCombatGearItem(CombatGearSnapshot combatGear, uint itemId)
    {
        return combatGear.Items.Any(item => item.ItemID == itemId);
    }

    private static bool IsLegacyArchivePirateClass(long classCode)
    {
        return classCode / 1_000 == 7;
    }

    private static bool TryGetLegacyNewJobCenterTeleportDestination(
        long characterClass,
        out LegacyNpcTeleportDestination destination)
    {
        destination = characterClass switch
        {
            >= 4_000 and <= 4_049 => new(10_445, 82, 64, "NpcHandler.NewJopCenter"),
            >= 2_000 and <= 2_049 => new(10_446, 82, 64, "NpcHandler.NewJopCenter"),
            >= 5_000 and <= 5_049 => new(10_447, 82, 64, "NpcHandler.NewJopCenter"),
            >= 1_000 and <= 1_049 => new(10_448, 82, 64, "NpcHandler.NewJopCenter"),
            >= 6_000 and <= 6_049 => new(10_449, 82, 64, "NpcHandler.NewJopCenter"),
            >= 16_000 and <= 16_051 => new(10_450, 82, 64, "NpcHandler.NewJopCenter"),
            >= 7_000 and <= 7_049 => new(10_451, 82, 64, "NpcHandler.NewJopCenter"),
            >= 8_000 and <= 8_049 => new(10_452, 82, 64, "NpcHandler.NewJopCenter"),
            >= 13_000 and <= 14_051 => new(10_453, 82, 64, "NpcHandler.NewJopCenter"),
            10_000 or 10_001 => new(10_453, 82, 64, "NpcHandler.NewJopCenter"),
            >= 9_000 and <= 9_049 => new(10_444, 82, 64, "NpcHandler.NewJopCenter"),
            _ => default,
        };
        return !string.IsNullOrEmpty(destination.SourceMethod);
    }

    private readonly record struct LegacyArchiveCombatGearOption(byte OptionId, uint ItemId, string Name);
}
