using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcSaleTransactionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompleteLoginFailedFinalSendDoesNotPublishAndCanRetry(bool cancelSend)
    {
        const uint uid = 1_299_185;
        Seed(uid);
        var owner = NewOwner();
        using var operationCancellation = new CancellationTokenSource();
        var armed = false;
        var attempts = 0;
        var player = await Login(owner, uid, bytes =>
        {
            if (!armed || !IsCompleteLoginAbility(bytes)) return;
            attempts++;
            if (cancelSend)
            {
                // Only this operation is cancelled. The real login lifetime
                // stays live; this is not proof of transport disconnect cleanup.
                operationCancellation.Cancel();
                throw new OperationCanceledException(operationCancellation.Token);
            }
            throw new IOException("Synthetic final startup send failure.");
        });
        var original = Current(owner, player);
        var native = player.Connection.NativeGameClient!.Player;
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        var first = player.Sent.Count;
        armed = true;

        if (cancelSend)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CompleteLogin(owner, player, operationCancellation.Token));
        else
            await CompleteLogin(owner, player);

        Assert.Equal(1, attempts);
        Assert.False(player.Lifetime.IsCancellationRequested);
        Assert.Same(original, Current(owner, player));
        Assert.False(native.CompleteLogin);
        Assert.False(LegacyCompleteLoginRuntime.IsComplete(player.Connection, Current(owner, player)));
        Assert.False(HasCompleteLoginProjection(Current(owner, player)));
        Assert.DoesNotContain(player.Sent.Skip(first), IsCompleteLoginAbility);
        Assert.DoesNotContain("WorldServer handled MsgAction CompleteLogin", owner.Log.ToString());
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(uid)));
        armed = false;

        await CompleteLogin(owner, player);

        Assert.Equal(1, attempts);
        Assert.True(native.CompleteLogin);
        Assert.True(LegacyCompleteLoginRuntime.IsComplete(player.Connection, Current(owner, player)));
        Assert.True(HasCompleteLoginProjection(Current(owner, player)));
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(uid)));
    }

    [Fact]
    public async Task CompleteLoginFinalSendCannotCompleteReplacementPhysicalPlayerWithSameUid()
    {
        const uint uid = 1_299_186;
        Seed(uid);
        var owner = NewOwner();
        Player? player = null;
        var armed = false;
        Exception? callbackFailure = null;
        LegacyNativePlayerRuntime? replacement = null;
        player = await Login(owner, uid, bytes =>
        {
            try
            {
                if (!armed || !IsCompleteLoginAbility(bytes)) return;
                var client = player!.Connection.NativeGameClient!;
                replacement = new LegacyNativePlayerRuntime(client) { UID = uid };
                client.Player = replacement;
            }
            catch (Exception ex) { callbackFailure ??= ex; }
        });
        var original = Current(owner, player);
        var oldNative = player.Connection.NativeGameClient!.Player;
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        var first = player.Sent.Count;
        armed = true;

        await CompleteLogin(owner, player);

        Assert.Null(callbackFailure);
        Assert.NotNull(replacement);
        Assert.NotSame(oldNative, replacement);
        Assert.Same(replacement, player.Connection.NativeGameClient.Player);
        Assert.Equal(uid, replacement.UID);
        Assert.False(oldNative.CompleteLogin);
        Assert.False(replacement.CompleteLogin);
        Assert.Same(original, Current(owner, player));
        Assert.False(HasCompleteLoginProjection(Current(owner, player)));
        Assert.False(LegacyCompleteLoginRuntime.IsComplete(player.Connection, Current(owner, player)));
        Assert.Single(player.Sent.Skip(first), IsCompleteLoginAbility);
        Assert.DoesNotContain("WorldServer handled MsgAction CompleteLogin", owner.Log.ToString());
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(uid)));
    }

    [Fact]
    public async Task CompleteLoginFinalSendCannotResurrectRemovedCurrentSession()
    {
        const uint uid = 1_299_187;
        Seed(uid);
        var owner = NewOwner();
        Player? player = null;
        var armed = false;
        var removed = false;
        Exception? callbackFailure = null;
        WorldSession? removedSession = null;
        player = await Login(owner, uid, bytes =>
        {
            try
            {
                if (armed && IsCompleteLoginAbility(bytes))
                    removed = owner.Sessions.TryRemove(player!.Connection, out removedSession);
            }
            catch (Exception ex) { callbackFailure ??= ex; }
        });
        var original = Current(owner, player);
        var native = player.Connection.NativeGameClient!.Player;
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        var first = player.Sent.Count;
        armed = true;

        await CompleteLogin(owner, player);

        Assert.Null(callbackFailure);
        Assert.True(removed);
        Assert.Same(original, removedSession);
        Assert.False(owner.Sessions.TryGet(player.Connection, out _));
        Assert.False(native.CompleteLogin);
        Assert.False(HasCompleteLoginProjection(original));
        Assert.False(LegacyCompleteLoginRuntime.IsComplete(player.Connection, original));
        Assert.Single(player.Sent.Skip(first), IsCompleteLoginAbility);
        Assert.DoesNotContain("WorldServer handled MsgAction CompleteLogin", owner.Log.ToString());
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(uid)));
    }
}
