using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed partial class LegacyRuntimeReviverRaceTests
{
    [Theory]
    [InlineData(ConquerProtocolProfile.RootD11_7899)]
    [InlineData(ConquerProtocolProfile.VisibleDx8D13)]
    public Task ActualPlayerTickCannotReapplyPreparedStaminaFiveAfterGlobalReviverHundred(
        ConquerProtocolProfile profile) => RunAsync(async fixture =>
    {
        await fixture.StartAsync(profile);
        // Actual StartLegacyRuntimeUpdateLoop starts player, then global.
        Assert.Equal(2, fixture.Clock.Count);
        fixture.Clock.Fire(1, TimeSpan.FromMilliseconds(20));
        await fixture.PlayerFiveEntered.Task.WaitAsync(Fixture.Bound);
        fixture.Clock.Fire(2, TimeSpan.Zero);
        try
        {
            // CURRENT global finishes while player send is held. A real future
            // shared publication gate instead waits: neither observation alone
            // is acceptance, and the SAME two phases must finish after release.
            await Task.WhenAny(fixture.ReviverCompleted.Task, Task.Delay(200));
        }
        finally { fixture.ReleasePlayerFive.TrySetResult(); }
        await fixture.ReviverCompleted.Task.WaitAsync(Fixture.Bound);
        await fixture.Clock.WaitForCountAsync(4).WaitAsync(Fixture.Bound);
        Assert.Equal(4, fixture.Clock.Count);
        fixture.ActualRearmsObserved = true;
        fixture.AssertFinalRevival();
    });
}
