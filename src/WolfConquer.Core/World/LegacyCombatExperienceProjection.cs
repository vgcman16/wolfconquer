using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Bounded calculation only; it grants no combat, file or publication authority.
internal sealed record LegacyCombatExperienceProjection(
    CharacterSnapshot Before, CharacterSnapshot After, ulong AppliedGain,
    bool LevelChanged, LegacyClassLevelStatProgressionChange StatProgression)
{
    internal static LegacyCombatExperienceProjection Create(WorldSession session, WorldDataCatalog? catalog, ulong appliedGain)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (appliedGain == 0 || appliedGain > long.MaxValue || session.Character.Experience < 0 ||
            session.Character.Level is < 1 or > ushort.MaxValue)
            throw new InvalidOperationException("combatExperienceInputInvalid");
        var before = session.Character;
        var experience = checked(before.Experience + (long)appliedGain);
        var level = before.Level;
        while (level < 140 && catalog?.LevelExperienceByTypeAndLevel.TryGetValue((0, checked((int)level)), out var threshold) == true &&
            threshold.Experience > 0 && experience >= threshold.Experience)
        {
            experience -= threshold.Experience;
            level++;
        }
        bool changedLevel = level != before.Level;
        var leveled = before with { Experience = level >= 140 ? 0 : experience, Level = level };
        LegacyClassLevelStatProgressionChange progression = default;
        var after = changedLevel ? LegacyClassLevelStatProgression.Apply(leveled, catalog, before.Level, out progression) : leveled;
        // A real level-up query and its owned HP restoration run after this
        // stat step; do not substitute modeled vitals for completed caches.
        return new(before, after, appliedGain, changedLevel, progression);
    }
}
