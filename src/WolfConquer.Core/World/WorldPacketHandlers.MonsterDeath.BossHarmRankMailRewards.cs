namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask TryApplyLegacyBossHarmRankMailRewardsAsync(
        WorldPacketHandlerContext context,
        LegacyMonsterInstance monster,
        CancellationToken cancellationToken)
    {
        if (!monster.Boss)
        {
            return;
        }

        if (context.MonsterStates is not { } monsterStates)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP legacy boss harm-rank mail rewards missing runtime state; " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} monsterStates=False.")
                .ConfigureAwait(false);
            return;
        }

        var nowUtc = DateTimeOffset.UtcNow;
        if (monsterStates.GetBossHarmRanking(monster, nowUtc, maxEntries: 9).Count == 0)
        {
            return;
        }

        if (context.Sessions is not { } sessions ||
            context.Mailboxes is not { } mailboxes)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP legacy boss harm-rank mail rewards missing runtime state; " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} sessions={context.Sessions is not null} " +
                    $"mailboxes={context.Mailboxes is not null}.")
                .ConfigureAwait(false);
            return;
        }

        var ranking = monsterStates.ConsumeBossHarmRanking(monster, nowUtc, maxEntries: 9);
        await LogLegacyBossKillRewardAsync(context, monster, ranking)
            .ConfigureAwait(false);

        var now = DateTime.Now;
        var activeSessions = sessions.Snapshot();
        var createdCount = 0;
        var skippedOfflineCount = 0;
        foreach (var entry in ranking)
        {
            var active = activeSessions.FirstOrDefault(activeSession =>
                activeSession.Session.Character.Uid == entry.PlayerUid);
            if (active is null)
            {
                skippedOfflineCount++;
                continue;
            }

            var playerUid = checked((uint)entry.PlayerUid);
            if (!LegacyMailboxEntry.TryCreateBossHarmRankReward(
                entry.Rank,
                mailboxes.GetNextId(playerUid),
                now,
                out var rewardMail))
            {
                continue;
            }

            var storedMail = mailboxes.Add(playerUid, rewardMail);
            mailboxes.Save(playerUid);
            var mailListEntry = LegacyMailListPacket.FromMailboxEntry(storedMail, now);

            await active.Connection.SendAsync(
                    LegacyMailListPacket.NewMail(mailListEntry).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await active.Connection.SendAsync(
                    LegacyMailNotifyPacket.Startup(hasMail: true).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);

            if (LegacyBossDeathRewardPolicy.TryGetHarmRankMailConquerPointReward(
                entry.Rank,
                out var reward))
            {
                await active.Connection.SendAsync(
                        LegacyMsgMessagePacket.SystemTalk(
                                LegacyBossDeathRewardPolicy.FormatHarmRankMailMessage(
                                    monster.Name,
                                    entry.Rank,
                                    reward.ConquerPoints))
                            .ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            createdCount++;
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy boss harm-rank mail rewards " +
                $"{FormatSession(context)} monsterUid={monster.Uid} ranking={ranking.Count} " +
                $"created={createdCount} skippedOffline={skippedOfflineCount}; " +
                "source=MonsterRole.GetBossReward MailList rank 1..9.")
            .ConfigureAwait(false);
    }

    private static async ValueTask LogLegacyBossKillRewardAsync(
        WorldPacketHandlerContext context,
        LegacyMonsterInstance monster,
        IReadOnlyList<LegacyBossHarmRankingEntry> ranking)
    {
        var killerName = string.IsNullOrWhiteSpace(context.Session?.Character.Name)
            ? "unknown"
            : context.Session.Character.Name;
        var topHunters = string.Concat(
            ranking.Take(5).Select(entry => entry.PlayerName + "     |      "));
        var sourceLog = $"[Bosses] Name: {monster.Name} | Killer: {killerName} | Hunters: {topHunters}";

        await context.Log.WriteLineAsync(
                "WorldServer logged legacy boss kill reward " +
                $"{FormatSession(context)} monsterUid={monster.Uid} sourceLog=\"{sourceLog}\"; " +
                "source=MonsterRole.GetScorePosition LoginQueue boss reward log.")
            .ConfigureAwait(false);
    }
}
