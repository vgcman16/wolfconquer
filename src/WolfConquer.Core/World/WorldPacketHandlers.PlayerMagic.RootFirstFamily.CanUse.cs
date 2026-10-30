using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession?> TryPrepareLegacyRootFirstFamilyCanUseAsync(
        WorldPacketHandlerContext context, WorldSession session, LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken,
        object? expectedNativeClientIdentity = null)
    {
        if (!TryCaptureLegacyRootAdmissionOwner(
                context, session, expectedNativeClientIdentity, out var owner)) return null;
        session = owner.Expected;
        if (!LegacyRuneCastEligibility.TryCapture(context, session, spell, out var runeEligibility)) return null;
        owner.RuneEligibility = runeEligibility;
        LegacyRootPreparedCosts plan = null!;
        string reason;
        bool prepared;
        try
        {
            var now = (context.TimeProvider ?? TimeProvider.System).GetUtcNow();
            prepared = TryPrepareLegacyRootCooldown(context, session, spell, now, out var cooldowns, out reason);
            if (prepared)
                prepared = TryPrepareLegacyRootCosts(context, session, spell, cooldowns,
                    ReadLegacyRootRequiredMana(context, session, spell), null, out plan, out reason);
        }
        catch (Exception error) when (IsLegacyRootPreparationFailure(error))
        {
            plan = null!;
            prepared = false;
            reason = $"preparationInvalid={error.GetType().Name}";
        }
        if (!prepared)
            return await RejectLegacyRootFirstFamilyWithDiagnosticAsync(
                context, session, spell, reason).ConfigureAwait(false);

        try
        {
            if (!TryCommitLegacyRootAdmission(owner, plan.Candidate, out session)) return null;
            // An ordinary Store observer may already have retired/replaced the
            // committed record. That does not roll the accepted costs back.
            if (!IsLegacyRootCommittedRecordCurrent(owner, session)) return null;
            return await PublishLegacyRootAcceptedCostsAsync(
                context, owner, session, plan.Frames, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (owner.CommitAttempted || owner.Committed)
        {
            await ReportLegacyRootAdmissionFaultAsync(context, owner, error).ConfigureAwait(false);
            throw;
        }
        // Resource-only primitive: no HP/Alive, AttackStamp, manual effects,
        // weapon authorization or new range policy for CounterKill/Rise callers.
    }

    private static int ReadLegacyRootColdSigilPower(WorldPacketHandlerContext context, WorldSession session, ushort spellId)
    {
        var type = spellId switch { 16_520 => 102, 16_540 => 105, 16_460 => 304, 16_500 => 402, _ => 0 };
        if (type == 0 || !IsLegacyNinjaClass(session.Character.Class) || context.WorldData is not { } worldData ||
            !session.Character.Attributes.TryGetValue("MsgNinja", out var raw) ||
            !TryReadLegacyMsgNinjaEquippedItemLevel(raw, type, session.AlternateEquipmentActive, out var level) ||
            !worldData.GouyuTypesByType.TryGetValue(type, out var rows)) return 0;
        foreach (var row in rows)
            if (row.Level == level && row.Values.Count > 5) return row.Values[5];
        return 0;
    }
}
