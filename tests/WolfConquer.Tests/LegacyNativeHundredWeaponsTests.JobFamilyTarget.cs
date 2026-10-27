using System.Runtime.ExceptionServices;
using Weapon = WolfConquer.Core.Data.LegacyNativeMagicType.WeaponsType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHundredWeaponsTests
{
    [Theory]
    [InlineData(1051u, 40, 0, true)]
    [InlineData(1052u, 40, 0, true)] [InlineData(1053u, 40, 0, true)]
    [InlineData(1054u, 40, 0, true)] [InlineData(1055u, 40, 0, true)]
    [InlineData(1056u, 40, 0, true)] [InlineData(1057u, 40, 0, true)]
    [InlineData(1052u, 39, 0, false)] [InlineData(1052u, 1, 1, true)]
    [InlineData(1000u, 140, 1, false)] [InlineData(1058u, 140, 1, false)]
    public Task TargetTrojanStagesKeepActualHundredWeaponLevelRebornAndPerfectionScoreGates(
        uint job, ushort level, byte reborn, bool valid) => NativePacketTestThread.Run(() =>
        RunTargetHundredScope(scope =>
        {
            scope.SetClass(job); scope.SetLevel(level); scope.Client.Player.Reborn = reborn;
            var item = scope.Item(Weapon.Blade);
            var metadata = scope.Meta(Weapon.Blade, 1);
            item.BitVector.Add(1);
            scope.Owner.Unlocked = true;
            var objects = scope.Owner.Objects;
            var attributes = item.Attributes.ToArray();

            uint score = scope.Owner.PerfectionScore;

            Assert.Equal(valid, scope.Owner.Valid());
            Assert.Equal(1000u, item.Score);
            Assert.Equal(1000u, scope.Owner.TotalScore);
            Assert.Equal(valid ? 1000u : 0u, score); //1000/50 +1000 -(1000*2/100).
            Assert.Same(scope.Owner, scope.Client.HundredWeapons);
            Assert.Same(objects, scope.Owner.Objects);
            Assert.Same(item, Assert.Single(objects).Value);
            Assert.Same(metadata, item.DBInfo);
            Assert.Equal(attributes, item.Attributes.ToArray());
            Assert.True(item.BitVector.Contain(1));
            Assert.Empty(scope.Queue);
        }));

    private static void RunTargetHundredScope(Action<Scope> action)
    {
        Scope? scope = null;
        var errors = new List<Exception>();
        try { scope = new(); action(scope); } catch (Exception error) { errors.Add(error); }
        try { scope?.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Target HundredWeapons assertion and owned fixture restore failed.", errors);
    }
}
