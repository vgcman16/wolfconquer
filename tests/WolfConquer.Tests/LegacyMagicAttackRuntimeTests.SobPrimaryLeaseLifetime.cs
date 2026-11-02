using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public void WrongThreadCannotRetireTheActualGuildPermissionOwnersLease()
    {
        var manager = new LegacyGuildWarRuntimeStateStore();
        manager.Start();
        Assert.True(manager.TryCapturePermission(out var observation));
        Assert.True(observation!.TryEnter(out var captured));
        var lease = Assert.IsType<LegacyGuildWarRuntimeStateStore.PermissionLease>(captured);
        try
        {
            var error = Task.Run(() => Record.Exception(lease.Dispose)).GetAwaiter().GetResult();
            Assert.IsType<SynchronizationLockException>(error);
            Assert.True(lease.IsCurrentPlain());
            Assert.Throws<InvalidOperationException>(manager.Stop);
        }
        finally { lease.Dispose(); }
        manager.Stop();
        Assert.False(manager.IsActive);
        Assert.True(manager.TryCapturePermission(out var next));
        Assert.True(next!.TryEnter(out var nextLease));
        nextLease!.Dispose();
    }

    [Fact]
    public void WrongThreadCannotRetireTheSobLeaseOrItsAttachedRealGuildOwner()
    {
        var source = CreateEventSobNpc() with { Life = 100, MaxLife = 100 };
        var states = new SobNpcRuntimeStateStore();
        var manager = new LegacyGuildWarRuntimeStateStore();
        manager.Start();
        Assert.True(states.TryCaptureMagicPrimaryTarget(source, out var target));
        Assert.True(target!.TryEnter(out var captured));
        var lease = Assert.IsType<SobNpcRuntimeStateStore.MagicPrimaryLease>(captured);
        try
        {
            Assert.True(manager.TryCapturePermission(out var permission));
            Assert.True(permission!.TryEnter(out var permissionLease));
            lease.AttachGuildPermission(permissionLease!);
            var error = Task.Run(() => Record.Exception(lease.Dispose)).GetAwaiter().GetResult();
            Assert.IsType<SynchronizationLockException>(error);
            Assert.True(lease.IsCurrentPlain());
            Assert.True(permissionLease!.IsCurrentPlain());
            Assert.Throws<InvalidOperationException>(manager.Stop);
        }
        finally { lease.Dispose(); }
        Assert.True(states.TryCaptureMagicPrimaryTarget(source, out var nextTarget));
        Assert.True(nextTarget!.TryEnter(out var nextLease));
        nextLease!.Dispose();
        manager.Stop();
        Assert.False(manager.IsActive);
        Assert.True(manager.TryCapturePermission(out _));
    }
}
