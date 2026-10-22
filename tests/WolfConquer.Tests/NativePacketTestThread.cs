namespace WolfConquer.Tests;

// Native Packet rejects constructors deeper than70 physical stack frames. Run
// component tests on a genuine fresh thread instead of xUnit's deep call stack;
// preserve the production guard and propagate every assertion/operation failure.
internal static class NativePacketTestThread
{
    internal static Task Run(Action body) => Task.Factory.StartNew(body,
        CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
}
