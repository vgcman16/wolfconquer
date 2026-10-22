using System.IO;
using WolfConquer.Updates.Progress;

namespace WolfConquer.Launcher.Tests;

// Pure argument/projection tests. No WPF windows, dispatchers, processes or files.
public sealed class LauncherIntegrationHelpersTests
{
    [Fact]
    public void EmptyArgumentsDoNotInventConfigurationOrPreviewOptions() => Assert.Empty(LauncherArguments.Parse([]));

    [Theory]
    [InlineData("--unknown")]
    [InlineData("positional")]
    [InlineData("--CONFIG")]
    public void UnknownArgumentsAreRefused(string argument) =>
        Assert.Throws<ArgumentException>(() => LauncherArguments.Parse([argument]));

    [Fact]
    public void EveryFlagAndValueOptionRejectsDuplicates()
    {
        foreach (var option in new[] { "--fresh", "--config", "--client-root", "--source-root", "--render-preview" })
        {
            string[] args = option == "--fresh" ? [option, option] : [option, "first value", option, "second value"];
            Assert.Throws<ArgumentException>(() => LauncherArguments.Parse(args));
        }
    }

    [Fact]
    public void ValueOptionsRejectMissingValuesAndDoNotConsumeAnotherOptionAsAValue()
    {
        foreach (var option in new[] { "--config", "--client-root", "--source-root", "--render-preview" })
        {
            Assert.Throws<ArgumentException>(() => LauncherArguments.Parse([option]));
            Assert.Throws<ArgumentException>(() => LauncherArguments.Parse([option, "--fresh"]));
        }
    }

    [Fact]
    public void PreviewAndSourceArgumentsRetainExactSuppliedPathsWithSpaces()
    {
        var root = Path.Combine(Path.GetTempPath(), "wolf helper presentation only");
        var preview = Path.Combine(root, "source preview.png");
        var source = Path.Combine(root, "source checkout");
        var parsed = LauncherArguments.Parse(["--render-preview", preview, "--source-root", source]);
        Assert.Equal(2, parsed.Count);
        Assert.Equal(preview, parsed["--render-preview"]); Assert.Equal(source, parsed["--source-root"]);
        Assert.False(parsed.ContainsKey("--fresh")); Assert.False(parsed.ContainsKey("--config"));
    }

    [Fact]
    public void ClientConfigurationAndRootAreExplicitAndFreshIsABooleanFlag()
    {
        var parsed = LauncherArguments.Parse(["--config", "explicit config.json", "--client-root", "explicit client", "--fresh"]);
        Assert.Equal(3, parsed.Count);
        Assert.Equal("explicit config.json", parsed["--config"]);
        Assert.Equal("explicit client", parsed["--client-root"]); Assert.Equal("true", parsed["--fresh"]);
        Assert.False(parsed.ContainsKey("--render-preview"));
    }

