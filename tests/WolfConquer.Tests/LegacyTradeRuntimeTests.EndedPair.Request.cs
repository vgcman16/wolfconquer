using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyTradeRuntimeTests
{
    [Theory]
    [InlineData("none")] [InlineData("sender")] [InlineData("target")]
    public Task QueuedTradeRequestResolvesEndedTargetBeforeCreatingInviteWhileCurrentLivePairStillWorks(string retired) =>
        RunItemClosureAsync(async () =>
        {
            var left = AddItemClosurePlayer(1_298_051, 1_000, 50, [Item(10)]);
            var right = AddItemClosurePlayer(1_298_052, 2_000, 60, [Item(20)]);
            var beforeLeft = CaptureItemClosure(left);
            var beforeRight = CaptureItemClosure(right);
            var ended = retired switch
            {
                "none" => null,
                "sender" => left,
                "target" => right,
                _ => throw new ArgumentOutOfRangeException(nameof(retired)),
            };

            await QueueTradeWithEndedEndpointAsync(left, new(0) { UID = right.Uid }, ended);

            AssertItemClosureUnchanged(left, beforeLeft);
            AssertItemClosureUnchanged(right, beforeRight);
            Assert.False(Current(left).LegacyTradeActive);
            Assert.False(Current(right).LegacyTradeActive);
            if (ended is not null)
            {
                Assert.Equal(beforeLeft.SentCount, left.Sent.Count);
                Assert.Equal(beforeRight.SentCount, right.Sent.Count);
                Assert.False(trades.Contains(left.Connection));
                Assert.False(trades.Contains(right.Connection));
                return;
            }
            Assert.True(trades.Contains(left.Connection));
            Assert.True(trades.Contains(right.Connection));
            Assert.Contains(TradeFrames(left), packet => packet.Action == 0);
            Assert.Contains(TradeFrames(right), packet => packet.Action == 0);
            await SendItemClosureAsync(right, new(1) { UID = left.Uid });
            Assert.True(Current(left).LegacyTradeActive);
            Assert.True(Current(right).LegacyTradeActive);
            await SendItemClosureAsync(left, new(2));
            AssertItemClosureSettled(left, right, 1_000, 2_000);
            AssertItemClosureFiles(beforeLeft.Files);
        });
}
