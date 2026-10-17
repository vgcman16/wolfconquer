using System.Text.Json;

internal static partial class Program
{
    static int CachedClientPatchResultSummary(string[] args)
    {
        CachedClientPatchResultSummaryOptions options;
        try
        {
            options = ResolveCachedClientPatchResultSummaryOptions(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            Console.WriteLine("Cached client patch result summary:");
            foreach (var jsonPath in options.JsonPaths)
            {
                var summary = ReadCachedClientPatchResultSummary(jsonPath);
                Console.WriteLine(FormatCachedClientPatchResultSummary(summary));
            }

            return 0;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Cached client patch result summary failed: {ex.Message}");
            return 1;
        }
    }

    static CachedClientPatchResultSummaryOptions ResolveCachedClientPatchResultSummaryOptions(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException(
                "Usage: WolfConquer.DataTool cached-client-patch-result-summary <patchResultJsonPath> [patchResultJsonPath ...]");
        }

        return new CachedClientPatchResultSummaryOptions(
            args.Skip(1)
                .Select(Path.GetFullPath)
                .ToArray());
    }

    static CachedClientPatchResultSummaryResult ReadCachedClientPatchResultSummary(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var payload = document.RootElement.TryGetProperty("Result", out var result)
            ? result
            : document.RootElement;

        return new CachedClientPatchResultSummaryResult(
            Path.GetFileName(path),
            FormatOptionalBool(ReadOptionalBool(payload, "ProcessRespondingAfterObserve")),
            FormatOptionalText(ReadOptionalString(payload, "MainWindowHandleAfterObserve")),
            CountArrayProperty(payload, "WindowEvidenceAfterObserve"),
            CountArrayProperties(payload, ["ExceptionEvidenceAfterObserve", "ExceptionAddressEvidenceAfterObserve"]),
            CountArrayProperty(payload, "RouteEvidenceAfterObserve"),
            CountArrayProperty(payload, "FinalRouteEvidenceAfterObserve"),
            CountArrayProperty(payload, "LogEvidenceAfterObserve"),
            CountArrayProperties(payload, ["ClientTcpConnectionsAfterObserve", "TcpConnectionsAfterObserve"]),
            HasHiddenWerEvidence(payload),
            FormatOptionalBool(ReadOptionalBool(payload, "KnownFallbackRoutesPatched")),
            FormatOptionalArrayCount(payload, "DelayedRoutePatches"),
            ReadCachedClientPatchStates(payload));
    }

    static string FormatCachedClientPatchResultSummary(CachedClientPatchResultSummaryResult summary)
    {
        var patches = summary.ModulePatchStates.Count == 0
            ? "<none>"
            : string.Join(
                "; ",
                summary.ModulePatchStates.Select(patch =>
                    $"{patch.Name} patched={patch.Patched} already={patch.AlreadyPatched} before={patch.Before} fail={patch.FailureReason}"));

        return
            $"file={summary.FileName} responding={summary.ProcessRespondingAfterObserve} " +
            $"mainWindow={summary.MainWindowHandleAfterObserve} " +
            $"counts(window={summary.WindowEvidenceCount} exception={summary.ExceptionEvidenceCount} " +
            $"route={summary.RouteEvidenceCount} finalRoute={summary.FinalRouteEvidenceCount} " +
            $"log={summary.LogEvidenceCount} tcp={summary.TcpConnectionCount}) " +
            $"hiddenWer={summary.HiddenWerPresent} knownFallback={summary.KnownFallbackRoutesPatched} " +
            $"delayedRoutes={summary.DelayedRoutePatchCount} modulePatches={summary.ModulePatchStates.Count} " +
            $"patches={patches} outcome={ClassifyCachedClientPatchOutcome(summary)}";
    }

    static string ClassifyCachedClientPatchOutcome(CachedClientPatchResultSummaryResult summary)
    {
        if (summary.HiddenWerPresent)
        {
            return "hidden-wer";
        }

        if (summary.RouteEvidenceCount > 0 ||
            summary.FinalRouteEvidenceCount > 0 ||
            summary.TcpConnectionCount > 0)
        {
            return "route-moved";
        }

        if (summary.LogEvidenceCount > 0)
        {
            return "log-moved";
        }

        if (summary.WindowEvidenceCount > 0 ||
            summary.ExceptionEvidenceCount > 0 ||
            HasObservedMainWindow(summary.MainWindowHandleAfterObserve))
        {
            return "window-or-exception";
        }

        return string.Equals(summary.ProcessRespondingAfterObserve, bool.TrueString, StringComparison.OrdinalIgnoreCase)
            ? "clean-negative"
            : "no-movement";
    }

