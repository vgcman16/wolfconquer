using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal readonly record struct LegacyVendorBoothLocation(int MapId, int X, int Y);

internal sealed record LegacyVendorSessionResolution(
    WorldActiveSession ActiveSession,
    LegacyVendorBoothLocation BoothLocation);

internal static class LegacyVendorSessionResolver
{
    private const string RuntimeVendorUidAttribute = "RuntimeVendorUid";
    private const string RuntimeVendorMapIdAttribute = "RuntimeVendorMapId";
    private const string RuntimeVendorXAttribute = "RuntimeVendorX";
    private const string RuntimeVendorYAttribute = "RuntimeVendorY";

    internal static bool TryResolveSeller(
        WorldSessionStore sessions,
        long boothUid,
        long expectedMapId,
        out LegacyVendorSessionResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        return TryResolveSeller(
            sessions,
            sessions.Snapshot(),
            boothUid,
            expectedMapId,
            out resolution);
    }

    internal static bool TryResolveSeller(
        WorldSessionStore sessions,
        IReadOnlyList<WorldActiveSession> snapshot,
        long boothUid,
        long expectedMapId,
        out LegacyVendorSessionResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(snapshot);

        resolution = default!;
        if (boothUid is <= 0 or > int.MaxValue ||
            expectedMapId is <= 0 or > int.MaxValue)
        {
            return false;
        }

        List<LegacyVendorSessionResolution>? candidates = null;
        foreach (var active in snapshot)
        {
            if (!TryReadVendorState(active.Session.Character, out var parsedUid, out var location) ||
                parsedUid != boothUid ||
                location.MapId != expectedMapId ||
                !HasCurrentVendorLocation(active.Session.Character, location))
            {
                continue;
            }

            (candidates ??= []).Add(new LegacyVendorSessionResolution(active, location));
        }

        LegacyVendorSessionResolution? currentMatch = null;
        foreach (var candidate in candidates ?? [])
        {
            if (!IsCurrentSession(sessions, candidate.ActiveSession))
            {
                continue;
            }

            if (currentMatch is not null)
            {
                return false;
            }

            currentMatch = candidate;
        }

        if (currentMatch is null ||
            !IsCurrentSession(sessions, currentMatch.ActiveSession))
        {
            return false;
        }

        resolution = currentMatch;
        return true;
    }

    private static bool TryReadVendorState(
        CharacterSnapshot character,
        out long boothUid,
        out LegacyVendorBoothLocation location)
    {
        boothUid = 0;
        location = default;
        var attributes = character.Attributes;
        if (!TryReadInt64(attributes, RuntimeVendorUidAttribute, out var parsedUid) ||
            parsedUid is <= 0 or > int.MaxValue ||
            !TryReadInt64(attributes, RuntimeVendorMapIdAttribute, out var parsedMapId) ||
            parsedMapId is <= 0 or > int.MaxValue ||
            !TryReadInt64(attributes, RuntimeVendorXAttribute, out var parsedX) ||
            parsedX is < 0 or > ushort.MaxValue ||
            !TryReadInt64(attributes, RuntimeVendorYAttribute, out var parsedY) ||
            parsedY is < 0 or > ushort.MaxValue)
        {
            return false;
        }

        boothUid = parsedUid;
        location = new LegacyVendorBoothLocation(
            checked((int)parsedMapId),
            checked((int)parsedX),
            checked((int)parsedY));
        return true;
    }

    private static bool TryReadInt64(
        IReadOnlyDictionary<string, string> attributes,
        string key,
        out long value)
    {
        value = 0;
        return attributes.TryGetValue(key, out var raw) &&
            long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool HasCurrentVendorLocation(
        CharacterSnapshot character,
        LegacyVendorBoothLocation location)
    {
        return character.MapId == location.MapId &&
            character.X is >= 0 and < ushort.MaxValue &&
            character.Y is >= 0 and <= ushort.MaxValue &&
            character.X + 1 == location.X &&
            character.Y == location.Y;
    }

    private static bool IsCurrentSession(
        WorldSessionStore sessions,
        WorldActiveSession active)
    {
        return sessions.TryGet(active.Connection, out var currentSession) &&
            ReferenceEquals(currentSession, active.Session);
    }
}
