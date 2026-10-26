using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Fact]
    public async Task ActualBlockedRecoveryOwnerPreventsEveryOrdinaryStartupWrite()
    {
        using var input = new LoginInput();
        PrepareAdmissionInput(input);
        var files = CaptureAdmissionFiles(input);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var native = birth.Connection.NativeGameClient!;
            var before = ObservePrefixPlayer(native.Player);
            AdmissionOwner<LegacyTradeRecoveryState>(handler, "_tradeRecovery").Block("owned refusal control");
            Assert.Null((await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(before, ObservePrefixPlayer(native.Player));
            Assert.Null(native.HairfaceStorage);
            Assert.Null(native.Beasts);
            AssertAdmissionFiles(files);
            Assert.DoesNotContain("ordinary admission committed", log.Text);
        });
    }

    [Fact]
    public async Task RealInitialStoreGateRefusesASecondAccountOwnerBeforeJournalPublication()
    {
        using var input = new LoginInput();
        PrepareAdmissionInput(input);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, firstBirth) = await ConnectBornAsync(fixture);
            var first = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            var (_, secondBirth) = await ConnectBornAsync(fixture);
            var native = secondBirth.Connection.NativeGameClient!;
            var before = ObservePrefixPlayer(native.Player);
            var files = CaptureAdmissionFiles(input);
            var recovery = AdmissionOwner<LegacyTradeRecoveryState>(handler, "_tradeRecovery");
            using var operation = await AdmissionOwner<WorldOperationBarrier>(handler, "_worldOperations").EnterExclusiveAsync(fixture.Token);
            var loader = new CharacterSnapshotLoader(input.Users);
            Assert.True(LegacyOrdinaryAdmissionPlan.TryPrepare(handler, secondBirth.Connection, Uid, "owned",
                loader.TryLoadByAccountIdForAdmission(Uid).Snapshot!, loader, null, recovery, TimeProvider.System,
                out var plan, out var reason), reason);
            var store = AdmissionOwner<WorldSessionStore>(handler, "_sessions");

            Assert.False(store.TryCommitOrdinaryAdmission(secondBirth.Connection, plan!, out var rejected, out _));

            Assert.Null(rejected);
            Assert.Same(first, handler.ReadCurrentSessionForNativeOwnerObservation(firstBirth.Connection));
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(secondBirth.Connection));
            Assert.Equal(before, ObservePrefixPlayer(native.Player));
            Assert.Null(native.Beasts);
            Assert.Null(native.HairfaceStorage);
            AssertAdmissionFiles(files);
            Assert.False(LegacyTradeJournal.HasPending(input.Users));
        });
    }

    [Fact]
    public async Task PreservingAdmissionDoesNotDispatchForeignMetadataCopyToCallbacks()
    {
        using var input = new LoginInput();
        PrepareAdmissionInput(input, starter: true);
        using var uids = new AdmissionUidFixture(input);
        var bytes = File.ReadAllBytes(input.FilePath);
        var uidBefore = uids.StateBytes();
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var commitObservations = ObserveRawAdmissionCommit(log, () => AssertOnlyPresenceChanged(input, bytes));
            var native = birth.Connection.NativeGameClient!;
            var store = AdmissionOwner<WorldSessionStore>(handler, "_sessions");
            var fired = false;
            Action remove = () =>
            {
                if (fired || !WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate) return;
                fired = true;
                Assert.False(store.TryRemove(birth.Connection, out var current));
                Assert.Null(current); // Removes the genuine empty CWT holder.
            };
            LegacyNativeBeastsAtrribute.Attributes = new AdmissionCallbackBeastRows(LegacyNativeBeastsAtrribute.Attributes, remove);

            var admitted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(1, commitObservations());

            Assert.False(fired);
            Assert.Equal(Uid, native.Player.UID);
            Assert.NotNull(native.HairfaceStorage);
            Assert.NotNull(native.Beasts);
            Assert.Same(admitted, store.TryGet(birth.Connection, out var actual) ? actual : null);
            var committed = System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(bytes)
                .Replace("Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal));
            Assert.Equal(OrdinaryAdmissionImageExpectations.RuntimeHpSave(committed, 0, 0, 0),
                File.ReadAllBytes(input.FilePath));
            Assert.Equal(uidBefore, uids.StateBytes());
            Assert.False(LegacyTradeJournal.HasPending(input.Users));
            Assert.Contains("ordinary admission committed", log.Text);
        }, admissionUids: uids.Owner);
    }

    private sealed class AdmissionCallbackBeastRows : Dictionary<byte, LegacyNativeBeastsAtrribute.Attribute>,
        ICollection<KeyValuePair<byte, LegacyNativeBeastsAtrribute.Attribute>>
    {
        private readonly Action observed;
        internal AdmissionCallbackBeastRows(Dictionary<byte, LegacyNativeBeastsAtrribute.Attribute> source, Action observed)
            : base(source) { this.observed = observed; }
        void ICollection<KeyValuePair<byte, LegacyNativeBeastsAtrribute.Attribute>>.CopyTo(
            KeyValuePair<byte, LegacyNativeBeastsAtrribute.Attribute>[] array, int arrayIndex)
        {
            observed(); // Same-value actual metadata enumeration, never an accepting owner or saver.
            foreach (var row in this) array[arrayIndex++] = row;
        }
    }
}
