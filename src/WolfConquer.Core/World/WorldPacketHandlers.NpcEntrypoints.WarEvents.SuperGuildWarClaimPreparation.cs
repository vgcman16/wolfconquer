using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // CPU-only preparation. This does not own the in-memory entitlement store or
    // turn the two existing independent file writes into a durable transaction.
    private sealed record LegacySuperGuildWarPreparedReward(
        WorldPacketHandlerContext Context,
        WorldSession Session,
        IReadOnlyDictionary<string, string> Values,
        string? ItemPath,
        byte[]? ItemImage,
        byte[][] OwnerFrames,
        byte[][] DialogFrames,
        byte[] BroadcastFrame);

    private static LegacySuperGuildWarRewardKind ResolveLegacySuperGuildWarPreparedKind(ulong guildRank) =>
        guildRank switch
        {
            1 or 100 => LegacySuperGuildWarRewardKind.Leader,
            2 or 90 => LegacySuperGuildWarRewardKind.DeputyLeader,
            _ => LegacySuperGuildWarRewardKind.None,
        };

    private static LegacySuperGuildWarPreparedReward PrepareLegacySuperGuildWarReward(
        WorldPacketHandlerContext context, WorldSession session,
        LegacySuperGuildWarRewardKind kind, PlayerItemSnapshotItem? item)
    {
        var character = session.Character;
        var items = session.PlayerItems;
        if (kind == LegacySuperGuildWarRewardKind.Leader)
        {
            if (item is null) throw new InvalidOperationException("Leader item was not prepared.");
            var conquerPoints = checked(character.ConquerPoints + LegacySuperGuildWarSourceConquerPointsReward);
            var money = checked(character.Money + TunedLegacySuperGuildWarMoneyReward);
            character = character with { ConquerPoints = conquerPoints, Money = money };
            character = WithCharacterAttribute(character, "ConquerPoints",
                CheckedUInt64(conquerPoints, nameof(conquerPoints)));
            character = WithCharacterAttribute(character, "Money", CheckedUInt64(money, nameof(money)));
            items = items with
            {
                InventoryItems = items.InventoryItems.Concat([item]).OrderBy(row => row.Uid).ToArray(),
            };
        }
        else if (kind != LegacySuperGuildWarRewardKind.DeputyLeader)
            throw new InvalidOperationException("No supported reward kind was prepared.");

        character = WithLegacyStatusFlag(character, kind == LegacySuperGuildWarRewardKind.Leader
            ? LegacySuperGuildWarFiveStarsFlag : LegacySuperGuildWarThreeStarsFlag);
        character = WithLegacySuperGuildWarRewardClaimed(character, kind);
        var updated = session with { Character = character, PlayerItems = items };
        var updatedContext = context with { Session = updated };
        var values = CreateLegacySuperGuildWarPersistenceValues(character, kind);
        byte[]? image = kind == LegacySuperGuildWarRewardKind.Leader
            ? PlayerItemSnapshotLoader.ToLegacyBytes(items) : null;
        string? itemPath = image is not null && !string.IsNullOrWhiteSpace(context.PlayerItemsRoot)
            ? Path.GetFullPath(Path.Combine(context.PlayerItemsRoot,
                character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin")) : null;
        var frames = CreateLegacySuperGuildWarOwnerFrames(character, kind, item);
        var dialog = CreateLegacySuperGuildWarClaimDialogFrames(kind);
        var broadcast = CreateLegacySuperGuildWarClaimBroadcastFrame(character, kind);
        return new(updatedContext, updated, values, itemPath, image, frames, dialog, broadcast);
    }

    private static bool IsLegacySuperGuildWarPreparationFailure(Exception error) =>
        error is OverflowException or ArgumentException or InvalidOperationException or
            InvalidDataException or NullReferenceException or FormatException or NotSupportedException;

    private static Task LogLegacySuperGuildWarPreparationRefusalAsync(
        WorldPacketHandlerContext context, string sourceMethod, Exception error) =>
        context.Log.WriteLineAsync("WorldServer SuperGuildWar reward preparation rejected " +
            $"{FormatSession(context)} source={sourceMethod} reason={error.GetType().Name}; entitlement unchanged.");
}
