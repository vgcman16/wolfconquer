using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PostcommitStoreRetirementOrActualTransportCloseDoesNotUndoOrRegrantStartup(bool closeTransport)
    {
        using var input = new LoginInput();
        PrepareAdmissionInput(input, starter: true);
        using var uids = new AdmissionUidFixture(input);
        using var beastMetadata = new NativeLoginBeastMetadataScope(1000);
        using var log = new AdmissionDeliveryLog();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(input.Users), null, AdmissionCatalog(),
            uids.Owner, log, ConquerProtocolProfile.VisibleDx8D13);
        var fired = false;
        await NativePlayerLoopback.RunAsync(ConquerTcpWireMode.LegacyGameServer, async fixture =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            log.OnWritten = value =>
            {
                if (fired || !value.Contains("ordinary admission committed key=", StringComparison.Ordinal)) return;
                fired = true;
                if (closeTransport)
                    birth.Connection.DisconnectAsync().GetAwaiter().GetResult(); // Actual producer closes its owned socket synchronously.
                else
                    Assert.True(AdmissionOwner<WorldSessionStore>(handler, "_sessions").TryRemove(birth.Connection, out _));
            };
            try
            {
                var dispatched = await SendLoginAsync(fixture, wire);
                if (!closeTransport) Assert.Null(dispatched.After);
            }
            catch (OperationCanceledException) when (closeTransport && fired)
            {
                // The genuine connection lifetime was canceled by its real TCP
                // producer. This is not a successful-delivery assertion.
            }
            Assert.True(fired);
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
            var items = new PlayerItemSnapshotLoader(Path.Combine(AdmissionRoot(input), "PlayersItems")).LoadByCharacterUid(Uid);
            Assert.Empty(items.EquipmentItems);
            Assert.Equal(1000L, new CharacterSnapshotLoader(input.Users).TryLoadByAccountIdForAdmission(Uid).Snapshot!.Money);
            Assert.False(LegacyTradeJournal.HasPending(input.Users));
        }, handler.HandleFrameAsync, handler.ReadCurrentSessionForNativeOwnerObservation);
        var files = CaptureAdmissionFiles(input);
        var uidBytes = uids.StateBytes();
        await WithHandlerAsync(input, async (fixture, fresh, freshLog) =>
        {
            var (wire, freshBirth) = await ConnectBornAsync(fixture);
            var commitObservations = ObserveRawAdmissionCommit(freshLog, () => AssertAdmissionFiles(files));
            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(1, commitObservations());
            Assert.Empty(accepted.PlayerItems.EquipmentItems);
            Assert.Equal(1L, accepted.Character.Online);
            Assert.Equal(1L, new CharacterSnapshotLoader(input.Users).TryLoadByAccountIdForAdmission(Uid).Snapshot!.Online);
            Assert.Equal(1U, freshBirth.Connection.NativeGameClient!.Player.Online);
            AssertAdmissionFilesAfterRuntimeHpSave(files, input, hitpoints: 0);
            Assert.Equal(uidBytes, uids.StateBytes());
        }, worldData: AdmissionCatalog(), admissionUids: uids.Owner);
    }

    private sealed class AdmissionDeliveryLog : TextWriter
    {
        private readonly StringBuilder text = new();
        private readonly object gate = new();
        internal Action<string>? OnWritten { get; set; }
        internal string Text { get { lock (gate) return text.ToString(); } }
        public override Encoding Encoding => System.Text.Encoding.UTF8;
        public override void WriteLine(string? value)
        {
            lock (gate)
            {
                text.AppendLine(value);
                OnWritten?.Invoke(value ?? "");
            }
        }
        public override Task WriteLineAsync(string? value)
        { WriteLine(value); return Task.CompletedTask; }
    }
}
