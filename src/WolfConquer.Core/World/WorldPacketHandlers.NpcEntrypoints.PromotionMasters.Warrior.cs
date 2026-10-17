using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyPromotionWarriorNpcId = 10_001;
    private const string LegacyPromotionWarriorSource = "NpcHandler.PromotionWarrior";
    private const ushort LegacyPromotionWarriorAvatarId = 8;
    private const ushort LegacyPromotionWarriorAdvancedItemAvatarId = 6;
    private const long LegacyPromotionWarriorSkillLevelGate = 100;
    private const string LegacyPromotionWarriorStarterSourceRange = "NpcHandler.cs:46029-46084";
    private const string LegacyPromotionWarriorAdvancedSourceRange = "NpcHandler.cs:45324-45500";
    private const string LegacyPromotionWarriorQuestFinishSourceRange = "NpcHandler.cs:45273-45323";
    private const string LegacyPromotionWarriorQuestFinishLookupSourceRange = "NpcHandler.cs:45013";

    private const string LegacyPromotionWarriorBackfireSourceRange = "NpcHandler.cs:45078-45116";
    private const string LegacyPromotionWarriorTwistOfWarSourceRange = "NpcHandler.cs:45117-45155";
    private const string LegacyPromotionWarriorManiacDanceSourceRange = "NpcHandler.cs:45156-45194";
    private const string LegacyPromotionWarriorWaveOfBloodSourceRange = "NpcHandler.cs:45195-45233";
    private const string LegacyPromotionWarriorScarOfEarthSourceRange = "NpcHandler.cs:45234-45272";
    private const string LegacyPromotionWarriorPerseveranceSourceRange = "NpcHandler.cs:45501-45533";
    private const string LegacyPromotionWarriorWeaponSkillSourceRange = "NpcHandler.cs:45583-45635";
    private const string LegacyPromotionWarriorFlyingMoonSourceRange = "NpcHandler.cs:45658-45696";
    private const string LegacyPromotionWarriorShieldSourceRange = "NpcHandler.cs:45697-45735";
    private const string LegacyPromotionWarriorRoarSourceRange = "NpcHandler.cs:45736-45774";
    private const string LegacyPromotionWarriorAccuracySourceRange = "NpcHandler.cs:45775-45813";
    private const string LegacyPromotionWarriorSupermanSourceRange = "NpcHandler.cs:45814-45852";
    private const string LegacyPromotionWarriorShieldSkillDialogSourceRange = "NpcHandler.cs:45853-45872";
    private const string LegacyPromotionWarriorDashSourceRange = "NpcHandler.cs:45873-45911";
    private const string LegacyPromotionWarriorShieldBlockSourceRange = "NpcHandler.cs:45912-45950";
    private const string LegacyPromotionWarriorMagicDefenderSourceRange = "NpcHandler.cs:45951-45989";
    private const string LegacyPromotionWarriorDefensiveStanceSourceRange = "NpcHandler.cs:45990-46028";

    private const byte LegacyPromotionWarriorStarterAcceptOption = 2;
    private const byte LegacyPromotionWarriorAdvancedOption = 3;
    private const byte LegacyPromotionWarriorShieldSkillDialogOption = 4;
    private const byte LegacyPromotionWarriorDashOption = 5;
    private const byte LegacyPromotionWarriorShieldBlockOption = 6;
    private const byte LegacyPromotionWarriorMagicDefenderOption = 7;
    private const byte LegacyPromotionWarriorDefensiveStanceOption = 8;
    private const byte LegacyPromotionWarriorSupermanOption = 11;
    private const byte LegacyPromotionWarriorAccuracyOption = 12;
    private const byte LegacyPromotionWarriorRoarOption = 13;
    private const byte LegacyPromotionWarriorShieldOption = 14;
    private const byte LegacyPromotionWarriorFlyingMoonOption = 15;
    private const byte LegacyPromotionWarriorWeaponSkillOption = 20;
    private const byte LegacyPromotionWarriorPerseveranceOption = 22;
    private const byte LegacyPromotionWarriorQuestFinishOption = 111;
    private const byte LegacyPromotionWarriorScarOfEarthOption = 131;
    private const byte LegacyPromotionWarriorWaveOfBloodOption = 132;
    private const byte LegacyPromotionWarriorManiacDanceOption = 133;
    private const byte LegacyPromotionWarriorTwistOfWarOption = 134;
    private const byte LegacyPromotionWarriorBackfireOption = 135;

    private const ushort LegacyPromotionWarriorSupermanSpellId = 1_025;
    private const ushort LegacyPromotionWarriorFastBladeSpellId = 1_045;
    private const ushort LegacyPromotionWarriorScentSwordSpellId = 1_046;
    private const ushort LegacyPromotionWarriorAccuracySpellId = 1_015;
    private const ushort LegacyPromotionWarriorRoarSpellId = 1_040;
    private const ushort LegacyPromotionWarriorShieldSpellId = 1_020;
    private const ushort LegacyPromotionWarriorFlyingMoonSpellId = 1_320;
    private const ushort LegacyPromotionWarriorScarOfEarthSpellId = 12_670;
    private const ushort LegacyPromotionWarriorWaveOfBloodSpellId = 12_690;
    private const ushort LegacyPromotionWarriorManiacDanceSpellId = 12_700;
    private const ushort LegacyPromotionWarriorTwistOfWarSpellId = 12_660;
    private const ushort LegacyPromotionWarriorBackfireSpellId = 12_680;
    private const ushort LegacyPromotionWarriorPerseveranceSpellId = 10_311;
    private const ushort LegacyPromotionWarriorDashSpellId = 1_051;
    private const ushort LegacyPromotionWarriorShieldBlockSpellId = 10_470;
    private const ushort LegacyPromotionWarriorMagicDefenderSpellId = 11_200;
    private const ushort LegacyPromotionWarriorDefensiveStanceSpellId = 11_160;
    private const long LegacyPromotionWarriorStarterLevelGate = 15;
    private const long LegacyPromotionWarriorInternClass = 2_000;
    private const long LegacyPromotionWarriorRankOneClass = 2_001;
    private const long LegacyPromotionWarriorRankTwoClass = 2_002;
    private const long LegacyPromotionWarriorRankThreeClass = 2_003;
    private const long LegacyPromotionWarriorRankFourClass = 2_004;
    private const long LegacyPromotionWarriorRankOneLevelGate = 40;
    private const long LegacyPromotionWarriorRankTwoLevelGate = 70;
    private const long LegacyPromotionWarriorRankThreeLevelGate = 100;
    private const long LegacyPromotionWarriorRankFourLevelGate = 110;
    private const string LegacyPromotionWarriorStarterSpellIdsLog = "1025,1045,1046,1020,1015,1040";
    private const uint LegacyPromotionWarriorQuestFinishMissionId = 2_001;
    private const uint LegacyPromotionWarriorQuestFinishRewardItemId = 3_005_160;
    private const string LegacyPromotionWarriorEmeraldMissingText =
        "I'm sorry but you need to have 1 Emerald at your inventory , to get one go and kill some HillMonsters ( Desert City ) to get one .";
    private const string LegacyPromotionWarriorMeteorMissingText =
        "I'm sorry but you need to have 1 Meteor at your inventory.";
    private const string LegacyPromotionWarriorMoonBoxMissingText =
        "I'm sorry but you need to have 1 MoonBox at your inventory.";
    private const string LegacyPromotionWarriorRankOneSuccessText =
        "Excellent! You`ve learned ways of the Warrior. Do you feel a different energy in your body? That`s stronger power and battle prowess!";
    private const string LegacyPromotionWarriorRankTwoSuccessText =
        "Excellent! You`ve learned ways of the Taoist. Do you feel a different energy in your body? That`s stronger power and battle prowess!";
    private const string LegacyPromotionWarriorRankThreeSuccessText =
        "Excellent! Do you feel a different energy in your body? That`s stronger power and battle prowess!";
    private const string LegacyPromotionWarriorRankFourSuccessText =
        "Excellent!Do you feel a different energy in your body? That`s stronger power and battle prowess!";
    private const string LegacyPromotionWarriorFastBladerSourceName = "FastBlader";

    private static readonly ushort[] LegacyPromotionWarriorStarterSpellIds =
    [
        LegacyPromotionWarriorSupermanSpellId,
        LegacyPromotionWarriorFastBladeSpellId,
        LegacyPromotionWarriorScentSwordSpellId,
        LegacyPromotionWarriorShieldSpellId,
        LegacyPromotionWarriorAccuracySpellId,
        LegacyPromotionWarriorRoarSpellId,
    ];

    private static ValueTask<bool> TryHandleLegacyWarriorPromotionSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == LegacyPromotionWarriorPerseveranceOption)
        {
            return TryHandleLegacyWarriorPureSkillAsync(
                context,
                session,
                optionId,
                cancellationToken);
        }

        if (optionId == LegacyPromotionWarriorStarterAcceptOption)
        {
            return TryHandleLegacyWarriorStarterPromotionAsync(
                context,
                session,
                optionId,
                cancellationToken);
        }

        if (optionId == LegacyPromotionWarriorAdvancedOption)
        {
            return TryHandleLegacyWarriorAdvancedPromotionAsync(
                context,
                session,
                optionId,
                cancellationToken);
        }

        if (optionId == LegacyPromotionWarriorShieldSkillDialogOption)
        {
            return TryHandleLegacyWarriorShieldSkillDialogAsync(
                context,
                session,
                optionId,
                cancellationToken);
        }

        if (optionId == LegacyPromotionWarriorWeaponSkillOption)
        {
            return TryHandleLegacyWarriorWeaponSkillAsync(
                context,
                session,
                optionId,
                cancellationToken);
        }

        return optionId switch
        {
            LegacyPromotionWarriorQuestFinishOption => TryHandleLegacyWarriorQuestFinishOptionAsync(
                context,
                session,
                optionId,
                cancellationToken),
            LegacyPromotionWarriorDashOption => TryLearnLegacyWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionWarriorDashSpellId,
                "Dash",
                LegacyPromotionWarriorDashSourceRange,
                cancellationToken),
            LegacyPromotionWarriorShieldBlockOption => TryLearnLegacyWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionWarriorShieldBlockSpellId,
                "ShieldBlock",
                LegacyPromotionWarriorShieldBlockSourceRange,
                cancellationToken),
            LegacyPromotionWarriorMagicDefenderOption => TryLearnLegacyWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionWarriorMagicDefenderSpellId,
                "MagicDefender",
                LegacyPromotionWarriorMagicDefenderSourceRange,
                cancellationToken),
            LegacyPromotionWarriorDefensiveStanceOption => TryLearnLegacyWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionWarriorDefensiveStanceSpellId,
                "DefensiveStance",
                LegacyPromotionWarriorDefensiveStanceSourceRange,
                cancellationToken),
            LegacyPromotionWarriorSupermanOption => TryLearnLegacyWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionWarriorSupermanSpellId,
                "Superman",
                LegacyPromotionWarriorSupermanSourceRange,
                cancellationToken),
            LegacyPromotionWarriorAccuracyOption => TryLearnLegacyWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionWarriorAccuracySpellId,
                "Accuracy",
                LegacyPromotionWarriorAccuracySourceRange,
                cancellationToken),
            LegacyPromotionWarriorRoarOption => TryLearnLegacyWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionWarriorRoarSpellId,
                "Roar",
                LegacyPromotionWarriorRoarSourceRange,
                cancellationToken),
            LegacyPromotionWarriorShieldOption => TryLearnLegacyWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionWarriorShieldSpellId,
                "Shield",
                LegacyPromotionWarriorShieldSourceRange,
                cancellationToken),
            LegacyPromotionWarriorFlyingMoonOption => TryLearnLegacyWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionWarriorFlyingMoonSpellId,
                "FlyingMoon",
                LegacyPromotionWarriorFlyingMoonSourceRange,
                cancellationToken),
            LegacyPromotionWarriorScarOfEarthOption => TryLearnLegacyWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionWarriorScarOfEarthSpellId,
                "ScarofEarth",
                LegacyPromotionWarriorScarOfEarthSourceRange,
                cancellationToken),
            LegacyPromotionWarriorWaveOfBloodOption => TryLearnLegacyWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionWarriorWaveOfBloodSpellId,
                "WaveofBlood",
                LegacyPromotionWarriorWaveOfBloodSourceRange,
                cancellationToken),
            LegacyPromotionWarriorManiacDanceOption => TryLearnLegacyWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionWarriorManiacDanceSpellId,
                "ManiacDance",
                LegacyPromotionWarriorManiacDanceSourceRange,
                cancellationToken),
            LegacyPromotionWarriorTwistOfWarOption => TryLearnLegacyWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionWarriorTwistOfWarSpellId,
                "TwistofWar",
                LegacyPromotionWarriorTwistOfWarSourceRange,
                cancellationToken),
            LegacyPromotionWarriorBackfireOption => TryLearnLegacyWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionWarriorBackfireSpellId,
                "Backfire",
                LegacyPromotionWarriorBackfireSourceRange,
                cancellationToken),
            _ => ValueTask.FromResult(false),
        };
    }

    private static async ValueTask<bool> TryHandleLegacyWarriorQuestFinishOptionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var activeSession = await TryApplyLegacyPromotionQuestFinishRewardAsync(
                context,
                session,
                optionId,
                new LegacyPromotionQuestFinishRewardSpec(
                    LegacyPromotionWarriorSource,
                    LegacyPromotionWarriorQuestFinishSourceRange,
                    LegacyPromotionWarriorQuestFinishLookupSourceRange,
                    "GetFinishQuest(2001, class, 1004)",
                    "PromotionWarrior",
                    LegacyPromotionWarriorQuestFinishMissionId,
                    LegacyPromotionWarriorQuestFinishRewardItemId),
                cancellationToken)
            .ConfigureAwait(false);

        byte nextOption = activeSession.Character.Class == LegacyPromotionWarriorInternClass
            ? (byte)1
            : LegacyPromotionWarriorAdvancedOption;

        await SendLegacyNpcDialogAsync(
                context with { Session = activeSession },
                LegacyPromotionWarriorSource,
                [
                    "Wow, look at you, you`ll be another hero in the world of Conquer! Here, you can learn some skills and get promoted! Do you want to learn more?",
                    "~get promoted here to start your life. Would you like to learn something about your class promotion and quests?.",
                ],
                [new LegacyNpcDialogOption("I~want~to~get~promoted", nextOption)],
                259,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyWarriorShieldSkillDialogAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionWarriorSource,
                ["Which skill do you want to learn?"],
                [
                    new LegacyNpcDialogOption(
                        "Dash" + GetLegacyWarriorShieldSkillLabel(session, LegacyPromotionWarriorDashSpellId, "Lv.40"),
                        LegacyPromotionWarriorDashOption),
                    new LegacyNpcDialogOption(
                        "Shield~Block" + GetLegacyWarriorShieldSkillLabel(session, LegacyPromotionWarriorShieldBlockSpellId, "Lv.40"),
                        LegacyPromotionWarriorShieldBlockOption),
                    new LegacyNpcDialogOption(
                        "Magic~Defender" + GetLegacyWarriorShieldSkillLabel(session, LegacyPromotionWarriorMagicDefenderSpellId, "Lv.70"),
                        LegacyPromotionWarriorMagicDefenderOption),
                    new LegacyNpcDialogOption(
                        "Defensive~Stance" + GetLegacyWarriorShieldSkillLabel(session, LegacyPromotionWarriorDefensiveStanceSpellId, "Lv.70"),
                        LegacyPromotionWarriorDefensiveStanceOption),
                    new LegacyNpcDialogOption("Let~me~think~it~over.", LegacyNpcClosedOption),
                ],
                LegacyPromotionWarriorAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionWarrior shield skill dialog " +
                $"{FormatSession(context)} source={LegacyPromotionWarriorSource} " +
                $"sourceRange={LegacyPromotionWarriorShieldSkillDialogSourceRange} option={optionId}.")
            .ConfigureAwait(false);
        return true;
    }

    private static string GetLegacyWarriorShieldSkillLabel(
        WorldSession session,
        ushort spellId,
        string sourceLevelLabel)
    {
        return session.PlayerSpells.ContainsSpell(spellId) ? "(Learnt)" : $"({sourceLevelLabel})";
    }

    private static async ValueTask<bool> TryHandleLegacyWarriorWeaponSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level < LegacyPromotionWarriorSkillLevelGate)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionWarriorSource,
                    ["You`re not strong enough to learn this skill. Keep practicing, and come back when you`re qualified."],
                    [new LegacyNpcDialogOption("Alright.", LegacyNpcClosedOption)],
                    LegacyPromotionWarriorAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionWarrior weapon skill " +
                    $"{FormatSession(context)} source={LegacyPromotionWarriorSource} " +
                    $"sourceRange={LegacyPromotionWarriorWeaponSkillSourceRange} option={optionId} " +
                    $"spellId={LegacyPromotionWarriorFastBladeSpellId} level={session.Character.Level} " +
                    $"requiredLevel={LegacyPromotionWarriorSkillLevelGate}; source level gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.PlayerSpells.ContainsSpell(LegacyPromotionWarriorFastBladeSpellId))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionWarriorSource,
                    [$"You`ve learned {LegacyPromotionWarriorFastBladerSourceName}!"],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    LegacyPromotionWarriorAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionWarrior weapon skill " +
                    $"{FormatSession(context)} source={LegacyPromotionWarriorSource} " +
                    $"sourceRange={LegacyPromotionWarriorWeaponSkillSourceRange} option={optionId} " +
                    $"spellId={LegacyPromotionWarriorFastBladeSpellId}; already learned.")
                .ConfigureAwait(false);
            return true;
        }

        var learnedSpells = new List<PlayerSpellSnapshotEntry>(capacity: 2);
        var updatedSpells = session.PlayerSpells;

        var learnedFastBlade = CreateLegacyPromotionSpellEntry(LegacyPromotionWarriorFastBladeSpellId);
        learnedSpells.Add(learnedFastBlade);
        updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(updatedSpells, learnedFastBlade);

        if (!updatedSpells.ContainsSpell(LegacyPromotionWarriorScentSwordSpellId))
        {
            var learnedScentSword = CreateLegacyPromotionSpellEntry(LegacyPromotionWarriorScentSwordSpellId);
            learnedSpells.Add(learnedScentSword);
            updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(updatedSpells, learnedScentSword);
        }

        var learnedSpellIds = string.Join(
            ",",
            learnedSpells.Select(static spell =>
                spell.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var updatedSession = await ApplyLegacyPromotionMasterSpellsAsync(
                context,
                session,
                updatedSpells,
                optionId,
                LegacyPromotionWarriorSource,
                LegacyPromotionWarriorWeaponSkillSourceRange,
                learnedSpellIds)
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        foreach (var learnedSpell in learnedSpells)
        {
            await context.Connection.SendAsync(
                    LegacySpellPacket.FromSnapshot(learnedSpell).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPromotionWarriorSource,
                [$"Congratulation! You just have learned the skill {LegacyPromotionWarriorFastBladerSourceName}"],
                [new LegacyNpcDialogOption("Ok , thank you.", LegacyNpcClosedOption)],
                LegacyPromotionWarriorAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionWarrior weapon skill " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionWarriorSource} " +
                $"sourceRange={LegacyPromotionWarriorWeaponSkillSourceRange} option={optionId} " +
                $"spellIds={learnedSpellIds}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyWarriorPureSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (session.Character.Reborn < 2)
        {
            await SendLegacyWarriorPerseverancePureGateDialogAsync(context, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionWarrior pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionWarriorSource} " +
                    $"sourceRange={LegacyPromotionWarriorPerseveranceSourceRange} option={optionId} " +
                    $"reborn={session.Character.Reborn} requiredReborn=2; source reborn gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (!IsLegacyWarriorClass(session.Character.FirstClass) ||
            !IsLegacyWarriorClass(session.Character.SecondClass))
        {
            await SendLegacyWarriorPerseverancePureGateDialogAsync(context, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionWarrior pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionWarriorSource} " +
                    $"sourceRange={LegacyPromotionWarriorPerseveranceSourceRange} option={optionId} " +
                    $"firstClass={session.Character.FirstClass} secondClass={session.Character.SecondClass}; " +
                    "source pure Warrior combo gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.Character.Level < 40 ||
            session.Character.Class < LegacyPromotionWarriorRankTwoClass)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionWarriorSource,
                    ["Sorry. I can`t teach you this skill until you reach level 40 and get promoted."],
                    [new LegacyNpcDialogOption("Okay.~I~see..", LegacyNpcClosedOption)],
                    LegacyPromotionWarriorAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionWarrior pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionWarriorSource} " +
                    $"sourceRange={LegacyPromotionWarriorPerseveranceSourceRange} option={optionId} " +
                    $"level={session.Character.Level} requiredLevel=40 " +
                    $"class={session.Character.Class} requiredClass={LegacyPromotionWarriorRankTwoClass}; " +
                    "source promotion gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.PlayerSpells.ContainsSpell(LegacyPromotionWarriorPerseveranceSpellId))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionWarriorSource,
                    ["You`ve learned Perseverance!"],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    LegacyPromotionWarriorAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionWarrior pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionWarriorSource} " +
                    $"sourceRange={LegacyPromotionWarriorPerseveranceSourceRange} option={optionId} " +
                    $"spellId={LegacyPromotionWarriorPerseveranceSpellId}; already learned.")
                .ConfigureAwait(false);
            return true;
        }

        var learned = CreateLegacyPromotionSpellEntry(LegacyPromotionWarriorPerseveranceSpellId);
        var updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(session.PlayerSpells, learned);
        var updatedSession = await ApplyLegacyPromotionMasterSpellsAsync(
                context,
                session,
                updatedSpells,
                optionId,
                LegacyPromotionWarriorSource,
                LegacyPromotionWarriorPerseveranceSourceRange,
                LegacyPromotionWarriorPerseveranceSpellId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(LegacySpellPacket.FromSnapshot(learned).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPromotionWarriorSource,
                ["Congratulation! You just have learned the skill Perseverance"],
                [new LegacyNpcDialogOption("Ok , thank you.", LegacyNpcClosedOption)],
                LegacyPromotionWarriorAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionWarrior pure skill " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionWarriorSource} " +
                $"sourceRange={LegacyPromotionWarriorPerseveranceSourceRange} option={optionId} " +
                $"spellId={LegacyPromotionWarriorPerseveranceSpellId}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask SendLegacyWarriorPerseverancePureGateDialogAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionWarriorSource,
                ["Only pure Warriors, who have spent all 3 lives as a Warrior, can learn the Pure Skill, Perseverance."],
                [new LegacyNpcDialogOption("Ok~I~see.", LegacyNpcClosedOption)],
                LegacyPromotionWarriorAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyWarriorAdvancedPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return session.Character.Class switch
        {
            LegacyPromotionWarriorRankOneClass => await TryHandleLegacyWarriorRankOnePromotionAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionWarriorRankTwoClass => await TryHandleLegacyWarriorItemPromotionAsync(
                    context,
                    session,
                    optionId,
                    LegacyPromotionWarriorRankTwoLevelGate,
                    LegacyPromotionEmeraldItemId,
                    "Emerald",
                    LegacyPromotionWarriorEmeraldMissingText,
                    LegacyPromotionWarriorRankTwoSuccessText,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionWarriorRankThreeClass => await TryHandleLegacyWarriorItemPromotionAsync(
                    context,
                    session,
                    optionId,
                    LegacyPromotionWarriorRankThreeLevelGate,
                    LegacyPromotionMeteorItemId,
                    "Meteor",
                    LegacyPromotionWarriorMeteorMissingText,
                    LegacyPromotionWarriorRankThreeSuccessText,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionWarriorRankFourClass => await TryHandleLegacyWarriorMoonBoxPromotionAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => await SendLegacyWarriorAdvancedNoMorePromotionsAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
        };
    }

    private static async ValueTask<bool> TryHandleLegacyWarriorRankOnePromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level < LegacyPromotionWarriorRankOneLevelGate)
        {
            await SendLegacyWarriorAdvancedLevelGateAsync(
                    context,
                    session,
                    optionId,
                    LegacyPromotionWarriorRankOneLevelGate,
                    LegacyPromotionWarriorAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var updatedSession = await PromoteLegacyWarriorAdvancedClassAsync(context, session, optionId)
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedSession };
        await SendLegacyWarriorAdvancedSuccessAsync(
                updatedContext,
                LegacyPromotionWarriorRankOneSuccessText,
                LegacyPromotionWarriorAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyWarriorAdvancedPromotionHandledAsync(
                updatedContext,
                optionId,
                "requiredItemId=0 grantedItemId=0")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyWarriorItemPromotionAsync(
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
            await SendLegacyWarriorAdvancedLevelGateAsync(
                    context,
                    session,
                    optionId,
                    requiredLevel,
                    LegacyPromotionWarriorAdvancedItemAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyInventoryStackCount(session.PlayerItems, requiredItemId, 1))
        {
            await SendLegacyWarriorAdvancedMissingItemAsync(
                    context,
                    session,
                    optionId,
                    missingItemText,
                    requiredItemId,
                    requiredItemName,
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
                LegacyPromotionWarriorSource,
                LegacyPromotionWarriorAdvancedSourceRange,
                $"consumedItemId={requiredItemId} consumedCount=1")
            .ConfigureAwait(false);
        await SendLegacyPromotionMasterItemFramesAsync(context, mutationFrames, cancellationToken)
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        updatedSession = await PromoteLegacyWarriorAdvancedClassAsync(updatedContext, updatedSession, optionId)
            .ConfigureAwait(false);
        updatedContext = context with { Session = updatedSession };
        await SendLegacyWarriorAdvancedSuccessAsync(
                updatedContext,
                successText,
                LegacyPromotionWarriorAdvancedItemAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyWarriorAdvancedPromotionHandledAsync(
                updatedContext,
                optionId,
                $"consumedItemId={requiredItemId} consumedCount=1 grantedItemId=0")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyWarriorMoonBoxPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level < LegacyPromotionWarriorRankFourLevelGate)
        {
            await SendLegacyWarriorAdvancedLevelGateAsync(
                    context,
                    session,
                    optionId,
                    LegacyPromotionWarriorRankFourLevelGate,
                    LegacyPromotionWarriorAdvancedItemAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyInventoryStackCount(session.PlayerItems, LegacyPromotionMoonBoxItemId, 1))
        {
            await SendLegacyWarriorAdvancedMissingItemAsync(
                    context,
                    session,
                    optionId,
                    LegacyPromotionWarriorMoonBoxMissingText,
                    LegacyPromotionMoonBoxItemId,
                    "MoonBox",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!TryReadItemType(context.WorldData, LegacyPromotionDragonBallItemId, out var dragonBallType))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc PromotionWarrior advanced promotion missing DragonBall item type " +
                    $"{FormatSession(context)} source={LegacyPromotionWarriorSource} " +
                    $"sourceRange={LegacyPromotionWarriorAdvancedSourceRange} option={optionId} " +
                    $"itemId={LegacyPromotionDragonBallItemId}; promotion skipped to avoid consuming MoonBox without reward.")
                .ConfigureAwait(false);
            return true;
        }

        var mutationFrames = new List<byte[]>();
        var updatedItems = ConsumeLegacyInventoryStack(
            session.PlayerItems,
            LegacyPromotionMoonBoxItemId,
            1,
            mutationFrames);
        var grantedDragonBall = CreatePurchasedItems(updatedItems, dragonBallType, 1)[0];
        updatedItems = updatedItems with
        {
            InventoryItems = updatedItems.InventoryItems.Concat([grantedDragonBall]).ToArray(),
        };

        var updatedSession = await ApplyLegacyPromotionMasterItemsAsync(
                context,
                session,
                updatedItems,
                optionId,
                LegacyPromotionWarriorSource,
                LegacyPromotionWarriorAdvancedSourceRange,
                $"consumedItemId={LegacyPromotionMoonBoxItemId} consumedCount=1 grantedItemId={LegacyPromotionDragonBallItemId} grantedCount=1")
            .ConfigureAwait(false);
        await SendLegacyPromotionMasterItemFramesAsync(context, mutationFrames, cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(grantedDragonBall).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk("You received DragonBall.").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        updatedSession = await PromoteLegacyWarriorAdvancedClassAsync(updatedContext, updatedSession, optionId)
            .ConfigureAwait(false);
        updatedContext = context with { Session = updatedSession };
        await SendLegacyWarriorAdvancedSuccessAsync(
                updatedContext,
                LegacyPromotionWarriorRankFourSuccessText,
                LegacyPromotionWarriorAdvancedItemAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyWarriorAdvancedPromotionHandledAsync(
                updatedContext,
                optionId,
                $"consumedItemId={LegacyPromotionMoonBoxItemId} consumedCount=1 grantedItemId={LegacyPromotionDragonBallItemId} grantedCount=1")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<WorldSession> PromoteLegacyWarriorAdvancedClassAsync(
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
                LegacyPromotionWarriorSource,
                LegacyPromotionWarriorAdvancedSourceRange)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyWarriorAdvancedLevelGateAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        long requiredLevel,
        ushort avatarId,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionWarriorSource,
                [$"There is much that you must learn before you can accept my training. Slaughter a legion of monsters first! [Level {requiredLevel}+]"],
                [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                avatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc PromotionWarrior advanced promotion " +
                $"{FormatSession(context)} source={LegacyPromotionWarriorSource} " +
                $"sourceRange={LegacyPromotionWarriorAdvancedSourceRange} option={optionId} " +
                $"level={session.Character.Level} requiredLevel={requiredLevel}; source level gate rejected.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyWarriorAdvancedMissingItemAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string missingItemText,
        uint requiredItemId,
        string requiredItemName,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionWarriorSource,
                [missingItemText],
                [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                LegacyPromotionWarriorAdvancedItemAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc PromotionWarrior advanced promotion " +
                $"{FormatSession(context)} source={LegacyPromotionWarriorSource} " +
                $"sourceRange={LegacyPromotionWarriorAdvancedSourceRange} option={optionId} " +
                $"itemId={requiredItemId} itemName={requiredItemName}; required inventory item missing.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyWarriorAdvancedSuccessAsync(
        WorldPacketHandlerContext context,
        string successText,
        ushort avatarId,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionWarriorSource,
                [successText],
                [new LegacyNpcDialogOption("Thank~you,~Master.", LegacyNpcClosedOption)],
                avatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> SendLegacyWarriorAdvancedNoMorePromotionsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionWarriorSource,
                ["You cannot be promoted anymore. You have mastered your class"],
                [new LegacyNpcDialogOption("Thank~you,~Master.", LegacyNpcClosedOption)],
                LegacyPromotionWarriorAdvancedItemAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc PromotionWarrior advanced promotion " +
                $"{FormatSession(context)} source={LegacyPromotionWarriorSource} " +
                $"sourceRange={LegacyPromotionWarriorAdvancedSourceRange} option={optionId} " +
                $"class={session.Character.Class}; no source promotion rank matched.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask LogLegacyWarriorAdvancedPromotionHandledAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        string itemSummary)
    {
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionWarrior advanced promotion " +
                $"{FormatSession(context)} source={LegacyPromotionWarriorSource} " +
                $"sourceRange={LegacyPromotionWarriorAdvancedSourceRange} option={optionId} {itemSummary}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyWarriorStarterPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!IsLegacyWarriorStarterPromotionEligible(session))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionWarrior starter promotion " +
                    $"{FormatSession(context)} source={LegacyPromotionWarriorSource} " +
                    $"sourceRange={LegacyPromotionWarriorStarterSourceRange} option={optionId} " +
                    $"level={session.Character.Level} class={session.Character.Class}; source promotion gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        var learnedSpells = new List<PlayerSpellSnapshotEntry>(capacity: LegacyPromotionWarriorStarterSpellIds.Length);
        var updatedSpells = session.PlayerSpells;
        foreach (var spellId in LegacyPromotionWarriorStarterSpellIds)
        {
            if (updatedSpells.ContainsSpell(spellId))
            {
                continue;
            }

            var learned = CreateLegacyPromotionSpellEntry(spellId);
            learnedSpells.Add(learned);
            updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(updatedSpells, learned);
        }

        var updatedSession = await ApplyLegacyPromotionMasterSpellsAsync(
                context,
                session,
                updatedSpells,
                optionId,
                LegacyPromotionWarriorSource,
                LegacyPromotionWarriorStarterSourceRange,
                LegacyPromotionWarriorStarterSpellIdsLog)
            .ConfigureAwait(false);

        foreach (var learnedSpell in learnedSpells)
        {
            await context.Connection.SendAsync(
                    LegacySpellPacket.FromSnapshot(learnedSpell).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var updatedCharacter = updatedSession.Character with
        {
            Class = checked(updatedSession.Character.Class + 1),
        };
        updatedSession = await ApplyLegacyPromotionCharacterAsync(
                context,
                updatedSession,
                updatedCharacter,
                optionId,
                LegacyPromotionWarriorSource,
                LegacyPromotionWarriorStarterSourceRange)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionWarrior starter promotion " +
                $"{FormatSession(context with { Session = updatedSession })} source={LegacyPromotionWarriorSource} " +
                $"sourceRange={LegacyPromotionWarriorStarterSourceRange} option={optionId} " +
                $"spellIds={LegacyPromotionWarriorStarterSpellIdsLog}.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool IsLegacyWarriorStarterPromotionEligible(WorldSession session)
    {
        return session.Character.Level >= LegacyPromotionWarriorStarterLevelGate &&
            session.Character.Class == LegacyPromotionWarriorInternClass;
    }

    private static ValueTask<bool> TryLearnLegacyWarriorPromotionSkillAsync(
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
            LegacyPromotionWarriorSkillLevelGate,
            LegacyPromotionWarriorSource,
            sourceRange,
            LegacyPromotionWarriorAvatarId,
            cancellationToken);
    }
}
