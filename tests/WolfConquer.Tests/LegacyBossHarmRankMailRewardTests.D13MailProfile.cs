using System.Buffers;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyBossHarmRankMailRewardTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MailProducerValidatesHostProfileBeforeConsumingRankingOrSaving(bool validD13)
    {
        var family = CreateBossFamily();
        var spawn = CreateBossSpawn(family);
        var worldData = CreateWorldDataCatalog(family, spawn);
        var boss = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var states = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var mailboxes = new LegacyMailboxStore(rootDirectory);
        using var log = new StringWriter();
        var targetFrames = new List<byte[]>();
        var targetConnection = CreateConnection(targetFrames);
        var target = CreateSession(1001, "MailHunter");
        sessions.Set(targetConnection, target);
        var killerConnection = CreateConnection([]);
        var killer = CreateSession(KillerUid, "LastHit");
        sessions.Set(killerConnection, killer);
        var now = DateTimeOffset.UtcNow;
        Assert.False(states.ApplyDamage(boss, 1, now, 1001, "MailHunter").Killed);
        Assert.True(states.ApplyDamage(boss, 200_000, now, KillerUid, "LastHit").Killed);
        var beforeRanking = states.GetBossHarmRanking(boss, DateTimeOffset.UtcNow).ToArray();
        Assert.Single(beforeRanking);
        mailboxes.Add(1001, new LegacyMailboxEntry
        {
            Id = 57, Title = "Retained", SenderName = "Before", Money = 37,
            Timer = new DateTime(2099, 1, 1).ToBinary(),
        });
        mailboxes.Save(1001);
        var path = mailboxes.GetMailboxPath(1001);
        var beforeBytes = File.ReadAllBytes(path);
        var context = new WorldPacketHandlerContext(killerConnection, log, killer,
            WorldData: worldData, Sessions: sessions, MonsterStates: states, Mailboxes: mailboxes,
            ProtocolProfile: validD13 ? ConquerProtocolProfile.VisibleDx8D13 : (ConquerProtocolProfile)12345);

        // Invoke the actual producer body with genuine ranking/session/mail owners.
        // This isolates its admission gate; the separate current-API tests exercise
        // the actual D13 Frame listing and ClaimAll handlers.
        var method = typeof(WorldPacketHandlers).GetMethod("TryApplyLegacyBossHarmRankMailRewardsAsync",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var invoke = method.CreateDelegate<Func<WorldPacketHandlerContext, LegacyMonsterInstance, CancellationToken, ValueTask>>();
        await invoke(context, boss, CancellationToken.None);

        Assert.True(sessions.TryGet(targetConnection, out var current));
        Assert.Same(target, current);
        if (!validD13)
        {
            Assert.Equal(beforeRanking, states.GetBossHarmRanking(boss, DateTimeOffset.UtcNow));
            Assert.Equal(beforeBytes, File.ReadAllBytes(path));
            Assert.Empty(targetFrames);
            Assert.Contains("unsupported protocol profile", log.ToString());
            return;
        }

        Assert.Empty(states.GetBossHarmRanking(boss, DateTimeOffset.UtcNow));
        Assert.Equal(2, new LegacyMailboxStore(rootDirectory).Load(1001).Count);
        var frame = Assert.Single(targetFrames.Select(ReadFrame), item => item.Header.Opcode == 2183);
        var bytes = frame.Payload.ToArray();
        var text = Encoding.UTF8.GetBytes("STR_SYSTEM_NAME@@");
        byte[] title = [0x4a, checked((byte)text.Length), .. text];
        byte[] sender = [0x52, checked((byte)text.Length), .. text];
        Assert.True(bytes.AsSpan().IndexOf(title) >= 0, "Actual D13 NewMail must contain literal title field9.");
        Assert.True(bytes.AsSpan().IndexOf(sender) >= 0, "Actual D13 NewMail must contain literal sender field10.");
    }
}
