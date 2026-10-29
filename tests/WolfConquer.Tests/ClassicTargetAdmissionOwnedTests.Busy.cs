using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyMagicAttackRuntimeTests;

namespace WolfConquer.Tests;

public sealed partial class ClassicTargetAdmissionOwnedTests
{
    [Fact]
    public Task RealMovementContentionRefusesBeforeActualScalarWriterWithDistinctBusyReason() => F.ClassicTargetRunAsync(1075, async (f, monster) =>
    {
        var states = States(f); var actor = f.Current(); var peer = f.Current(Peer); var files = f.Data.Files(); Owner(f);
        Assert.True(states.TryCaptureClassicBuffTarget(monster, actor.Character.MapId, 0,
            actor.Character.X, actor.Character.Y, 18, out var target, out var captureReason), captureReason);
        Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(f.Store, f.Connection(Actor), Actor, out var identity));
        var candidate = actor with { Character = actor.Character with { MinMana = 493,
            Attributes = new Dictionary<string, string>(actor.Character.Attributes, StringComparer.OrdinalIgnoreCase) { ["Stamina"] = "96" } } };
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var movement = Task.Run(() => states.TryMoveTowardTarget(monster, 830, 584, DateTimeOffset.UtcNow,
            TimeSpan.Zero, out _, out _, isCandidateTileValid: (_, _) =>
            {
                entered.TrySetResult();
                if (!release.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException("Real movement test lease was not released.");
                return true;
            }));
        Exception? primary = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(f.Store.TryCommitClassicBuffCosts(f.Connection(Actor), actor, identity, candidate,
                null, target, out var receipt, out var reason));
            Assert.Null(receipt); Assert.Equal("classicTargetBusy", reason); Rejected(f, actor, peer, files);
        }
        catch (Exception error) { primary = error; throw; }
        finally
        {
            release.Set();
            try { Assert.True(await movement.WaitAsync(TimeSpan.FromSeconds(3))); }
            catch (Exception cleanup) when (primary is not null) { throw new AggregateException(primary, cleanup); }
        }
    });
}
