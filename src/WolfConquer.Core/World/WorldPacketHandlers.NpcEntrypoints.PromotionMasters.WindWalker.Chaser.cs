using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyPromotionWindWalkerChaserSkillAvatarId = 8;
    private const long LegacyPromotionWindWalkerChaserSkillLevelGate = 100;

    private const string LegacyPromotionWindWalkerFrostGazeISourceRange = "NpcHandler.cs:46584-46628";
    private const string LegacyPromotionWindWalkerFrostGazeIISourceRange = "NpcHandler.cs:46539-46583";
    private const string LegacyPromotionWindWalkerFrostGazeIIISourceRange = "NpcHandler.cs:46494-46538";
    private const string LegacyPromotionWindWalkerSwirlingStormSourceRange = "NpcHandler.cs:46910-46948";
    private const string LegacyPromotionWindWalkerShadowOfChaserSourceRange = "NpcHandler.cs:46871-46909";
    private const string LegacyPromotionWindWalkerTripleBlastsSourceRange = "NpcHandler.cs:46832-46870";
    private const string LegacyPromotionWindWalkerThundercloudSourceRange = "NpcHandler.cs:46793-46831";
    private const string LegacyPromotionWindWalkerThunderboltSourceRange = "NpcHandler.cs:46754-46792";

    private const byte LegacyPromotionWindWalkerSwirlingStormOption = 30;
    private const byte LegacyPromotionWindWalkerShadowOfChaserOption = 31;
    private const byte LegacyPromotionWindWalkerTripleBlastsOption = 32;
    private const byte LegacyPromotionWindWalkerThundercloudOption = 33;
    private const byte LegacyPromotionWindWalkerThunderboltOption = 34;
    private const byte LegacyPromotionWindWalkerFrostGazeIOption = 40;
    private const byte LegacyPromotionWindWalkerFrostGazeIIOption = 41;
    private const byte LegacyPromotionWindWalkerFrostGazeIIIOption = 42;

    private const ushort LegacyPromotionWindWalkerFrostGazeISpellId = 12_830;
    private const ushort LegacyPromotionWindWalkerThundercloudSpellId = 12_840;
    private const ushort LegacyPromotionWindWalkerTripleBlastsSpellId = 12_850;
    private const ushort LegacyPromotionWindWalkerSwirlingStormSpellId = 12_890;
    private const ushort LegacyPromotionWindWalkerThunderboltSpellId = 12_970;
    private const ushort LegacyPromotionWindWalkerFrostGazeIISpellId = 13_070;
    private const ushort LegacyPromotionWindWalkerFrostGazeIIISpellId = 13_080;
    private const ushort LegacyPromotionWindWalkerShadowOfChaserSpellId = 13_090;

    private static readonly LegacyWindWalkerChaserSkill[] LegacyPromotionWindWalkerChaserSkills =
    [
        new(
            LegacyPromotionWindWalkerSwirlingStormOption,
            LegacyPromotionWindWalkerSwirlingStormSpellId,
            "SwirlingStorm",
            LegacyPromotionWindWalkerSwirlingStormSourceRange),
        new(
            LegacyPromotionWindWalkerShadowOfChaserOption,
            LegacyPromotionWindWalkerShadowOfChaserSpellId,
            "ShadowofChaser",
            LegacyPromotionWindWalkerShadowOfChaserSourceRange),
        new(
            LegacyPromotionWindWalkerTripleBlastsOption,
            LegacyPromotionWindWalkerTripleBlastsSpellId,
            "TripleBlasts",
            LegacyPromotionWindWalkerTripleBlastsSourceRange),
        new(
            LegacyPromotionWindWalkerThundercloudOption,
            LegacyPromotionWindWalkerThundercloudSpellId,
            "Thundercloud",
            LegacyPromotionWindWalkerThundercloudSourceRange),
        new(
            LegacyPromotionWindWalkerThunderboltOption,
            LegacyPromotionWindWalkerThunderboltSpellId,
            "Thunderbolt",
            LegacyPromotionWindWalkerThunderboltSourceRange),
    ];

    private static async ValueTask<bool> TryHandleLegacyWindWalkerChaserPromotionSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (TryGetLegacyWindWalkerChaserSkill(optionId, out var chaserSkill))
        {
            return await TryLearnLegacyWindWalkerChaserSkillAsync(
                    context,
                    session,
                    optionId,
                    chaserSkill.SpellId,
                    chaserSkill.SpellName,
                    chaserSkill.SourceRange,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        switch (optionId)
        {
            case LegacyPromotionWindWalkerFrostGazeIOption:
                return await TryLearnLegacyWindWalkerFrostGazeSkillAsync(
                    context,
                    session,
                    optionId,
                    LegacyPromotionWindWalkerFrostGazeISpellId,
                    "FrostGazeI",
                    CanLearnLegacyWindWalkerFrostGazeI,
                    LegacyPromotionWindWalkerFrostGazeISourceRange,
                    cancellationToken)
                .ConfigureAwait(false);
            case LegacyPromotionWindWalkerFrostGazeIIOption:
                return await TryLearnLegacyWindWalkerFrostGazeSkillAsync(
                    context,
                    session,
                    optionId,
                    LegacyPromotionWindWalkerFrostGazeIISpellId,
                    "FrostGazeII",
                    CanLearnLegacyWindWalkerFrostGazeII,
                    LegacyPromotionWindWalkerFrostGazeIISourceRange,
                    cancellationToken)
                .ConfigureAwait(false);
            case LegacyPromotionWindWalkerFrostGazeIIIOption:
                return await TryLearnLegacyWindWalkerFrostGazeSkillAsync(
                    context,
                    session,
                    optionId,
                    LegacyPromotionWindWalkerFrostGazeIIISpellId,
                    "FrostGazeIII",
                    CanLearnLegacyWindWalkerFrostGazeIII,
                    LegacyPromotionWindWalkerFrostGazeIIISourceRange,
                    cancellationToken)
                .ConfigureAwait(false);
            default:
                return false;
        }
    }

    private static bool TryGetLegacyWindWalkerChaserSkill(byte optionId, out LegacyWindWalkerChaserSkill skill)
    {
        foreach (var candidate in LegacyPromotionWindWalkerChaserSkills)
        {
            if (candidate.OptionId == optionId)
            {
                skill = candidate;
                return true;
            }
        }

        skill = default;
        return false;
    }

    private static ValueTask<bool> TryLearnLegacyWindWalkerChaserSkillAsync(
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
            LegacyPromotionWindWalkerChaserSkillLevelGate,
            LegacyPromotionWindWalkerSource,
            sourceRange,
            LegacyPromotionWindWalkerChaserSkillAvatarId,
            cancellationToken);
    }

    private static async ValueTask<bool> TryLearnLegacyWindWalkerFrostGazeSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        ushort spellId,
        string spellName,
        Func<CharacterSnapshot, bool> canLearn,
        string sourceRange,
        CancellationToken cancellationToken)
    {
        if (!canLearn(session.Character))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox("You are not qualified to learn this skill.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionMaster skill " +
                    $"{FormatSession(context)} source={LegacyPromotionWindWalkerSource} sourceRange={sourceRange} " +
                    $"option={optionId} spellId={spellId} reborn={session.Character.Reborn} " +
                    $"class={session.Character.Class} firstClass={session.Character.FirstClass} " +
                    $"secondClass={session.Character.SecondClass}; source rebirth/class gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.PlayerSpells.ContainsSpell(spellId))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionWindWalkerSource,
                    ["Comrade has already acquired this skill. Go practice and feel its power."],
                    [new LegacyNpcDialogOption("Okay.", 8)],
                    LegacyPromotionWindWalkerAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionMaster skill " +
                    $"{FormatSession(context)} source={LegacyPromotionWindWalkerSource} sourceRange={sourceRange} " +
                    $"option={optionId} spellId={spellId}; already learned.")
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
                LegacyPromotionWindWalkerSource,
                sourceRange,
                spellId.ToString(CultureInfo.InvariantCulture))
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        await SendLegacyPromotionMasterLearnedSpellAsync(
                updatedContext,
                LegacyPromotionWindWalkerSource,
                LegacyPromotionWindWalkerAvatarId,
                learned,
                spellName,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionMaster skill " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionWindWalkerSource} sourceRange={sourceRange} " +
                $"option={optionId} spellId={spellId}.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool CanLearnLegacyWindWalkerFrostGazeI(CharacterSnapshot character)
    {
        return character.Reborn > 0 &&
            (IsLegacyWindWalkerClass(character.Class) ||
                IsLegacyWindWalkerClass(character.FirstClass) ||
                IsLegacyWindWalkerClass(character.SecondClass));
    }

    private static bool CanLearnLegacyWindWalkerFrostGazeII(CharacterSnapshot character)
    {
        return character.Reborn >= 1 &&
            IsLegacyWindWalkerClass(character.Class) &&
            (IsLegacyWindWalkerClass(character.FirstClass) ||
                IsLegacyWindWalkerClass(character.SecondClass));
    }

    private static bool CanLearnLegacyWindWalkerFrostGazeIII(CharacterSnapshot character)
    {
        return character.Reborn >= 2 &&
            IsLegacyWindWalkerClass(character.Class) &&
            IsLegacyWindWalkerClass(character.FirstClass) &&
            IsLegacyWindWalkerClass(character.SecondClass);
    }

    private readonly record struct LegacyWindWalkerChaserSkill(
        byte OptionId,
        ushort SpellId,
        string SpellName,
        string SourceRange);
}
