using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeClientItemSaveTests
{
    [Theory]
    [InlineData("uid")] [InlineData("player")] [InlineData("configuration")]
    public void RealSharedFileGateCannotPublishACapturedPlayerPathAfterIdentityRetirement(string retirement)
    {
        using var owned = SeededSave(); var client = ClientWithNativeItemOwners();
        var capturedPlayer = client.Player; byte[] before = File.ReadAllBytes(owned.FilePath);
        LegacyNativeClientItemSave.Result? result = null; Exception? error = null;
        using var started = new ManualResetEventSlim();
        var worker = new Thread(() =>
        {
            started.Set();
            try { result = LegacyNativeClientItemSave.TrySaveClientItems(client); }
            catch (Exception exception) { error = exception; }
        }) { IsBackground = true };
        try
        {
            SnapshotFileLockCoordinator.ExecuteLocked(owned.FilePath, () =>
            {
                worker.Start();
                Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
                Assert.True(SpinWait.SpinUntil(() =>
                    (worker.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0 &&
                    GateHeldElsewhere(client.SpellAdmissionSyncRoot) &&
                    GateHeldElsewhere(capturedPlayer.SpellAdmissionSyncRoot), TimeSpan.FromSeconds(5)));
                // The actual save has both physical gates and is blocked by this genuine file
                // coordinator after capturing its location. No saver/provider hook is injected.
                switch (retirement)
                {
                    case "uid": capturedPlayer.UID++; break;
                    case "player": client.Player = new(client) { UID = capturedPlayer.UID }; break;
                    case "configuration": LegacyNativeServerConfig.DbLocation =
                        Path.Combine(LegacyNativeServerConfig.DbLocation, "changed-root"); break;
                }
                return true;
            });
        }
        finally
        {
            if ((worker.ThreadState & System.Threading.ThreadState.Unstarted) == 0)
                Assert.True(worker.Join(TimeSpan.FromSeconds(10)));
        }
        Assert.Null(error);
        var completed = result ?? throw new InvalidOperationException("Owned save worker returned no result.");
        Assert.Equal(LegacyNativeClientItemSave.Outcome.Rejected, completed.Status);
        Assert.Equal(before, File.ReadAllBytes(owned.FilePath));
        Assert.Equal(new[] { owned.FilePath }, Directory.GetFiles(owned.PlayersItemsDirectory));
        if (retirement == "uid") Assert.Equal(1293601u, capturedPlayer.UID);
        if (retirement == "player") Assert.NotSame(capturedPlayer, client.Player);
    }

    private static bool GateHeldElsewhere(object gate)
    {
        if (!Monitor.TryEnter(gate)) return true;
        Monitor.Exit(gate);
        return false;
    }
}
