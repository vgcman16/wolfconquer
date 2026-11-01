using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlPhysicalLifecycleTests
{
    private static bool IsOwnedExpiryClear(byte[] bytes) =>
        LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) &&
        packet.Uid == Actor && packet.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.StatusFlag) &&
        !WireFlag(bytes, Actor, Flashing);

    private static async Task AssertExpiryLossEndsActualGlobalWorkerAsync(OwnedEquipmentBehaviorFixture f,
        RetirementClock clock, Task[] admittedWorkers)
    {
        var ended = Task.WhenAny(admittedWorkers);
        var rearmed = clock.WaitForCountAsync(5);
        await Task.WhenAny(ended, rearmed).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(ended.IsCompleted, "Expiry output loss must end the actual global worker. " + f.Log);
        var completed = await ended;
        await completed.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Single(admittedWorkers, worker => worker.IsCompleted);
        Assert.Equal(4, clock.Count); // No global rearm after the failed expiry.
        // RuntimeTicks logs the real SocketException and returns; a faulted Task
        // is not its contract, and no fabricated worker result is accepted.
        Assert.True(completed.IsCompletedSuccessfully, f.Log.ToString());
        Assert.Contains("surface=legacy-global-runtime-update-loop", f.Log.ToString(), StringComparison.Ordinal);
        Assert.Contains("exception=SocketException", f.Log.ToString(), StringComparison.Ordinal);
    }

    private static void AssertExpiryLossOutput(OwnedEquipmentBehaviorFixture f, bool visibleRecipient)
    {
        Assert.Equal(visibleRecipient ? 1 : 0, f.ActorFrames.Count);
        Assert.Empty(f.Frames(Target));
        foreach (var bytes in f.ActorFrames)
        {
            Assert.True(IsOwnedExpiryClear(bytes));
            Assert.Equal(2176, (int)OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode);
        }
        Assert.DoesNotContain(f.ActorFrames, Animation);
        Assert.DoesNotContain(f.Frames(Target), bytes => Hp(bytes, Target));
    }
}
