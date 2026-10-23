using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static byte[][] CreateLegacySuperGuildWarOwnerFrames(CharacterSnapshot character,
        LegacySuperGuildWarRewardKind kind, PlayerItemSnapshotItem? item)
    {
        var frames = new List<byte[]>();
        var uid = CheckedUInt32(character.Uid, nameof(character.Uid));
        if (kind == LegacySuperGuildWarRewardKind.Leader)
        {
            frames.Add(new LegacyMsgUpdatePacket(uid,
                [new(LegacyMsgUpdateDataType.ConquerPoints, checked((ulong)character.ConquerPoints))]).ToFrameBytes());
            frames.Add(new LegacyMsgUpdatePacket(uid,
                [new(LegacyMsgUpdateDataType.Money, checked((ulong)character.Money))]).ToFrameBytes());
            frames.Add(LegacyGameItemPacket.FromSnapshotItem(item!).ToFrameBytes());
            // Same native follow-up ordering, prepared before entitlement rather
            // than serialized after an earlier successful wallet/item publication.
            var now = DateTime.Now;
            if (LegacyItemExtraPacket.TryCreate(item!, now, out var extra)) frames.Add(extra.ToFrameBytes());
            if (LegacyItemLockPacket.TryCreateUnlockDate(item!, now, out var itemLock)) frames.Add(itemLock.ToFrameBytes());
        }
        frames.Add(LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
            uid, CreateLegacyRuntimeStatusFlagVector(character)).ToFrameBytes());
        return frames.ToArray();
    }

    private static byte[][] CreateLegacySuperGuildWarClaimDialogFrames(LegacySuperGuildWarRewardKind kind)
    {
        var text = kind == LegacySuperGuildWarRewardKind.Leader
            ? $"You claimed {LegacySuperGuildWarSourceConquerPointsReward.ToString(CultureInfo.InvariantCulture)} CPs, {TunedLegacySuperGuildWarMoneyReward.ToString(CultureInfo.InvariantCulture)} silver, and item {LegacySuperGuildWarLeaderRewardItemId.ToString(CultureInfo.InvariantCulture)} for winning Super Guild War."
            : "You claimed the Deputy Leader Super Guild War title flag.";
        var frames = new List<byte[]> { LegacyNpcReplyPacket.ForDialog(text).ToFrameBytes() };
        foreach (var option in LegacyCompetitionCloseOptions)
            frames.Add(LegacyNpcReplyPacket.ForOption(option.Text, option.OptionId).ToFrameBytes());
        frames.Add(LegacyNpcReplyPacket.ForAvatar(LegacyCaptureTheFlagRewardAvatarId).ToFrameBytes());
        frames.Add(LegacyNpcReplyPacket.ForFinish().ToFrameBytes());
        return frames.ToArray();
    }

    private static byte[] CreateLegacySuperGuildWarClaimBroadcastFrame(
        CharacterSnapshot winner, LegacySuperGuildWarRewardKind kind)
    {
        var role = kind == LegacySuperGuildWarRewardKind.Leader ? "GuildLeader" : "DeputyLeader";
        var guildName = ReadAttributeString(winner.Attributes, "GuildName", "LegacyGuildName", "SuperGuildWarWinnerGuildName");
        if (string.IsNullOrWhiteSpace(guildName)) guildName = "the winning guild";
        var message = kind == LegacySuperGuildWarRewardKind.Leader
            ? $"{winner.Name} , {role} from {guildName} was rewarded with {LegacySuperGuildWarSourceConquerPointsReward.ToString(CultureInfo.InvariantCulture)} Cps for the domination the SuperGuildWar."
            : $"{winner.Name} , {role} from {guildName} claimed the SuperGuildWar title reward.";
        return new LegacyMsgMessagePacket(LegacyMsgMessagePacket.RedColor, ChatType: 2_010,
            MessageUid1: 0, MessageUid2: 0, ServerId: 0, Mesh: 0, UnionType: 0, Count: 1, New1: 0, New2: 0,
            ["SYSTEM", "ALL", string.Empty, message, string.Empty, string.Empty, string.Empty]).ToFrameBytes();
    }

    private static async ValueTask PublishLegacySuperGuildWarClaimFramesAsync(
        LegacySuperGuildWarPreparedReward prepared, string sourceMethod, CancellationToken cancellationToken)
    {
        foreach (var frame in prepared.OwnerFrames)
            await prepared.Context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        foreach (var frame in prepared.DialogFrames)
            await prepared.Context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        await prepared.Context.Log.WriteLineAsync(
            $"WorldServer sent LegacyNpc dialog {FormatSession(prepared.Context)} source={sourceMethod} " +
            $"texts=1 options={LegacyCompetitionCloseOptions.Length} avatar={LegacyCaptureTheFlagRewardAvatarId.ToString(CultureInfo.InvariantCulture)}.")
            .ConfigureAwait(false);
        // Preserve the original recipient sampling AFTER owner/dialog publication.
        // This is not a delivery outbox or a stale-recipient transaction.
        var activeSessions = prepared.Context.Sessions?.Snapshot();
        if (activeSessions is null || activeSessions.Count == 0)
        {
            await prepared.Context.Connection.SendAsync(prepared.BroadcastFrame, cancellationToken).ConfigureAwait(false);
            return;
        }
        foreach (var active in activeSessions)
            await active.Connection.SendAsync(prepared.BroadcastFrame, cancellationToken).ConfigureAwait(false);
    }
}
