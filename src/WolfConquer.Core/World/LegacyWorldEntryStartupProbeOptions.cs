namespace WolfConquer.Core.World;

public enum LegacyWorldEntryStartupPrefixProbeMode
{
    DecompiledSourceCompatible = 0,
    ConfigBeforeTimer = 1,
    SkipTimer = 2,
}

public sealed record LegacyWorldEntryStartupProbeOptions(
    LegacyWorldEntryStartupPrefixProbeMode PrefixMode)
{
    public const string PrefixModeEnvironmentVariable =
        "WOLFCONQUER_WORLD_ENTRY_STARTUP_PREFIX_PROBE";

    public static LegacyWorldEntryStartupProbeOptions Default { get; } =
        new(LegacyWorldEntryStartupPrefixProbeMode.DecompiledSourceCompatible);

    public bool IsDefault => PrefixMode == LegacyWorldEntryStartupPrefixProbeMode.DecompiledSourceCompatible;

    public static LegacyWorldEntryStartupProbeOptions FromEnvironment(
        Func<string, string?>? readEnvironmentVariable = null)
    {
        readEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        var value = readEnvironmentVariable(PrefixModeEnvironmentVariable);
        return TryParsePrefixMode(value, out var mode)
            ? new LegacyWorldEntryStartupProbeOptions(mode)
            : Default;
    }

    private static bool TryParsePrefixMode(
        string? value,
        out LegacyWorldEntryStartupPrefixProbeMode mode)
    {
        mode = LegacyWorldEntryStartupPrefixProbeMode.DecompiledSourceCompatible;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = value.Trim();
        if (string.Equals(normalized, "default", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "decompiled", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "decompiled-source", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "source", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(normalized, "config-before-timer", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "configbeforetimer", StringComparison.OrdinalIgnoreCase))
        {
            mode = LegacyWorldEntryStartupPrefixProbeMode.ConfigBeforeTimer;
            return true;
        }

        if (string.Equals(normalized, "skip-timer", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "skiptimer", StringComparison.OrdinalIgnoreCase))
        {
            mode = LegacyWorldEntryStartupPrefixProbeMode.SkipTimer;
            return true;
        }

        return false;
    }
}
