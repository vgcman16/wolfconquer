using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    private static readonly DateTime ExpiredAdmissionPinDeadline = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    private static byte[] WriteAdmissionPinExpiryInput(LoginInput input, DateTime deadline, bool malformedHairface = false)
    {
        PrepareAdmissionInput(input);
        var text = Encoding.UTF8.GetString(input.InitialBytes).Replace("RelicSpirit=True\r\n",
            "RelicSpirit=True\r\nSecurityPass=111,1," + deadline.Ticks.ToString(CultureInfo.InvariantCulture) + ",\r\n" +
            (malformedHairface ? "HairfaceStorage=1/4201/0/-1/\r\n" : ""), StringComparison.Ordinal);
        var bytes = Encoding.UTF8.GetBytes(text);
        File.WriteAllBytes(input.FilePath, bytes);
        return bytes;
    }

    private static byte[] ExpectedAdmissionPinExpiryBytes(byte[] before, DateTime deadline, bool expired)
    {
        var text = Encoding.UTF8.GetString(before).Replace("Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal);
        if (expired) text = text.Replace("SecurityPass=111,1," + deadline.Ticks.ToString(CultureInfo.InvariantCulture) + ",",
            "SecurityPass=0,0," + deadline.Ticks.ToString(CultureInfo.InvariantCulture) + ",", StringComparison.Ordinal);
        return Encoding.UTF8.GetBytes(text);
    }

    private static async Task WithAdmissionPinExpiryHandler(LoginInput input,
        Func<NativePlayerLoopback, WorldServerFrameHandler, AdmissionPinExpiryLog, Task> body)
    {
        using var metadata = new NativeLoginBeastMetadataScope(1000);
        using var log = new AdmissionPinExpiryLog();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(input.Users), null, worldData: null,
            runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled, log: log,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        await NativePlayerLoopback.RunAsync(ConquerTcpWireMode.LegacyGameServer,
            fixture => body(fixture, handler, log), handler.HandleFrameAsync,
            handler.ReadCurrentSessionForNativeOwnerObservation);
        log.AssertNoWorldRuntimeDiagnostics();
    }

    private static async Task<(NativePlayerWireClient Wire, NativePlayerDispatch Birth)> ConnectAdmissionPinWire(NativePlayerLoopback fixture)
    {
        var wire = await fixture.ConnectGameAsync(false, captureOutput: true);
        var birth = await fixture.NextAsync();
        Assert.Equal(NativePlayerLoopback.ProbeOpcode, birth.Opcode);
        Assert.Null(birth.Before); Assert.Null(birth.After);
        Assert.Equal(0U, birth.Connection.NativeGameClient!.Player.UID);
        return (wire, birth);
    }

    private static (uint Password, uint Reset, DateTime Deadline, bool Checked) AdmissionPinFields(LegacyNativePlayerRuntime player) =>
        (player.SecurityPassword, player.OnReset, player.ResetSecurityPassowrd, player.IsCheckedPass);

    private static void AssertAdmissionPinBinding(ConquerTcpConnection connection, WorldSession session,
        uint password, uint reset, DateTime deadline, bool verified)
    {
        Assert.True(LegacySecondaryPasswordRuntime.TryGet(connection, session, out var state, out var isChecked));
        Assert.Equal(password, state.Password); Assert.Equal(reset, state.OnReset);
        Assert.Equal(deadline, state.ResetAt); Assert.Equal(verified, isChecked);
        Assert.Equal(verified, LegacySecondaryPasswordRuntime.IsAdmitted(connection, session));
    }

    private static void AssertNoAdmissionAnswerOk(byte[][] frames) =>
        Assert.DoesNotContain(frames, frame => frame.SequenceEqual(LegacyMsgMessagePacket.AnswerOk().ToFrameBytes()));

    private static void AssertOneAdmissionAnswerOk(byte[][] frames) =>
        Assert.Single(frames, frame => frame.SequenceEqual(LegacyMsgMessagePacket.AnswerOk().ToFrameBytes()));

    private static byte[][] AdmissionPinFrames(byte[][] frames) => frames.Where(frame =>
        PacketHeader.TryRead(frame, out var header) && header.Opcode == LegacySecondaryPasswordPacket.Opcode).ToArray();

    private static async Task SendAdmissionPinPacket(NativePlayerLoopback fixture, NativePlayerWireClient wire,
        LegacySecondaryPasswordPacket packet)
    {
        await wire.SendAsync(packet.ToFrameBytes(), fixture.Token);
        Assert.Equal(LegacySecondaryPasswordPacket.Opcode, (await fixture.NextAsync()).Opcode);
    }

    // This actual diagnostic sink only observes committed state at the existing
    // pre-AnswerOk log boundary; it cannot accept/persist/publish a transaction.
    private sealed class AdmissionPinExpiryLog : TextWriter
    {
        private readonly NativePlayerDiagnosticLog inner = new();
        internal Action? ObserveCommitted { get; set; }
        internal string Text => inner.Text;
        public override Encoding Encoding => Encoding.UTF8;
        public override void WriteLine(string? value)
        {
            inner.WriteLine(value);
            if (value?.Contains("ordinary admission committed key=", StringComparison.Ordinal) == true)
                ObserveCommitted?.Invoke();
        }
        public override Task WriteLineAsync(string? value) { WriteLine(value); return Task.CompletedTask; }
        internal void AssertNoWorldRuntimeDiagnostics() => inner.AssertNoWorldRuntimeDiagnostics();
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }
}
