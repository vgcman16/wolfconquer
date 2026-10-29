using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LocalRuntimeInspectionTests
{
    // The mail tests' same-named attribute is private to their different class.
    private sealed class WindowsOperatorPipeFactAttribute : FactAttribute
    {
        public WindowsOperatorPipeFactAttribute()
        { if (!OperatingSystem.IsWindows()) Skip = "Actual CurrentUserOnly local endpoint lifecycle requires Windows."; }
    }

    [WindowsOperatorPipeFact]
    public Task ActualHostInspectorOnlyNonlistenCompletionReleasesAndJoinsItsEndpoint() => HostCompletesWithoutListen(false);

    [WindowsOperatorPipeFact]
    public Task ActualHostBothEnabledNonlistenCompletionReleasesAndJoinsBothEndpoints() => HostCompletesWithoutListen(true);

    private static Task HostCompletesWithoutListen(bool mailEnabled) => WithHostEnvironment(mailEnabled, async (f, endpoints, runs) =>
    {
        var files = Files(f); var sent = f.SentSnapshot(); var water = f.HighWater; var current = f.Current;
        var host = InvokeActualManagedHost(f); runs.Add(host);
        Assert.Equal(0, await host.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.True(host.IsCompletedSuccessfully); Assert.False(f.Runtime.IsStopping);
        // FirstPipeInstance reacquisition fails if the actual host leaked its
        // endpoint. Real endpoint Dispose joins its worker before releasing it.
        if (mailEnabled) KeepHostEndpoint(endpoints, Assert.IsType<LocalOperatorMailPipe>(
            LocalOperatorMailPipe.StartIfEnabled(f.Handler, f.Runtime, f.Domain)));
        var inspector = KeepHostEndpoint(endpoints, Assert.IsType<LocalRuntimeInspectionPipe>(
            LocalRuntimeInspectionPipe.StartIfEnabled(f.Handler, f.Runtime, f.Domain)));
        Diagnostic(await Exchange(inspector.PipeName, Query(f)));
        Assert.Same(current, f.Current); Assert.False(f.Native.OrdinarySpellBinding!.IsRetired);
        Assert.Equal(water, f.HighWater); Unchanged(f, files, sent);
    });

    [WindowsOperatorPipeFact]
    public Task ActualHostInspectorCollisionDisposesItsNewMailAndRetainsTheExistingInspector() =>
        WithHostEnvironment(true, async (f, endpoints, runs) =>
        {
            var inspector = KeepHostEndpoint(endpoints, Assert.IsType<LocalRuntimeInspectionPipe>(
                LocalRuntimeInspectionPipe.StartIfEnabled(f.Handler, f.Runtime, f.Domain)));
            var files = Files(f); var sent = f.SentSnapshot(); var water = f.HighWater; var current = f.Current;
            Diagnostic(await Exchange(inspector.PipeName, Query(f)));
            var host = InvokeActualManagedHost(f); runs.Add(host);
            var error = await Assert.ThrowsAnyAsync<Exception>(() => host.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.True(error is IOException or UnauthorizedAccessException, error.GetType().FullName);
            Assert.IsNotType<TargetInvocationException>(error);
            Assert.True(host.IsFaulted); Assert.False(inspector.Completion.IsCompleted); Assert.False(f.Runtime.IsStopping);
            // Host started mail before the real inspector collision. Its actual
            // mail handle must now be gone, without adopting/stopping the peer.
            KeepHostEndpoint(endpoints, Assert.IsType<LocalOperatorMailPipe>(
                LocalOperatorMailPipe.StartIfEnabled(f.Handler, f.Runtime, f.Domain)));
            Diagnostic(await Exchange(inspector.PipeName, Query(f)));
            Assert.False(inspector.Completion.IsCompleted); Assert.Same(current, f.Current);
            Assert.False(f.Native.OrdinarySpellBinding!.IsRetired);
            Assert.Equal(water, f.HighWater); Unchanged(f, files, sent);
        });

    private static Task<int> InvokeActualManagedHost(OperatorGearMailFixture f)
    {
        // This Process supplies the existing built-project path convention only;
        // it is never started. Loading an assembly does not run its entry point.
        using var location = TestProjectProcess.Create(Repository(), "WolfConquer.WorldServer");
        var path = Path.GetFullPath(location.StartInfo.ArgumentList[0]);
        var assembly = Assembly.LoadFrom(path);
        Assert.True(string.Equals(path, Path.GetFullPath(assembly.Location), StringComparison.OrdinalIgnoreCase),
            "The requested actual managed WorldServer output must be loaded.");
        var host = assembly.GetType("LocalOperatorMailHost", throwOnError: true)!;
        var method = host.GetMethod("RunAsync", BindingFlags.Static | BindingFlags.NonPublic, binder: null,
            types: [typeof(ConquerEndpoint), typeof(string[]), typeof(WorldServerFrameHandler), typeof(LegacyOwnedAdmissionRuntime), typeof(Guid)],
            modifiers: null) ?? throw new MissingMethodException(host.FullName, "RunAsync");
        try
        {
            return Assert.IsAssignableFrom<Task<int>>(method.Invoke(null,
                [new ConquerEndpoint(ConquerRuntimeDefaults.WorldServer.Role, "127.0.0.1", 0), Array.Empty<string>(), f.Handler, f.Runtime, f.Domain]));
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }

    private static T KeepHostEndpoint<T>(List<IAsyncDisposable> endpoints, T actual) where T : IAsyncDisposable
    { endpoints.Add(actual); return actual; }

    private static Task WithHostEnvironment(bool mailEnabled,
        Func<OperatorGearMailFixture, List<IAsyncDisposable>, List<Task<int>>, Task> body) =>
        LegacyOperatorGearMailOwnerTests.WithOwnerAsync(async f =>
        {
            var previousMail = Environment.GetEnvironmentVariable("WOLFCONQUER_OPERATOR_MAIL");
            var previousInspector = Environment.GetEnvironmentVariable("WOLFCONQUER_RUNTIME_INSPECTOR");
            var endpoints = new List<IAsyncDisposable>(); var runs = new List<Task<int>>(); var failures = new List<Exception>();
            try
            {
                Environment.SetEnvironmentVariable("WOLFCONQUER_OPERATOR_MAIL", mailEnabled ? "1" : null);
                Environment.SetEnvironmentVariable("WOLFCONQUER_RUNTIME_INSPECTOR", "1");
                await body(f, endpoints, runs);
            }
            catch (Exception error) { failures.Add(error); }
            foreach (var endpoint in endpoints.AsEnumerable().Reverse())
            {
                try
                {
                    await endpoint.DisposeAsync();
                    var completion = endpoint switch
                    { LocalOperatorMailPipe mail => mail.Completion, LocalRuntimeInspectionPipe inspector => inspector.Completion,
                        _ => throw new InvalidOperationException("Only actual owned operator endpoints belong to this test.") };
                    Assert.True(completion.IsCompleted);
                    try { await completion; }
                    catch (OperationCanceledException) when (completion.IsCanceled) { }
                }
                catch (Exception error) { failures.Add(error); }
            }
            var pending = runs.Where(run => !run.IsCompleted).ToArray();
            if (pending.Length != 0)
            {
                // Failure cleanup cancels this genuine temporary fixture only;
                // completion must still be awaited and is never assumed.
                try { await f.Runtime.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10)); }
                catch (Exception error) { failures.Add(error); }
                foreach (var run in pending)
                    try { await run.WaitAsync(TimeSpan.FromSeconds(10)); }
                    catch (Exception error) { failures.Add(error); }
            }
            try { Environment.SetEnvironmentVariable("WOLFCONQUER_OPERATOR_MAIL", previousMail); }
            catch (Exception error) { failures.Add(error); }
            try { Environment.SetEnvironmentVariable("WOLFCONQUER_RUNTIME_INSPECTOR", previousInspector); }
            catch (Exception error) { failures.Add(error); }
            if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures.Count > 1) throw new AggregateException("Managed host assertion and/or owned cleanup failed.", failures);
        });
}
