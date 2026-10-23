using System.Runtime.CompilerServices;
using Xunit.Sdk;

namespace WolfConquer.Tests;

public sealed class TitleStorageLinkTestFailureTests
{
    [Fact]
    public async Task SuccessfulBodyAndEveryCleanupRunInOrder()
    {
        var observed = new List<string>();
        await TitleStorageLinkTestFailures.RunAsync(() =>
        {
            observed.Add("body");
            return Task.CompletedTask;
        }, () => observed.Add("link"), () => observed.Add("alias"), () => observed.Add("fixture"));
        Assert.Equal(new[] { "body", "link", "alias", "fixture" }, observed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrimaryAssertionOrSkipIsRethrownWithItsOriginalStackWhenCleanupSucceeds(bool skip)
    {
        Exception primary = skip ? SkipException.ForSkip("owned unavailable privilege") : new XunitException("primary assertion");
        bool cleaned = false;
        var actual = await Record.ExceptionAsync(() => TitleStorageLinkTestFailures.RunAsync(
            () => FailBody(primary), () => cleaned = true));
        Assert.Same(primary, actual);
        Assert.Contains(nameof(FailBody), actual!.StackTrace);
        Assert.True(cleaned);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CleanupCannotMaskPrimaryAssertionOrSkipAndAllLaterCleanupStillRuns(bool skip)
    {
        Exception primary = skip ? SkipException.ForSkip("owned unavailable privilege") : new XunitException("primary assertion");
        var cleanup = new IOException("changed owned link");
        bool finalCleanup = false;
        var aggregate = await Assert.ThrowsAsync<AggregateException>(() => TitleStorageLinkTestFailures.RunAsync(
            () => FailBody(primary), () => FailCleanup(cleanup), () => finalCleanup = true));
        Assert.Equal(new[] { primary, cleanup }, aggregate.InnerExceptions);
        Assert.Contains(nameof(FailBody), primary.StackTrace);
        Assert.Contains(nameof(FailCleanup), cleanup.StackTrace);
        Assert.True(finalCleanup);
    }

    [Fact]
    public async Task SoleCleanupFailureIsRethrownWithItsOriginalStack()
    {
        var cleanup = new IOException("changed alias root");
        bool laterCleanup = false;
        var actual = await Record.ExceptionAsync(() => TitleStorageLinkTestFailures.RunAsync(
            () => Task.CompletedTask, () => FailCleanup(cleanup), () => laterCleanup = true));
        Assert.Same(cleanup, actual);
        Assert.Contains(nameof(FailCleanup), actual!.StackTrace);
        Assert.True(laterCleanup);
    }

    [Fact]
    public async Task EveryCleanupFailureRetainsItsOrderAfterThePrimaryFailure()
    {
        var primary = new XunitException("body");
        var link = new IOException("changed link");
        var alias = new IOException("changed alias");
        var fixture = new IOException("preserved fixture");
        var aggregate = await Assert.ThrowsAsync<AggregateException>(() => TitleStorageLinkTestFailures.RunAsync(
            () => FailBody(primary), () => FailCleanup(link), () => FailCleanup(alias), () => FailCleanup(fixture)));
        Assert.Equal(new Exception[] { primary, link, alias, fixture }, aggregate.InnerExceptions);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Task FailBody(Exception error) => throw error;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FailCleanup(Exception error) => throw error;
}
