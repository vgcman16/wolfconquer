using System.Runtime.ExceptionServices;
using Disdain = WolfConquer.Core.Data.LegacyNativeDisdain;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    [Theory]
    [InlineData(1052u, 0, 1000)] [InlineData(1057u, 0, 1000)]
    [InlineData(4052u, 1, 1400)] [InlineData(4057u, 1, 1400)]
    [InlineData(14052u, 1, 2700)] [InlineData(14057u, 1, 2700)]
    [InlineData(1058u, 0, 600)] [InlineData(4058u, 1, 1800)] [InlineData(14058u, 1, 1800)]
    public Task TargetFamilyDamageUsesTheActualTrojanArcherAndFireFactorsThrough57Only(
        uint job, byte reborn, int expected) => NativePacketTestThread.Run(() =>
        RunTargetDisdainScope(scope =>
        {
            scope.Power(200, 100);
            var target = scope.Peer(200, 100, job, reborn);
            var row = FourGroups();
            Disdain.DisdainRecords.Add(0, row);
            var equipment = target.Owner.Equipment;
            var status = (target.Class, target.Reborn, target.Level, target.HitPoints, scope.Player.HitPoints);

            int damage = Disdain.UserAttackUser(scope.Player, target, 0);

            Assert.Equal(expected, damage); // Existing real factors, fixed independent expected damage.
            Assert.Same(row, Assert.Single(Disdain.DisdainRecords).Value);
            Assert.Same(equipment, target.Owner.Equipment);
            Assert.Equal(status, (target.Class, target.Reborn, target.Level, target.HitPoints, scope.Player.HitPoints));
            Assert.Empty(scope.Queue);
        }));

    private static void RunTargetDisdainScope(Action<Scope> action)
    {
        Scope? scope = null;
        var errors = new List<Exception>();
        try { scope = new(); action(scope); } catch (Exception error) { errors.Add(error); }
        try { scope?.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Target Disdain assertion and owned fixture restore failed.", errors);
    }
}
