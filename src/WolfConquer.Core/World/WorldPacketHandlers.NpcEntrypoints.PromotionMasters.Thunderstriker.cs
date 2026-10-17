using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyPromotionThunderstrikerNpcId = 23_599;
    private const string LegacyPromotionThunderstrikerSource = "NpcHandler.PromotionThunderstriker";
    private const ushort LegacyPromotionThunderstrikerAvatarId = 234;
    private const ushort LegacyPromotionThunderstrikerPureAlreadyLearnedAvatarId = 5;
    private const long LegacyPromotionThunderstrikerXpLevelGate = 3;
    private const long LegacyPromotionThunderstrikerPureLevelGate = 100;

    private const string LegacyPromotionThunderstrikerThunderRampageSourceRange = "NpcHandler.cs:102571-102598";
    private const string LegacyPromotionThunderstrikerHeavensWrathSourceRange = "NpcHandler.cs:102599-102620";
    private const string LegacyPromotionThunderstrikerClassSkillSourceRange = "NpcHandler.cs:102622-102677";
    private const string LegacyPromotionThunderstrikerAdvancedPromotionSourceRange = "NpcHandler.cs:102382-102525";

    private const byte LegacyPromotionThunderstrikerPromotionOption = 1;
    private const byte LegacyPromotionThunderstrikerHeavensWrathOption = 3;
    private const byte LegacyPromotionThunderstrikerThunderRampageOption = 13;
    private const byte LegacyPromotionThunderstrikerWindstormBattleaxeOption = 20;
    private const byte LegacyPromotionThunderstrikerDevouringStrikeOption = 21;
    private const byte LegacyPromotionThunderstrikerSkyFallOption = 22;
    private const byte LegacyPromotionThunderstrikerCrackingShockOption = 23;
    private const byte LegacyPromotionThunderstrikerUndyingImprintingOption = 24;
    private const byte LegacyPromotionThunderstrikerLightningShieldOption = 25;
    private const byte LegacyPromotionThunderstrikerThunderBlastOption = 26;
    private const byte LegacyPromotionThunderstrikerMegaboltOption = 27;

    private const ushort LegacyPromotionThunderstrikerDevouringStrikeSpellId = 15_700;
    private const ushort LegacyPromotionThunderstrikerHeavensWrathSpellId = 15_710;
    private const ushort LegacyPromotionThunderstrikerCrackingShockSpellId = 15_720;
    private const ushort LegacyPromotionThunderstrikerLightningShieldSpellId = 15_750;
    private const ushort LegacyPromotionThunderstrikerSkyFallSpellId = 15_770;
    private const ushort LegacyPromotionThunderstrikerThunderRampageSpellId = 15_780;
    private const ushort LegacyPromotionThunderstrikerMegaboltSpellId = 15_790;
    private const ushort LegacyPromotionThunderstrikerWindstormBattleaxeSpellId = 15_800;
    private const ushort LegacyPromotionThunderstrikerThunderBlastSpellId = 15_810;
    private const ushort LegacyPromotionThunderstrikerUndyingImprintingSpellId = 15_820;
    private const ushort LegacyPromotionThunderstrikerFirstPromotionSpellId = LegacyPromotionThunderstrikerCrackingShockSpellId;
    private const ushort LegacyPromotionThunderstrikerSecondPromotionSpellId = LegacyPromotionThunderstrikerUndyingImprintingSpellId;

    private const ushort LegacyPromotionThunderstrikerItemPromotionAvatarId = 6;
    private const long LegacyPromotionThunderstrikerInternClass = 9_000;
    private const long LegacyPromotionThunderstrikerRankOneClass = 9_001;
    private const long LegacyPromotionThunderstrikerRankTwoClass = 9_002;
    private const long LegacyPromotionThunderstrikerRankThreeClass = 9_003;
    private const long LegacyPromotionThunderstrikerRankFourClass = 9_004;
    private const long LegacyPromotionThunderstrikerRankOneLevelGate = 15;
    private const long LegacyPromotionThunderstrikerRankTwoLevelGate = 40;
    private const long LegacyPromotionThunderstrikerRankThreeLevelGate = 70;
    private const long LegacyPromotionThunderstrikerRankFourLevelGate = 100;
    private const long LegacyPromotionThunderstrikerRankFiveLevelGate = 110;
    private const string LegacyPromotionThunderstrikerPromotionSuccessText =
        "Congratulations! You have been promoted.";
    private const string LegacyPromotionThunderstrikerEmeraldMissingText =
        "I'm sorry but you need to have 1 Emerald at your inventory , to get one go and kill some HillMonsters ( Desert City ) to get one .";
    private const string LegacyPromotionThunderstrikerMeteorMissingText =
        "I'm sorry but you need to have 1 Meteor at your inventory.";
    private const string LegacyPromotionThunderstrikerMoonBoxMissingText =
        "I'm sorry but you need to have 1 MoonBox at your inventory.";
    private const string LegacyPromotionThunderstrikerEmeraldSuccessText =
        "Excellent! You`ve learned ways of the Taoist. Do you feel a different energy in your body? That`s stronger power and battle prowess!";
    private const string LegacyPromotionThunderstrikerMeteorSuccessText =
        "Excellent! Do you feel a different energy in your body? That`s stronger power and battle prowess!";
    private const string LegacyPromotionThunderstrikerMoonBoxSuccessText =
        "Excellent!Do you feel a different energy in your body? That`s stronger power and battle prowess!";

    private static ValueTask<bool> TryHandleLegacyThunderstrikerPromotionSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return optionId switch
        {
            LegacyPromotionThunderstrikerPromotionOption => TryHandleLegacyThunderstrikerPromotionAsync(
                context,
                session,
                optionId,
                cancellationToken),
            LegacyPromotionThunderstrikerHeavensWrathOption => TryLearnLegacyThunderstrikerHeavensWrathAsync(
                context,
                session,
                optionId,
                cancellationToken),
            LegacyPromotionThunderstrikerThunderRampageOption => TryLearnLegacyThunderstrikerSourceTextSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionThunderstrikerThunderRampageSpellId,
                "ThunderRampage",
                LegacyPromotionThunderstrikerXpLevelGate,
                LegacyPromotionThunderstrikerThunderRampageSourceRange,
                cancellationToken),
            LegacyPromotionThunderstrikerWindstormBattleaxeOption => TryLearnLegacyThunderstrikerClassSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionThunderstrikerWindstormBattleaxeSpellId,
                "WindstormBattleaxe",
                requiredLevel: 1,
                cancellationToken),
            LegacyPromotionThunderstrikerDevouringStrikeOption => TryLearnLegacyThunderstrikerClassSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionThunderstrikerDevouringStrikeSpellId,
                "DevouringStrike",
                requiredLevel: 1,
                cancellationToken),
            LegacyPromotionThunderstrikerSkyFallOption => TryLearnLegacyThunderstrikerClassSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionThunderstrikerSkyFallSpellId,
                "SkyFall",
                requiredLevel: 1,
                cancellationToken),
            LegacyPromotionThunderstrikerCrackingShockOption => TryLearnLegacyThunderstrikerClassSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionThunderstrikerCrackingShockSpellId,
                "CrackingShock",
                requiredLevel: 15,
                cancellationToken),
            LegacyPromotionThunderstrikerUndyingImprintingOption => TryLearnLegacyThunderstrikerClassSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionThunderstrikerUndyingImprintingSpellId,
                "UndyingImprinting",
                requiredLevel: 40,
                cancellationToken),
            LegacyPromotionThunderstrikerLightningShieldOption => TryLearnLegacyThunderstrikerClassSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionThunderstrikerLightningShieldSpellId,
                "LightningShield",
                requiredLevel: 70,
                cancellationToken),
            LegacyPromotionThunderstrikerThunderBlastOption => TryLearnLegacyThunderstrikerClassSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionThunderstrikerThunderBlastSpellId,
                "ThunderBlast",
                requiredLevel: 100,
                cancellationToken),
            LegacyPromotionThunderstrikerMegaboltOption => TryLearnLegacyThunderstrikerClassSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionThunderstrikerMegaboltSpellId,
                "Megabolt",
                requiredLevel: 100,
                cancellationToken),
            _ => ValueTask.FromResult(false),
        };
    }

    private static async ValueTask<bool> TryHandleLegacyThunderstrikerPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return session.Character.Class switch
        {
            LegacyPromotionThunderstrikerInternClass => await TryHandleLegacyThunderstrikerSpellPromotionAsync(
                    context,
                    session,
                    optionId,
                    LegacyPromotionThunderstrikerRankOneLevelGate,
                    LegacyPromotionThunderstrikerFirstPromotionSpellId,
                    "\tWhen you reach Level 15, you can get promoted to [Thunderstriker] and learn more thunder skills.",
                    "I`ll~try~to~reach~Lv.15.",
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionThunderstrikerRankOneClass => await TryHandleLegacyThunderstrikerSpellPromotionAsync(
                    context,
                    session,
                    optionId,
                    LegacyPromotionThunderstrikerRankTwoLevelGate,
                    LegacyPromotionThunderstrikerSecondPromotionSpellId,
                    "\tWhen you reach Level 40, you can get promoted to [Elite~T.striker] and learn more thunder skills.",
                    "I`ll~try~to~reach~Lv.40.",
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionThunderstrikerRankTwoClass => await TryHandleLegacyThunderstrikerItemPromotionAsync(
                    context,
                    session,
                    optionId,
                    LegacyPromotionThunderstrikerRankThreeLevelGate,
                    LegacyPromotionEmeraldItemId,
                    "Emerald",
                    LegacyPromotionThunderstrikerEmeraldMissingText,
                    LegacyPromotionThunderstrikerEmeraldSuccessText,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionThunderstrikerRankThreeClass => await TryHandleLegacyThunderstrikerItemPromotionAsync(
                    context,
                    session,
                    optionId,
                    LegacyPromotionThunderstrikerRankFourLevelGate,
                    LegacyPromotionMeteorItemId,
                    "Meteor",
                    LegacyPromotionThunderstrikerMeteorMissingText,
                    LegacyPromotionThunderstrikerMeteorSuccessText,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionThunderstrikerRankFourClass => await TryHandleLegacyThunderstrikerMoonBoxPromotionAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => await SendLegacyThunderstrikerNoMorePromotionsAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
        };
    }

    private static async ValueTask<bool> TryHandleLegacyThunderstrikerSpellPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        long requiredLevel,
        ushort spellId,
        string levelGateText,
        string levelGateOptionText,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level < requiredLevel)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionThunderstrikerSource,
                    [levelGateText],
                    [new LegacyNpcDialogOption(levelGateOptionText, LegacyNpcClosedOption)],
                    LegacyPromotionThunderstrikerAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await LogLegacyThunderstrikerPromotionSkippedAsync(
                    context,
                    optionId,
                    $"level={session.Character.Level} requiredLevel={requiredLevel}; source level gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        PlayerSpellSnapshotEntry? learned = null;
        var updatedSpells = session.PlayerSpells;
        if (!updatedSpells.ContainsSpell(spellId))
        {
            learned = CreateLegacyPromotionSpellEntry(spellId);
            updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(updatedSpells, learned);
        }

        var updatedSession = await ApplyLegacyPromotionMasterSpellsAsync(
                context,
                session,
                updatedSpells,
                optionId,
                LegacyPromotionThunderstrikerSource,
                LegacyPromotionThunderstrikerAdvancedPromotionSourceRange,
                spellId.ToString(CultureInfo.InvariantCulture))
            .ConfigureAwait(false);

        if (learned is not null)
        {
            await context.Connection.SendAsync(
                    LegacySpellPacket.FromSnapshot(learned).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        updatedSession = await PromoteLegacyThunderstrikerAsync(
                context with { Session = updatedSession },
                updatedSession,
                optionId)
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedSession };
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPromotionThunderstrikerSource,
                [LegacyPromotionThunderstrikerPromotionSuccessText],
                [new LegacyNpcDialogOption("Great,~thanks!", LegacyNpcClosedOption)],
                LegacyPromotionThunderstrikerAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyThunderstrikerPromotionHandledAsync(
                updatedContext,
                optionId,
                $"spellId={spellId}")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyThunderstrikerItemPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        long requiredLevel,
        uint requiredItemId,
        string requiredItemName,
        string missingItemText,
        string successText,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level < requiredLevel)
        {
            await SendLegacyThunderstrikerItemLevelGateAsync(
                    context,
                    session,
                    optionId,
                    requiredLevel,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyInventoryStackCount(session.PlayerItems, requiredItemId, 1))
        {
            await SendLegacyThunderstrikerMissingItemAsync(
                    context,
                    session,
                    optionId,
                    requiredItemId,
                    requiredItemName,
                    missingItemText,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var mutationFrames = new List<byte[]>();
        var updatedItems = ConsumeLegacyInventoryStack(
            session.PlayerItems,
            requiredItemId,
            1,
            mutationFrames);
        var updatedSession = await ApplyLegacyPromotionMasterItemsAsync(
                context,
                session,
                updatedItems,
                optionId,
                LegacyPromotionThunderstrikerSource,
                LegacyPromotionThunderstrikerAdvancedPromotionSourceRange,
                $"consumedItemId={requiredItemId} consumedCount=1")
            .ConfigureAwait(false);
        await SendLegacyPromotionMasterItemFramesAsync(
                context with { Session = updatedSession },
                mutationFrames,
                cancellationToken)
            .ConfigureAwait(false);

        updatedSession = await PromoteLegacyThunderstrikerAsync(
                context with { Session = updatedSession },
                updatedSession,
                optionId)
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedSession };
        await SendLegacyThunderstrikerItemPromotionSuccessAsync(
                updatedContext,
                successText,
                cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyThunderstrikerPromotionHandledAsync(
                updatedContext,
                optionId,
                $"consumedItemId={requiredItemId} consumedCount=1 grantedItemId=0")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyThunderstrikerMoonBoxPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level < LegacyPromotionThunderstrikerRankFiveLevelGate)
        {
            await SendLegacyThunderstrikerItemLevelGateAsync(
                    context,
                    session,
                    optionId,
                    LegacyPromotionThunderstrikerRankFiveLevelGate,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyInventoryStackCount(session.PlayerItems, LegacyPromotionMoonBoxItemId, 1))
        {
            await SendLegacyThunderstrikerMissingItemAsync(
                    context,
                    session,
                    optionId,
                    LegacyPromotionMoonBoxItemId,
                    "MoonBox",
                    LegacyPromotionThunderstrikerMoonBoxMissingText,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!TryReadItemType(context.WorldData, LegacyPromotionDragonBallItemId, out var dragonBallType))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc PromotionThunderstriker advanced promotion missing DragonBall item type " +
                    $"{FormatSession(context)} source={LegacyPromotionThunderstrikerSource} " +
                    $"sourceRange={LegacyPromotionThunderstrikerAdvancedPromotionSourceRange} option={optionId} " +
                    $"rewardItemId={LegacyPromotionDragonBallItemId}; promotion skipped to avoid consuming MoonBox without reward.")
                .ConfigureAwait(false);
            return true;
        }

        var mutationFrames = new List<byte[]>();
        var updatedItems = ConsumeLegacyInventoryStack(
            session.PlayerItems,
            LegacyPromotionMoonBoxItemId,
            1,
            mutationFrames);
        var dragonBall = CreatePurchasedItems(updatedItems, dragonBallType, 1)[0];
        updatedItems = updatedItems with
        {
            InventoryItems = updatedItems.InventoryItems.Concat([dragonBall]).OrderBy(item => item.Uid).ToArray(),
        };

        var updatedSession = await ApplyLegacyPromotionMasterItemsAsync(
                context,
                session,
                updatedItems,
                optionId,
                LegacyPromotionThunderstrikerSource,
                LegacyPromotionThunderstrikerAdvancedPromotionSourceRange,
                $"consumedItemId={LegacyPromotionMoonBoxItemId} consumedCount=1 grantedItemId={LegacyPromotionDragonBallItemId} grantedCount=1")
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedSession };
        await SendLegacyPromotionMasterItemFramesAsync(updatedContext, mutationFrames, cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(dragonBall).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk("You received DragonBall.").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        updatedSession = await PromoteLegacyThunderstrikerAsync(
                updatedContext,
                updatedSession,
                optionId)
            .ConfigureAwait(false);
        updatedContext = context with { Session = updatedSession };
        await SendLegacyThunderstrikerItemPromotionSuccessAsync(
                updatedContext,
                LegacyPromotionThunderstrikerMoonBoxSuccessText,
                cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyThunderstrikerPromotionHandledAsync(
                updatedContext,
                optionId,
                $"consumedItemId={LegacyPromotionMoonBoxItemId} consumedCount=1 grantedItemId={LegacyPromotionDragonBallItemId} grantedCount=1")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<WorldSession> PromoteLegacyThunderstrikerAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId)
    {
        var updatedCharacter = session.Character with
        {
            Class = checked(session.Character.Class + 1),
        };
        return await ApplyLegacyPromotionCharacterAsync(
                context,
                session,
                updatedCharacter,
                optionId,
                LegacyPromotionThunderstrikerSource,
                LegacyPromotionThunderstrikerAdvancedPromotionSourceRange)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyThunderstrikerItemLevelGateAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        long requiredLevel,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionThunderstrikerSource,
                [$"There is much that you must learn before you can accept my training. Slaughter a legion of monsters first! [Level {requiredLevel}+]"],
                [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                LegacyPromotionThunderstrikerItemPromotionAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyThunderstrikerPromotionSkippedAsync(
                context,
                optionId,
                $"level={session.Character.Level} requiredLevel={requiredLevel}; source level gate rejected.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyThunderstrikerMissingItemAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        uint itemId,
        string itemName,
        string missingItemText,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionThunderstrikerSource,
                [missingItemText],
                [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                LegacyPromotionThunderstrikerItemPromotionAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyThunderstrikerPromotionSkippedAsync(
                context,
                optionId,
                $"level={session.Character.Level} class={session.Character.Class} itemId={itemId} itemName={itemName}; source item gate rejected.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyThunderstrikerItemPromotionSuccessAsync(
        WorldPacketHandlerContext context,
        string successText,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionThunderstrikerSource,
                [successText],
                [new LegacyNpcDialogOption("Thank~you,~Master.", LegacyNpcClosedOption)],
                LegacyPromotionThunderstrikerItemPromotionAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> SendLegacyThunderstrikerNoMorePromotionsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionThunderstrikerSource,
                ["You cannot be promoted anymore. You have mastered your class"],
                [new LegacyNpcDialogOption("Thank~you,~Master.", LegacyNpcClosedOption)],
                LegacyPromotionThunderstrikerItemPromotionAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyThunderstrikerPromotionSkippedAsync(
                context,
                optionId,
                $"class={session.Character.Class}; no source promotion rank matched.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask LogLegacyThunderstrikerPromotionSkippedAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc PromotionThunderstriker advanced promotion " +
                $"{FormatSession(context)} source={LegacyPromotionThunderstrikerSource} " +
                $"sourceRange={LegacyPromotionThunderstrikerAdvancedPromotionSourceRange} option={optionId} {reason}")
            .ConfigureAwait(false);
    }

    private static async ValueTask LogLegacyThunderstrikerPromotionHandledAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        string mutationSummary)
    {
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionThunderstriker advanced promotion " +
                $"{FormatSession(context)} source={LegacyPromotionThunderstrikerSource} " +
                $"sourceRange={LegacyPromotionThunderstrikerAdvancedPromotionSourceRange} option={optionId} " +
                $"{mutationSummary}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryLearnLegacyThunderstrikerClassSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        ushort spellId,
        string spellName,
        long requiredLevel,
        CancellationToken cancellationToken)
    {
        return await TryLearnLegacyThunderstrikerSourceTextSkillAsync(
                context,
                session,
                optionId,
                spellId,
                spellName,
                requiredLevel,
                LegacyPromotionThunderstrikerClassSkillSourceRange,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryLearnLegacyThunderstrikerHeavensWrathAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level < LegacyPromotionThunderstrikerPureLevelGate ||
            !IsLegacyThunderstrikerClass(session.Character.FirstClass) ||
            !IsLegacyThunderstrikerClass(session.Character.SecondClass) ||
            session.Character.Reborn != 2)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionThunderstrikerSource,
                    ["\tYou must be a Thunderstriker level 100 in three straight lives till now before you can learn [Heaven`sWrath]."],
                    [new LegacyNpcDialogOption("Alright.", LegacyNpcClosedOption)],
                    LegacyPromotionThunderstrikerAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionThunderstriker pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionThunderstrikerSource} " +
                    $"sourceRange={LegacyPromotionThunderstrikerHeavensWrathSourceRange} option={optionId} " +
                    $"spellId={LegacyPromotionThunderstrikerHeavensWrathSpellId} " +
                    $"level={session.Character.Level} requiredLevel={LegacyPromotionThunderstrikerPureLevelGate} " +
                    $"reborn={session.Character.Reborn} firstClass={session.Character.FirstClass} " +
                    $"secondClass={session.Character.SecondClass}; source pure Thunderstriker gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.PlayerSpells.ContainsSpell(LegacyPromotionThunderstrikerHeavensWrathSpellId))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionThunderstrikerSource,
                    ["You`ve learned HeavensWrath!"],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    LegacyPromotionThunderstrikerPureAlreadyLearnedAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionThunderstriker pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionThunderstrikerSource} " +
                    $"sourceRange={LegacyPromotionThunderstrikerHeavensWrathSourceRange} option={optionId} " +
                    $"spellId={LegacyPromotionThunderstrikerHeavensWrathSpellId}; already learned.")
                .ConfigureAwait(false);
            return true;
        }

        var learned = CreateLegacyPromotionSpellEntry(LegacyPromotionThunderstrikerHeavensWrathSpellId);
        var updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(session.PlayerSpells, learned);
        var updatedSession = await ApplyLegacyPromotionMasterSpellsAsync(
                context,
                session,
                updatedSpells,
                optionId,
                LegacyPromotionThunderstrikerSource,
                LegacyPromotionThunderstrikerHeavensWrathSourceRange,
                LegacyPromotionThunderstrikerHeavensWrathSpellId.ToString(CultureInfo.InvariantCulture))
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        await updatedContext.Connection.SendAsync(LegacySpellPacket.FromSnapshot(learned).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPromotionThunderstrikerSource,
                ["\tYou`ve acquired [HeavensWrath]. Go practice and feel its power."],
                [new LegacyNpcDialogOption("Thanks!", LegacyNpcClosedOption)],
                LegacyPromotionThunderstrikerAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionThunderstriker pure skill " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionThunderstrikerSource} " +
                $"sourceRange={LegacyPromotionThunderstrikerHeavensWrathSourceRange} option={optionId} " +
                $"spellId={LegacyPromotionThunderstrikerHeavensWrathSpellId}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryLearnLegacyThunderstrikerSourceTextSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        ushort spellId,
        string spellName,
        long requiredLevel,
        string sourceRange,
        CancellationToken cancellationToken)
    {
        if (session.PlayerSpells.ContainsSpell(spellId))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionThunderstrikerSource,
                    ["ThunderGeneral has already acquired this skill. Go practice and feel its power."],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    LegacyPromotionThunderstrikerAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionMaster skill " +
                    $"{FormatSession(context)} source={LegacyPromotionThunderstrikerSource} " +
                    $"sourceRange={sourceRange} option={optionId} " +
                    $"spellId={spellId}; already learned.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.Character.Level < requiredLevel)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionThunderstrikerSource,
                    [$"\tThe study of [{spellName}] requires you to reach Level {requiredLevel.ToString(CultureInfo.InvariantCulture)}."],
                    [new LegacyNpcDialogOption($"I`ll~find~you~at~Lv.{requiredLevel.ToString(CultureInfo.InvariantCulture)}!", LegacyNpcClosedOption)],
                    LegacyPromotionThunderstrikerAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionMaster skill " +
                    $"{FormatSession(context)} source={LegacyPromotionThunderstrikerSource} " +
                    $"sourceRange={sourceRange} option={optionId} " +
                    $"spellId={spellId} level={session.Character.Level} requiredLevel={requiredLevel}; " +
                    "source level gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        var learned = CreateLegacyPromotionSpellEntry(spellId);
        var updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(session.PlayerSpells, learned);
        var updatedSession = await ApplyLegacyPromotionMasterSpellsAsync(
                context,
                session,
                updatedSpells,
                optionId,
                LegacyPromotionThunderstrikerSource,
                sourceRange,
                spellId.ToString(CultureInfo.InvariantCulture))
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        await updatedContext.Connection.SendAsync(LegacySpellPacket.FromSnapshot(learned).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPromotionThunderstrikerSource,
                [$"\tYou`ve acquired [{spellName}]. Go practice and feel its power."],
                [new LegacyNpcDialogOption("Thanks!", LegacyNpcClosedOption)],
                LegacyPromotionThunderstrikerAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionMaster skill " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionThunderstrikerSource} " +
                $"sourceRange={sourceRange} " +
                $"option={optionId} spellId={spellId}.")
            .ConfigureAwait(false);
        return true;
    }
}
