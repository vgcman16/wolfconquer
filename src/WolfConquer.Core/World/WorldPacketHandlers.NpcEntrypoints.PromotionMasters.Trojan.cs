using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyPromotionTrojanNpcId = 10_022;
    private const string LegacyPromotionTrojanSource = "NpcHandler.PromotionTrojan";
    private const string LegacyPromotionTrojanAccuracyCycloneSourceRange = "NpcHandler.cs:100743-100779";
    private const byte LegacyPromotionTrojanAccuracyCycloneOption = 9;
    private const long LegacyPromotionTrojanAccuracyCycloneLevelGate = 40;
    private const long LegacyPromotionTrojanAccuracyCycloneClassGate = 1_002;
    private const long LegacyPromotionTrojanSkillLevelGate = 100;
    private const ushort LegacyPromotionTrojanAvatarId = 5;
    private const ushort LegacyTrojanSkillAvatarId = 8;
    private const ushort LegacyPromotionTrojanAccuracySpellId = 1_015;
    private const ushort LegacyPromotionTrojanCycloneSpellId = 1_110;

    private static async ValueTask<bool> TryHandleLegacyTrojanPromotionSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (npcId != LegacyPromotionTrojanNpcId)
        {
            return false;
        }

        if (await TryHandleLegacyTrojanNewSkillAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false) ||
            await TryHandleLegacyTrojanAdvancedPromotionAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false) ||
            await TryHandleLegacyTrojanEpicQuestRouteAsync(context, optionId)
                .ConfigureAwait(false) ||
            await TryHandleLegacyTrojanSimpleSkillAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false) ||
            await TryHandleLegacyTrojanSuperiorSkillAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false) ||
            await TryHandleLegacyTrojanAccuracyCycloneSkillAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false) ||
            await TryHandleLegacyTrojanWeaponSkillAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false) ||
            await TryHandleLegacyTrojanEpicSkillAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false) ||
            await TryHandleLegacyTrojanPureSkillAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false) ||
            await TryHandleLegacyTrojanStarterPromotionAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false))
        {
            return true;
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacyTrojanAccuracyCycloneSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId != LegacyPromotionTrojanAccuracyCycloneOption)
        {
            return false;
        }

        if (session.Character.Level < LegacyPromotionTrojanAccuracyCycloneLevelGate ||
            session.Character.Class < LegacyPromotionTrojanAccuracyCycloneClassGate)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionTrojanSource,
                    ["Sorry. I can`t teach you this skill until you reach level 40 and get promoted."],
                    [new LegacyNpcDialogOption("Okay.~I~see..", LegacyNpcClosedOption)],
                    LegacyPromotionTrojanAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionTrojan skill " +
                    $"{FormatSession(context)} source={LegacyPromotionTrojanSource} " +
                    $"sourceRange={LegacyPromotionTrojanAccuracyCycloneSourceRange} option={optionId} " +
                    $"level={session.Character.Level} requiredLevel={LegacyPromotionTrojanAccuracyCycloneLevelGate} " +
                    $"class={session.Character.Class} requiredClass={LegacyPromotionTrojanAccuracyCycloneClassGate}; " +
                    "source promotion gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        var alreadyLearnedAccuracy = session.PlayerSpells.ContainsSpell(LegacyPromotionTrojanAccuracySpellId);
        var alreadyLearnedCyclone = session.PlayerSpells.ContainsSpell(LegacyPromotionTrojanCycloneSpellId);
        if (alreadyLearnedAccuracy && alreadyLearnedCyclone)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionTrojanSource,
                    ["You`ve learned Accuracy and Cyclone!"],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    LegacyPromotionTrojanAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionTrojan skill " +
                    $"{FormatSession(context)} source={LegacyPromotionTrojanSource} " +
                    $"sourceRange={LegacyPromotionTrojanAccuracyCycloneSourceRange} option={optionId} " +
                    $"spellIds={LegacyPromotionTrojanAccuracySpellId},{LegacyPromotionTrojanCycloneSpellId}; already learned.")
                .ConfigureAwait(false);
            return true;
        }

        var learnedAccuracy = alreadyLearnedAccuracy
            ? null
            : CreateLegacyPromotionSpellEntry(LegacyPromotionTrojanAccuracySpellId);
        var learnedCyclone = alreadyLearnedCyclone
            ? null
            : CreateLegacyPromotionSpellEntry(LegacyPromotionTrojanCycloneSpellId);

        var updatedSpells = session.PlayerSpells;
        if (learnedAccuracy is not null)
        {
            updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(updatedSpells, learnedAccuracy);
        }

        if (learnedCyclone is not null)
        {
            updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(updatedSpells, learnedCyclone);
        }

        var updatedSession = await ApplyLegacyPromotionSpellsAsync(
                context,
                session,
                updatedSpells,
                optionId,
                LegacyPromotionTrojanAccuracyCycloneSourceRange,
                $"{LegacyPromotionTrojanAccuracySpellId},{LegacyPromotionTrojanCycloneSpellId}")
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        var learnedLine = "You just have learned the skill ";
        if (learnedAccuracy is not null)
        {
            await context.Connection.SendAsync(LegacySpellPacket.FromSnapshot(learnedAccuracy).ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
            learnedLine += "Accuracy";
        }
        if (learnedCyclone is not null)
        {
            await context.Connection.SendAsync(LegacySpellPacket.FromSnapshot(learnedCyclone).ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
            learnedLine += "/Cyclone";
        }

        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPromotionTrojanSource,
                [learnedLine],
                [new LegacyNpcDialogOption("Thank you..", LegacyNpcClosedOption)],
                LegacyPromotionTrojanAvatarId,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionTrojan skill " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionTrojanSource} " +
                $"sourceRange={LegacyPromotionTrojanAccuracyCycloneSourceRange} option={optionId} " +
                $"spellIds={LegacyPromotionTrojanAccuracySpellId},{LegacyPromotionTrojanCycloneSpellId}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryLearnLegacyTrojanLevelGatedSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        ushort spellId,
        string spellName,
        string sourceRange,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level < LegacyPromotionTrojanSkillLevelGate)
        {
            await SendLegacyTrojanPromotionSkillLevelGateAsync(context, optionId, sourceRange, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!IsLegacyTrojanClass(session.Character.Class))
        {
            await SendLegacyTrojanPromotionSkillClassGateAsync(context, optionId, sourceRange, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (session.PlayerSpells.ContainsSpell(spellId))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionTrojanSource,
                    [$"You`ve learned {spellName}!"],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    LegacyTrojanSkillAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionTrojan skill " +
                    $"{FormatSession(context)} source={LegacyPromotionTrojanSource} " +
                    $"sourceRange={sourceRange} option={optionId} spellId={spellId}; already learned.")
                .ConfigureAwait(false);
            return true;
        }

        var learned = CreateLegacyPromotionSpellEntry(spellId);
        var updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(session.PlayerSpells, learned);
        var updatedSession = await ApplyLegacyPromotionSpellsAsync(
                context,
                session,
                updatedSpells,
                optionId,
                sourceRange,
                spellId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        await SendLegacyPromotionLearnedSpellAsync(updatedContext, learned, spellName, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionTrojan skill " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionTrojanSource} " +
                $"sourceRange={sourceRange} option={optionId} spellId={spellId}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask SendLegacyTrojanPromotionSkillLevelGateAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        string sourceRange,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionTrojanSource,
                ["You`re not strong enough to learn this skill. Keep practicing, and come back when you`re qualified."],
                [new LegacyNpcDialogOption("Alright.", LegacyNpcClosedOption)],
                LegacyTrojanSkillAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc PromotionTrojan skill " +
                $"{FormatSession(context)} source={LegacyPromotionTrojanSource} " +
                $"sourceRange={sourceRange} option={optionId} " +
                $"level={context.Session?.Character.Level ?? 0} requiredLevel={LegacyPromotionTrojanSkillLevelGate}; source level gate rejected.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyTrojanPromotionSkillClassGateAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        string sourceRange,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionTrojanSource,
                ["Sorry, but you are not a member of the Trojan class."],
                [new LegacyNpcDialogOption("Alright.", LegacyNpcClosedOption)],
                LegacyPromotionTrojanAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc PromotionTrojan skill " +
                $"{FormatSession(context)} source={LegacyPromotionTrojanSource} " +
                $"sourceRange={sourceRange} option={optionId} " +
                $"class={context.Session?.Character.Class ?? 0}; source class-family gate rejected.")
            .ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession> ApplyLegacyPromotionSpellsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        PlayerSpellSnapshot updatedSpells,
        byte optionId,
        string sourceRange,
        string learnedSpellIds)
    {
        if (!TryCommitLegacyFreeSpells(context, session, updatedSpells, CancellationToken.None,
                out var updatedSession, out var reason))
            throw new LegacySpellMutationRefusalException(reason);
        await context.Log.WriteLineAsync("WorldServer committed promotion spell learning " +
                $"{FormatSession(context with { Session = updatedSession })} option={optionId} spellIds={learnedSpellIds}.")
            .ConfigureAwait(false);

        return updatedSession;
    }

    private static async ValueTask<WorldSession> ApplyLegacyPromotionCharacterAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CharacterSnapshot updatedCharacter,
        byte optionId,
        string sourceRange)
    {
        return await ApplyLegacyPromotionCharacterAsync(
                context,
                session,
                updatedCharacter,
                optionId,
                LegacyPromotionTrojanSource,
                sourceRange)
            .ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession> ApplyLegacyPromotionCharacterAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CharacterSnapshot updatedCharacter,
        byte optionId,
        string sourceMethod,
        string sourceRange)
    {
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);

        if (!CharacterSnapshotScalarPersistence.TryPersist(
                updatedSession.UsersRoot,
                updatedCharacter,
                out _,
                out var reason))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc PromotionMaster character persistence skipped " +
                    $"{FormatSession(context)} source={sourceMethod} " +
                    $"sourceRange={sourceRange} option={optionId} reason={reason}; character update is session-only.")
                .ConfigureAwait(false);
        }

        return updatedSession;
    }

    private static bool HasLegacyEquippedTrojanEpicWeapon(PlayerItemSnapshot items)
    {
        return items.EquipmentItems.Any(
            static item => item.Position is 4 or 5 && IsLegacyTrojanEpicWeapon(item.ItemId));
    }

    private static async ValueTask SendLegacyPromotionLearnedSpellAsync(
        WorldPacketHandlerContext context,
        PlayerSpellSnapshotEntry spell,
        string spellName,
        CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(LegacySpellPacket.FromSnapshot(spell).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionTrojanSource,
                [$"Congratulation! You just have learned the skill {spellName}"],
                [new LegacyNpcDialogOption("Ok , thank you.", LegacyNpcClosedOption)],
                LegacyTrojanSkillAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static PlayerSpellSnapshotEntry CreateLegacyPromotionSpellEntry(ushort spellId)
    {
        return new PlayerSpellSnapshotEntry(
            spellId,
            Level: 0,
            Experience: 0,
            PreviousLevel: 0,
            SoulLevel: 0,
            UseSpellSoul: 0);
    }
}
