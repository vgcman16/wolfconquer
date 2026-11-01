using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedActionTests
{
    [Fact]
    public Task StoppedActualRuntimeCannotUseTheUnscheduledDirectionPath() => Run(async f =>
    {
        var current = f.Current; var binding = f.Native.OrdinarySpellBinding!;
        var files = f.Data.Files(); int output = f.Sent.Count;
        await f.Runtime.DisposeAsync();
        Assert.True(f.Runtime.IsStopping); Assert.True(f.Runtime.Recovery.IsBlocked);
        await f.SendAsync(new MsgActionPacket { ObjId = Uid, Type = MsgActionType.ChangeDirection, Facing = 3 }.ToFrameBytes());
        Assert.Same(current, f.Current); Assert.Same(binding, f.Native.OrdinarySpellBinding);
        Assert.False(binding.IsRetired); Assert.False(f.Native.OnAutoAttack); Assert.Null(f.Native.AutoAttack);
        Assert.Equal(output, f.Sent.Count); f.Data.AssertFiles(files);
    });
}
