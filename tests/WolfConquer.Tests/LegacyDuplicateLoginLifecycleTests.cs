using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Fact]
    public async Task HandlerRepeatedLegacyLoginOnAcceptedConnectionIsIgnored()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1_293_600, uid: 1_293_600);
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot), log);
        var sessions = GetDuplicateLoginSessionStore(handler);
        var loginFrame = ReadFrame(new LegacyMsgLoginGamePacket(1_293_600).ToFrameBytes());
        var sent = new List<byte[]>();
        var disconnects = 0;
        var connection = CreateDuplicateLoginConnection(
            sent,
            () => Interlocked.Increment(ref disconnects));

        await handler.HandleFrameAsync(connection, loginFrame);
        Assert.True(sessions.TryGet(connection, out var acceptedSession));
        var firstFrameCount = sent.Count;

        await handler.HandleFrameAsync(connection, loginFrame);

        Assert.Equal(firstFrameCount, sent.Count);
        Assert.Equal(0, Volatile.Read(ref disconnects));
        Assert.True(sessions.TryGet(connection, out var currentSession));
        Assert.Same(acceptedSession, currentSession);
        Assert.Contains(
            "WorldServer ignored repeated legacy MsgLoginGame on accepted connection key=1293600",
            log.ToString());
    }

    [Fact]
    public async Task HandlerLegacyDuplicateLoginEvictsOldRejectsSecondAndAllowsThirdRetry()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1_293_600, uid: 1_293_600);
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot), log);
        var sessions = GetDuplicateLoginSessionStore(handler);
        var loginFrame = ReadFrame(new LegacyMsgLoginGamePacket(1_293_600).ToFrameBytes());

        var firstSent = new List<byte[]>();
        var firstDisconnects = 0;
        var removedBeforeDisconnect = false;
        ConquerTcpConnection? firstConnection = null;
        firstConnection = CreateDuplicateLoginConnection(
            firstSent,
            () =>
            {
                removedBeforeDisconnect = !sessions.TryGet(firstConnection!, out _);
                Interlocked.Increment(ref firstDisconnects);
            });

        await handler.HandleFrameAsync(firstConnection, loginFrame);

        Assert.True(sessions.TryGet(firstConnection, out var firstSession));
        Assert.NotNull(firstSession);
        Assert.Contains(firstSent, IsAnswerOk);
        Assert.Contains(firstSent, IsLegacyStartupFrame);

        var secondSent = new List<byte[]>();
        var dialogSentAfterDisconnect = false;
        var secondConnection = CreateDuplicateLoginConnection(
            secondSent,
            onDisconnect: null,
            beforeSend: () => dialogSentAfterDisconnect = Volatile.Read(ref firstDisconnects) == 1);

        await handler.HandleFrameAsync(secondConnection, loginFrame);

        Assert.Equal(1, Volatile.Read(ref firstDisconnects));
        Assert.True(removedBeforeDisconnect);
        Assert.True(dialogSentAfterDisconnect);
        Assert.False(sessions.TryGet(firstConnection, out _));
        Assert.False(sessions.TryGet(secondConnection, out _));
        var duplicateFrame = Assert.Single(secondSent);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(duplicateFrame), out var duplicateDialog));
        Assert.Equal(
            "Your account is already logged in on another computer. Please try again.",
            duplicateDialog.Content[3]);
        Assert.DoesNotContain(secondSent, IsAnswerOk);
        Assert.DoesNotContain(secondSent, IsLegacyStartupFrame);

        var thirdSent = new List<byte[]>();
        var thirdConnection = CreateDuplicateLoginConnection(thirdSent);

        await handler.HandleFrameAsync(thirdConnection, loginFrame);

        Assert.True(sessions.TryGet(thirdConnection, out var thirdSession));
        Assert.NotNull(thirdSession);
        Assert.Contains(thirdSent, IsAnswerOk);
        Assert.Contains(thirdSent, IsLegacyStartupFrame);
        Assert.Contains("WorldServer rejected duplicate legacy MsgLoginGame key=1293600", log.ToString());
    }

    private static ConquerTcpConnection CreateDuplicateLoginConnection(
        List<byte[]> sent,
        Action? onDisconnect = null,
        Action? beforeSend = null)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            new IPEndPoint(IPAddress.Loopback, 9958),
            (frame, _) =>
            {
                beforeSend?.Invoke();
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            },
            _ =>
            {
                onDisconnect?.Invoke();
                return ValueTask.CompletedTask;
            });
    }

    private static bool IsAnswerOk(byte[] bytes)
    {
        return LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Content.Count > 3 &&
            packet.Content[3] == "ANSWER_OK";
    }

    private static bool IsLegacyStartupFrame(byte[] bytes)
    {
        return LegacyServerVersionPacket.TryParse(ReadFrame(bytes), out _);
    }

    private static WorldSessionStore GetDuplicateLoginSessionStore(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField(
            "_sessions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<WorldSessionStore>(field.GetValue(handler));
    }
}
