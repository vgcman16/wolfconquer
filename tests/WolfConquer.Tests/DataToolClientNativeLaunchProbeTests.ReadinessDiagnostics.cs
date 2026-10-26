namespace WolfConquer.Tests;

public sealed partial class DataToolClientNativeLaunchProbeTests
{
    private static async Task<Exception> AddReadinessProbeDiagnosticsAsync(
        Task<ConsoleRunResult> running, long failureElapsedMilliseconds, Exception primary)
    {
        ConsoleRunResult result;
        try
        {
            result = await running;
        }
        catch (Exception diagnosticFailure)
        {
            return ReferenceEquals(primary, diagnosticFailure)
                ? new InvalidOperationException("The bounded native-readiness fixture failed before a report was available.", primary)
                : new AggregateException("Native-readiness failure and report collection both failed.", primary, diagnosticFailure);
        }

        return new InvalidOperationException(
            $"""
            Native readiness integration failed; complete real DataTool result follows.
            FailureElapsedMs: {failureElapsedMilliseconds}
            DataToolExitCode: {result.ExitCode}
            --- stdout begin ---
            {result.StandardOutput}
            --- stdout end ---
            --- stderr begin ---
            {result.StandardError}
            --- stderr end ---
            """,
            primary);
    }
}
