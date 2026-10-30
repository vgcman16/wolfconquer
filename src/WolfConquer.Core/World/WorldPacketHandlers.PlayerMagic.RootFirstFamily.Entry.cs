using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<(WorldSession Session, PlayerSpellSnapshotEntry Book,
        LegacyMagicRuntimeSpell Spell, LegacyRootFirstFamilyInvocation Capture)?> TryEnterLegacyRootFirstFamilyAsync(
        WorldPacketHandlerContext context, WorldSession session, MsgAttackQuery query,
        LegacyMagicRuntimeSpell selected, bool ignoreAttackStamp, CancellationToken cancellationToken)
    {
        if ((((selected.Sort == LegacyMagicSort.Attack && IsLegacyNativeAttackPlayerCompletion(selected.Id)) ||
              IsLegacyNativeRiseOfTaoismRootSpell(selected)) && context.Sessions is null) ||
            !TryCaptureLegacyRootAdmissionOwner(context, session, null, out var owner)) return null;
        session = owner.Expected;
        if (!LegacyRuneCastEligibility.TryCapture(context, session, selected, out var runeEligibility)) return null;
        owner.RuneEligibility = runeEligibility;
        LegacyRootEntryPreparation plan = null!;
        string reason;
        bool prepared;
        try
        {
            prepared = TryPrepareLegacyRootEntry(context, session, query, selected.Id,
                ignoreAttackStamp, out plan, out reason);
            // Re-read the genuine learned/native descriptor and raw-map range
            // after fallible preparation, before exact baseline admission.
            if (prepared && !IsLegacyRootEntryPreparationCurrent(context, session, query, plan))
            {
                prepared = false;
                reason = "preparationStale";
            }
        }
        catch (Exception error) when (IsLegacyRootPreparationFailure(error))
        {
            prepared = false;
            reason = $"preparationInvalid={error.GetType().Name}";
        }
        if (!prepared)
        {
            if (reason.Length != 0)
                await RejectLegacyRootFirstFamilyWithDiagnosticAsync(
                    context, session, selected, reason).ConfigureAwait(false);
            return null;
        }

        try
        {
            if (!TryCommitLegacyRootAdmission(owner, plan.Costs.Candidate, out session)) return null;
            if (!IsLegacyRootCommittedRecordCurrent(owner, session)) return null;

            // Admission is already committed. Preserve successful MP -> Ring ->
            // Neck -> stamina -> mana wire order, without repricing costs after
            // observer edits/retirements in those later publication phases.
            var prefix = await ApplyLegacyRootManualItemEffectsAsync(
                context with { Session = session }, session, plan.Mp,
                cancellationToken, owner.Identity).ConfigureAwait(false);
            if (prefix is null) return null; // Accepted admission; no speculative refund.
            var published = await PublishLegacyRootAcceptedCostsAsync(
                context, owner, prefix, plan.Costs.Frames, cancellationToken).ConfigureAwait(false);
            if (published is null) return null; // Accepted, publication stopped on retirement.
            var capture = new LegacyRootFirstFamilyInvocation(context.Connection, session.Character.Uid,
                plan.EffectiveSpell.Id, plan.Book.Level,
                plan.Book.UseSpellSoul > 0 ? plan.Book.Id : (ushort)0,
                NativeCanUseEntered: true, Prepared: true) { NativeClientIdentity = owner.Identity };
            return (published, plan.Book, plan.EffectiveSpell, capture);
        }
        catch (Exception error) when (owner.CommitAttempted || owner.Committed)
        {
            await ReportLegacyRootAdmissionFaultAsync(context, owner, error).ConfigureAwait(false);
            throw;
        }
    }

    private static bool IsLegacyNativeRootWeaponSpell(WorldDataCatalog worldData, ushort spellId) =>
        IsLegacyWeaponProcSpellId(spellId) &&
        worldData.MagicByTypeId.TryGetValue(spellId, out var rows) &&
        rows.Any(row => row.Fields.Count > 22 && uint.TryParse(row.Fields[22], out var type) &&
            HasLegacyWeaponProcSpellWeaponTypes(type));

    private static bool IsInLegacyRootFirstFamilyRange(
        WorldPacketHandlerContext context, WorldSession session, uint opponentUid, byte range)
    {
        // Native IsInSpellRange uses RoleView.TryGetValue -> raw MapView blocks, NOT CanSee.
        // Preserve Monster -> Player -> SobNpc priority and each first-found object's range verdict.
        if (FindLegacyHellVortexSelectedMonster(context, session, opponentUid) is { } monster)
            return LegacyGetDistance(session.Character.X, session.Character.Y, monster.X, monster.Y) <= range;
        if (context.Sessions is { } sessions &&
            FindLegacyMapBlockPlayer(sessions, session, opponentUid) is { } player)
            return LegacyGetDistance(session.Character.X, session.Character.Y, player.Session.Character.X, player.Session.Character.Y) <= range;
        if (opponentUid <= int.MaxValue && context.WorldData is { } worldData &&
            worldData.SobNpcsById.TryGetValue((int)opponentUid, out var sobs))
        {
            foreach (var sob in sobs)
            {
                if (sob.MapId == session.Character.MapId &&
                    Math.Abs(sob.X / 18 - session.Character.X / 18) <= 1 &&
                    Math.Abs(sob.Y / 18 - session.Character.Y / 18) <= 1)
                    return LegacyGetDistance(session.Character.X, session.Character.Y, sob.X, sob.Y) <= range;
            }
        }
        return false;
    }
}
