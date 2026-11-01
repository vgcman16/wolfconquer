using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Theory]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(false, 2)]
    [InlineData(true, 0)] [InlineData(true, 1)] [InlineData(true, 2)] [InlineData(true, 3)]
    public Task GenuineAbsentBeforeJointHitRecoversEveryReplacementPrefixOnce(bool lethal, int boundary) =>
        RunFirstUseHitAsync(lethal, true, lethal, async f =>
        {
            var capture = await CaptureFirstUseRecoveryAsync(f, boundary, flushed: false);
            await AssertHitRecoveryRestartsAsync(capture);
        });

    [Theory]
    [InlineData(false, 1)] [InlineData(false, 2)]
    [InlineData(true, 1)] [InlineData(true, 2)] [InlineData(true, 3)]
    public Task GenuineAbsentBeforeJointHitRecoversEveryFlushedStageOnce(bool lethal, int stages) =>
        RunFirstUseHitAsync(lethal, true, lethal, async f =>
        {
            var capture = await CaptureFirstUseRecoveryAsync(f, stages, flushed: true);
            await AssertHitRecoveryRestartsAsync(capture);
        });
}
