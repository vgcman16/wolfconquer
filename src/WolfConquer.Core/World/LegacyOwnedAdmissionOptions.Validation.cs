namespace WolfConquer.Core.World;

public sealed partial record LegacyOwnedAdmissionOptions
{
    internal bool TryNormalize(out LegacyOwnedAdmissionOptions configured, out string reason)
    {
        configured = this;
        reason = "ownedAdmissionConfigurationInvalid";
        if (ExpectedDomain == Guid.Empty || string.IsNullOrWhiteSpace(HistoricalEvidence) ||
            HistoricalEvidence.Length > 4096 || HistoricalEvidence.IndexOfAny(['\r', '\n', '\0']) >= 0) return false;
        var paths = new[] { UsersRoot, ItemsRoot, SpellsRoot, ProficienciesRoot, QuestsRoot,
            CombatGearRoot, MailboxRoot, RedeemFile, ClaimFile };
        if (paths.Any(path => string.IsNullOrWhiteSpace(path) || path.Length > 4096 ||
            path.IndexOfAny(['\r', '\n', '\0']) >= 0 || !Path.IsPathFullyQualified(path))) return false;
        try
        {
            var full = paths.Select(path => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path))).ToArray();
            configured = this with { UsersRoot = full[0], ItemsRoot = full[1], SpellsRoot = full[2],
                ProficienciesRoot = full[3], QuestsRoot = full[4], CombatGearRoot = full[5],
                MailboxRoot = full[6], RedeemFile = full[7], ClaimFile = full[8] };
            reason = string.Empty;
            return true;
        }
        catch (Exception error) when (IsConfigurationFailure(error)) { return false; }
    }

    internal static bool IsConfigurationFailure(Exception error) => error is IOException or InvalidDataException or
        UnauthorizedAccessException or ArgumentException or NotSupportedException or FormatException or OverflowException or
        InvalidOperationException or System.Text.Json.JsonException or System.Security.SecurityException;
}