    static bool HasObservedMainWindow(string mainWindowHandle)
    {
        return !string.IsNullOrWhiteSpace(mainWindowHandle) &&
            !string.Equals(mainWindowHandle, "<missing>", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(mainWindowHandle, "0", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(mainWindowHandle, "0x0", StringComparison.OrdinalIgnoreCase);
    }

    static IReadOnlyList<CachedClientPatchStateSummary> ReadCachedClientPatchStates(JsonElement payload)
    {
        if (!payload.TryGetProperty("ModuleMemoryPatchesBeforeRoutePatch", out var patchesElement) ||
            patchesElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var patches = new List<CachedClientPatchStateSummary>();
        foreach (var patchElement in patchesElement.EnumerateArray())
        {
            patches.Add(new CachedClientPatchStateSummary(
                FormatOptionalText(ReadOptionalString(patchElement, "Name")),
                FormatOptionalBool(ReadOptionalBool(patchElement, "Patched")),
                FormatOptionalBool(ReadOptionalBool(patchElement, "AlreadyPatched")),
                FormatOptionalText(ReadFirstOptionalString(patchElement, ["BeforeHex", "Before"])),
                FormatFailureReason(ReadOptionalString(patchElement, "FailureReason"))));
        }

        return patches;
    }

    static int CountArrayProperties(JsonElement payload, IReadOnlyList<string> names)
    {
        return names.Sum(name => CountArrayProperty(payload, name));
    }

    static int CountArrayProperty(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        return value.GetArrayLength();
    }

    static string FormatOptionalArrayCount(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return "<missing>";
        }

        return value.ValueKind == JsonValueKind.Array
            ? value.GetArrayLength().ToString()
            : "<not-array>";
    }

    static bool HasHiddenWerEvidence(JsonElement payload)
    {
        return ContainsWerEvidence(payload, "WindowEvidenceAfterObserve") ||
            ContainsWerEvidence(payload, "WindowTreeEvidenceAfterObserve") ||
            ContainsWerEvidence(payload, "ProcessEvidenceAfterObserve");
    }

    static bool ContainsWerEvidence(JsonElement payload, string propertyName)
    {
        return payload.TryGetProperty(propertyName, out var value) &&
            JsonElementContainsWerEvidence(value);
    }

    static bool JsonElementContainsWerEvidence(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var value = element.GetString() ?? string.Empty;
                return value.Contains("WerFault", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("WER Fault", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("WerFaultWndClass", StringComparison.OrdinalIgnoreCase);
            case JsonValueKind.Array:
                return element.EnumerateArray().Any(JsonElementContainsWerEvidence);
            case JsonValueKind.Object:
                return element.EnumerateObject().Any(property => JsonElementContainsWerEvidence(property.Value));
            default:
                return false;
        }
    }

    static string ReadFirstOptionalString(JsonElement element, IReadOnlyList<string> names)
    {
        foreach (var name in names)
        {
            var value = ReadOptionalString(element, name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    static string FormatOptionalBool(bool? value)
    {
        return value.HasValue ? value.Value.ToString() : "<missing>";
    }

    static string FormatOptionalText(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<missing>" : value;
    }

    static string FormatFailureReason(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<none>" : value;
    }
}

internal sealed record CachedClientPatchResultSummaryOptions(
    IReadOnlyList<string> JsonPaths);

internal sealed record CachedClientPatchResultSummaryResult(
    string FileName,
    string ProcessRespondingAfterObserve,
    string MainWindowHandleAfterObserve,
    int WindowEvidenceCount,
    int ExceptionEvidenceCount,
    int RouteEvidenceCount,
    int FinalRouteEvidenceCount,
    int LogEvidenceCount,
    int TcpConnectionCount,
    bool HiddenWerPresent,
    string KnownFallbackRoutesPatched,
    string DelayedRoutePatchCount,
    IReadOnlyList<CachedClientPatchStateSummary> ModulePatchStates);

internal sealed record CachedClientPatchStateSummary(
    string Name,
    string Patched,
    string AlreadyPatched,
    string Before,
    string FailureReason);
