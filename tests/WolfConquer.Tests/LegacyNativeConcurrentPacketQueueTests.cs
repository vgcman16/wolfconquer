using System.Reflection;
using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

public sealed class LegacyNativeConcurrentPacketQueueTests
{
    [Fact]
    public void CompleteOwnerKeepsNativePrivateStorageAndPublicSequentialSurface()
    {
        var type = typeof(LegacyNativeConcurrentPacketQueue);
        Assert.False(type.IsPublic || type.IsSealed);
        Assert.False(typeof(IDisposable).IsAssignableFrom(type));
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Equal(new[] { "capacity", "currentPacketSize", "dequeuePointer", "enqueuePointer", "enqueuedData", "queue", "syncRoot", "tqPadding" },
            fields.Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.All(fields, field => Assert.True(field.IsPrivate));
        var capacity = type.GetField("capacity", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.True(capacity.IsLiteral);
        Assert.Equal(typeof(int), capacity.FieldType);
        Assert.Equal(2048, Assert.IsType<int>(capacity.GetRawConstantValue()));
        foreach (var name in new[] { "currentPacketSize", "dequeuePointer", "enqueuePointer", "enqueuedData", "tqPadding" })
            Assert.Equal(typeof(int), type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.FieldType);
        Assert.Equal(typeof(byte[]), type.GetField("queue", BindingFlags.NonPublic | BindingFlags.Instance)!.FieldType);
        Assert.Equal(typeof(object), type.GetField("syncRoot", BindingFlags.NonPublic | BindingFlags.Instance)!.FieldType);
        Assert.Equal(new[] { "CanDequeue", "Dequeue", "Enqueue", "get_CurrentLength" },
            type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(method => method.Name).OrderBy(name => name, StringComparer.Ordinal));
        var property = Assert.Single(type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal("CurrentLength", property.Name);
        Assert.Equal(typeof(int), property.PropertyType);
        Assert.True(property.CanRead); Assert.False(property.CanWrite);
        var review = Assert.Single(type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal("reviewPacketSize", review.Name); Assert.Equal(typeof(void), review.ReturnType);
    }

    [Fact]
    public void ConstructorAndMethodSignaturesRetainRequiredArgumentsAndPaddingEight()
    {
        var type = typeof(LegacyNativeConcurrentPacketQueue);
        var parameter = Assert.Single(Assert.Single(type.GetConstructors()).GetParameters());
        Assert.Equal("padding", parameter.Name); Assert.Equal(typeof(int), parameter.ParameterType);
        Assert.True(parameter.IsOptional); Assert.Equal(8, parameter.DefaultValue);
        var enqueue = type.GetMethod("Enqueue")!;
        Assert.Equal(typeof(void), enqueue.ReturnType);
        Assert.Equal(new[] { typeof(byte[]), typeof(int) }, enqueue.GetParameters().Select(value => value.ParameterType));
        Assert.Equal(new[] { "buffer", "length" }, enqueue.GetParameters().Select(value => value.Name));
        Assert.All(enqueue.GetParameters(), value => Assert.False(value.IsOptional));
        Assert.Equal(typeof(bool), type.GetMethod("CanDequeue")!.ReturnType);
        Assert.Empty(type.GetMethod("CanDequeue")!.GetParameters());
        Assert.Equal(typeof(byte[]), type.GetMethod("Dequeue")!.ReturnType);
        Assert.Empty(type.GetMethod("Dequeue")!.GetParameters());
        var owner = new LegacyNativeConcurrentPacketQueue();
        Assert.Equal(2049, Field<byte[]>(owner, "queue").Length);
        Assert.All(Field<byte[]>(owner, "queue"), value => Assert.Equal((byte)0, value));
        Assert.Equal(0, Field<int>(owner, "enqueuePointer")); Assert.Equal(0, Field<int>(owner, "dequeuePointer"));
        Assert.Equal(0, Field<int>(owner, "enqueuedData")); Assert.Equal(-1, Field<int>(owner, "currentPacketSize"));
        Assert.Equal(8, Field<int>(owner, "tqPadding")); Assert.NotNull(Field<object>(owner, "syncRoot"));
    }

    [Fact]
    public void EmptyReviewRejectsDequeueWithTheExactNativeCancellationMessage()
    {
        var owner = new LegacyNativeConcurrentPacketQueue();
        Assert.Equal(-1, owner.CurrentLength); Assert.False(owner.CanDequeue());
        var fault = Assert.Throws<OperationCanceledException>(() => owner.Dequeue());
        Assert.Equal("Before calling Dequeue(), always call CanDequeue()!", fault.Message);
        Assert.Equal(0, Field<int>(owner, "enqueuedData")); Assert.Equal(0, Field<int>(owner, "dequeuePointer"));
    }

    [Fact]
    public void ZeroAndNegativeEnqueueLengthsDoNotReadEvenANullBufferAndKeepSignedCount()
    {
        var owner = new LegacyNativeConcurrentPacketQueue();
        owner.Enqueue(null!, 0); owner.Enqueue(null!, -3);
        Assert.Equal(-3, Field<int>(owner, "enqueuedData")); Assert.Equal(0, Field<int>(owner, "enqueuePointer"));
        Assert.Equal(-1, owner.CurrentLength); Assert.False(owner.CanDequeue());
        owner.Enqueue(new byte[] { 0, 0 }, 2);
        Assert.Equal(-1, Field<int>(owner, "enqueuedData")); Assert.Equal(2, Field<int>(owner, "enqueuePointer"));
        Assert.Equal(-1, owner.CurrentLength);
    }

    [Fact]
    public void LowPointersOverwriteSlotZeroAndReadTheLatestByteTwiceAsTheHeader()
    {
        var owner = new LegacyNativeConcurrentPacketQueue();
        owner.Enqueue(new byte[] { 1, 2 }, 2);
        Assert.Equal((byte)2, Field<byte[]>(owner, "queue")[0]);
        Assert.Equal((byte)0, Field<byte[]>(owner, "queue")[1]);
        Assert.Equal(522, owner.CurrentLength); Assert.False(owner.CanDequeue());
        Assert.Equal(2, Field<int>(owner, "enqueuedData"));
    }

    [Fact]
    public void AnIncompletePositivePacketRejectsBeforeAllocationOrPointerChanges()
    {
        var owner = new LegacyNativeConcurrentPacketQueue(padding: 0);
        owner.Enqueue(new byte[] { 0, 1 }, 2);
        Assert.False(owner.CanDequeue()); Assert.Equal(257, Field<int>(owner, "currentPacketSize"));
        Assert.Throws<OperationCanceledException>(() => owner.Dequeue());
        Assert.Equal(2, Field<int>(owner, "enqueuedData")); Assert.Equal(0, Field<int>(owner, "dequeuePointer"));
        Assert.Equal(257, Field<int>(owner, "currentPacketSize"));
    }

    [Fact]
    public void DequeueDoesNotRefreshInitialMinusOneButCurrentLengthAloneCanAdmitIt()
    {
        var owner = new LegacyNativeConcurrentPacketQueue(padding: -255);
        owner.Enqueue(new byte[] { 1, 1 }, 2);
        Assert.Equal(-1, Field<int>(owner, "currentPacketSize"));
        Assert.Throws<OperationCanceledException>(() => owner.Dequeue());
        Assert.Equal(2, owner.CurrentLength);
        Assert.Equal(new byte[] { 1, 1 }, owner.Dequeue());
        Assert.Equal(0, Field<int>(owner, "enqueuedData")); Assert.Equal(-1, owner.CurrentLength);
    }

    [Fact]
    public void EnqueueLeavesAnAdmittedSizeStaleAndDequeueUsesItBeforeReReviewing()
    {
        var owner = new LegacyNativeConcurrentPacketQueue(padding: 1);
        owner.Enqueue(new byte[] { 0, 0 }, 2); Assert.Equal(1, owner.CurrentLength);
        owner.Enqueue(new byte[] { 1 }, 1);
        Assert.Equal(1, Field<int>(owner, "currentPacketSize"));
        Assert.Equal(new byte[] { 1 }, owner.Dequeue());
        Assert.Equal(1, Field<int>(owner, "dequeuePointer")); Assert.Equal(2, Field<int>(owner, "enqueuedData"));
        Assert.Equal(258, Field<int>(owner, "currentPacketSize")); Assert.False(owner.CanDequeue());
    }

    [Fact]
    public void ARejectedDequeueDoesNotRefreshTheCachedHeaderAfterAnotherEnqueue()
    {
        var owner = new LegacyNativeConcurrentPacketQueue();
        owner.Enqueue(new byte[] { 0, 0 }, 2); Assert.Equal(8, owner.CurrentLength);
        owner.Enqueue(new byte[] { 9 }, 1);
        Assert.Throws<OperationCanceledException>(() => owner.Dequeue());
        Assert.Equal(8, Field<int>(owner, "currentPacketSize")); Assert.Equal(3, Field<int>(owner, "enqueuedData"));
        Assert.Equal(0, Field<int>(owner, "dequeuePointer")); Assert.Equal(2321, owner.CurrentLength);
    }

    [Fact]
    public void EnqueueOverrunCommitsEarlierBytesAndPointerButNotTheFinalCountAddition()
    {
        var owner = new LegacyNativeConcurrentPacketQueue(padding: 0);
        Assert.Throws<IndexOutOfRangeException>(() => owner.Enqueue(new byte[] { 4 }, 2));
        Assert.Equal((byte)4, Field<byte[]>(owner, "queue")[0]);
        Assert.Equal(1, Field<int>(owner, "enqueuePointer")); Assert.Equal(0, Field<int>(owner, "enqueuedData"));
        Assert.Equal(-1, owner.CurrentLength);
        owner.Enqueue(new byte[] { 0, 0 }, 2);
        Assert.Equal(3, Field<int>(owner, "enqueuePointer")); Assert.Equal(2, Field<int>(owner, "enqueuedData"));
        Assert.Equal(0, owner.CurrentLength); Assert.True(owner.CanDequeue());
    }

    [Fact]
    public void PositiveLengthNullBufferFaultsBeforeAnyBytePointerOrCountCommit()
    {
        var owner = new LegacyNativeConcurrentPacketQueue();
        Assert.Throws<NullReferenceException>(() => owner.Enqueue(null!, 1));
        Assert.Equal((byte)0, Field<byte[]>(owner, "queue")[0]);
        Assert.Equal(0, Field<int>(owner, "enqueuePointer")); Assert.Equal(0, Field<int>(owner, "enqueuedData"));
        Assert.Equal(-1, Field<int>(owner, "currentPacketSize"));
    }

    [Fact]
    public void ACompletePositiveDequeueRepeatsSlotZeroAndReentersTheNativeReviewLock()
    {
        var owner = new LegacyNativeConcurrentPacketQueue(padding: -255);
        owner.Enqueue(new byte[] { 99, 1 }, 2);
        Assert.True(owner.CanDequeue());
        Assert.Equal(new byte[] { 1, 1 }, owner.Dequeue());
        Assert.Equal(2, Field<int>(owner, "dequeuePointer")); Assert.Equal(0, Field<int>(owner, "enqueuedData"));
        Assert.Equal(-1, Field<int>(owner, "currentPacketSize")); Assert.False(owner.CanDequeue());
    }

    [Fact]
    public void ZeroPacketSizeReturnsEmptyWithoutConsumingAndCanRepeatBoundedly()
    {
        var owner = new LegacyNativeConcurrentPacketQueue(padding: 0);
        owner.Enqueue(new byte[] { 0, 0 }, 2); Assert.True(owner.CanDequeue());
        Assert.Empty(owner.Dequeue()); Assert.Empty(owner.Dequeue());
        Assert.Equal(0, Field<int>(owner, "dequeuePointer")); Assert.Equal(2, Field<int>(owner, "enqueuedData"));
        Assert.Equal(0, Field<int>(owner, "currentPacketSize")); Assert.True(owner.CanDequeue());
    }

    [Fact]
    public void PaddingCanProduceTheMinusOneSentinelEvenWithTwoEnqueuedBytes()
    {
        var owner = new LegacyNativeConcurrentPacketQueue(padding: -258);
        owner.Enqueue(new byte[] { 1, 1 }, 2);
        Assert.Equal(-1, owner.CurrentLength); Assert.False(owner.CanDequeue());
        Assert.Throws<OperationCanceledException>(() => owner.Dequeue());
        Assert.Equal(2, Field<int>(owner, "enqueuedData")); Assert.Equal(0, Field<int>(owner, "dequeuePointer"));
    }

    [Fact]
    public void ASizeBelowMinusOnePassesAdmissionThenKeepsTheActualNegativeArrayFault()
    {
        var owner = new LegacyNativeConcurrentPacketQueue(padding: -259);
        owner.Enqueue(new byte[] { 1, 1 }, 2);
        Assert.Equal(-2, owner.CurrentLength); Assert.True(owner.CanDequeue());
        Assert.Throws<OverflowException>(() => owner.Dequeue());
        Assert.Equal(-2, Field<int>(owner, "currentPacketSize"));
        Assert.Equal(2, Field<int>(owner, "enqueuedData")); Assert.Equal(0, Field<int>(owner, "dequeuePointer"));
    }

    [Fact]
    public void TheTwoThousandFortyEighthBoundaryUsesSlot2048RatherThanModulo2048()
    {
        var owner = new LegacyNativeConcurrentPacketQueue(padding: 1277);
        var input = new byte[2049]; input[2047] = 3; input[2048] = 4;
        owner.Enqueue(input, input.Length);
        Assert.Equal((byte)3, Field<byte[]>(owner, "queue")[0]);
        Assert.Equal((byte)4, Field<byte[]>(owner, "queue")[2048]);
        Assert.Equal(2049, Field<int>(owner, "enqueuePointer")); Assert.Equal(2048, owner.CurrentLength);
        Assert.True(owner.CanDequeue());
        var output = owner.Dequeue(); Assert.Equal(2048, output.Length);
        Assert.All(output, value => Assert.Equal((byte)3, value));
        Assert.Equal(2048, Field<int>(owner, "dequeuePointer")); Assert.Equal(1, Field<int>(owner, "enqueuedData"));
        Assert.Equal(-1, Field<int>(owner, "currentPacketSize")); Assert.False(owner.CanDequeue());
    }

    [Fact]
    public void AHeaderCrossingPointer2047ReadsOneByteFromEachNativeMaskBank()
    {
        var owner = new LegacyNativeConcurrentPacketQueue(padding: 1276);
        var input = new byte[2049]; input[2047] = 3; input[2048] = 4;
        owner.Enqueue(input, input.Length); Assert.Equal(2047, owner.CurrentLength);
        var output = owner.Dequeue(); Assert.Equal(2047, output.Length);
        Assert.All(output, value => Assert.Equal((byte)3, value));
        Assert.Equal(2047, Field<int>(owner, "dequeuePointer")); Assert.Equal(2, Field<int>(owner, "enqueuedData"));
        Assert.Equal(2303, Field<int>(owner, "currentPacketSize")); Assert.False(owner.CanDequeue());
    }

    [Fact]
    public void OnlyTheRequestedPrefixIsCopiedAndTheCallerArrayIsNotRetained()
    {
        var owner = new LegacyNativeConcurrentPacketQueue(padding: 0);
        var input = new byte[] { 9, 0, 255 };
        owner.Enqueue(input, 2); input[1] = 7;
        Assert.Equal((byte)0, Field<byte[]>(owner, "queue")[0]);
        Assert.Equal(2, Field<int>(owner, "enqueuePointer")); Assert.Equal(2, Field<int>(owner, "enqueuedData"));
        Assert.Equal(0, owner.CurrentLength);
    }

    [Fact]
    public void NegativeCountCanStarveAnOldAdmissionAndLaterSmallWritesRestoreIt()
    {
        var owner = new LegacyNativeConcurrentPacketQueue(padding: -255);
        owner.Enqueue(new byte[] { 1, 1 }, 2); Assert.True(owner.CanDequeue());
        owner.Enqueue(null!, -3);
        Assert.Equal(-1, Field<int>(owner, "enqueuedData")); Assert.Equal(2, Field<int>(owner, "currentPacketSize"));
        Assert.Throws<OperationCanceledException>(() => owner.Dequeue());
        Assert.Equal(-1, owner.CurrentLength);
        owner.Enqueue(new byte[] { 1, 1, 1 }, 3);
        Assert.Equal(2, Field<int>(owner, "enqueuedData")); Assert.True(owner.CanDequeue());
        Assert.Equal(new byte[] { 1, 1 }, owner.Dequeue()); Assert.False(owner.CanDequeue());
    }

    [Fact]
    public void SeparateOwnersRetainIndependentQueuesLocksPointersAndPadding()
    {
        var first = new LegacyNativeConcurrentPacketQueue(padding: 0);
        var second = new LegacyNativeConcurrentPacketQueue(padding: -255);
        Assert.NotSame(Field<byte[]>(first, "queue"), Field<byte[]>(second, "queue"));
        Assert.NotSame(Field<object>(first, "syncRoot"), Field<object>(second, "syncRoot"));
        first.Enqueue(new byte[] { 8, 9 }, 2); Assert.Equal(2313, first.CurrentLength);
        Assert.Equal((byte)0, Field<byte[]>(second, "queue")[0]); Assert.Equal(-1, second.CurrentLength);
        Assert.Equal(0, Field<int>(second, "enqueuePointer")); Assert.Equal(0, Field<int>(second, "enqueuedData"));
        Assert.Equal(0, Field<int>(first, "tqPadding")); Assert.Equal(-255, Field<int>(second, "tqPadding"));
    }

    private static T Field<T>(LegacyNativeConcurrentPacketQueue owner, string name) =>
        (T)typeof(LegacyNativeConcurrentPacketQueue).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner)!;
}
