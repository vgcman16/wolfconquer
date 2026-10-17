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
        CancellationToken cancellationToken)
    {
        if (totalDamage == 0 ||
            context.WorldData is not { } worldData)
        {
            return session;
        }

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
            CheckedInt32(session.Character.Level, nameof(session.Character.Level)));
        if (!result.Updated)
        {
            return session;
        }

        var spells = session.PlayerSpells.Spells.ToArray();
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
        context.Sessions?.Set(context.Connection, updatedSession);

        if (!string.IsNullOrWhiteSpace(context.PlayerSpellsRoot))
        {
            new PlayerSpellSnapshotLoader(context.PlayerSpellsRoot).SaveByCharacterUid(
                updatedSession.Character.Uid,
                updatedSession.PlayerSpells);
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP player magic spell experience missing PlayerSpellsRoot " +
                    $"{FormatSession(context)} spellId={spell.Id}; spell experience is session-only.")
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(result.Packet.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer updated legacy player magic spell " +
                $"{FormatSession(context)} spellId={spell.Id} appliedExperience={result.AppliedExperience} " +
                $"level={result.Spell.Level} levelChanged={result.LevelChanged}.")
            .ConfigureAwait(false);
        return updatedSession;
    }
}
