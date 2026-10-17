using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private readonly record struct LegacyFineRainExpiryEntry(int Flag, DateTimeOffset CapturedExpiresAtUtc);

    private static IReadOnlyList<LegacyFineRainExpiryEntry> CaptureLegacyFineRainExpiryEntries(
        WorldSession session,
        DateTimeOffset now,
        out WorldSession cleaned)
    {
        var entries = new List<LegacyFineRainExpiryEntry>();
        var character = session.Character;
        foreach (var flag in new[] { LegacyFineRain1StatusFlag, LegacyFineRain2StatusFlag })
        {
            var deadlineKey = LegacyFineRainDeadlineKey(flag);
            if (!character.Attributes.TryGetValue(deadlineKey, out var raw) ||
                !DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var expiresAt) ||
                expiresAt > now.ToUniversalTime())
                continue;

            if (LegacyPlayerDeathState.HasStatusFlag(character.Attributes, flag))
                entries.Add(new LegacyFineRainExpiryEntry(flag, expiresAt));
            else
                character = RetireLegacyFineRainRegistration(character, flag, out _);
        }

        // Inert metadata is local cleanup, not a source registration/query event.
        cleaned = ReferenceEquals(character, session.Character) ? session : session with { Character = character };
        return entries;
    }

    private static string LegacyFineRainDeadlineKey(int flag) =>
        flag == LegacyFineRain1StatusFlag
            ? WorldPacketHandlers.RuntimeLegacyFineRain1ExpiresAtUtcAttribute
            : WorldPacketHandlers.RuntimeLegacyFineRain2ExpiresAtUtcAttribute;

    private static CharacterSnapshot RetireLegacyFineRainRegistration(
        CharacterSnapshot character,
        int flag,
        out bool removedVisibleFlag)
    {
        if (flag == LegacyFineRain1StatusFlag) character = LegacyFineRain212Invocation.Forget(character);
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove(LegacyFineRainDeadlineKey(flag));
        attributes.Remove(flag == LegacyFineRain1StatusFlag
            ? RuntimeLegacyFineRain1RemoveOnDeadAttribute : RuntimeLegacyFineRain2RemoveOnDeadAttribute);
        attributes.Remove(LegacyStatusFlagDeathRegistration.Key(flag));
        removedVisibleFlag = RemoveLegacyStatusFlag(attributes, flag);
        return character with { Attributes = attributes };
    }
}
