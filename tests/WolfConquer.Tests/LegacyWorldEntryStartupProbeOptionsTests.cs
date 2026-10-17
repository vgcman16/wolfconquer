using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyWorldEntryStartupProbeOptionsTests
{
    [Theory]
    [InlineData(null, LegacyWorldEntryStartupPrefixProbeMode.DecompiledSourceCompatible)]
    [InlineData("", LegacyWorldEntryStartupPrefixProbeMode.DecompiledSourceCompatible)]
    [InlineData("default", LegacyWorldEntryStartupPrefixProbeMode.DecompiledSourceCompatible)]
    [InlineData("decompiled-source", LegacyWorldEntryStartupPrefixProbeMode.DecompiledSourceCompatible)]
    [InlineData("config-before-timer", LegacyWorldEntryStartupPrefixProbeMode.ConfigBeforeTimer)]
    [InlineData("configbeforetimer", LegacyWorldEntryStartupPrefixProbeMode.ConfigBeforeTimer)]
    [InlineData("skip-timer", LegacyWorldEntryStartupPrefixProbeMode.SkipTimer)]
    [InlineData("skiptimer", LegacyWorldEntryStartupPrefixProbeMode.SkipTimer)]
    public void FromEnvironmentParsesStartupPrefixProbeMode(
        string? value,
        LegacyWorldEntryStartupPrefixProbeMode expectedMode)
    {
        var options = LegacyWorldEntryStartupProbeOptions.FromEnvironment(
            name => name == LegacyWorldEntryStartupProbeOptions.PrefixModeEnvironmentVariable
                ? value
                : null);

        Assert.Equal(expectedMode, options.PrefixMode);
    }

    [Fact]
    public void FromEnvironmentIgnoresUnknownStartupPrefixProbeMode()
    {
        var options = LegacyWorldEntryStartupProbeOptions.FromEnvironment(
            name => name == LegacyWorldEntryStartupProbeOptions.PrefixModeEnvironmentVariable
                ? "not-a-mode"
                : null);

        Assert.Equal(LegacyWorldEntryStartupPrefixProbeMode.DecompiledSourceCompatible, options.PrefixMode);
        Assert.True(options.IsDefault);
    }
}
