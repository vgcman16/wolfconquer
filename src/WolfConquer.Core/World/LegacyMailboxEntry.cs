namespace WolfConquer.Core.World;

public sealed class LegacyMailboxEntry
{
    public const int TitleByteLength = 64;
    public const int SenderNameByteLength = 128;
    public const int ContentByteLength = 512;

    public string Title { get; init; } = string.Empty;

    public string SenderName { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public uint Id { get; init; }

    public ulong Money { get; init; }

    public uint EMoney { get; init; }

    public uint ItemId { get; init; }

    public uint EmoneyRecordType { get; init; }

    public uint Flag { get; init; }

    public long Timer { get; init; }

    public bool IsLive(DateTime now)
    {
        return Timer > 0 && now < DateTime.FromBinary(Timer);
    }

    public DateTime ExpiresAt => DateTime.FromBinary(Timer);

    public bool HasClaimableRewards =>
        Money != 0 ||
        EMoney != 0 ||
        ItemId != 0 ||
        Flag != 0;

    public bool CanDelete => !HasClaimableRewards;

    public LegacyMailboxEntry WithClaimedMoney()
    {
        return WithValues(money: 0, eMoney: 0);
    }

    public LegacyMailboxEntry WithClearedFlag()
    {
        return WithValues(flag: 0);
    }

    public LegacyMailboxEntry WithClearedItemId()
    {
        return WithValues(itemId: 0);
    }

    private LegacyMailboxEntry WithValues(
        ulong? money = null,
        uint? eMoney = null,
        uint? itemId = null,
        uint? flag = null)
    {
        return new LegacyMailboxEntry
        {
            Title = Title,
            SenderName = SenderName,
            Content = Content,
            Id = Id,
            Money = money ?? Money,
            EMoney = eMoney ?? EMoney,
            ItemId = itemId ?? ItemId,
            EmoneyRecordType = EmoneyRecordType,
            Flag = flag ?? Flag,
            Timer = Timer
        };
    }

    public static bool TryCreateBossHarmRankReward(
        int rank,
        uint id,
        DateTime now,
        out LegacyMailboxEntry entry)
    {
        if (!LegacyBossDeathRewardPolicy.TryGetHarmRankMailConquerPointReward(rank, out var reward))
        {
            entry = new LegacyMailboxEntry();
            return false;
        }

        entry = new LegacyMailboxEntry
        {
            Title = reward.Title,
            SenderName = reward.Sender,
            Content = reward.Content,
            Id = id,
            Money = checked((ulong)reward.Money),
            EMoney = checked((uint)reward.ConquerPoints),
            ItemId = reward.ItemId,
            EmoneyRecordType = checked((uint)reward.EmoneyRecordType),
            Flag = checked((uint)reward.Flag),
            Timer = now.AddSeconds(reward.ExpirationDate).Ticks
        };
        return true;
    }
}
