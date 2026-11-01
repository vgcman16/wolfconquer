using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyRuntimeUpdateTickerPreparationTests
{
    [Fact]
    public void DiscardedDataPreparationDoesNotConsumeSourceCountersClocksOrInvitation()
    {
        var local = new DateTime(2026, 6, 5, 8, 1, 6, DateTimeKind.Unspecified);
        var now = new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
        var source = new LegacyRuntimeUpdateTicker(OwnedEquipmentBehaviorFixture.Character(), now, false, false);
        var staged = source.CopyForPreparation();
        var preparedFrames = staged.CreateDueFrameBytes(now);
        Assert.Contains(preparedFrames, bytes => LegacyMsgMessagePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _));
        Assert.Empty(staged.CreateDueFrameBytes(now));

        var actualFrames = source.CreateDueFrameBytes(now);
        Assert.Equal(preparedFrames.Select(Convert.ToHexString), actualFrames.Select(Convert.ToHexString));
        Assert.Empty(source.CreateDueFrameBytes(now));
    }

    [Fact]
    public void ExpiringPreparedXpFlagDoesNotAlterSourceStatusWords()
    {
        var now = new DateTimeOffset(2026, 6, 5, 8, 0, 0, TimeSpan.Zero);
        var character = OwnedEquipmentBehaviorFixture.Character() with
        { Attributes = new Dictionary<string, string> { ["StatusFlags"] = "16/", ["Stamina"] = "0", ["XPCount"] = "0" } };
        var source = new LegacyRuntimeUpdateTicker(character, now, false, false);
        var staged = source.CopyForPreparation();
        _ = staged.CreateDueUpdates(now.AddSeconds(20));
        var original = source.CreateDueUpdates(now);
        var status = original.Select(update => LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(update.FrameBytes), out var packet)
            ? packet : null).Where(packet => packet is not null).SelectMany(packet => packet!.Updates)
            .Single(update => update.Type == LegacyMsgUpdateDataType.StatusFlag);
        Assert.NotNull(status.Values);
        Assert.Equal(16UL, status.Values[0] & 16UL);
    }
}
