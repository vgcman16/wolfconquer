using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacySecondaryPasswordRuntimeTests
{
    [WindowsSecondaryPasswordPersistenceFact]
    public async Task ReadSharedCredentialLeaseAllowsVerificationWithoutAnyFileReplacement()
    {
        const uint uid = 1_297_141;
        Seed(uid, "111,0,0");
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var originalSession = Current(owner, player);
        var before = File.ReadAllBytes(UserPath(uid));
        Assert.False(player.NativeClient.Player.IsCheckedPass);

        using (File.Open(UserPath(uid), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            AssertReply(await Password(owner, player, 1, newPin: 111), 5, 257);
            Assert.True(player.NativeClient.Player.IsCheckedPass);
            Assert.Same(originalSession, Current(owner, player));
            Assert.Equal(before, File.ReadAllBytes(UserPath(uid)));
        }

        Assert.Equal(before, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal((111U, 0U, 0L), ReadCredential(uid));
        Assert.True(LegacySecondaryPasswordRuntime.TryGet(player.Connection, Current(owner, player), out _, out var isChecked));
        Assert.True(isChecked);
    }

    [WindowsSecondaryPasswordPersistenceFact]
    public async Task ReadSharedCredentialLeaseBlocksForgetReplacementButUnlockedRetrySchedulesExactlySevenDays()
    {
        const uint uid = 1_297_151;
        Seed(uid, "111,0,0");
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var originalSession = Current(owner, player);
        var before = File.ReadAllBytes(UserPath(uid));

        // Windows permits the baseline read through this lease but denies the
        // destination replacement because deletion sharing was not granted.
        using (File.Open(UserPath(uid), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            AssertNoSuccess(await Password(owner, player, 2));
            Assert.Same(originalSession, Current(owner, player));
            Assert.False(player.NativeClient.Player.IsCheckedPass);
            Assert.Equal(111U, player.NativeClient.Player.SecurityPassword);
            Assert.Equal(0U, player.NativeClient.Player.OnReset);
            Assert.Equal(DateTime.MinValue, player.NativeClient.Player.ResetSecurityPassowrd);
            Assert.Equal(before, File.ReadAllBytes(UserPath(uid)));
        }
        Assert.Contains("secondaryPasswordWriteFailed", owner.Log.ToString());
        Assert.Equal(before, File.ReadAllBytes(UserPath(uid)));

        AssertReply(await Password(owner, player, 2), 5, 257);

        var deadline = TestNow.DateTime.AddDays(7);
        Assert.False(player.NativeClient.Player.IsCheckedPass);
        Assert.Equal(111U, player.NativeClient.Player.SecurityPassword);
        Assert.Equal(1U, player.NativeClient.Player.OnReset);
        Assert.Equal(deadline.Ticks, player.NativeClient.Player.ResetSecurityPassowrd.Ticks);
        Assert.Equal((111U, 1U, deadline.Ticks), ReadCredential(uid));
        Assert.True(LegacySecondaryPasswordRuntime.TryGet(player.Connection, Current(owner, player), out _, out var isChecked));
        Assert.False(isChecked);
    }

    [WindowsSecondaryPasswordPersistenceFact]
    public async Task ReadSharedCredentialLeaseBlocksPinChangeButUnlockedRetryChangesPinAndClearsReset()
    {
        const uint uid = 1_297_161;
        var deadline = TestNow.DateTime.AddDays(3);
        Seed(uid, $"111,1,{deadline.Ticks}");
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var originalSession = Current(owner, player);
        var before = File.ReadAllBytes(UserPath(uid));

        using (File.Open(UserPath(uid), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            AssertNoSuccess(await Password(owner, player, 4, oldPin: 111, newPin: 222));
            Assert.Same(originalSession, Current(owner, player));
            Assert.False(player.NativeClient.Player.IsCheckedPass);
            Assert.Equal(111U, player.NativeClient.Player.SecurityPassword);
            Assert.Equal(1U, player.NativeClient.Player.OnReset);
            Assert.Equal(deadline.Ticks, player.NativeClient.Player.ResetSecurityPassowrd.Ticks);
            Assert.Equal(before, File.ReadAllBytes(UserPath(uid)));
        }
        Assert.Contains("secondaryPasswordWriteFailed", owner.Log.ToString());
        Assert.Equal(before, File.ReadAllBytes(UserPath(uid)));

        AssertReply(await Password(owner, player, 4, oldPin: 111, newPin: 222), 5, 257);

        Assert.False(player.NativeClient.Player.IsCheckedPass);
        Assert.Equal(222U, player.NativeClient.Player.SecurityPassword);
        Assert.Equal(0U, player.NativeClient.Player.OnReset);
        Assert.Equal(deadline.Ticks, player.NativeClient.Player.ResetSecurityPassowrd.Ticks);
        Assert.Equal((222U, 0U, deadline.Ticks), ReadCredential(uid));
        Assert.True(LegacySecondaryPasswordRuntime.TryGet(player.Connection, Current(owner, player), out _, out var isChecked));
        Assert.False(isChecked);
    }
}
