using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Same nonparallel MODEL substitution boundary as the counter prerequisite tests.
// No physical shell.ini, running host, socket client, or global Pool birth is claimed.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeItemCounterShellLifecycleTests
{
    [Theory]
    [InlineData(0u, 1u, 2u)]
    [InlineData(40u, 41u, 42u)]
    [InlineData(uint.MaxValue, 0u, 1u)]
    public void InitializeReadsNativeSectionAndKeyThenDiscardsExactlyOneNext(
        uint persisted, uint afterStartup, uint firstConsumer)
    {
        WithDeclaredCounter(new LegacyNativeCounter(900u), counter =>
        {
            var calls = new List<string>();
            var ini = new DeclaredShellIni
            {
                Read = (section, key) =>
                {
                    calls.Add(section + "/" + key);
                    return persisted;
                },
            };

            LegacyNativeItemCounterShellLifecycle.InitializeAtNativeShellPhase(ini);

            Assert.Equal(new[] { "Database/ItemUID" }, calls);
            Assert.Equal(afterStartup, counter.Count);
            Assert.Equal(firstConsumer, counter.Next);
        });
    }

    [Fact]
    public void InitializeCapturesSetReceiverBeforeReadAndRereadsFieldForDiscardNext()
    {
        WithDeclaredCounter(new LegacyNativeCounter(100u), original =>
        {
            var replacement = new LegacyNativeCounter(900u);
            var ini = new DeclaredShellIni
            {
                Read = (_, _) =>
                {
                    LegacyNativePoolItems.ItemCounter = replacement;
                    return 40u;
                },
            };

            LegacyNativeItemCounterShellLifecycle.InitializeAtNativeShellPhase(ini);

            Assert.Equal(40u, original.Count);
            Assert.Same(replacement, LegacyNativePoolItems.ItemCounter);
            Assert.Equal(901u, replacement.Count);
            Assert.Equal(902u, replacement.Next);
        });
    }

    [Fact]
    public void InitializeReadFailurePropagatesWithoutSetOrDiscard()
    {
        WithDeclaredCounter(new LegacyNativeCounter(100u), counter =>
        {
            var expected = new InvalidOperationException("declared read failure");
            var ini = new DeclaredShellIni { Read = (_, _) => throw expected };

            var actual = Assert.Throws<InvalidOperationException>(
                () => LegacyNativeItemCounterShellLifecycle.InitializeAtNativeShellPhase(ini));

            Assert.Same(expected, actual);
            Assert.Same(counter, LegacyNativePoolItems.ItemCounter);
            Assert.Equal(100u, counter.Count);
        });
    }

    [Fact]
    public void InitializeReadReplacementSideEffectRemainsWhenReadThrows()
    {
        WithDeclaredCounter(new LegacyNativeCounter(100u), original =>
        {
            var replacement = new LegacyNativeCounter(900u);
            var expected = new InvalidOperationException("declared read failure");
            var ini = new DeclaredShellIni
            {
                Read = (_, _) =>
                {
                    LegacyNativePoolItems.ItemCounter = replacement;
                    throw expected;
                },
            };

            var actual = Assert.Throws<InvalidOperationException>(
                () => LegacyNativeItemCounterShellLifecycle.InitializeAtNativeShellPhase(ini));

            Assert.Same(expected, actual);
            Assert.Equal(100u, original.Count);
            Assert.Same(replacement, LegacyNativePoolItems.ItemCounter);
            Assert.Equal(900u, replacement.Count);
        });
    }

    [Fact]
    public void InitializeNullSetReceiverStillEvaluatesReadBeforeThrowing()
    {
        WithDeclaredCounter(new LegacyNativeCounter(100u), _ =>
        {
            var replacement = new LegacyNativeCounter(900u);
            LegacyNativePoolItems.ItemCounter = null!;
            var readCalls = 0;
            var ini = new DeclaredShellIni
            {
                Read = (_, _) =>
                {
                    readCalls++;
                    LegacyNativePoolItems.ItemCounter = replacement;
                    return 40u;
                },
            };

            Assert.Throws<NullReferenceException>(
                () => LegacyNativeItemCounterShellLifecycle.InitializeAtNativeShellPhase(ini));

            Assert.Equal(1, readCalls);
            Assert.Same(replacement, LegacyNativePoolItems.ItemCounter);
            Assert.Equal(900u, replacement.Count);
        });
    }

    [Fact]
    public void InitializeNullReplacementFailsAtDiscardAfterSettingOriginalReceiver()
    {
        WithDeclaredCounter(new LegacyNativeCounter(100u), original =>
        {
            var ini = new DeclaredShellIni
            {
                Read = (_, _) =>
                {
                    LegacyNativePoolItems.ItemCounter = null!;
                    return 40u;
                },
            };

            Assert.Throws<NullReferenceException>(
                () => LegacyNativeItemCounterShellLifecycle.InitializeAtNativeShellPhase(ini));

            Assert.Equal(40u, original.Count);
            Assert.Null(LegacyNativePoolItems.ItemCounter);
        });
    }

    [Fact]
    public void SaveNotFullyLoadedDoesNotUseAnyDependencyOrReadCounter()
    {
        WithDeclaredCounter(new LegacyNativeCounter(100u), _ =>
        {
            LegacyNativePoolItems.ItemCounter = null!;

            LegacyNativeItemCounterShellLifecycle.SaveAtNativeShellPhase(
                false, null!, null!, null!, null!);

            Assert.Null(LegacyNativePoolItems.ItemCounter);
        });
    }

    [Fact]
    public void SaveRunsPrefixThenFactoryThenNativeItemWriteThenTail()
    {
        WithDeclaredCounter(new LegacyNativeCounter(100u), counter =>
        {
            var events = new List<string>();
            var ini = new DeclaredShellIni
            {
                Write = (section, key, value) =>
                    events.Add("write:" + section + "/" + key + "=" + value),
            };
            var errors = new List<Exception>();

            LegacyNativeItemCounterShellLifecycle.SaveAtNativeShellPhase(
                true,
                () => events.Add("prefix"),
                () =>
                {
                    events.Add("factory");
                    return ini;
                },
                actualIni =>
                {
                    Assert.Same(ini, actualIni);
                    events.Add("tail");
                },
                errors.Add);

            Assert.Equal(new[] { "prefix", "factory", "write:Database/ItemUID=100", "tail" }, events);
            Assert.Empty(errors);
            Assert.Equal(100u, counter.Count);
        });
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(17u)]
    [InlineData(uint.MaxValue)]
    public void SaveUsesCountWithoutConsumingNext(uint count)
    {
        WithDeclaredCounter(new LegacyNativeCounter(count), counter =>
        {
            var values = new List<uint>();
            var ini = new DeclaredShellIni { Write = (_, _, value) => values.Add(value) };
            var errors = new List<Exception>();

            LegacyNativeItemCounterShellLifecycle.SaveAtNativeShellPhase(
                true, () => { }, () => ini, _ => { }, errors.Add);
            LegacyNativeItemCounterShellLifecycle.SaveAtNativeShellPhase(
                true, () => { }, () => ini, _ => { }, errors.Add);

            Assert.Equal(new[] { count, count }, values);
            Assert.Equal(count, counter.Count);
            Assert.Equal(unchecked(count + 1u), counter.Next);
            Assert.Empty(errors);
        });
    }

    [Fact]
    public void SaveFactoryReplacementIsObservedByLaterCountRead()
    {
        WithDeclaredCounter(new LegacyNativeCounter(100u), original =>
        {
            var replacement = new LegacyNativeCounter(900u);
            var values = new List<uint>();
            var ini = new DeclaredShellIni { Write = (_, _, value) => values.Add(value) };

            LegacyNativeItemCounterShellLifecycle.SaveAtNativeShellPhase(
                true,
                () => { },
                () =>
                {
                    LegacyNativePoolItems.ItemCounter = replacement;
                    return ini;
                },
                _ => { },
                exception => throw exception);

            Assert.Equal(new[] { 900u }, values);
            Assert.Equal(100u, original.Count);
            Assert.Equal(900u, replacement.Count);
        });
    }

    [Fact]
    public void SaveWriteReceivesCapturedCountBeforeItsOwnReplacementSideEffect()
    {
        WithDeclaredCounter(new LegacyNativeCounter(100u), original =>
        {
            var replacement = new LegacyNativeCounter(900u);
            var values = new List<uint>();
            var ini = new DeclaredShellIni
            {
                Write = (_, _, value) =>
                {
                    LegacyNativePoolItems.ItemCounter = replacement;
                    values.Add(value);
                },
            };

            LegacyNativeItemCounterShellLifecycle.SaveAtNativeShellPhase(
                true, () => { }, () => ini, _ => { }, exception => throw exception);

            Assert.Equal(new[] { 100u }, values);
            Assert.Same(replacement, LegacyNativePoolItems.ItemCounter);
            Assert.Equal(100u, original.Count);
            Assert.Equal(900u, replacement.Count);
        });
    }

    [Fact]
    public void SavePrefixExceptionReportsOnceAndSkipsFactoryWriteAndTail()
    {
        AssertSaveFailureStopsAfter("prefix", new[] { "prefix", "report" });
    }

    [Fact]
    public void SaveFactoryExceptionReportsOnceAndSkipsWriteAndTail()
    {
        AssertSaveFailureStopsAfter("factory", new[] { "prefix", "factory", "report" });
    }

    [Fact]
    public void SaveWriteExceptionReportsOnceAndSkipsTail()
    {
        AssertSaveFailureStopsAfter("write", new[] { "prefix", "factory", "write", "report" });
    }

    [Fact]
    public void SaveTailExceptionReportsOnceAfterItemWrite()
    {
        AssertSaveFailureStopsAfter("tail", new[] { "prefix", "factory", "write", "tail", "report" });
    }

    [Fact]
    public void SaveNullCounterFailsAfterFactoryAndIsReportedWithoutWriteOrTail()
    {
        WithDeclaredCounter(new LegacyNativeCounter(100u), _ =>
        {
            var events = new List<string>();
            var errors = new List<Exception>();
            var ini = new DeclaredShellIni { Write = (_, _, _) => events.Add("write") };

            LegacyNativeItemCounterShellLifecycle.SaveAtNativeShellPhase(
                true,
                () => events.Add("prefix"),
                () =>
                {
                    events.Add("factory");
                    LegacyNativePoolItems.ItemCounter = null!;
                    return ini;
                },
                _ => events.Add("tail"),
                exception =>
                {
                    errors.Add(exception);
                    events.Add("report");
                });

            Assert.Equal(new[] { "prefix", "factory", "report" }, events);
            Assert.Single(errors);
            Assert.IsType<NullReferenceException>(errors[0]);
        });
    }

    [Fact]
    public void SaveReporterFailurePropagatesWithoutSecondReportAttempt()
    {
        WithDeclaredCounter(new LegacyNativeCounter(100u), _ =>
        {
            var saveError = new InvalidOperationException("declared prefix failure");
            var reportError = new ApplicationException("declared reporter failure");
            var reports = 0;

            var actual = Assert.Throws<ApplicationException>(() =>
                LegacyNativeItemCounterShellLifecycle.SaveAtNativeShellPhase(
                    true,
                    () => throw saveError,
                    () => throw new InvalidOperationException("unexpected factory"),
                    _ => throw new InvalidOperationException("unexpected tail"),
                    exception =>
                    {
                        reports++;
                        Assert.Same(saveError, exception);
                        throw reportError;
                    }));

            Assert.Same(reportError, actual);
            Assert.Equal(1, reports);
        });
    }

    [Fact]
    public void SavePrefixMayContainIndividuallyHandledFailureWithoutAbortingLaterPhases()
    {
        WithDeclaredCounter(new LegacyNativeCounter(100u), _ =>
        {
            var events = new List<string>();
            var ini = new DeclaredShellIni { Write = (_, _, _) => events.Add("write") };
            var outerErrors = new List<Exception>();

            LegacyNativeItemCounterShellLifecycle.SaveAtNativeShellPhase(
                true,
                () =>
                {
                    try
                    {
                        throw new InvalidOperationException("declared wrapped owner failure");
                    }
                    catch (InvalidOperationException)
                    {
                        events.Add("prefix-owner-report");
                    }

                    events.Add("prefix-next-owner");
                },
                () => ini,
                _ => events.Add("tail"),
                outerErrors.Add);

            Assert.Equal(new[] { "prefix-owner-report", "prefix-next-owner", "write", "tail" }, events);
            Assert.Empty(outerErrors);
        });
    }

    private static void AssertSaveFailureStopsAfter(string failingPhase, string[] expectedEvents)
    {
        WithDeclaredCounter(new LegacyNativeCounter(100u), counter =>
        {
            var events = new List<string>();
            var errors = new List<Exception>();
            var expected = new InvalidOperationException("declared " + failingPhase + " failure");
            void Phase(string name)
            {
                events.Add(name);
                if (name == failingPhase)
                {
                    throw expected;
                }
            }

            var ini = new DeclaredShellIni { Write = (_, _, _) => Phase("write") };
            LegacyNativeItemCounterShellLifecycle.SaveAtNativeShellPhase(
                true,
                () => Phase("prefix"),
                () =>
                {
                    Phase("factory");
                    return ini;
                },
                _ => Phase("tail"),
                exception =>
                {
                    errors.Add(exception);
                    events.Add("report");
                });

            Assert.Equal(expectedEvents, events);
            Assert.Single(errors);
            Assert.Same(expected, errors[0]);
            Assert.Equal(100u, counter.Count);
        });
    }

    private static void WithDeclaredCounter(LegacyNativeCounter declared, Action<LegacyNativeCounter> body)
    {
        var original = LegacyNativePoolItems.ItemCounter;
        LegacyNativePoolItems.ItemCounter = declared;
        try
        {
            body(declared);
        }
        finally
        {
            LegacyNativePoolItems.ItemCounter = original;
        }
    }

    private sealed class DeclaredShellIni : ILegacyNativeItemCounterShellIni
    {
        public Func<string, string, uint> Read { get; init; } =
            (_, _) => throw new InvalidOperationException("Unexpected MODEL read.");

        public Action<string, string, uint> Write { get; init; } =
            (_, _, _) => throw new InvalidOperationException("Unexpected MODEL write.");

        public uint ReadUInt32(string section, string key) => Read(section, key);

        public void WriteUInt32(string section, string key, uint value) => Write(section, key, value);
    }
}
