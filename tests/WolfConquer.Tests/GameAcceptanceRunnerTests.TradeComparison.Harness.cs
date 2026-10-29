using System.Diagnostics;
using System.Text.Json.Nodes;

namespace WolfConquer.Tests;

public sealed partial class GameAcceptanceRunnerTests
{
    private static async Task<JsonObject> CompareTradeAsync(TradeComparisonFixture fixture,
        string? intentText = null, string? expectedHash = null, string? planText = null, string? rawReportNumber = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "wolf-trade-comparison-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var complete = false;
        try
        {
            const string wrapper = """
                param([string]$SourceRoot,[string]$InputFile)
                $ErrorActionPreference='Stop'
                $tools=Join-Path $SourceRoot 'tools/testing'
                . (Join-Path $tools 'GameAcceptance.Status.Files.ps1')
                . (Join-Path $tools 'GameAcceptance.Walk.ps1')
                . (Join-Path $tools 'GameAcceptance.Health.Assertions.ps1')
                . (Join-Path $tools 'GameAcceptance.Trade.Compare.ps1')
                $inputData=Get-Content -LiteralPath $InputFile -Raw | ConvertFrom-Json -AsHashtable
                try {
                    $plan=Read-GaTradePlan ([Text.Encoding]::UTF8.GetBytes($inputData.planText))
                    $intent=Read-GaTradeIntent ([Text.Encoding]::UTF8.GetBytes($inputData.intentText)) $inputData.expectedHash $inputData.evidenceId
                    $selection=@(Test-GaTradeSelection $inputData.beforeA $inputData.beforeB $intent)
                    $issues=@(Test-GaTradeTransition $inputData.beforeA $inputData.beforeB $inputData.afterA $inputData.afterB $intent)
                    $result=@{selection=$selection;issues=$issues;state=(New-GaTradeObservation $issues);planParticipants=$plan.participants.Count}
                } catch { $result=@{readerError=$_.Exception.Message} }
                $result | ConvertTo-Json -Depth 24 -Compress
                """;
            var script = Path.Combine(root, "compare.ps1"); var input = Path.Combine(root, "input.json");
            File.WriteAllText(script, wrapper);
            intentText ??= fixture.Intent.ToJsonString();
            var payload = new JsonObject
            {
                ["beforeA"] = fixture.BeforeA.DeepClone(), ["beforeB"] = fixture.BeforeB.DeepClone(),
                ["afterA"] = fixture.AfterA.DeepClone(), ["afterB"] = fixture.AfterB.DeepClone(),
                ["intentText"] = intentText, ["expectedHash"] = expectedHash ?? TradeComparisonFixture.Hash(intentText),
                ["planText"] = planText ?? fixture.Plan().ToJsonString(), ["evidenceId"] = fixture.EvidenceId
            };
            var payloadText = payload.ToJsonString();
            // Test-only JSON literal substitution. It never enters executable
            // script text; the actual JSON parser consumes the retained bytes.
            if (rawReportNumber is not null)
                payloadText = payloadText.Replace("\"__trade_report_number__\"", rawReportNumber, StringComparison.Ordinal);
            File.WriteAllText(input, payloadText);
            var before = Files(root);
            var start = new ProcessStartInfo(FindApplication("pwsh", "WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH"))
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File", script,
                "-SourceRoot", RepositoryRoot(), "-InputFile", input }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException primary)
            {
                try { process.Kill(entireProcessTree: true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
                catch (Exception cleanup) { throw new AggregateException(primary, cleanup); }
                throw;
            }
            Assert.Equal(0, process.ExitCode); Assert.Empty(await stderr);
            var result = JsonNode.Parse(await stdout)!.AsObject();
            AssertFiles(root, before); // The actual pure comparison leaves both synthetic inputs untouched.
            complete = true; return result;
        }
        finally { if (complete) DeleteOwned(root, "wolf-trade-comparison-"); }
    }
}