    [Theory]
    [InlineData(UpdateProgressUnit.Bytes)]
    [InlineData(UpdateProgressUnit.Files)]
    public void MapperReportsOnlyTheMeasuredUnitPercentageAndNeverLaunchReadiness(UpdateProgressUnit unit)
    {
        var sink = new Sink(); var mapper = new LauncherProgressMapper(sink);
        mapper.Report(new(UpdateOperationPhase.Downloading, "Measured synthetic transfer.", 3, 12, unit));
        var update = Assert.Single(sink.Updates);
        Assert.Equal(unit == UpdateProgressUnit.Bytes ? (double?)null : 25d, update.StagePercent);
        Assert.Equal(unit == UpdateProgressUnit.Bytes ? 25d : (double?)null, update.BytePercent);
        Assert.Equal(false, update.IsReady);
        Assert.Equal("Measured synthetic transfer.", update.Detail);
        Assert.Equal("Measured synthetic transfer.", update.Activity);
        Assert.Contains(unit == UpdateProgressUnit.Bytes ? "transferred" : "files", update.ByteText!);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, 10L)]
    [InlineData(1L, null)]
    [InlineData(0L, 0L)]
    [InlineData(1L, -1L)]
    [InlineData(-1L, 10L)]
    [InlineData(11L, 10L)]
    [InlineData(long.MaxValue, 1L)]
    public void MissingOrInvalidMeasurementBoundsStayUnknown(long? completed, long? total)
    {
        Assert.Null(LauncherProgressMapper.Percentage(completed, total));
        var sink = new Sink(); var mapper = new LauncherProgressMapper(sink);
        foreach (var unit in new[] { UpdateProgressUnit.Bytes, UpdateProgressUnit.Files })
            mapper.Report(new(UpdateOperationPhase.Verifying, "Unknown measurement.", completed, total, unit));
        Assert.Equal(2, sink.Updates.Count);
        Assert.All(sink.Updates, update =>
        { Assert.Null(update.StagePercent); Assert.Null(update.BytePercent); Assert.Equal(false, update.IsReady); });
    }

    [Theory]
    [InlineData(0L, 10L, 0d)]
    [InlineData(5L, 10L, 50d)]
    [InlineData(10L, 10L, 100d)]
    [InlineData(long.MaxValue, long.MaxValue, 100d)]
    public void ValidMeasuredBoundsIncludeZeroAndHundredWithoutIntegerOverflow(long completed, long total, double expected)
    {
        Assert.Equal(expected, LauncherProgressMapper.Percentage(completed, total));
        var sink = new Sink();
        new LauncherProgressMapper(sink).Report(new(UpdateOperationPhase.Completed, "Measured phase only.", completed, total,
            UpdateProgressUnit.Files));
        Assert.Equal(expected, sink.Updates.Single().StagePercent);
        Assert.Equal(false, sink.Updates.Single().IsReady);
    }

    [Fact]
    public void GitCheckingFetchingAndFastForwardingRemainIndeterminateWithoutInventedCounts()
    {
        var sink = new Sink(); var mapper = new LauncherProgressMapper(sink);
        foreach (var phase in new[] { UpdateOperationPhase.Checking, UpdateOperationPhase.Fetching, UpdateOperationPhase.FastForwarding })
            mapper.Report(new(phase, "Synthetic source phase."));
        Assert.Equal(3, sink.Updates.Count);
        Assert.All(sink.Updates, update =>
        {
            Assert.Null(update.StagePercent); Assert.Null(update.BytePercent);
            Assert.Equal("This step has no measurable percentage.", update.ByteText);
            Assert.Equal(false, update.IsReady);
        });
    }

    [Fact]
    public void OnlyPhaseTransitionsAddActivityWhileEveryMeasurementStillReachesTheSink()
    {
        var sink = new Sink(); var mapper = new LauncherProgressMapper(sink);
        mapper.Report(new(UpdateOperationPhase.Downloading, "Start download.", 0, 10, UpdateProgressUnit.Bytes));
        mapper.Report(new(UpdateOperationPhase.Downloading, "Continue download.", 5, 10, UpdateProgressUnit.Bytes));
        mapper.Report(new(UpdateOperationPhase.Verifying, "Start verification.", 0, 2, UpdateProgressUnit.Files));
        Assert.Equal(3, sink.Updates.Count);
        Assert.Equal("Start download.", sink.Updates[0].Activity);
        Assert.Null(sink.Updates[1].Activity);
        Assert.Equal("Start verification.", sink.Updates[2].Activity);
        Assert.Equal(50d, sink.Updates[1].BytePercent);
    }

    [Fact]
    public void SynchronousThrowingPresentationObserverCannotEscapeMapperOrBlockLaterReports()
    {
        var sink = new Sink(throws: true); var mapper = new LauncherProgressMapper(sink);
        var error = Record.Exception(() =>
        {
            mapper.Report(new(UpdateOperationPhase.Downloading, "First report.", 1, 2, UpdateProgressUnit.Bytes));
            mapper.Report(new(UpdateOperationPhase.Verifying, "Second report.", 1, 1, UpdateProgressUnit.Files));
        });
        Assert.Null(error); Assert.Equal(2, sink.Updates.Count);
        Assert.Equal(100d, sink.Updates[1].StagePercent); Assert.Equal(false, sink.Updates[1].IsReady);
    }

    private sealed class Sink(bool throws = false) : IProgress<LauncherUiUpdate>
    {
        internal List<LauncherUiUpdate> Updates { get; } = [];
        public void Report(LauncherUiUpdate value)
        {
            Updates.Add(value);
            if (throws) throw new InvalidOperationException("Synthetic synchronous presentation failure.");
        }
    }
}
