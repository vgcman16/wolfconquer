using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyAgate1NpcId = 1_882;
    private const uint LegacyAgate2NpcId = 1_883;
    private const uint LegacyAgate3NpcId = 1_884;
    private const uint LegacyAgate4NpcId = 1_885;
    private const uint LegacyAgate5NpcId = 1_886;
    private const uint LegacyAgate6NpcId = 1_887;
    private const uint LegacyAgate7NpcId = 1_888;
    private const uint LegacyPrizeCenterTeleporter1NpcId = 9_619;
    private const uint LegacyPrizeCenterTeleporter2NpcId = 9_620;
    private const uint LegacyPrizeCenterTeleporter3NpcId = 9_621;
    private const uint LegacyPrizeCenterTeleporter4NpcId = 9_622;
    private const uint LegacyMammonEnvoyNpcId = 9_623;
    private const uint LegacyPrizeCenterTeleporter5NpcId = 9_650;
    private const uint LegacyPrizeCenterTeleporter6NpcId = 9_651;
    private const uint LegacyPrizeCenterTeleporter7NpcId = 9_652;
    private const uint LegacyPrizeCenterTeleporter8NpcId = 9_653;

    private const ushort LegacyAgateAvatarId = 31;
    private const ushort LegacyPrizeCenterTeleporterAvatarId = 14;
    private const ushort LegacyMammonEnvoyAvatarId = 60;
    private const uint LegacyAgateRefineryBadgeItemId = 720_148;
    private const ushort LegacyAgate1RefineryBadgeAmount = 2;
    private const int LegacyAgate1RefineryBadgeRequiredCells = 2;
    private const int LegacyItemTypeStackSizeFieldIndex = 46;
    private const string RuntimeLegacyAgateTeleportRollAttribute = "RuntimeLegacyAgateTeleportRoll";
    private const string RuntimeLegacyPrizeCenterTeleporterLastUnixSecondsAttribute =
        "RuntimeLegacyPrizeCenterTeleporterLastUnixSeconds";
    private const long LegacyPrizeCenterTeleporterCooldownSeconds = 180;
    private const string LegacyAgateQuitPromptText =
        "If you quite the Power Arena now, you need to wait for 10 minutes to enter the arena again. Are you sure you want to quit now?";
    private const string LegacyAgateWithdrawPromptText =
        "After you enter a lower stage, there will be some requirements for you to meet before advancing a stage. Are you sure you want to enter a lower stage?";
    private const string LegacyAgateArenaClosedText = "The Power Arena is closed, please come tomorrow!";
    private const string LegacyAgateRewardGapReason =
        "this remaining source branch grants EXP/refinery badges, decrements defeated guardians, sends visual effects, and may advance stages; it is not modeled by the local slice.";
    private const string LegacyAgate1ExperienceRewardMessage =
        "You have claimed experience worth an Exp Ball!";
    private const string LegacyAgate1RefineryBadgeRewardMessage = "You`ve received 2 Refinery Badges!";
    private const string LegacyAgate1RefineryBadgeGuardText =
        "Your inventory is full! Please make 2 cells first before you claim the reward!";
    private const string LegacyPrizeCenterTeleporterDialogText1 =
        "Hello!~You~want~to~exchange~for~rewards~of~the~`Treasure~In~The~Blue`~event?~I~can~send~you~to~the~Prize~Center.~If~I~just~sent~you~to~somewhere,~you~need~to~wait~3~minutes~to~use";
    private const string LegacyPrizeCenterTeleporterDialogText2 =
        "~the~teleporter~again,~since~there~are~too~many~people.~So,~where~do~you~want~to~go?";
    private const string LegacyPrizeCenterTeleporterCooldownText =
        "Sorry,~but~I~remember~I~have~sent~you~to~somewhere~just~now~and~it~hasn`t~been~3~minutes~since~that.~Please~be~patient,~okay?~";
    private const string LegacyMammonEnvoySourceMethod = "NpcHandler.MammonEnvoy";
    private const string LegacyMammonEnvoyDialogText =
        "Welcome~to~the~Prize~Center!~Did~you~bring~the~treasure~we~want?~So,~which~kind~of~coins~you~want~to~exchange,~the~Gold,~Silver~or~the~Copper?";
    private const string LegacyMammonGoldConfirmText =
        "Really?~You~got~a~Gold~Coin?~That`s~great!~So,~are~you~sure~you~want~to~exchange~now?~~You`ll~receive~a~Small~Lottery~Ticket~and~have~a~chance~to~get~a~rare~mount~armor.";
    private const string LegacyMammonSilverConfirmText1 =
        "Are~you~sure~you~want~to~exchange~the~Silver~Coin,~now?~You`ll~receive~a~gift~randomly~picked~from~a~Tiny~Class2~Money~Pouch,~a~Tiny~Horse~Racing~Points~Bag,~3~Praying~Stone~";
    private const string LegacyMammonSilverConfirmText2 =
        "~Scraps(S)~and~3~Special~EXP~Ball~Scraps.~Not~only~that~gift,~you~will~also~get~an~Endurance~Book.";
    private const string LegacyMammonCopperConfirmText1 =
        "You~have~a~Copper~Coin,~right?~Let~me~see~what~you~can~get.~Oh,~here.~You`ll~receive~a~gift~randomly~picked~from~a~Meteor,~a~Tiny~Class1~Money~Pouch,~a~Praying~Stone~Scrap(S)~and~a";
    private const string LegacyMammonCopperConfirmText2 = "~Special~EXP~Ball~Scrap.";

    private static readonly LegacyNpcDialogOption[] LegacyAgateQuitPromptOptions =
    [
        new("Yes,~I~am~sure.", 31),
        new("Let~me~think~about~it!", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyAgate1QuitPromptOptions =
    [
        new("Yes,~I~am~sure.", 21),
        new("Let~me~think~about~it!", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyAgateWithdrawPromptOptions =
    [
        new("Enter~a~lower~stage.", 21),
        new("Let~me~think~about~it!", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyAgate1WithdrawPromptOptions =
    [
        new("Yes,~enter~a~lower~stage.", 11),
        new("Let~me~think~about~it!", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyPrizeCenterTeleporterOptions =
    [
        new("The~Prize~Center.", 1),
        new("Twin~City.", 2),
        new("I`m~just~passing~by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyPrizeCenterTeleporterCooldownOptions =
    [
        new("Alright.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcTeleportPoint[] LegacyAgateMap1770Points =
    [
        new(1_770, 9, 37),
        new(1_770, 37, 8),
        new(1_770, 21, 21),
        new(1_770, 31, 58),
        new(1_770, 55, 33),
        new(1_770, 75, 76),
        new(1_770, 80, 70),
        new(1_770, 88, 59),
    ];

    private static readonly LegacyNpcTeleportPoint[] LegacyAgateMap1771Points =
    [
        new(1_771, 32, 7),
        new(1_771, 34, 54),
        new(1_771, 53, 24),
        new(1_771, 64, 80),
        new(1_771, 23, 19),
        new(1_771, 10, 34),
        new(1_771, 81, 70),
        new(1_771, 86, 58),
    ];

    private static readonly LegacyNpcTeleportPoint[] LegacyAgateMap1772Points =
    [
        new(1_772, 86, 58),
        new(1_772, 28, 10),
        new(1_772, 44, 14),
        new(1_772, 17, 40),
        new(1_772, 61, 89),
        new(1_772, 81, 70),
        new(1_772, 17, 40),
        new(1_772, 61, 89),
    ];

    private static readonly LegacyNpcTeleportPoint[] LegacyAgateMap1773Points =
    [
        new(1_773, 39, 66),
        new(1_773, 65, 37),
        new(1_773, 61, 88),
        new(1_773, 74, 78),
        new(1_773, 89, 60),
        new(1_773, 35, 6),
        new(1_773, 9, 36),
        new(1_773, 22, 20),
    ];

    private static readonly LegacyNpcTeleportPoint[] LegacyAgateMap1774Points =
    [
        new(1_774, 90, 61),
        new(1_774, 73, 79),
        new(1_774, 62, 89),
        new(1_774, 44, 16),
        new(1_774, 20, 48),
        new(1_774, 34, 8),
        new(1_774, 24, 25),
        new(1_774, 21, 37),
    ];

    private static readonly LegacyNpcTeleportPoint[] LegacyAgateMap1775Points =
    [
        new(1_775, 89, 60),
        new(1_775, 78, 73),
        new(1_775, 63, 85),
        new(1_775, 58, 29),
        new(1_775, 27, 53),
        new(1_775, 34, 5),
        new(1_775, 20, 20),
        new(1_775, 6, 35),
    ];

    private static readonly LegacyNpcTeleportPoint[] LegacyAgateMap1777Points =
    [
        new(1_777, 29, 28),
        new(1_777, 53, 34),
        new(1_777, 67, 54),
        new(1_777, 63, 78),
        new(1_777, 38, 59),
        new(1_777, 25, 37),
        new(1_777, 48, 34),
        new(1_777, 34, 44),
    ];

    private static async ValueTask<bool> TryHandleLegacyAgatePrizeCenterAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (TryGetLegacyAgateDefinition(npcId, out var agate))
        {
            return await TryHandleLegacyAgateAsync(context, session, npcId, optionId, agate, cancellationToken)
                .ConfigureAwait(false);
        }

        if (IsLegacyPrizeCenterTeleporter(npcId))
        {
            return await TryHandleLegacyPrizeCenterTeleporterAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (npcId == LegacyMammonEnvoyNpcId)
        {
            return await TryHandleLegacyMammonEnvoyAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false);
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacyAgateAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        LegacyAgateDefinition agate,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            await SendLegacyAgateIntroAsync(context, agate, cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (optionId == agate.ClaimOptionId || optionId == agate.ClaimAndProceedOptionId)
        {
            var proceed = optionId == agate.ClaimAndProceedOptionId;
            await SendLegacyAgateRewardChoiceAsync(context, session.Character, agate, proceed, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == agate.WithdrawPromptOptionId)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    agate.SourceMethod,
                    [LegacyAgateWithdrawPromptText],
                    agate.WithdrawPromptOptions,
                    LegacyAgateAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == agate.WithdrawConfirmOptionId && agate.WithdrawDestinations.Count > 0)
        {
            await SendRandomLegacyNpcTeleportAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    agate.WithdrawDestinations,
                    agate.SourceMethod,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == agate.QuitPromptOptionId)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    agate.SourceMethod,
                    [LegacyAgateQuitPromptText],
                    agate.QuitPromptOptions,
                    LegacyAgateAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == agate.QuitConfirmOptionId)
        {
            await SendLegacyNpcTeleportAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    new LegacyNpcTeleportDestination(1_002, 410, 354, agate.SourceMethod),
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (IsLegacyAgateRewardApplyOption(optionId))
        {
            return await TryHandleLegacyAgateRewardApplyAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    agate,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return false;
    }

    private static async ValueTask SendLegacyAgateIntroAsync(
        WorldPacketHandlerContext context,
        LegacyAgateDefinition agate,
        CancellationToken cancellationToken)
    {
        var options = new List<LegacyNpcDialogOption>();
        if (agate.ClaimAndProceedOptionId.HasValue)
        {
            options.Add(new LegacyNpcDialogOption("Claim~Experience~and~proceed.", agate.ClaimAndProceedOptionId.Value));
        }

        options.Add(new LegacyNpcDialogOption("Claim~Experience.", agate.ClaimOptionId));
        if (agate.WithdrawPromptOptionId.HasValue && agate.WithdrawIntroOptionText is { } withdrawText)
        {
            options.Add(new LegacyNpcDialogOption(withdrawText, agate.WithdrawPromptOptionId.Value));
        }

        options.Add(new LegacyNpcDialogOption("Quit~Arena.", agate.QuitPromptOptionId));

        await SendLegacyNpcDialogAsync(
                context,
                agate.SourceMethod,
                [agate.IntroText],
                options,
                LegacyAgateAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyAgateRewardChoiceAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        LegacyAgateDefinition agate,
        bool proceed,
        CancellationToken cancellationToken)
    {
        if (!IsLegacyPowerArenaAlive(character))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    agate.SourceMethod,
                    [LegacyAgateArenaClosedText],
                    [new LegacyNpcDialogOption("Okay!", LegacyNpcClosedOption)],
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (ReadLegacyAgateDefeatedGuardians(character) < (ulong)agate.RequiredGuardians)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    agate.SourceMethod,
                    [agate.NeedGuardiansText],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await SendLegacyNpcDialogAsync(
                context,
                agate.SourceMethod,
                [agate.RewardChoiceText],
                [
                    new LegacyNpcDialogOption(agate.ExpRewardOptionText, proceed ? (byte)12 : (byte)2),
                    new LegacyNpcDialogOption(agate.BadgeRewardOptionText, proceed ? (byte)13 : (byte)3),
                    new LegacyNpcDialogOption("Let~me~think...", LegacyNpcClosedOption),
                ],
                LegacyAgateAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyAgateRewardApplyAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        LegacyAgateDefinition agate,
        CancellationToken cancellationToken)
    {
        if ((npcId == LegacyAgate1NpcId && optionId == 3) ||
            (npcId == LegacyAgate2NpcId && optionId is 3 or 13))
        {
            return await TryApplyLegacyAgateRefineryBadgeRewardAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    agate,
                    proceed: optionId == 13,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (ReadLegacyAgateDefeatedGuardians(session.Character) < (ulong)agate.RequiredGuardians)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    agate.SourceMethod,
                    [agate.RewardApplyNeedGuardiansText],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if ((npcId == LegacyAgate1NpcId && optionId == 2) ||
            (npcId == LegacyAgate2NpcId && optionId is 2 or 12))
        {
            return await TryApplyLegacyAgateExperienceRewardAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    agate,
                    proceed: optionId == 12,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc Agate reward side effects " +
                $"{FormatSession(context)} npcId={npcId} option={optionId} source={agate.SourceMethod}; " +
                LegacyAgateRewardGapReason)
            .ConfigureAwait(false);

        if (optionId is 12 or 13 && agate.AdvanceDestinations.Count > 0)
        {
            await SendRandomLegacyNpcTeleportAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    agate.AdvanceDestinations,
                    agate.SourceMethod,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return true;
    }

    private static async ValueTask<bool> TryApplyLegacyAgateRefineryBadgeRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        LegacyAgateDefinition agate,
        bool proceed,
        CancellationToken cancellationToken)
    {
        var rewardName = npcId == LegacyAgate1NpcId ? "Agate1" : "Agate2";
        var defeatedGuardians = ReadLegacyAgateDefeatedGuardians(session.Character);
        if (!HasLegacyInventorySpace(session.PlayerItems, LegacyAgate1RefineryBadgeRequiredCells) ||
            defeatedGuardians < (ulong)agate.RequiredGuardians)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    agate.SourceMethod,
                    [LegacyAgate1RefineryBadgeGuardText],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    LegacyAgateAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (context.Sessions is null)
        {
            await context.Log.WriteLineAsync(
                    $"PARITY-GAP LegacyNpc {rewardName} refinery badge reward " +
                    $"{FormatSession(context)} source={agate.SourceMethod} option={optionId}; " +
                    "the world session store is unavailable, so the reward cannot be committed safely.")
                .ConfigureAwait(false);
            return true;
        }

        if (string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            await context.Log.WriteLineAsync(
                    $"PARITY-GAP LegacyNpc {rewardName} refinery badge reward " +
                    $"{FormatSession(context)} source={agate.SourceMethod} option={optionId}; " +
                    "PlayerItemsRoot is unavailable, so the reward cannot be persisted safely.")
                .ConfigureAwait(false);
            return true;
        }

        if (!TryReadItemType(context.WorldData, LegacyAgateRefineryBadgeItemId, out var badgeItemType) ||
            badgeItemType.Fields.Count <= LegacyItemTypeStackSizeFieldIndex ||
            !ushort.TryParse(
                badgeItemType.Fields[LegacyItemTypeStackSizeFieldIndex],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var badgeStackLimit) ||
            badgeStackLimit < LegacyAgate1RefineryBadgeAmount)
        {
            await context.Log.WriteLineAsync(
                    $"PARITY-GAP LegacyNpc {rewardName} refinery badge reward " +
                    $"{FormatSession(context)} source={agate.SourceMethod} option={optionId}; " +
                    $"itemtype data for itemId={LegacyAgateRefineryBadgeItemId} does not expose the source stack limit.")
                .ConfigureAwait(false);
            return true;
        }

        var compatibleStack = session.PlayerItems.InventoryItems.FirstOrDefault(item =>
            item.ItemId == LegacyAgateRefineryBadgeItemId &&
            item.Bound == 0 &&
            (uint)item.StackSize + LegacyAgate1RefineryBadgeAmount <= badgeStackLimit);

        PlayerItemSnapshotItem rewardItem;
        PlayerItemSnapshot updatedItems;
        ushort itemMode;
        if (compatibleStack is not null)
        {
            rewardItem = compatibleStack.WithStackSize(
                checked((ushort)(compatibleStack.StackSize + LegacyAgate1RefineryBadgeAmount)));
            updatedItems = session.PlayerItems with
            {
                InventoryItems = session.PlayerItems.InventoryItems
                    .Select(item => item.Uid == compatibleStack.Uid ? rewardItem : item)
                    .ToArray(),
            };
            itemMode = LegacyGameItemPacket.UpdateItemMode;
        }
        else
        {
            try
            {
                rewardItem = CreateLegacyNpcItemGrant(context, session.PlayerItems, LegacyAgateRefineryBadgeItemId)
                    .WithStackSize(LegacyAgate1RefineryBadgeAmount)
                    .WithBound(0);
            }
            catch (OverflowException)
            {
                await context.Log.WriteLineAsync(
                        $"PARITY-GAP LegacyNpc {rewardName} refinery badge reward " +
                        $"{FormatSession(context)} source={agate.SourceMethod} option={optionId}; " +
                        "the inventory item UID space is exhausted, so the reward cannot be committed safely.")
                    .ConfigureAwait(false);
                return true;
            }

            updatedItems = session.PlayerItems with
            {
                InventoryItems = session.PlayerItems.InventoryItems
                    .Concat([rewardItem])
                    .OrderBy(item => item.Uid)
                    .ToArray(),
            };
            itemMode = LegacyGameItemPacket.AddItemMode;
        }

        LegacyNpcTeleportPoint? advancePoint = null;
        if (proceed && agate.AdvanceDestinations.Count > 0)
        {
            advancePoint = SelectLegacyTeleportPoint(session.Character, agate.AdvanceDestinations);
        }

        if (advancePoint.HasValue)
        {
            var preflight = await PrepareLegacyTeleportAsync(
                    context,
                    session,
                    new LegacyTeleportRequest(agate.SourceMethod, advancePoint.Value.MapId),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!preflight.Allowed)
            {
                return true;
            }

            session = preflight.Session;
        }

        var remainingGuardians = checked(defeatedGuardians - (ulong)agate.RequiredGuardians);
        var updatedCharacter = WithLegacyAgateDefeatedGuardians(session.Character, remainingGuardians);
        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };

        var itemLoader = new PlayerItemSnapshotLoader(context.PlayerItemsRoot);
        try
        {
            itemLoader.SaveByCharacterUid(updatedCharacter.Uid, updatedItems);
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is ArgumentException ||
            ex is NotSupportedException)
        {
            await context.Log.WriteLineAsync(
                    $"PARITY-GAP LegacyNpc {rewardName} refinery badge reward " +
                    $"{FormatSession(context)} source={agate.SourceMethod} option={optionId}; " +
                    $"item reward persistence failed reason={ex.Message}.")
                .ConfigureAwait(false);
            return true;
        }

        var persistenceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DefeatedArenaGuardians"] = remainingGuardians.ToString(CultureInfo.InvariantCulture),
            ["PowerArenaDefeatedGuardians"] = remainingGuardians.ToString(CultureInfo.InvariantCulture),
        };
        if (advancePoint.HasValue)
        {
            var point = advancePoint.Value;
            var persistedCharacter = ReduceLegacyTeleportDestination(
                    context,
                    updatedCharacter,
                    new LegacyTeleportDestination(point.MapId, point.X, point.Y))
                .FinalCharacter;
            AddLegacyAgateAdvancePersistenceValues(persistenceValues, persistedCharacter);
        }

        if (!TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                updatedCharacter.Uid,
                persistenceValues,
                out var persistedPath,
                out var persistenceReason))
        {
            var rollbackReason = "none";
            try
            {
                itemLoader.SaveByCharacterUid(session.Character.Uid, session.PlayerItems);
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is ArgumentException ||
                ex is NotSupportedException)
            {
                rollbackReason = ex.Message;
            }

            await context.Log.WriteLineAsync(
                    $"PARITY-GAP LegacyNpc {rewardName} refinery badge reward " +
                    $"{FormatSession(context)} source={agate.SourceMethod} option={optionId}; " +
                    $"character reward persistence failed reason={persistenceReason} itemRollback={rollbackReason}.")
                .ConfigureAwait(false);
            return true;
        }

        context.Sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        var uid = CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid));

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemRuneRank(LegacyAgate1RefineryBadgeRewardMessage).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyStringPacket.PlayerEffect(uid, "angelwing").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        if (advancePoint.HasValue)
        {
            var teleportedSession = await SendCommittedLegacyAgateAdvanceAsync(
                    context,
                    updatedSession,
                    npcId,
                    optionId,
                    advancePoint.Value,
                    agate.SourceMethod,
                    cancellationToken)
                .ConfigureAwait(false);
            if (teleportedSession is null)
            {
                return true;
            }

            updatedSession = teleportedSession;
            updatedContext = context with { Session = updatedSession };
        }

        await context.Log.WriteLineAsync(
                $"WorldServer applied LegacyNpc {rewardName} refinery badge reward " +
                $"{FormatSession(updatedContext)} source={agate.SourceMethod} option={optionId} " +
                $"guardians={defeatedGuardians}->{remainingGuardians} " +
                $"itemId={LegacyAgateRefineryBadgeItemId} stack={rewardItem.StackSize} " +
                $"itemMode={(itemMode == LegacyGameItemPacket.UpdateItemMode ? "update" : "add")} " +
                $"path={Path.GetFileName(persistedPath)}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryApplyLegacyAgateExperienceRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        LegacyAgateDefinition agate,
        bool proceed,
        CancellationToken cancellationToken)
    {
        var rewardName = npcId == LegacyAgate1NpcId ? "Agate1" : "Agate2";
        var defeatedGuardians = ReadLegacyAgateDefeatedGuardians(session.Character);
        var remainingGuardians = checked(defeatedGuardians - (ulong)agate.RequiredGuardians);
        var modeledExperience = TryCalculateLegacyExpBallUse(
            context.WorldData,
            session.Character,
            LegacyExpBallAmount,
            out var updatedLevel,
            out var updatedExperience);
        if (!modeledExperience && session.Character.Level < LegacyMaxPlayerLevel)
        {
            await context.Log.WriteLineAsync(
                    $"PARITY-GAP LegacyNpc {rewardName} experience reward " +
                    $"{FormatSession(context)} source={agate.SourceMethod} option={optionId}; " +
                    "level experience data is not loaded, so GainExpBall(600) cannot be modeled safely.")
                .ConfigureAwait(false);
            return true;
        }

        if (context.Sessions is null)
        {
            await context.Log.WriteLineAsync(
                    $"PARITY-GAP LegacyNpc {rewardName} experience reward " +
                    $"{FormatSession(context)} source={agate.SourceMethod} option={optionId}; " +
                    "the world session store is unavailable, so the reward cannot be committed safely.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedCharacter = WithLegacyAgateDefeatedGuardians(session.Character, remainingGuardians);
        if (modeledExperience)
        {
            updatedCharacter = updatedCharacter with
            {
                Level = updatedLevel,
                Experience = updatedExperience,
            };
        }

        LegacyNpcTeleportPoint? advancePoint = null;
        if (proceed && agate.AdvanceDestinations.Count > 0)
        {
            advancePoint = SelectLegacyTeleportPoint(updatedCharacter, agate.AdvanceDestinations);
        }

        if (advancePoint.HasValue)
        {
            var preflight = await PrepareLegacyTeleportAsync(
                    context,
                    session,
                    new LegacyTeleportRequest(agate.SourceMethod, advancePoint.Value.MapId),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!preflight.Allowed)
            {
                return true;
            }

            session = preflight.Session;
            updatedCharacter = CarryLegacyTeleportEntrySideEffects(
                    preflight,
                    session with { Character = updatedCharacter })
                .Character;
        }

        var persistenceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DefeatedArenaGuardians"] = remainingGuardians.ToString(CultureInfo.InvariantCulture),
            ["PowerArenaDefeatedGuardians"] = remainingGuardians.ToString(CultureInfo.InvariantCulture),
            ["Level"] = updatedCharacter.Level.ToString(CultureInfo.InvariantCulture),
            ["Experience"] = updatedCharacter.Experience.ToString(CultureInfo.InvariantCulture),
        };
        if (advancePoint.HasValue)
        {
            var point = advancePoint.Value;
            var persistedCharacter = ReduceLegacyTeleportDestination(
                    context,
                    updatedCharacter,
                    new LegacyTeleportDestination(point.MapId, point.X, point.Y))
                .FinalCharacter;
            AddLegacyAgateAdvancePersistenceValues(persistenceValues, persistedCharacter);
        }

        if (!TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                updatedCharacter.Uid,
                persistenceValues,
                out var persistedPath,
                out var persistenceReason))
        {
            await context.Log.WriteLineAsync(
                    $"PARITY-GAP LegacyNpc {rewardName} experience reward " +
                    $"{FormatSession(context)} source={agate.SourceMethod} option={optionId}; " +
                    $"character reward persistence failed reason={persistenceReason}.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions.Set(context.Connection, updatedSession);
        var uid = CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid));

        if (modeledExperience)
        {
            if (updatedCharacter.Level != session.Character.Level)
            {
                await context.Connection.SendAsync(
                        LegacyMsgUpdatePacket
                            .CreateLegacyRuntimeLevelUpdate(uid, checked((ulong)updatedCharacter.Level))
                            .ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await context.Connection.SendAsync(
                    LegacyMsgUpdatePacket
                        .CreateLegacyRuntimeExperienceUpdate(uid, checked((ulong)updatedCharacter.Experience))
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyStringPacket.PlayerEffect(uid, "angelwing").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk(LegacyAgate1ExperienceRewardMessage).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        if (advancePoint.HasValue)
        {
            var teleportedSession = await SendCommittedLegacyAgateAdvanceAsync(
                    context,
                    updatedSession,
                    npcId,
                    optionId,
                    advancePoint.Value,
                    agate.SourceMethod,
                    cancellationToken)
                .ConfigureAwait(false);
            if (teleportedSession is null)
            {
                return true;
            }

            updatedSession = teleportedSession;
        }

        var experienceEvidence = modeledExperience
            ? $"experience={session.Character.Experience}->{updatedCharacter.Experience} " +
                $"level={session.Character.Level}->{updatedCharacter.Level}"
            : "source GainExpBall returns without EXP mutation at level 140 or above";
        await context.Log.WriteLineAsync(
                $"WorldServer applied LegacyNpc {rewardName} experience reward " +
                $"{FormatSession(context with { Session = updatedSession })} source={agate.SourceMethod} " +
                $"option={optionId} guardians={defeatedGuardians}->{remainingGuardians} " +
                $"GainExpBall({LegacyExpBallAmount.ToString(CultureInfo.InvariantCulture)}) {experienceEvidence} " +
                $"path={Path.GetFileName(persistedPath)}.")
            .ConfigureAwait(false);
        return true;
    }

    private static void AddLegacyAgateAdvancePersistenceValues(
        IDictionary<string, string> values,
        CharacterSnapshot character)
    {
        values["Map"] = character.MapId.ToString(CultureInfo.InvariantCulture);
        values["X"] = character.X.ToString(CultureInfo.InvariantCulture);
        values["Y"] = character.Y.ToString(CultureInfo.InvariantCulture);
        values["PMap"] = character.PreviousMapId.ToString(CultureInfo.InvariantCulture);
        values["PMapX"] = character.PreviousMapX.ToString(CultureInfo.InvariantCulture);
        values["PMapY"] = character.PreviousMapY.ToString(CultureInfo.InvariantCulture);
        values["DynamicID"] = character.Attributes.TryGetValue("DynamicID", out var dynamicId)
            ? dynamicId
            : "0";
        if (character.Attributes.TryGetValue("PDinamycID", out var previousDynamicId))
        {
            values["PDinamycID"] = previousDynamicId;
        }
    }

    private static async ValueTask<WorldSession?> SendCommittedLegacyAgateAdvanceAsync(
        WorldPacketHandlerContext context,
        WorldSession updatedSession,
        uint npcId,
        byte optionId,
        LegacyNpcTeleportPoint point,
        string sourceMethod,
        CancellationToken cancellationToken)
    {
        var teleportedSession = await ExecuteLegacyTeleportDestinationAsync(
                context with { Session = updatedSession },
                updatedSession,
                new LegacyTeleportDestination(point.MapId, point.X, point.Y),
                timestamp: 0,
                cancellationToken)
            .ConfigureAwait(false);
        if (teleportedSession is null)
        {
            return null;
        }

        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyNpc teleport transition " +
                $"{FormatSession(context with { Session = teleportedSession })} npcId={npcId} option={optionId} source={sourceMethod} " +
                $"map={point.MapId} x={point.X} y={point.Y}.")
            .ConfigureAwait(false);
        return teleportedSession;
    }

    private static async ValueTask<bool> TryHandleLegacyPrizeCenterTeleporterAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var sourceMethod = GetLegacyPrizeCenterTeleporterSourceMethod(npcId);
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    [LegacyPrizeCenterTeleporterDialogText1, LegacyPrizeCenterTeleporterDialogText2],
                    LegacyPrizeCenterTeleporterOptions,
                    LegacyPrizeCenterTeleporterAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 2)
        {
            await SendLegacyNpcTeleportAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    new LegacyNpcTeleportDestination(1_002, 410, 354, sourceMethod),
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 1)
        {
            return await TrySendLegacyPrizeCenterTeleportAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    sourceMethod,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return false;
    }

    private static async ValueTask<bool> TrySendLegacyPrizeCenterTeleportAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        string sourceMethod,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (TryReadInt64Attribute(
                session.Character.Attributes,
                RuntimeLegacyPrizeCenterTeleporterLastUnixSecondsAttribute,
                out var lastTeleportSeconds) &&
            now <= checked(lastTeleportSeconds + LegacyPrizeCenterTeleporterCooldownSeconds))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    [LegacyPrizeCenterTeleporterCooldownText],
                    LegacyPrizeCenterTeleporterCooldownOptions,
                    LegacyPrizeCenterTeleporterAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var preflight = await PrepareLegacyTeleportAsync(
                context,
                session,
                new LegacyTeleportRequest(sourceMethod, 1_068),
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflight.Allowed)
        {
            return true;
        }

        session = preflight.Session;

        var updatedCharacter = WithCharacterAttribute(
            session.Character,
            RuntimeLegacyPrizeCenterTeleporterLastUnixSecondsAttribute,
            now.ToString(CultureInfo.InvariantCulture));
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);

        await SendLegacyNpcTeleportAsync(
                context,
                updatedSession,
                npcId,
                optionId,
                new LegacyNpcTeleportDestination(1_068, 33, 33, sourceMethod),
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyMammonEnvoyAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyMammonEnvoySourceMethod,
                    [LegacyMammonEnvoyDialogText],
                    CreateLegacyMammonEnvoyOptions(session.Character),
                    LegacyMammonEnvoyAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 1)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyMammonEnvoySourceMethod,
                    [LegacyMammonGoldConfirmText],
                    [
                        new LegacyNpcDialogOption("Yeah,~please!", 2),
                        new LegacyNpcDialogOption("Sorry,~wait!~Wait!", LegacyNpcClosedOption),
                    ],
                    LegacyMammonEnvoyAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 20)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyMammonEnvoySourceMethod,
                    [LegacyMammonSilverConfirmText1, LegacyMammonSilverConfirmText2],
                    [
                        new LegacyNpcDialogOption("Yeah,~please!", 21),
                        new LegacyNpcDialogOption("No,~not~now!", LegacyNpcClosedOption),
                    ],
                    LegacyMammonEnvoyAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 30)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyMammonEnvoySourceMethod,
                    [LegacyMammonCopperConfirmText1, LegacyMammonCopperConfirmText2],
                    [
                        new LegacyNpcDialogOption("Yeah,~please!", 31),
                        new LegacyNpcDialogOption("Can~I~say~`NO`?", LegacyNpcClosedOption),
                    ],
                    LegacyMammonEnvoyAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 40)
        {
            await SendLegacyNpcTeleportAsync(
                    context,
                    session,
                    LegacyMammonEnvoyNpcId,
                    optionId,
                    new LegacyNpcTeleportDestination(1_002, 410, 354, LegacyMammonEnvoySourceMethod),
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static LegacyNpcDialogOption[] CreateLegacyMammonEnvoyOptions(CharacterSnapshot character)
    {
        var goldRemain = ReadAttributeUInt64(character.Attributes, "PrizeCenterGoldRemain", "GoldRemain");
        var silverRemain = ReadAttributeUInt64(character.Attributes, "PrizeCenterSilverRemain", "SilverRemain");
        var copperRemain = ReadAttributeUInt64(character.Attributes, "PrizeCenterCopperRemain", "CopperRemain");

        return
        [
            new LegacyNpcDialogOption($"Gold.~(Remain:{goldRemain.ToString(CultureInfo.InvariantCulture)})", 1),
            new LegacyNpcDialogOption($"Silver.(Remain:{silverRemain.ToString(CultureInfo.InvariantCulture)})", 20),
            new LegacyNpcDialogOption($"Copper.(Remain:{copperRemain.ToString(CultureInfo.InvariantCulture)})", 30),
            new LegacyNpcDialogOption("Send~me~to~Twin~City.", 40),
            new LegacyNpcDialogOption("I`ll~think~it~over.", LegacyNpcClosedOption),
        ];
    }

    private static async ValueTask SendRandomLegacyNpcTeleportAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        IReadOnlyList<LegacyNpcTeleportPoint> points,
        string sourceMethod,
        CancellationToken cancellationToken)
    {
        var point = SelectLegacyTeleportPoint(session.Character, points);
        await SendLegacyNpcTeleportAsync(
                context,
                session,
                npcId,
                optionId,
                new LegacyNpcTeleportDestination(point.MapId, point.X, point.Y, sourceMethod),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static LegacyNpcTeleportPoint SelectLegacyTeleportPoint(
        CharacterSnapshot character,
        IReadOnlyList<LegacyNpcTeleportPoint> points)
    {
        var index = TryReadUInt64(character.Attributes, out var forcedRoll, RuntimeLegacyAgateTeleportRollAttribute)
            ? checked((int)(forcedRoll % (ulong)points.Count))
            : Random.Shared.Next(points.Count);
        return points[index];
    }

    private static bool IsLegacyPowerArenaAlive(CharacterSnapshot character)
    {
        var process = ReadAttributeString(character.Attributes, "PowerArenaProcess", "RuntimePowerArenaProcess");
        return process.Length == 0 || process.Equals("Alive", StringComparison.OrdinalIgnoreCase);
    }

    private static ulong ReadLegacyAgateDefeatedGuardians(CharacterSnapshot character)
    {
        return ReadAttributeUInt64(
            character.Attributes,
            "DefeatedArenaGuardians",
            "PowerArenaDefeatedGuardians");
    }

    private static CharacterSnapshot WithLegacyAgateDefeatedGuardians(
        CharacterSnapshot character,
        ulong value)
    {
        var formatted = value.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["DefeatedArenaGuardians"] = formatted,
            ["PowerArenaDefeatedGuardians"] = formatted,
        };
        return character with { Attributes = attributes };
    }

    private static bool IsLegacyAgateRewardApplyOption(byte optionId)
    {
        return optionId is 2 or 3 or 12 or 13;
    }

    private static bool IsLegacyPrizeCenterTeleporter(uint npcId)
    {
        return npcId is >= LegacyPrizeCenterTeleporter1NpcId and <= LegacyPrizeCenterTeleporter4NpcId ||
            npcId is >= LegacyPrizeCenterTeleporter5NpcId and <= LegacyPrizeCenterTeleporter8NpcId;
    }

    private static string GetLegacyPrizeCenterTeleporterSourceMethod(uint npcId)
    {
        return npcId switch
        {
            LegacyPrizeCenterTeleporter1NpcId => "NpcHandler.PrizeCenterTeleporter1",
            LegacyPrizeCenterTeleporter2NpcId => "NpcHandler.PrizeCenterTeleporter2",
            LegacyPrizeCenterTeleporter3NpcId => "NpcHandler.PrizeCenterTeleporter3",
            LegacyPrizeCenterTeleporter4NpcId => "NpcHandler.PrizeCenterTeleporter4",
            LegacyPrizeCenterTeleporter5NpcId => "NpcHandler.PrizeCenterTeleporter5",
            LegacyPrizeCenterTeleporter6NpcId => "NpcHandler.PrizeCenterTeleporter6",
            LegacyPrizeCenterTeleporter7NpcId => "NpcHandler.PrizeCenterTeleporter7",
            LegacyPrizeCenterTeleporter8NpcId => "NpcHandler.PrizeCenterTeleporter8",
            _ => "NpcHandler.PrizeCenterTeleporter",
        };
    }

    private static bool TryGetLegacyAgateDefinition(uint npcId, out LegacyAgateDefinition agate)
    {
        agate = npcId switch
        {
            LegacyAgate1NpcId => new LegacyAgateDefinition(
                "NpcHandler.Agate1",
                30,
                "You can claim experience from the Agate for every 30 guardians you have defeated. You can also use the Agate to enter a lower stage or to return to Twin City.",
                "You`ve defeated 30 Arena Guardians! Which reward would you like, EXP or 2 Refinery Badges?",
                "You need to defeat 30 guardians first to claim experience from the Agate.",
                "You need to defeat 30 guardians first to claim experience from the Agate.",
                "EXP~worth~1~EXP~Ball.",
                "2~Refinery~Badges.",
                null,
                10,
                11,
                "Withdraw~to~Stage~2.",
                LegacyAgate1WithdrawPromptOptions,
                LegacyAgate1QuitPromptOptions,
                20,
                21,
                LegacyAgateMap1771Points,
                []),
            LegacyAgate2NpcId => new LegacyAgateDefinition(
                "NpcHandler.Agate2",
                45,
                "You can claim experience from the Agate for every 45 guardians you have defeated. Also, you can use the Agate to proceed to an advanced stage, to withdraw to a lower stage or to return to Twin City.",
                "You`ve defeated 45 Arena Guardians! Which reward would you like, EXP or 2 Refinery Badges?",
                "You need to defeat 45 guardians first to claim experience from the Agate.",
                "You need to defeat 46 guardians first to claim experience from the Agate.",
                "EXP~worth~1~EXP~Ball.",
                "2~Refinery~Badges.",
                10,
                20,
                21,
                "Withdraw~to~Stage~3.",
                LegacyAgateWithdrawPromptOptions,
                LegacyAgateQuitPromptOptions,
                30,
                31,
                LegacyAgateMap1772Points,
                LegacyAgateMap1770Points),
            LegacyAgate3NpcId => new LegacyAgateDefinition(
                "NpcHandler.Agate3",
                60,
                "You can claim experience from the Agate for every 60 guardians you have defeated. Also, you can use the Agate to proceed to an advanced stage, to withdraw to a lower stage or to return to Twin City.",
                "You`ve defeated 60 Arena Guardians! Which reward would you like, EXP or 2 Refinery Badges?",
                "You need to defeat 60 guardians first to claim experience from the Agate.",
                "You need to defeat 60 guardians first to claim experience from the Agate.",
                "EXP~worth~1~EXP~Ball.",
                "2~Refinery~Badges.",
                10,
                20,
                21,
                "Withdraw~to~Stage~4.",
                LegacyAgateWithdrawPromptOptions,
                LegacyAgateQuitPromptOptions,
                30,
                31,
                LegacyAgateMap1773Points,
                LegacyAgateMap1771Points),
            LegacyAgate4NpcId => new LegacyAgateDefinition(
                "NpcHandler.Agate4",
                35,
                "You can claim experience from the Agate for every 35 guardians you have defeated. Also, you can use the Agate to proceed to an advanced stage, to withdraw to a lower stage or to return to Twin City.",
                "You`ve defeated 35 Arena Guardians! Which reward would you like, EXP or 1 Refinery Badge?",
                "You need to defeat 35 guardians first to claim experience from the Agate.",
                "You need to defeat 35 guardians first to claim experience from the Agate.",
                "Half~of~an~EXP~Ball.",
                "1~Refinery~Badges.",
                10,
                20,
                21,
                "Withdraw~to~Stage~5.",
                LegacyAgateWithdrawPromptOptions,
                LegacyAgateQuitPromptOptions,
                30,
                31,
                LegacyAgateMap1774Points,
                LegacyAgateMap1772Points),
            LegacyAgate5NpcId => new LegacyAgateDefinition(
                "NpcHandler.Agate5",
                45,
                "You can claim experience from the Agate for every 45 guardians you have defeated. Also, you can use the Agate to proceed to an advanced stage, to withdraw to a lower stage or to return to Twin City.",
                "You`ve defeated 45 Arena Guardians! Which reward would you like, EXP or 1 Refinery Badge?",
                "You need to defeat 45 guardians first to claim experience from the Agate.",
                "You need to defeat 45 guardians first to claim experience from the Agate.",
                "EXP~worth~1~EXP~Ball.",
                "1~Refinery~Badges.",
                10,
                20,
                21,
                "Withdraw~to~Stage~6.",
                LegacyAgateWithdrawPromptOptions,
                LegacyAgateQuitPromptOptions,
                30,
                31,
                LegacyAgateMap1775Points,
                LegacyAgateMap1773Points),
            LegacyAgate6NpcId => new LegacyAgateDefinition(
                "NpcHandler.Agate6",
                60,
                "You can claim experience from the Agate for every 60 guardians you have defeated. Also, you can use the Agate to proceed to an advanced stage, to withdraw to a lower stage or to return to Twin City.",
                "You`ve defeated 60 Arena Guardians! Which reward would you like, EXP or 1 Refinery Badge?",
                "You need to defeat 60 guardians first to claim experience from the Agate.",
                "You need to defeat 60 guardians first to claim experience from the Agate.",
                "EXP~worth~1~EXP~Ball.",
                "1~Refinery~Badges.",
                10,
                20,
                21,
                "Withdraw~to~Stage~7.",
                LegacyAgateWithdrawPromptOptions,
                LegacyAgateQuitPromptOptions,
                30,
                31,
                LegacyAgateMap1777Points,
                LegacyAgateMap1774Points),
            LegacyAgate7NpcId => new LegacyAgateDefinition(
                "NpcHandler.Agate7",
                90,
                "You can claim experience from the Agate for every 90 guardians you have defeated. Also, you can use the Agate to proceed to an advanced stage, to withdraw to a lower stage or to return to Twin City.",
                "You`ve defeated 90 Arena Guardians! Which reward would you like, EXP or 1 Refinery Badge?",
                "You need to defeat 90 guardians first to claim experience from the Agate.",
                "You need to defeat 90 guardians first to claim experience from the Agate.",
                "EXP~worth~1~EXP~Ball.",
                "1~Refinery~Badges.",
                10,
                null,
                null,
                null,
                [],
                LegacyAgateQuitPromptOptions,
                30,
                31,
                [],
                LegacyAgateMap1775Points),
            _ => default,
        };

        return npcId is >= LegacyAgate1NpcId and <= LegacyAgate7NpcId;
    }

    private readonly record struct LegacyNpcTeleportPoint(long MapId, long X, long Y);

    private readonly record struct LegacyAgateDefinition(
        string SourceMethod,
        int RequiredGuardians,
        string IntroText,
        string RewardChoiceText,
        string NeedGuardiansText,
        string RewardApplyNeedGuardiansText,
        string ExpRewardOptionText,
        string BadgeRewardOptionText,
        byte? ClaimAndProceedOptionId,
        byte? WithdrawPromptOptionId,
        byte? WithdrawConfirmOptionId,
        string? WithdrawIntroOptionText,
        IReadOnlyList<LegacyNpcDialogOption> WithdrawPromptOptions,
        IReadOnlyList<LegacyNpcDialogOption> QuitPromptOptions,
        byte QuitPromptOptionId,
        byte QuitConfirmOptionId,
        IReadOnlyList<LegacyNpcTeleportPoint> WithdrawDestinations,
        IReadOnlyList<LegacyNpcTeleportPoint> AdvanceDestinations)
    {
        public byte ClaimOptionId => 1;
    }
}
