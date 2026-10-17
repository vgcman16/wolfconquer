using System.Buffers;
using System.Net;
using System.Net.Sockets;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyRuntimeErrorTelemetryTests : IDisposable
{
    private readonly string _usersRoot = Path.Combine(Path.GetTempPath(), "wolf-runtime-error-telemetry-" + Guid.NewGuid());

    [Fact]
    public async Task WorldFrameDispatchLogsRuntimeExceptionWhenStartupSendFails()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1_293_600);
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot), log);
        var connection = new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (_, _) => throw new InvalidOperationException("send exploded"));

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(1_293_600).ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("PARITY-GAP category=runtime severity=error gap=runtime-exception", output);
        Assert.Contains("dedupe=runtime:runtime-exception:exceptionType=InvalidOperationException:opcode=2065:sourceSeam=WorldServerFrameHandler.Dispatch:surface=world-frame-dispatch", output);
        Assert.Contains("nextSeam=inspect-runtime-exception", output);
        Assert.Contains("payloadLength=4", output);
        Assert.Contains("sourceSeam=WorldServerFrameHandler.Dispatch", output);
        Assert.Contains("PARITY-BUG world runtime exception", output);
        Assert.Contains("opcode=2065 payloadLength=4", output);
        Assert.Contains("exception=InvalidOperationException: send exploded", output);
    }

    [Fact]
    public async Task WorldFrameDispatchTreatsPeerDisconnectDuringStartupSendAsNormalClose()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1_293_600);
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot), log);
        var connection = new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (_, _) => throw CreatePeerDisconnectIOException());

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(1_293_600).ToFrameBytes()));

        var output = log.ToString();
        Assert.DoesNotContain("PARITY-GAP category=runtime", output);
        Assert.DoesNotContain("PARITY-BUG world runtime exception", output);
    }

    public void Dispose()
    {
        if (Directory.Exists(_usersRoot))
        {
            Directory.Delete(_usersRoot, recursive: true);
        }
    }

    private void WriteSnapshot(long accountId)
    {
        File.WriteAllText(
            Path.Combine(_usersRoot, accountId + ".ini"),
            """
            [Character]
            UID=1293600
            Body=1008
            Face=154
            Name=HeroOne
            Class=7051
            Map=1002
            X=430
            Y=377
            FirstClass=0
            SecoundeClass=7050
            Conquer=0
            Reborn=2
            Level=140
            Haire=27
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=36
            Strength=0
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            ConquerPoints=639778904
            BankCps=123
            BoundConquerPoints=456
            Money=98602439956
            PkPoints=0
            Online=7

            [PaidNobility]
            Rank=King
            Identifier=1293600
            """);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static IOException CreatePeerDisconnectIOException()
    {
        return new IOException(
            "Unable to write data to the transport connection.",
            new SocketException((int)SocketError.ConnectionAborted));
    }
}
