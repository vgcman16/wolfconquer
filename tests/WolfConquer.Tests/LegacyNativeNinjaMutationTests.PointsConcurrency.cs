using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaMutationTests
{
    [Fact]
    public Task ConcurrentRealGateCreditsCannotReadOrBorrowAheadAndLoseEitherCredit() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); SeedPointCredit(scope);
            var before = new PointCreditImage(scope);
            var priorFrame = Assert.Single(scope.Queue);
            var gate = PointsGateField.GetValue(scope.Ninja)!;
            using var entered = new CountdownEvent(2);
            var failures = new ConcurrentQueue<Exception>();
            var workers = new[] { PointWorker(3), PointWorker(5) };
            var started = new List<Thread>();
            var available = scope.Available;
            var firstFrame = Convert.FromHexString("34005B09080210001800204A28003000380040004800500058006000680070007800800100880100900100980100A00100A80100");
            var secondFrame = Convert.FromHexString("34005B09080210001800204C28003000380040004800500058006000680070007800800100880100900100980100A00100A80100");
            Exception? phaseFailure = null;
            var phaseBlocked = false;
            var phaseAvailable = -1;
            uint phaseExp = 0;
            Monitor.Enter(gate);
            try
            {
                foreach (var worker in workers) { worker.Start(); started.Add(worker); }
                if (!entered.Wait(TimeSpan.FromSeconds(30)))
                    throw new TimeoutException("Point workers did not enter the bounded readiness phase.");
                if (!SpinWait.SpinUntil(Ready, TimeSpan.FromSeconds(30)))
                    throw new TimeoutException("Point workers did not reach an observable gated/encoded phase.");
                phaseBlocked = BothBlocked();
                phaseAvailable = scope.Available;
                phaseExp = scope.Ninja.Exp;
            }
            catch (Exception error) { phaseFailure = error; }
            finally
            {
                Monitor.Exit(gate);
                // Never dispose packets/events/Scope while an observed worker
                // might still use them, even when readiness/assertion failed.
                foreach (var worker in started) worker.Join();
            }
            var errors = failures.ToList();
            if (phaseFailure is not null) errors.Insert(0, phaseFailure);
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException("Point worker/phase failures retained.", errors);

            Assert.True(phaseBlocked, "Both workers must be observed waiting while the real gate is held.");
            Assert.Equal(71U, phaseExp);
            Assert.Equal(available, phaseAvailable); // No early native loan before the state gate.
            before.RequireNonPointState(79);
            Assert.Equal(3, scope.Queue.Count);
            Assert.Same(priorFrame, scope.Queue.First());
            var notices = scope.Queue.Skip(1).ToArray();
            var finalFrame = Convert.FromHexString("34005B09080210001800204F28003000380040004800500058006000680070007800800100880100900100980100A00100A80100");
            Assert.Single(notices.Where(frame => frame.SequenceEqual(finalFrame)));
            Assert.Single(notices.Where(frame => frame.SequenceEqual(firstFrame) || frame.SequenceEqual(secondFrame)));
            Assert.Empty(scope.Log);

            Thread PointWorker(uint amount) => new(() =>
            {
                try { entered.Signal(); scope.Ninja.AddPoints(amount); }
                catch (Exception error) { failures.Enqueue(error); }
            }) { IsBackground = true, Name = "owned-ninja-points-" + amount };

            bool BothBlocked() => workers.All(worker =>
                (worker.ThreadState & ThreadState.WaitSleepJoin) != 0);

            bool Ready()
            {
                if (!failures.IsEmpty || workers.Any(worker => worker.ThreadState.HasFlag(ThreadState.Stopped)))
                    return true; // A worker error is reported after actual joining.
                if (!BothBlocked()) return false;
                var count = scope.Available;
                if (count == available) return true; // Fixed: waiting before any reads/loans.
                if (count != available - 2) return false;
                // Old source: both real full-capacity frames have been written
                // before its separately locked Exp setter. Read only blocked workers' storage.
                var frames = new[] { NativeSpellTestFixture.Bytes(scope.Used(1)),
                    NativeSpellTestFixture.Bytes(scope.Used(2)) };
                return frames.Any(frame => frame.SequenceEqual(firstFrame)) &&
                    frames.Any(frame => frame.SequenceEqual(secondFrame));
            }
        });
}
