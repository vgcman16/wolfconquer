using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace WolfConquer.Launcher.Tests;

public sealed partial class LauncherViewModelTests
{
    [Fact]
    public async Task DispatcherHelperCompletesAfterTheRealStaThreadTerminates()
    {
        Thread? ownedThread = null;
        await OnDispatcher(async () =>
        {
            ownedThread = Thread.CurrentThread;
            Assert.Equal(ApartmentState.STA, ownedThread.GetApartmentState());
            Assert.True(Dispatcher.CurrentDispatcher.CheckAccess());
            await Drain();
        });

        Assert.NotNull(ownedThread);
        Assert.False(ownedThread.IsAlive);
    }

    [Theory]
    [InlineData("assertion")]
    [InlineData("skip")]
    [InlineData("operation")]
    public async Task DispatcherHelperRetainsTheOriginalPrimaryFailureAndStack(string failureKind)
    {
        Exception primary = CreateDispatcherDiagnosticFailure(failureKind);
        Thread? ownedThread = null;

        var observed = await Record.ExceptionAsync(() => OnDispatcher(async () =>
        {
            ownedThread = Thread.CurrentThread;
            await Drain();
            ThrowDispatcherDiagnosticFailure(primary);
        }));

        Assert.NotNull(observed);
        Assert.Same(primary, observed);
        Assert.Contains(nameof(ThrowDispatcherDiagnosticFailure), observed.StackTrace!);
        Assert.NotNull(ownedThread);
        Assert.False(ownedThread.IsAlive);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("assertion")]
    [InlineData("skip")]
    [InlineData("operation")]
    public async Task DispatcherHelperRetainsJoinTimeoutAlongsideTheOriginalPrimaryFailure(string failureKind)
    {
        using var releaseShutdown = new ManualResetEventSlim();
        using var shutdownEntered = new ManualResetEventSlim();
        Exception? primary = failureKind == "none" ? null : CreateDispatcherDiagnosticFailure(failureKind);
        Thread? ownedThread = null;
        Exception? observed;
        try
        {
            observed = await Record.ExceptionAsync(() => OnDispatcher(async () =>
            {
                ownedThread = Thread.CurrentThread;
                Dispatcher.CurrentDispatcher.ShutdownStarted += (_, _) =>
                {
                    shutdownEntered.Set();
                    // Hold the real dispatcher past its normal join deadline, with an emergency bound.
                    releaseShutdown.Wait(TimeSpan.FromSeconds(30));
                };
                await Drain();
                if (primary is not null) ThrowDispatcherDiagnosticFailure(primary);
            }));
        }
        finally
        {
            releaseShutdown.Set();
            // Observe actual owned-thread termination before disposing its gates, even if the helper fails.
            ownedThread?.Join();
        }

        Assert.NotNull(ownedThread);
        Assert.False(ownedThread.IsAlive);
        Assert.True(shutdownEntered.IsSet);
        TimeoutException timeout;
        if (primary is null)
        {
            timeout = Assert.IsType<TimeoutException>(observed);
        }
        else
        {
            var combined = Assert.IsType<AggregateException>(observed);
            Assert.Equal(2, combined.InnerExceptions.Count);
            Assert.Same(primary, combined.InnerExceptions[0]);
            Assert.Contains(nameof(ThrowDispatcherDiagnosticFailure), primary.StackTrace!);
            timeout = Assert.IsType<TimeoutException>(combined.InnerExceptions[1]);
        }
        Assert.Equal("Owned presentation dispatcher did not shut down.", timeout.Message);
        Assert.Contains(nameof(OnDispatcher), timeout.StackTrace!);
    }

    private static Exception CreateDispatcherDiagnosticFailure(string kind) => kind switch
    {
        "assertion" => new Xunit.Sdk.XunitException("Owned dispatcher assertion failed."),
        "skip" => Xunit.Sdk.SkipException.ForSkip("Owned dispatcher skip diagnostic."),
        "operation" => new InvalidOperationException("Owned dispatcher operation failed."),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowDispatcherDiagnosticFailure(Exception error) => ExceptionDispatchInfo.Capture(error).Throw();
}
