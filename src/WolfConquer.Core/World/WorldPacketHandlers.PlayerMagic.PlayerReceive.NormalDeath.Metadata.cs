using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static WorldSession StageLegacyNormalDeathMetadata(
        WorldSession session, LegacyNormalPlayerDeathCause cause,
        CharacterSnapshot? killer, DateTimeOffset deathAt)
    {
        // Do NOT use ApplyDeath here: it atomically zeros HP and clears ALL flags.
        var attributes = new Dictionary<string, string>(session.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            [LegacyPlayerDeathState.DeadStampAttribute] = deathAt.ToString("O", CultureInfo.InvariantCulture),
            [LegacyPlayerDeathState.DeadXAttribute] = cause.X.ToString(CultureInfo.InvariantCulture),
            [LegacyPlayerDeathState.DeadYAttribute] = cause.Y.ToString(CultureInfo.InvariantCulture),
            [LegacyPlayerDeathState.DeadKillerUidAttribute] = (killer?.Uid ?? cause.KillerUid).ToString(CultureInfo.InvariantCulture),
            [LegacyPlayerDeathState.DeadKillerNameAttribute] = killer?.Name ?? cause.KillerName,
        };
        attributes.Remove(LegacyPlayerDeathState.RevivePromptVisibleAttribute);
        return session with { Character = session.Character with { Attributes = attributes } };
    }

    private static WorldSession AddLegacyNormalDeathFlag(WorldSession session)
    {
        var character = WithLegacySourceAddFlag(
            session.Character, LegacyPlayerDeathState.DeadStatusFlag, removeOnDead: true);
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        var hpKey = attributes.ContainsKey("MinHitPoints") ? "MinHitPoints"
            : attributes.ContainsKey("HitPoints") ? "HitPoints" : "MinHitPoints";
        attributes[hpKey] = character.MinHitPoints.ToString(CultureInfo.InvariantCulture);
        return session with { Character = character with { Attributes = attributes } };
    }

    private sealed partial class LegacyEquipmentQueryExecution
    {
        public bool AdoptNormalDeathQuery(LegacyEquipmentQueryOutcome outcome)
        {
            Adopt(new(outcome.Session, outcome.RequiredParticipantSession, outcome.Loss));
            return Current.Completed;
        }

        public async ValueTask<bool> RemoveNormalDeathFlagAsync(int flag)
        {
            if (!Refresh()) return false;
            if (!HasLegacyStatusFlag(Session.Character, flag)) return true;
            if (!Commit(session => ClearLegacyNormalDeathExplicitFlag(session, flag)))
                return false;
            return await SendNormalDeathVectorAsync().ConfigureAwait(false);
        }
    }
}
