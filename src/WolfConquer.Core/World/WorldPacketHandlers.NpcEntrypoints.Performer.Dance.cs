namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleLegacyPerformerDanceAsync(
        WorldPacketHandlerContext context, WorldSession session,
        LegacyPerformerDanceDefinition dance, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LegacyPerformerDanceOwner? owner = null;
        if (context.Sessions is not null && !LegacyPerformerDanceOwner.TryCapture(context, session, out owner))
        {
            await LogLegacyPerformerDanceRefusalAsync(context, dance, "sessionChanged").ConfigureAwait(false);
            return;
        }

        LegacyPerformerSpellSource source;
        try
        {
            source = new(session.PlayerSpells?.Spells ?? throw InvalidPerformerDanceState());
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        {
            await LogLegacyPerformerDancePersistenceFailureAsync(context, dance, error).ConfigureAwait(false);
            return;
        }

        var state = ReadLegacySubClassState(session.Character);
        var phrase = GetLegacySubclassEntry(state, LegacySubclassMasterPerformerId)?.Phrase ?? 0;
        if (phrase < dance.RequiredPhase)
        {
            await SendLegacyNpcDialogAsync(context, LegacyPerformerSourceMethod,
                [dance.PhaseGateText], [new(dance.PhaseGateOptionText, LegacyNpcClosedOption)],
                LegacyPerformerAvatarId, cancellationToken).ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc Performer dance learning " +
                $"{FormatSession(context)} source={LegacyPerformerSourceMethod} " +
                $"sourceRange={LegacyPerformerDanceSourceRange} option={dance.OptionId} " +
                $"dance={dance.DanceName} spellId={dance.SpellId} phrase={phrase} " +
                $"requiredPhase={dance.RequiredPhase}; source subclass gate rejected.").ConfigureAwait(false);
            return;
        }

        if (source.Rows.Any(row => row.Id == dance.SpellId))
        {
            await SendLegacyNpcDialogAsync(context, LegacyPerformerSourceMethod,
                [dance.AlreadyLearnedText], [new(dance.AlreadyLearnedOptionText, LegacyNpcClosedOption)],
                LegacyPerformerAvatarId, cancellationToken).ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc Performer dance learning " +
                $"{FormatSession(context)} source={LegacyPerformerSourceMethod} " +
                $"sourceRange={LegacyPerformerDanceSourceRange} option={dance.OptionId} " +
                $"dance={dance.DanceName} spellId={dance.SpellId}; already learned.").ConfigureAwait(false);
            return;
        }

        var playerSpellsRoot = context.PlayerSpellsRoot;
        if (owner is null || string.IsNullOrWhiteSpace(playerSpellsRoot))
        {
            var missing = owner is null && string.IsNullOrWhiteSpace(playerSpellsRoot)
                ? "Sessions and PlayerSpellsRoot" : owner is null ? "Sessions" : "PlayerSpellsRoot";
            await SendLegacySubclassUnavailableAsync(context, LegacySubclassMasterPerformerNpcId,
                LegacyPerformerSourceMethod, LegacyPerformerSourceRange, dance.OptionId,
                $"Performer dance learning for {dance.DanceName} spellId={dance.SpellId} requires {missing} for durable live spell mutation",
                "Dance learning is not available yet. Your spells were not changed.",
                LegacyPerformerAvatarId, cancellationToken).ConfigureAwait(false);
            return;
        }

        LegacyPreparedPerformerDance? prepared = null;
        var committed = false;
        var reason = "sessionChanged";
        try
        {
            prepared = new(owner, source, playerSpellsRoot, dance, phrase);
            committed = prepared.TryCommit(cancellationToken, out reason);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException or InvalidOperationException or FormatException or OverflowException)
        {
            await LogLegacyPerformerDancePersistenceFailureAsync(context, dance, error).ConfigureAwait(false);
            return;
        }
        if (!committed)
        {
            await LogLegacyPerformerDanceRefusalAsync(context, dance, reason).ConfigureAwait(false);
            return;
        }

        // Commitment has finished. Transport/log failures below cannot be
        // described as an unchanged refusal or roll back the learned spell.
        if (!await prepared!.PublishAsync(context, cancellationToken).ConfigureAwait(false))
        {
            await context.Log.WriteLineAsync(
                $"WorldServer Performer dance committed; notification stopped for retired session spellId={dance.SpellId}.")
                .ConfigureAwait(false);
            return;
        }
        var updatedContext = context with { Session = prepared.Updated };
        await context.Log.WriteLineAsync(
            $"WorldServer sent LegacyNpc dialog {FormatSession(updatedContext)} source={LegacyPerformerSourceMethod} " +
            $"texts=1 options=1 avatar={LegacyPerformerAvatarId}.").ConfigureAwait(false);
        await context.Log.WriteLineAsync(
            "WorldServer handled LegacyNpc Performer dance learning " +
            $"{FormatSession(updatedContext)} source={LegacyPerformerSourceMethod} " +
            $"sourceRange={LegacyPerformerDanceSourceRange} spellHelper={LegacyPerformerSpellAddSourceRange} " +
            $"option={dance.OptionId} dance={dance.DanceName} spellId={dance.SpellId} " +
            $"requiredPhase={dance.RequiredPhase} phrase={phrase}; " +
            "persisted PlayerSpells before session commit and sent SpellCreate, effect, system message, then dialog.")
            .ConfigureAwait(false);
    }

    private static Task LogLegacyPerformerDanceRefusalAsync(WorldPacketHandlerContext context,
        LegacyPerformerDanceDefinition dance, string reason) => context.Log.WriteLineAsync(
            "WorldServer skipped LegacyNpc Performer dance learning " +
            $"{FormatSession(context)} option={dance.OptionId} dance={dance.DanceName} spellId={dance.SpellId} " +
            $"reason={reason}; no file or session mutation was committed.");

    private static async ValueTask LogLegacyPerformerDancePersistenceFailureAsync(WorldPacketHandlerContext context,
        LegacyPerformerDanceDefinition dance, Exception error)
    {
        await LogParityGapAsync(context, LegacyParityGapDiagnostics.ClickedNpc(
            LegacySubclassMasterPerformerNpcId, optionId: dance.OptionId,
            source: $"{LegacyPerformerSourceMethod} {LegacyPerformerDanceSourceRange}",
            reason: $"PlayerSpells persistence failed with {error.GetType().Name}; no session mutation was committed"))
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
            "PARITY-GAP LegacyNpc SubclassMasters Performer dance persistence " +
            $"{FormatSession(context)} source={LegacyPerformerSourceMethod} " +
            $"sourceRange={LegacyPerformerDanceSourceRange} spellHelper={LegacyPerformerSpellAddSourceRange} " +
            $"option={dance.OptionId} dance={dance.DanceName} spellId={dance.SpellId} " +
            $"reason={error.GetType().Name}; no session mutation was committed.").ConfigureAwait(false);
    }
}
