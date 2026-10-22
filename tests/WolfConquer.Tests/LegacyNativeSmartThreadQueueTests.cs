using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.Runtime;
using Xunit.Abstractions;

namespace WolfConquer.Tests;

public sealed class LegacyNativeSmartThreadQueueTests
{
    private readonly ITestOutputHelper output;

    public LegacyNativeSmartThreadQueueTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void BaseQueueRetainsTheNativeReplaceableFieldsAndAbstractDequeueContract()
    {
        var type = typeof(LegacyNativeConcurrentSmartThreadQueue<>);
        Assert.True(type.IsAbstract);
        Assert.False(type.IsPublic || type.IsSealed);
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Equal(new[] { "Queues", "thread" }, fields.Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.All(fields, field => Assert.False(field.IsInitOnly));
        Assert.Equal(typeof(ConcurrentQueue<>), type.GetField("Queues")!.FieldType.GetGenericTypeDefinition());
        Assert.Equal(typeof(LegacyNativeThreadItem), type.GetField("thread")!.FieldType);
        var constructor = Assert.Single(type.GetConstructors());
        var parameter = Assert.Single(constructor.GetParameters());
        Assert.Equal(typeof(int), parameter.ParameterType);
        Assert.Equal("Processors", parameter.Name);
        Assert.False(parameter.IsOptional);
        var dequeue = type.GetMethod("OnDequeue", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.True(dequeue.IsFamily && dequeue.IsAbstract && dequeue.IsVirtual);
        Assert.Equal(typeof(void), dequeue.ReturnType);
        Assert.Equal(new[] { "Value", "time" }, dequeue.GetParameters().Select(value => value.Name));
        Assert.Equal(type.GetGenericArguments()[0], dequeue.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(int), dequeue.GetParameters()[1].ParameterType);
        Assert.True(type.GetMethod("Enqueue")!.IsVirtual);
        Assert.Equal(new[] { "Enqueue", "Finish", "Start", "Work", "get_Count" },
            type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(method => method.Name).OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void ThreadItemRetainsItsNativeMutableStorageAndCompleteLifecycleSurface()
    {
        var type = typeof(LegacyNativeThreadItem);
        Assert.False(type.IsPublic || type.IsSealed);
        Assert.False(typeof(IDisposable).IsAssignableFrom(type));
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Equal(new[] { "Alive", "Event", "EventSleep", "Max_ProcessInterval", "Name", "ThreadMutex", "_thread" },
            fields.Select(field => field.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.All(fields, field => Assert.False(field.IsInitOnly));
        Assert.Equal(typeof(Action<int>), type.GetField("EventSleep")!.FieldType);
        Assert.All(fields.Where(field => field.Name != "EventSleep"), field => Assert.True(field.IsPrivate));
        var parameters = Assert.Single(type.GetConstructors()).GetParameters();
        Assert.Equal(new[] { typeof(int), typeof(string), typeof(Action), typeof(Action<int>) }, parameters.Select(value => value.ParameterType));
        Assert.Equal(new[] { "interval", "MutexName", "_Event", "Sleep" }, parameters.Select(value => value.Name));
        Assert.All(parameters.Take(3), value => Assert.False(value.IsOptional));
        Assert.True(parameters[3].IsOptional);
        Assert.Null(parameters[3].DefaultValue);
        Assert.Equal(new[] { "Close", "Open", "ThreadProc" },
            type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(method => method.Name).OrderBy(name => name, StringComparer.Ordinal));
        var onProcess = Assert.Single(type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal("OnProcess", onProcess.Name);
        Assert.True(onProcess.IsFamily);
        Assert.Equal(typeof(bool), onProcess.ReturnType);
        Assert.Empty(onProcess.GetParameters());
    }

    [Theory]
    [InlineData("Constructor")]
    [InlineData("EnqueueGateAndNull")]
    [InlineData("FifoAndLiveEnqueue")]
    [InlineData("WorkIgnoresAdmissionGate")]
    [InlineData("CapturedTimestamp")]
    [InlineData("CaughtCallbackFault")]
    [InlineData("EmptyFinish")]
    [InlineData("ThreadConstructor")]
    [InlineData("ProcessGate")]
    [InlineData("ProcessEventThenSleep")]
    [InlineData("ProcessEventFault")]
    [InlineData("ProcessSleepFault")]
    [InlineData("ProcessWithoutEvent")]
    [InlineData("ProcessNoRemainingInterval")]
    [InlineData("ProcessRealSleep")]
    [InlineData("CloseUnopened")]
    public void NativeQueueAndSynchronousThreadPhasesRunOnlyInFreshMeasuredChildren(string scenario)
    {
        var result = LegacyNativeSmartThreadQueueChildFixture.Run(scenario, output);
        Assert.True(result.GetProperty("Completed").GetBoolean());
        Assert.True(result.GetProperty("ConsoleGateRestored").GetBoolean());
        Assert.False(result.GetProperty("WorkerStarted").GetBoolean());
    }
}
