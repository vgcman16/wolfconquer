using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcSaleTransactionTests
{
    [Theory]
    [InlineData(0U, true)]
    [InlineData(42U, true)]
    [InlineData(65_536U, false)]
    [InlineData(uint.MaxValue, false)]
    public async Task ConfiguredServerIdentityIsAssignedBeforeFirstVersionWithNativeTruncation(uint serverId, bool expectedOwnServer)
    {
        const uint uid = 1_299_191;
        Seed(uid);
        var identity = new LegacyLocalServerIdentity(serverId);
        var owner = NewOwner(identity: identity);
        Player? player = null;
        Exception? callbackFailure = null;
        var beforeVersion = new List<(ushort ServerId, bool IsOwnServer, bool CompleteLogin)>();
        player = CreateConnection(uid, bytes =>
        {
            try
            {
                if (!IsServerIdentityVersion(bytes)) return;
                var current = Current(owner, player!);
                var native = player!.Connection.NativeGameClient!.Player;
                beforeVersion.Add((native.ServerID,
                    LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, current, identity),
                    native.CompleteLogin));
            }
            catch (Exception ex) { callbackFailure ??= ex; }
        });
        Assert.Equal((ushort)0, player.Connection.NativeGameClient!.Player.ServerID);
        // A nonzero preimage proves even ID65536's wrapped zero is assigned,
        // rather than merely observing the constructor's unchanged default.
        player.Connection.NativeGameClient.Player.ServerID = 17;

        await SendLogin(owner, player);

        Assert.Null(callbackFailure);
        Assert.Contains(player.Sent, IsAnswerOk);
        Assert.Single(player.Sent, IsServerIdentityVersion);
        Assert.Equal((unchecked((ushort)serverId), expectedOwnServer, false), Assert.Single(beforeVersion));
        var session = Current(owner, player);
        Assert.Equal(uid, session.Character.Uid);
        Assert.Equal(unchecked((ushort)serverId), player.Connection.NativeGameClient.Player.ServerID);
        Assert.Equal(expectedOwnServer, LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, session, identity));
        Assert.Equal(expectedOwnServer, LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, session,
            new LegacyLocalServerIdentity(serverId)));
        Assert.False(player.Connection.NativeGameClient.Player.CompleteLogin);
    }

    [Fact]
    public async Task MissingConfiguredServerIdentityDoesNotInferAuthorityFromSavedScalars()
    {
        const uint uid = 1_299_192;
        Seed(uid);
        File.AppendAllText(UserPath(uid), "ServerID=42\nMyServerID=42\nOnMyOwnServer=1\n");
        var owner = NewOwner();
        Player? player = null;
        Exception? callbackFailure = null;
        var beforeVersion = new List<(ushort ServerId, bool Unconfigured, bool ZeroIdentity, bool ForgedIdentity)>();
        player = CreateConnection(uid, bytes =>
        {
            try
            {
                if (!IsServerIdentityVersion(bytes)) return;
                var current = Current(owner, player!);
                beforeVersion.Add((player!.Connection.NativeGameClient!.Player.ServerID,
                    LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, current, null),
                    LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, current, new LegacyLocalServerIdentity(0)),
                    LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, current, new LegacyLocalServerIdentity(42))));
            }
            catch (Exception ex) { callbackFailure ??= ex; }
        });

        await SendLogin(owner, player);

        Assert.Null(callbackFailure);
        Assert.Contains(player.Sent, IsAnswerOk);
        Assert.Equal(((ushort)0, false, false, false), Assert.Single(beforeVersion));
        var session = Current(owner, player);
        Assert.Equal("42", session.Character.Attributes["ServerID"]);
        Assert.Equal("42", session.Character.Attributes["MyServerID"]);
        Assert.Equal("1", session.Character.Attributes["OnMyOwnServer"]);
        Assert.Equal((ushort)0, player.Connection.NativeGameClient!.Player.ServerID);
        Assert.False(LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, session, null));
        Assert.False(LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, session, new LegacyLocalServerIdentity(0)));
        Assert.False(LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, session, new LegacyLocalServerIdentity(42)));
    }

    [Fact]
    public async Task ServerIdentityBindingSurvivesLawfulCurrentSessionAndWalletCopies()
    {
        const uint uid = 1_299_193;
        Seed(uid);
        var identity = new LegacyLocalServerIdentity(42);
        var owner = NewOwner(identity: identity);
        var player = await Login(owner, uid);
        var original = Current(owner, player);
        var native = player.Connection.NativeGameClient!.Player;
        Assert.True(LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, original, identity));
        var copy = original with { ActiveNpcId = NpcUid };
        owner.Sessions.Set(player.Connection, copy);
        Assert.Same(copy, Current(owner, player));
        Assert.True(LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, copy, identity));

        var walletChanged = copy with { Character = copy.Character with { Money = 900 } };
        owner.Sessions.Set(player.Connection, walletChanged);

        Assert.Same(walletChanged, Current(owner, player));
        Assert.Same(native, player.Connection.NativeGameClient.Player);
        Assert.Equal((ushort)42, native.ServerID);
        Assert.True(LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, walletChanged, identity));
        Assert.True(LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, walletChanged, new LegacyLocalServerIdentity(42)));
        Assert.False(LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, walletChanged, new LegacyLocalServerIdentity(43)));
        Assert.Equal(NpcUid, walletChanged.ActiveNpcId);
        Assert.Equal(900, CharacterSnapshotParser.ParseText(File.ReadAllText(UserPath(uid))).Money);
        Assert.False(native.CompleteLogin);
    }

    [Fact]
    public async Task ServerIdentityBindingCannotTransferToSameUidReplacementOrForgedNativeField()
    {
        const uint uid = 1_299_194;
        Seed(uid);
        var identity = new LegacyLocalServerIdentity(42);
        var owner = NewOwner(identity: identity);
        var player = await Login(owner, uid);
        var original = Current(owner, player);
        var client = player.Connection.NativeGameClient!;
        var firstNative = client.Player;
        Assert.True(LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, original, identity));
        var replacement = new LegacyNativePlayerRuntime(client) { UID = uid };
        client.Player = replacement;
        Assert.Equal((ushort)0, replacement.ServerID);
        Assert.False(LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, original, identity));

        replacement.ServerID = 42;
        var copy = original with { ActiveNpcId = NpcUid };
        owner.Sessions.Set(player.Connection, copy);

        Assert.NotSame(firstNative, replacement);
        Assert.Same(replacement, client.Player);
        Assert.Equal(uid, replacement.UID);
        Assert.Equal((ushort)42, replacement.ServerID);
        Assert.Equal((ushort)42, firstNative.ServerID);
        Assert.False(LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, copy, identity));
        Assert.False(LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, copy, new LegacyLocalServerIdentity(42)));
        Assert.False(replacement.CompleteLogin);
    }

    [Fact]
    public async Task FirstVersionSendFailureKeepsAlreadyAssignedServerIdentityWithoutCompletingLogin()
    {
        const uint uid = 1_299_195;
        Seed(uid);
        var identity = new LegacyLocalServerIdentity(42);
        var owner = NewOwner(identity: identity);
        Player? player = null;
        Exception? callbackFailure = null;
        var attempts = 0;
        var beforeVersion = new List<(ushort ServerId, bool IsOwnServer, bool CompleteLogin)>();
        player = CreateConnection(uid, bytes =>
        {
            if (!IsServerIdentityVersion(bytes)) return;
            attempts++;
            try
            {
                var current = Current(owner, player!);
                var native = player!.Connection.NativeGameClient!.Player;
                beforeVersion.Add((native.ServerID,
                    LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, current, identity),
                    native.CompleteLogin));
            }
            catch (Exception ex) { callbackFailure ??= ex; }
            throw new IOException("Synthetic first server-version send failure.");
        });

        await SendLogin(owner, player);

        Assert.Null(callbackFailure);
        Assert.Equal(1, attempts);
        Assert.Equal(((ushort)42, true, false), Assert.Single(beforeVersion));
        Assert.Contains(player.Sent, IsAnswerOk);
        Assert.DoesNotContain(player.Sent, IsServerIdentityVersion);
        var session = Current(owner, player);
        Assert.Equal((ushort)42, player.Connection.NativeGameClient!.Player.ServerID);
        Assert.True(LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, session, identity));
        Assert.False(player.Connection.NativeGameClient.Player.CompleteLogin);
        Assert.False(LegacyCompleteLoginRuntime.IsComplete(player.Connection, session));
        Assert.False(HasCompleteLoginProjection(session));
    }

    private static bool IsServerIdentityVersion(byte[] bytes) =>
        LegacyServerVersionPacket.TryParse(ReadFrame(bytes), out _);
}
