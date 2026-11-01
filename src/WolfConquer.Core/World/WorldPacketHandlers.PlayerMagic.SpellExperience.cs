using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyPlayerMagicSpellExperienceAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        ulong totalDamage,
        CancellationToken cancellationToken,
        LegacyShockCasterOwner? capturedOwner = null)
    {
        if (totalDamage == 0 ||
            context.WorldData is not { } worldData)
        {
            return session;
        }
        if (capturedOwner is not null && !capturedOwner.IsCurrent(context, session)) return session;

        // Provider-owned levels are temporary equipment/rank state. Their
        // exact zero-progress row is validated before skipping XP; an unmarked
        // ordinary row with the same ID keeps its normal progression path.
        LegacyDerivedSpellOrigins.RequireRows(session.Character, session.PlayerSpells);
        if (LegacyDerivedSpellOrigins.Read(session.Character).ContainsKey(playerSpell.Id)) return session;

        // Original UpdateSpell checks the actual ID00 IsRuneSkill metadata
        // before changing spell XP. Keep Rune-origin grants at their exact row;
        // a later unequip refusal is too late to protect that invariant.
        if (spell.IsRuneSkill == true) return session;
        LegacyRuneSpellClassification? classification = null;
        if (LegacyRuneSpellClassification.TryCapture(worldData, spell.Id, out classification))
        {
            if (classification!.IsRuneSkill || !classification.IsCurrent()) return session;
        }
        else if (LegacyRuneSpellClassification.HasExplicitEntry(worldData, spell.Id)) return session;

        var availableLevelCount = worldData.MagicByTypeId.TryGetValue(spell.Id, out var rows)
            ? rows.Count
            : 0;
        var damageForExperience = totalDamage > uint.MaxValue ? uint.MaxValue : (uint)totalDamage;
        var result = LegacyMagicRuntime.ApplySpellExperience(
            playerSpell,
            spell,
            damageForExperience,
            experienceRate: 1,
            availableLevelCount,
            CheckedInt32(session.Character.Level, nameof(session.Character.Level)),
            isRuneSkill: classification?.IsRuneSkill == true);
        if (!result.Updated)
        {
            return session;
        }

        var spells = session.PlayerSpells.Spells.ToArray();
        if (capturedOwner is not null && !spells.Any(row => row == playerSpell)) return session;
        var updated = false;
        for (var index = 0; index < spells.Length; index++)
        {
            if (spells[index].Id != playerSpell.Id)
            {
                continue;
            }

            spells[index] = result.Spell;
            updated = true;
            break;
        }

        if (!updated)
        {
            return session;
        }

        var updatedSession = session with { PlayerSpells = new PlayerSpellSnapshot(spells) };
        var responseFrame = result.Packet.ToFrameBytes();

        context.OwnedHawksOutput?.RequireCurrent(context);
        if (!TryCommitLegacyFreeSpellsCore(context, session, updatedSession.PlayerSpells,
                capturedOwner?.Identity, capturedOwner, cancellationToken,
                out updatedSession, out var reason, classification))
        {
            await context.Log.WriteLineAsync("WorldServer refused spell experience persistence before commitment; spell unchanged. reason=" + reason)
                .ConfigureAwait(false);
            return session;
        }
        if (capturedOwner is not null && !capturedOwner.IsCurrent(context, updatedSession)) return updatedSession;

        context.OwnedHawksOutput?.RequireCurrent(context);
        await context.Connection.SendAsync(responseFrame, cancellationToken)
            .ConfigureAwait(false);
        context.OwnedHawksOutput?.RequireCurrent(context);
        if (capturedOwner is not null && !capturedOwner.IsCurrent(context, updatedSession)) return updatedSession;
        await context.Log.WriteLineAsync(
                "WorldServer updated legacy player magic spell " +
                $"{FormatSession(context)} spellId={spell.Id} appliedExperience={result.AppliedExperience} " +
                $"level={result.Spell.Level} levelChanged={result.LevelChanged}.")
            .ConfigureAwait(false);
        return updatedSession;
    }
}
