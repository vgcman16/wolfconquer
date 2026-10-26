using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacySecondaryPasswordRuntimeTests
{
    [Fact]
    public async Task NonexpiredStartupDiskMismatchCannotInitializeOrInheritVerification()
    {
        const uint uid = 1_297_141;
        Seed(uid, "111,0,0");
        var owner = NewOwner();
        var left = CreateConnection(uid);
        left.NativeClient.Player.IsCheckedPass = true; // An unissued field must not become authority.
        var nativeBefore = ObservePinAdmission(left);
        var recovery = PinAdmissionOwner<LegacyTradeRecoveryState>(owner, "_tradeRecovery");
        Assert.True(recovery.TryEnsureInitialized(out var reason), reason);
        var operations = PinAdmissionOwner<WorldOperationBarrier>(owner, "_worldOperations");
        var loader = new CharacterSnapshotLoader(UsersRoot, ItemsRoot);
        Dictionary<string, byte[]> changedFiles;

        using (await operations.EnterExclusiveAsync())
        {
            var raw = loader.TryLoadByAccountIdForAdmission(uid);
            Assert.Equal(CharacterSnapshotLoadStatus.Success, raw.Status);
            Assert.True(LegacyOrdinaryAdmissionPlan.TryPrepare(owner.Handler, left.Connection, uid, "owned",
                raw.Snapshot!, loader, null, recovery, owner.Clock, out var prepared, out reason), reason);
            var plan = Assert.IsType<LegacyOrdinaryAdmissionPlan>(prepared);
            Assert.Equal("111,0,0", plan.Baseline.Character.Attributes["SecurityPass"]);
            var before = File.ReadAllText(UserPath(uid));
            var changed = before.Replace("SecurityPass=111,0,0", "SecurityPass=222,0,0", StringComparison.Ordinal);
            Assert.NotEqual(before, changed);
            File.WriteAllText(UserPath(uid), changed);
            changedFiles = CapturePinAdmissionFiles();

            Assert.False(owner.Sessions.TryCommitOrdinaryAdmission(left.Connection, plan, out var admitted, out reason));

            Assert.Null(admitted);
            Assert.Equal("ordinaryAdmissionOwnerChanged", reason);
            Assert.False(owner.Sessions.TryGet(left.Connection, out _));
            Assert.False(LegacySecondaryPasswordRuntime.TryGet(left.Connection, plan.Baseline, out _, out _));
            Assert.False(LegacySecondaryPasswordRuntime.IsAdmitted(left.Connection, plan.Baseline));
            Assert.Equal(nativeBefore, ObservePinAdmission(left));
            AssertPinAdmissionFiles(changedFiles);
            Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
            Assert.False(recovery.IsBlocked); // Clean stale-input refusal is not recovery debt.
            Assert.Empty(left.Sent);
        }

        AssertNoSuccess(await Password(owner, left, 1, newPin: 111));
        AssertNoSuccess(await Password(owner, left, 1, newPin: 222));
        Assert.False(owner.Sessions.TryGet(left.Connection, out _));
        Assert.Equal(nativeBefore, ObservePinAdmission(left));
        AssertPinAdmissionFiles(changedFiles);

        // Retry through the real Frame, not a synthetic accepting Store: the new
        // saved credential gets its own genuine unchecked issuer only at commit.
        await owner.Handler.HandleFrameAsync(left.Connection,
            ReadFrame(new LegacyMsgLoginGamePacket(uid).ToFrameBytes()), left.Lifetime.Token);
        Assert.Contains(left.Sent, IsAnswerOk);
        var accepted = Current(owner, left);
        Assert.Equal("222,0,0", accepted.Character.Attributes["SecurityPass"]);
        Assert.False(left.NativeClient.Player.IsCheckedPass);
        Assert.Equal(222U, left.NativeClient.Player.SecurityPassword);
        Assert.True(LegacySecondaryPasswordRuntime.TryGet(left.Connection, accepted, out var fresh, out var isChecked));
        Assert.Equal(222U, fresh.Password);
        Assert.False(isChecked);
        var afterLogin = CapturePinAdmissionFiles();
        AssertReply(await Password(owner, left, 1, newPin: 111), 6, 0);
        Assert.False(left.NativeClient.Player.IsCheckedPass);
        AssertReply(await Password(owner, left, 1, newPin: 222), 5, 257);
        Assert.True(left.NativeClient.Player.IsCheckedPass);
        AssertPinAdmissionFiles(afterLogin);
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    private static T PinAdmissionOwner<T>(Owner owner, string name)
    {
        var field = typeof(WorldServerFrameHandler).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        return Assert.IsType<T>(field.GetValue(owner.Handler));
    }

    private Dictionary<string, byte[]> CapturePinAdmissionFiles() => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .ToDictionary(path => path, File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);

    private void AssertPinAdmissionFiles(Dictionary<string, byte[]> expected)
    {
        var actual = CapturePinAdmissionFiles();
        Assert.Equal(expected.Keys.Order(StringComparer.OrdinalIgnoreCase), actual.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, actual[path]);
    }

    private static object ObservePinAdmission(Player owner)
    {
        var client = owner.NativeClient;
        var player = client.Player;
        return new
        {
            Client = client, Player = player, player.UID, player.Class, player.ClassExperience, player.Online,
            player.SecurityPassword, player.OnReset, player.ResetSecurityPassowrd, player.IsCheckedPass,
            player.BitVector, player.MyRandom, client.Beasts, client.HairfaceStorage, client.MyNinja,
            client.CombatGear, client.RawEquipmentObservation,
        };
    }
}
