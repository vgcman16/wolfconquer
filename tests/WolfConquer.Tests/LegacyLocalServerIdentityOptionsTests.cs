using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyLocalServerIdentityOptionsTests
{
    [Fact]
    public void ExistingDefaultsHaveNoImplicitLocalServerIdentity()
    {
        Assert.Null(LegacyRuntimeUpdateLoopOptions.Disabled.LocalServerIdentity);
        Assert.Null(LegacyRuntimeUpdateLoopOptions.EnabledDefault.LocalServerIdentity);
        Assert.Null(new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), TimeProvider.System)
            .LocalServerIdentity);
    }

    [Theory]
    [InlineData(0U)]
    [InlineData(1U)]
    [InlineData(65536U)]
    public void RecordCopiesRetainExplicitFullWidthIdentityWithoutChangingOriginalDefaults(uint serverId)
    {
        var identity = new LegacyLocalServerIdentity(serverId);
        var configured = LegacyRuntimeUpdateLoopOptions.Disabled with { LocalServerIdentity = identity };
        var copied = configured with { Enabled = true, TickInterval = TimeSpan.FromSeconds(2) };

        Assert.Same(identity, configured.LocalServerIdentity);
        Assert.Same(identity, copied.LocalServerIdentity);
        Assert.Equal(serverId, copied.LocalServerIdentity!.ServerId);
        Assert.Null(LegacyRuntimeUpdateLoopOptions.Disabled.LocalServerIdentity);
        Assert.False(configured.Enabled);
        Assert.True(copied.Enabled);
    }

    [Fact]
    public void OptionalPropertyDoesNotChangeFiveFieldConstructorOrDeconstruction()
    {
        var options = new LegacyRuntimeUpdateLoopOptions(true, TimeSpan.FromMilliseconds(250), TimeProvider.System)
        { LocalServerIdentity = new(19) };
        var (enabled, interval, provider, legacyClock, environmentClock) = options;
        Assert.True(enabled);
        Assert.Equal(TimeSpan.FromMilliseconds(250), interval);
        Assert.Same(TimeProvider.System, provider);
        Assert.Null(legacyClock);
        Assert.Null(environmentClock);
        Assert.Equal(19U, options.LocalServerIdentity!.ServerId);
    }
}
