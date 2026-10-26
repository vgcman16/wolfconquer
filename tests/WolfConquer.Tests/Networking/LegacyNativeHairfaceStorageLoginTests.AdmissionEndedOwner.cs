using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData("Character")]
    [InlineData("Spells")]
    [InlineData("Proficiencies")]
    [InlineData("Store")]
    public async Task EndedGenuineOrdinaryOwnerDeniesRealWritersUntilACurrentOwnerIsReopened(string role)
    {
        using var input = new LoginInput();
        SeedOwnedOrdinaryParticipants(input);
        using var uids = new AdmissionUidFixture(input);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var current = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            var files = CaptureAdmissionFiles(input);
            var state = uids.StateBytes();
            var ended = uids.Owner;
            Assert.True(ended.IsCurrent(ended.Roots));
            uids.Dispose(); // Actual registry releases its actual process lease.
            var errors = new List<Exception>();
            try
            {
                Assert.False(ended.IsCurrent(ended.Roots));
                AssertEndedOrdinaryWriterRefused(role, input, handler, birth.Connection, current);
                Assert.Same(current, handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
                AssertAdmissionFiles(files);
                Assert.Equal(state, uids.StateBytes());
                Assert.False(LegacyTradeJournal.HasPending(input.Users));
            }
            catch (Exception error) { errors.Add(error); }
            try { uids.Open(bootstrap: false); } catch (Exception error) { errors.Add(error); }
            RethrowEndedOwnerFailures(errors);

            Assert.NotSame(ended, uids.Owner);
            Assert.True(uids.Owner.IsCurrent(uids.Owner.Roots));
            AssertCurrentOrdinaryWriterUsable(role, input, handler, birth.Connection, current);
            Assert.Equal(state, uids.StateBytes());
            Assert.False(LegacyTradeJournal.HasPending(input.Users));
        }, worldData: AdmissionCatalog(), admissionUids: uids.Owner);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedFreshOpenOfCorruptOrUnknownHistoryCannotTurnAnEndedOwnerIntoWriterPermission(bool unknownFile)
    {
        using var input = new LoginInput();
        PrepareAdmissionInput(input, starter: true);
        using var uids = new AdmissionUidFixture(input);
        SeedRetiredOrdinaryWork(input, uids, allocated: true, out var after);
        uids.Dispose();
        InstallRetiredAfterFacts(input, after); // Real historical already-after data, not a new grant path.
        uids.Open(bootstrap: false); // Genuine completion proof archives the original real range.
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var current = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            var files = CaptureAdmissionFiles(input);
            var state = uids.StateBytes();
            var history = Path.Combine(Path.GetDirectoryName(uids.StatePath)!, "history");
            var path = unknownFile ? Path.Combine(history, "foreign.json") : Assert.Single(Directory.GetFiles(history, "*.json"));
            var original = unknownFile ? null : File.ReadAllBytes(path);
            uids.Dispose();
            byte[] malformed = [0x7b, 0x7d];
            File.WriteAllBytes(path, malformed);
            var errors = new List<Exception>();
            try
            {
                Assert.False(LegacyItemUidRegistry.TryOpen(uids.OptionsForReopen, out var refused, out _));
                Assert.Null(refused);
                foreach (var role in new[] { "Character", "Spells", "Proficiencies", "Store" })
                    AssertEndedOrdinaryWriterRefused(role, input, handler, birth.Connection, current);
                Assert.Same(current, handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
                AssertAdmissionFiles(files);
                Assert.Equal(state, uids.StateBytes());
                Assert.Equal(malformed, File.ReadAllBytes(path));
                Assert.False(LegacyTradeJournal.HasPending(input.Users));
            }
            catch (Exception error) { errors.Add(error); }
            try
            {
                // Remove/restore ONLY the exact fault injected above, after its
                // retained refusal was checked. Production never discards it.
                if (original is null) File.Delete(path); else File.WriteAllBytes(path, original);
                uids.Open(bootstrap: false);
            }
            catch (Exception error) { errors.Add(error); }
            RethrowEndedOwnerFailures(errors);

            Assert.True(uids.Owner.IsCurrent(uids.Owner.Roots));
            AssertCurrentOrdinaryWriterUsable("Store", input, handler, birth.Connection, current);
            Assert.Equal(state, uids.StateBytes());
            AssertAdmissionFiles(files);
        }, worldData: AdmissionCatalog(), admissionUids: uids.Owner);
    }
}
