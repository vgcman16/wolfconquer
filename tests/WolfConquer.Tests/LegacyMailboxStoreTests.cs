using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMailboxStoreTests : IDisposable
{
    private readonly string rootDirectory;
    private readonly DateTime now = new(2026, 6, 13, 12, 0, 0, DateTimeKind.Unspecified);

    public LegacyMailboxStoreTests()
    {
        rootDirectory = Path.Combine(Path.GetTempPath(), "WolfConquerMailboxTests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    [Fact]
    public void GetNextIdReturnsSmallestPositiveMissingId()
    {
        var store = CreateStore();
        var playerUid = 1_001U;

        store.Add(playerUid, CreateEntry(id: 1));
        store.Add(playerUid, CreateEntry(id: 3));

        Assert.Equal(2U, store.GetNextId(playerUid));
    }

    [Fact]
    public void SaveAndLoadRoundTripsLegacyBinaryLayout()
    {
        var playerUid = 2_002U;
        var store = CreateStore();
        var entry = CreateEntry(
            id: 42,
            title: "Prize",
            senderName: "System",
            content: "Attached reward.",
            money: 123_456,
            eMoney: 789,
            itemId: 11,
            emoneyRecordType: 2,
            flag: 9,
            timer: now.AddDays(1).ToBinary());

        store.Add(playerUid, entry);
        store.Save(playerUid);

        var path = store.GetMailboxPath(playerUid);
        Assert.Equal(
            4 + 64 + 128 + 512 + 4 + 8 + 4 + 4 + 4 + 4 + 8,
            new FileInfo(path).Length);

        var loaded = CreateStore().Load(playerUid);
        var roundTrip = Assert.Single(loaded);
        Assert.Equal(entry.Title, roundTrip.Title);
        Assert.Equal(entry.SenderName, roundTrip.SenderName);
        Assert.Equal(entry.Content, roundTrip.Content);
        Assert.Equal(entry.Id, roundTrip.Id);
        Assert.Equal(entry.Money, roundTrip.Money);
        Assert.Equal(entry.EMoney, roundTrip.EMoney);
        Assert.Equal(entry.ItemId, roundTrip.ItemId);
        Assert.Equal(entry.EmoneyRecordType, roundTrip.EmoneyRecordType);
        Assert.Equal(entry.Flag, roundTrip.Flag);
        Assert.Equal(entry.Timer, roundTrip.Timer);
    }

    [Fact]
    public void LoadAndListFilterExpiredMail()
    {
        var playerUid = 3_003U;
        var store = CreateStore();
        store.Add(playerUid, CreateEntry(id: 1, timer: now.AddSeconds(-1).ToBinary()));
        store.Add(playerUid, CreateEntry(id: 2, timer: now.AddSeconds(1).ToBinary()));
        store.Save(playerUid);

        var loaded = CreateStore().Load(playerUid);
        var entry = Assert.Single(loaded);
        Assert.Equal(2U, entry.Id);
        Assert.Equal([2U], CreateStore().Load(playerUid).Select(mail => mail.Id).ToArray());
    }

    [Fact]
    public void TryCreateBossHarmRankRewardProjectsLegacyMailFields()
    {
        var found = LegacyMailboxEntry.TryCreateBossHarmRankReward(
            rank: 3,
            id: 77,
            now,
            out var entry);

        Assert.True(found);
        Assert.Equal("STR_SYSTEM_NAME@@", entry.Title);
        Assert.Equal("STR_SYSTEM_NAME@@", entry.SenderName);
        Assert.Equal("STR_SYSTEM_NAME@@", entry.Content);
        Assert.Equal(77U, entry.Id);
        Assert.Equal(100_000UL, entry.Money);
        Assert.Equal(600U, entry.EMoney);
        Assert.Equal(0U, entry.ItemId);
        Assert.Equal(1U, entry.EmoneyRecordType);
        Assert.Equal(3U, entry.Flag);
        Assert.Equal(now.AddSeconds(2_592_000).ToBinary(), entry.Timer);
    }

    [Fact]
    public void TryCreateBossHarmRankRewardRejectsUnrewardedRank()
    {
        var found = LegacyMailboxEntry.TryCreateBossHarmRankReward(
            rank: 10,
            id: 1,
            now,
            out var entry);

        Assert.False(found);
        Assert.Equal(0U, entry.Id);
    }

    [Fact]
    public void TryClaimMoneyReturnsAmountsAndZerosMoneyAndEMoney()
    {
        var store = CreateStore();
        var playerUid = 4_004U;
        store.Add(playerUid, CreateEntry(id: 9, money: 123, eMoney: 45));

        var claimed = store.TryClaimMoney(
            playerUid,
            mailId: 9,
            out var money,
            out var eMoney,
            out var updated);

        Assert.True(claimed);
        Assert.Equal(123UL, money);
        Assert.Equal(45U, eMoney);
        Assert.Equal(0UL, updated.Money);
        Assert.Equal(0U, updated.EMoney);
        Assert.True(store.TryGet(playerUid, 9, out var stored));
        Assert.Equal(0UL, stored.Money);
        Assert.Equal(0U, stored.EMoney);
    }

    [Fact]
    public void TryRemoveIfEmptyIsBlockedUntilRewardFieldsAreZero()
    {
        var store = CreateStore();
        var playerUid = 5_005U;
        store.Add(playerUid, CreateEntry(id: 7, money: 1, eMoney: 2, itemId: 3, flag: 4));

        var removed = store.TryRemoveIfEmpty(playerUid, mailId: 7, out var blocked);

        Assert.False(removed);
        Assert.Equal(7U, blocked.Id);
        Assert.True(store.TryGet(playerUid, 7, out _));
    }

    [Fact]
    public void TryRemoveIfEmptySucceedsAfterRewardFieldsAreCleared()
    {
        var store = CreateStore();
        var playerUid = 6_006U;
        store.Add(playerUid, CreateEntry(id: 7, money: 1, eMoney: 2, itemId: 3, flag: 4));

        Assert.True(store.TryClaimMoney(playerUid, 7, out _, out _, out _));
        Assert.True(store.TryClearItemId(playerUid, 7, itemClaimSucceeded: true, out _));
        Assert.True(store.TryClearFlag(playerUid, 7, packageClaimSucceeded: true, out _));
        var removed = store.TryRemoveIfEmpty(playerUid, mailId: 7, out var removedEntry);

        Assert.True(removed);
        Assert.Equal(7U, removedEntry.Id);
        Assert.False(store.TryGet(playerUid, 7, out _));
    }

    [Fact]
    public void TryClearFlagOnlyClearsWhenPackageClaimSucceeds()
    {
        var store = CreateStore();
        var playerUid = 7_007U;
        store.Add(playerUid, CreateEntry(id: 8, flag: 99));

        var failed = store.TryClearFlag(playerUid, mailId: 8, packageClaimSucceeded: false, out _);

        Assert.False(failed);
        Assert.True(store.TryGet(playerUid, 8, out var stillFlagged));
        Assert.Equal(99U, stillFlagged.Flag);

        var succeeded = store.TryClearFlag(playerUid, mailId: 8, packageClaimSucceeded: true, out var updated);

        Assert.True(succeeded);
        Assert.Equal(0U, updated.Flag);
        Assert.True(store.TryGet(playerUid, 8, out var stored));
        Assert.Equal(0U, stored.Flag);
    }

    private LegacyMailboxStore CreateStore()
    {
        return new LegacyMailboxStore(rootDirectory, nowProvider: () => now);
    }

    private LegacyMailboxEntry CreateEntry(
        uint id,
        string title = "Title",
        string senderName = "Sender",
        string content = "Content",
        ulong money = 1,
        uint eMoney = 2,
        uint itemId = 3,
        uint emoneyRecordType = 4,
        uint flag = 5,
        long? timer = null)
    {
        return new LegacyMailboxEntry
        {
            Title = title,
            SenderName = senderName,
            Content = content,
            Id = id,
            Money = money,
            EMoney = eMoney,
            ItemId = itemId,
            EmoneyRecordType = emoneyRecordType,
            Flag = flag,
            Timer = timer ?? now.AddDays(1).ToBinary()
        };
    }
}
