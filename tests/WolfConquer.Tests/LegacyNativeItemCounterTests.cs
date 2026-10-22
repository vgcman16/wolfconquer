using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// The native field is intentionally shared and unsynchronized. Serializing these tests
// isolates their declared MODEL substitutions; it does NOT add a runtime counter lock.
[CollectionDefinition("NativeItemCounterSourcePrerequisite", DisableParallelization = true)]
public sealed class NativeItemCounterSourcePrerequisiteCollection : ICollectionFixture<NativeCombatGearRegisteredTestContext>
{
}

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeItemCounterTests
{
    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(uint.MaxValue)]
    public void NativeCounterConstructionStoresExactStartWithoutConsumption(uint start)
    {
        var counter = new LegacyNativeCounter(start);

        Assert.Equal(start, counter.Count);
        Assert.Equal(start, counter.Count);
    }

    [Theory]
    [InlineData(0u, 1u, 2u)]
    [InlineData(1u, 2u, 3u)]
    [InlineData(4294967294u, uint.MaxValue, 0u)]
    [InlineData(uint.MaxValue, 0u, 1u)]
    public void NativeCounterNextPreincrementsAndWrapsEvenInsideCheckedCaller(
        uint start, uint first, uint second)
    {
        var counter = new LegacyNativeCounter(start);

        checked
        {
            Assert.Equal(first, counter.Next);
            Assert.Equal(first, counter.Count);
            Assert.Equal(first, counter.Count);
            Assert.Equal(second, counter.Next);
        }

        Assert.Equal(second, counter.Count);
    }

    [Theory]
    [InlineData(0u, 1u, 2u)]
    [InlineData(1u, 2u, 3u)]
    [InlineData(uint.MaxValue, 0u, 1u)]
    public void NativeCounterSetReplacesExactValueWithoutAllocatingOrIncrementing(
        uint replacement, uint first, uint second)
    {
        var counter = new LegacyNativeCounter(47u);
        var held = counter;
        Assert.Equal(48u, counter.Next);

        counter.Set(replacement);

        Assert.Same(held, counter);
        Assert.Equal(replacement, counter.Count);
        Assert.Equal(first, held.Next);
        Assert.Equal(second, counter.Next);
        Assert.Equal(second, held.Count);
    }

    [Fact]
    public void NativeItemPoolFieldIsOneReplaceableSharedReferenceNotPerConsumer()
    {
        WithDeclaredSharedCounter(1u, counter =>
        {
            var held = LegacyNativePoolItems.ItemCounter;
            Assert.Same(counter, held);
            Assert.Equal(1u, held.Count);

            var replacement = new LegacyNativeCounter(900u);
            LegacyNativePoolItems.ItemCounter = replacement;

            Assert.Same(replacement, LegacyNativePoolItems.ItemCounter);
            Assert.NotSame(held, LegacyNativePoolItems.ItemCounter);
            Assert.Equal(901u, LegacyNativePoolItems.ItemCounter.Next);
            Assert.Equal(1u, held.Count);
        });
    }

    [Theory]
    [InlineData(0u, 1u, 2u)]
    [InlineData(2u, 3u, 4u)]
    [InlineData(uint.MaxValue, 0u, 1u)]
    public void NativeItemPoolExplicitInitializationSetsThenDiscardsExactlyOneNext(
        uint persisted, uint afterStartup, uint firstConsumer)
    {
        WithDeclaredSharedCounter(900u, counter =>
        {
            LegacyNativePoolItems.InitializeFromShellItemUid(persisted);

            Assert.Same(counter, LegacyNativePoolItems.ItemCounter);
            Assert.Equal(afterStartup, counter.Count);
            Assert.Equal(afterStartup, LegacyNativePoolItems.CaptureItemUidForShellSave());
            Assert.Equal(firstConsumer, counter.Next);
            Assert.Equal(firstConsumer, LegacyNativePoolItems.CaptureItemUidForShellSave());
        });
    }

    [Fact]
    public void NativeItemPoolRepeatedInitializationUsesNewPersistedSeedNotPreviousMaximum()
    {
        WithDeclaredSharedCounter(900u, counter =>
        {
            LegacyNativePoolItems.InitializeFromShellItemUid(1000u);
            Assert.Equal(1001u, counter.Count);
            Assert.Equal(1002u, counter.Next);

            LegacyNativePoolItems.InitializeFromShellItemUid(2u);

            Assert.Same(counter, LegacyNativePoolItems.ItemCounter);
            Assert.Equal(3u, counter.Count);
            Assert.Equal(4u, counter.Next);
        });
    }

    [Fact]
    public void NativeItemPoolSaveCaptureReadsCountWithoutConsumingNext()
    {
        WithDeclaredSharedCounter(uint.MaxValue, counter =>
        {
            Assert.Equal(uint.MaxValue, LegacyNativePoolItems.CaptureItemUidForShellSave());
            Assert.Equal(uint.MaxValue, LegacyNativePoolItems.CaptureItemUidForShellSave());
            Assert.Equal(uint.MaxValue, counter.Count);
            Assert.Equal(0u, counter.Next);
            Assert.Equal(0u, LegacyNativePoolItems.CaptureItemUidForShellSave());
            Assert.Equal(0u, counter.Count);
        });
    }

    [Fact]
    public void NativeItemPoolSavedCountAndRestartRetainNativeDiscardGap()
    {
        WithDeclaredSharedCounter(1u, counter =>
        {
            LegacyNativePoolItems.InitializeFromShellItemUid(40u);
            Assert.Equal(41u, counter.Count);
            Assert.Equal(42u, counter.Next);
            var persisted = LegacyNativePoolItems.CaptureItemUidForShellSave();
            Assert.Equal(42u, persisted);

            LegacyNativePoolItems.InitializeFromShellItemUid(persisted);

            Assert.Equal(43u, counter.Count);
            Assert.Equal(44u, counter.Next);
        });
    }

    [Fact]
    public void NativeItemPoolTwoDeclaredConsumersAdvanceTheSameCounterInSourceOrder()
    {
        WithDeclaredSharedCounter(1u, counter =>
        {
            Func<uint> firstConsumer = () => LegacyNativePoolItems.ItemCounter.Next;
            Func<uint> secondConsumer = () => LegacyNativePoolItems.ItemCounter.Next;

            Assert.Equal(2u, firstConsumer());
            Assert.Equal(3u, secondConsumer());
            Assert.Equal(4u, firstConsumer());
            Assert.Same(counter, LegacyNativePoolItems.ItemCounter);
            Assert.Equal(4u, LegacyNativePoolItems.CaptureItemUidForShellSave());
        });
    }

    private static void WithDeclaredSharedCounter(uint start, Action<LegacyNativeCounter> body)
    {
        var original = LegacyNativePoolItems.ItemCounter;
        var declared = new LegacyNativeCounter(start);
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
}
