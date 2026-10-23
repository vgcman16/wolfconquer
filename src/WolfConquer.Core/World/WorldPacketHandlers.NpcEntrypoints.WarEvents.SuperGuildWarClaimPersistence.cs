using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static IReadOnlyDictionary<string, string> CreateLegacySuperGuildWarPersistenceValues(
        CharacterSnapshot character, LegacySuperGuildWarRewardKind kind)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = character.ConquerPoints.ToString(CultureInfo.InvariantCulture),
            ["Money"] = character.Money.ToString(CultureInfo.InvariantCulture),
            ["StatusFlags"] = ReadAttributeString(character.Attributes, "StatusFlags"),
            ["BitVector"] = ReadAttributeString(character.Attributes, "BitVector"),
            [LegacySuperGuildWarRewardClaimedAttribute] = "true",
            ["SuperGuildWarRewardClaimed"] = "true",
            ["LegacySuperGuildWarRewardKind"] = kind.ToString(),
            ["SuperGuildWarRewardKind"] = kind.ToString(),
        };
        if (kind == LegacySuperGuildWarRewardKind.Leader)
        {
            values[LegacySuperGuildWarLeaderRewardClaimedAttribute] = "true";
            values["SuperGuildWarLeaderRewardClaimed"] = "true";
        }
        else if (kind == LegacySuperGuildWarRewardKind.DeputyLeader)
        {
            values[LegacySuperGuildWarDeputyRewardClaimedAttribute] = "true";
            values["SuperGuildWarDeputyRewardClaimed"] = "true";
        }
        return values;
    }

    private static async ValueTask PersistLegacySuperGuildWarPreparedRewardAsync(
        LegacySuperGuildWarPreparedReward prepared, string sourceMethod)
    {
        var session = prepared.Session;
        if (!TryPersistLegacyCharacterAttributes(session.UsersRoot, session.AccountId, session.Character.Uid,
            prepared.Values, out var path, out var reason))
        {
            await prepared.Context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc SuperGuildWar reward persistence " +
                $"{FormatSession(prepared.Context)} source={sourceMethod} reason={reason}.").ConfigureAwait(false);
            return;
        }
        await prepared.Context.Log.WriteLineAsync(
            "WorldServer persisted LegacyNpc SuperGuildWar reward " +
            $"{FormatSession(prepared.Context)} source={sourceMethod} path={Path.GetFileName(path)} keys={string.Join(',', prepared.Values.Keys)}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask PersistLegacySuperGuildWarPreparedItemsAsync(
        LegacySuperGuildWarPreparedReward prepared, string sourceMethod)
    {
        if (prepared.ItemPath is null)
        {
            await prepared.Context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc item purchase missing PlayerItemsRoot " +
                $"{FormatSession(prepared.Context)} source={sourceMethod} itemId={LegacySuperGuildWarLeaderRewardItemId}; granted item is session-only.")
                .ConfigureAwait(false);
            return;
        }
        try
        {
            SnapshotFileLockCoordinator.ExecuteLocked(prepared.ItemPath, () =>
            {
                PlayerItemSnapshotFilePersistence.WriteAllBytesAtomically(prepared.ItemPath, prepared.ItemImage!);
                return true;
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            await prepared.Context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc item purchase persistence " +
                $"{FormatSession(prepared.Context)} source={sourceMethod} itemId={LegacySuperGuildWarLeaderRewardItemId} reason={ex.Message}.")
                .ConfigureAwait(false);
        }
    }
}
