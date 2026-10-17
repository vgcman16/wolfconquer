namespace WolfConquer.Core.Networking;

public enum EndpointProtocolEvidenceClassification
{
    Quiet,
    ServerFirstVariable,
    MissingEvidence,
}

public sealed record EndpointProtocolEvidence(
    string Role,
    string Address,
    int Port,
    EndpointProtocolEvidenceClassification Classification,
    EndpointRouteScope RouteScope,
    bool UnexpectedExternalClientRoute,
    int Captures,
    int Samples,
    IReadOnlyList<int> ByteCounts,
    int UniqueNonEmptyPayloads,
    string StablePrefixHex,
    int ServerFirstBytes,
    string EvidenceSummary);

public static class EndpointProtocolEvidenceReporter
{
    public static IReadOnlyList<EndpointProtocolEvidence> Create(IReadOnlyList<EndpointProbeCapture> captures)
    {
        ArgumentNullException.ThrowIfNull(captures);

        return EndpointProbeAnalyzer.Compare(captures)
            .Select(CreateEvidence)
            .ToArray();
    }

    private static EndpointProtocolEvidence CreateEvidence(EndpointProbeComparison comparison)
    {
        var serverFirstBytes = comparison.ByteCounts
            .Where(byteCount => byteCount > 0)
            .DefaultIfEmpty(0)
            .Max();
        var classification = Classify(comparison, serverFirstBytes);

        return new EndpointProtocolEvidence(
            comparison.Role,
            comparison.Address,
            comparison.Port,
            classification,
            comparison.RouteScope,
            comparison.UnexpectedExternalClientRoute,
            comparison.Captures,
            comparison.Samples,
            comparison.ByteCounts,
            comparison.UniqueNonEmptyPayloads,
            comparison.StablePrefixHex,
            serverFirstBytes,
            CreateSummary(comparison, classification, serverFirstBytes));
    }

    private static EndpointProtocolEvidenceClassification Classify(
        EndpointProbeComparison comparison,
        int serverFirstBytes)
    {
        if (serverFirstBytes == 0)
        {
            return IsQuiet(comparison)
                ? EndpointProtocolEvidenceClassification.Quiet
                : EndpointProtocolEvidenceClassification.MissingEvidence;
        }

        return comparison.UniqueNonEmptyPayloads > 1
            ? EndpointProtocolEvidenceClassification.ServerFirstVariable
            : EndpointProtocolEvidenceClassification.MissingEvidence;
    }

    private static bool IsQuiet(EndpointProbeComparison comparison)
    {
        return comparison.AllConnected
            && comparison.AnyTimedOut
            && !comparison.AnyRemoteClosed
            && comparison.ByteCounts.Count == 1
            && comparison.ByteCounts[0] == 0;
    }

    private static string CreateSummary(
        EndpointProbeComparison comparison,
        EndpointProtocolEvidenceClassification classification,
        int serverFirstBytes)
    {
        var summaryPrefix =
            $"{comparison.Role} routeScope={comparison.RouteScope} " +
            $"unexpectedExternalClientRoute={comparison.UnexpectedExternalClientRoute} " +
            $"captures={comparison.Captures} samples={comparison.Samples} " +
            $"byteCounts={FormatByteCounts(comparison.ByteCounts)} " +
            $"uniqueNonEmptyPayloads={comparison.UniqueNonEmptyPayloads} " +
            $"stablePrefixHex={FormatStablePrefix(comparison.StablePrefixHex)} " +
            $"serverFirstBytes={serverFirstBytes}";

        return classification switch
        {
            EndpointProtocolEvidenceClassification.Quiet => $"{summaryPrefix} is quiet.",
            EndpointProtocolEvidenceClassification.ServerFirstVariable when comparison.StablePrefixHex.Length > 0 =>
                $"{summaryPrefix} is server-first variable with stable prefix.",
            EndpointProtocolEvidenceClassification.ServerFirstVariable =>
                $"{summaryPrefix} is server-first variable {serverFirstBytes} bytes with no stable prefix.",
            _ => $"{summaryPrefix} is missing evidence.",
        };
    }

    private static string FormatByteCounts(IReadOnlyList<int> byteCounts)
    {
        return byteCounts.Count == 0 ? "<none>" : string.Join(",", byteCounts);
    }

    private static string FormatStablePrefix(string stablePrefixHex)
    {
        return stablePrefixHex.Length == 0 ? "<none>" : stablePrefixHex;
    }
}
