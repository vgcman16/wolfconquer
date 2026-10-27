using System.Runtime.ExceptionServices;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeStatusPrerequisiteTests
{
    [Theory]
    [InlineData(13052u, 172u, (int)Flag.FullPowerWater)]
    [InlineData(13057u, 172u, (int)Flag.FullPowerWater)]
    [InlineData(14052u, 173u, (int)Flag.FullPowerFire)]
    [InlineData(14057u, 173u, (int)Flag.FullPowerFire)]
    public Task TargetWaterAndFireStagesPublishTheRealFullPowerFlagAndUnchangedIconBytes(
        uint job, uint icon, int rawFlag) => NativePacketTestThread.Run(() =>
        RunTargetTaoistScope(scope =>
        {
            scope.Class(job); scope.Player.TaoistPower = 10;
            var stamp = scope.Player.TaoistStampPower;
            var player = scope.Player;
            var owner = player.Owner;
            var expectedFlag = (Flag)rawFlag;
            var otherFlag = expectedFlag == Flag.FullPowerWater ? Flag.FullPowerFire : Flag.FullPowerWater;

            player.UpdateTaoPower(scope.Packet);

            Assert.True(player.ContainFlag(expectedFlag));
            Assert.False(player.ContainFlag(otherFlag));
            var state = player.BitVector.ArrayFlags[rawFlag];
            Assert.Equal(2592000, state.Secounds);
            Assert.False(state.RemoveOnDead);
            Assert.Equal(new ushort[] { 2176, 2176 }, scope.Queue.Select(bytes => BitConverter.ToUInt16(bytes, 2)));
            Assert.Equal(Icon(17, icon, 10), scope.Queue.Last());
            Assert.Equal((byte)10, player.TaoistPower);
            Assert.Equal(stamp, player.TaoistStampPower);
            Assert.Same(owner, player.Owner);
            Assert.Same(player, owner.Player);
            scope.Queue.Clear(); scope.Packet.InitWriter();
            player.UpdateTaoPower(scope.Packet);
            Assert.Same(state, player.BitVector.ArrayFlags[rawFlag]);
            Assert.Equal(Icon(17, icon, 10), Assert.Single(scope.Queue));
        }));

    private static void RunTargetTaoistScope(Action<Scope> action)
    {
        Scope? scope = null;
        var errors = new List<Exception>();
        try { scope = new(); action(scope); } catch (Exception error) { errors.Add(error); }
        try { scope?.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Target Taoist assertion and owned fixture restore failed.", errors);
    }
}
