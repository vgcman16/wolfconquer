using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedExperienceContinuationDriverTests
{
    [Fact]
    public Task ActualMonsterAwardAdvancesItsOriginBefore2041AndLaterFrameWriterUsesThatOrigin() =>
        RunEarnedPhysicalSerialization(async (f, monster, clock) =>
        {
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Current().NativeGameClient!.OrdinarySpellBinding);
            var admitted = binding.SavedProficiencies; var original = File.ReadAllBytes(OwnedMonsterProficiencyPath(f));
            Assert.Equal(new byte[] { 0xA5, 0xC7, 0xD9 }, original.AsSpan(13, 3).ToArray());
            int proficiencyOutputs = 0;
            f.BeforeSend = (uid, bytes) =>
            {
                if (uid != EarnedActor || OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode != LegacyProficiencyPacket.Opcode) return;
                proficiencyOutputs++;
                Assert.Equal(OwnedMonsterFirstProficiencyGolden(), bytes);
                Assert.NotSame(admitted, binding.SavedProficiencies);
                Assert.True(binding.SavedProficiencies.Matches(File.ReadAllBytes(OwnedMonsterProficiencyPath(f))));
                AssertOwnedMonsterProficiency(f, 20_100, opaque: true);
            };
            try { await Hit(f, monster); } finally { f.BeforeSend = null; }
            Assert.Equal(1, proficiencyOutputs);
            Assert.Equal(139L, f.Current().Character.Level); Assert.Equal(18L, f.Current().Character.AttributePoints);
            Assert.Equal(118L, f.Current().Character.Experience); Assert.Equal(11_028, binding.Player.HitPoints);
            AssertOwnedMonsterProficiency(f, 20_100, opaque: true);

            // A proposed diagnostic DATA edit goes through the existing actual
            // Store/native/Frame writer, not a fake award owner or direct save.
            var written = binding.SavedProficiencies; var before = f.Current();
            var rows = before.PlayerProficiencies.Proficiencies.Select(row => row.Id == 410 ? row with { Experience = 20_101 } : row).ToArray();
            Assert.True(f.Store.TryCompareAndSet(f.Connection(EarnedActor), before,
                before with { PlayerProficiencies = new(rows) }, out var published));
            Assert.Same(published, f.Current()); Assert.NotSame(written, binding.SavedProficiencies);
            AssertOwnedMonsterProficiency(f, 20_101, opaque: true);
            Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        }, opaqueProficiencies: true);

    [Theory]
    [InlineData("changedBefore")] [InlineData("unownedExactAfter")] [InlineData("readLease")]
    public Task ActualMonsterProficiencyRefusesUnownedBeforeOrPreSaveWithout2041OrNativeBookChange(string fault) =>
        RunEarnedPhysicalSerialization(async (f, monster, clock) =>
        {
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Current().NativeGameClient!.OrdinarySpellBinding);
            var admitted = binding.SavedProficiencies; var map = binding.Proficiencies.ClientProf;
            var row = map[410]; string path = OwnedMonsterProficiencyPath(f);
            if (fault != "readLease")
            {
                var changed = File.ReadAllBytes(path);
                BinaryPrimitives.WriteUInt32LittleEndian(changed.AsSpan(16), fault == "unownedExactAfter" ? 20_100U : 101U);
                File.WriteAllBytes(path, changed); // Negative corruption, never a new admitted origin.
            }
            var files = f.Data.Files();
            FileStream? held = fault == "readLease" ? new(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
            try { await Hit(f, monster); } finally { held?.Dispose(); }
            f.Data.AssertFiles(files);
            Assert.Same(admitted, binding.SavedProficiencies); Assert.Same(map, binding.Proficiencies.ClientProf);
            Assert.Same(row, map[410]); Assert.Equal(100U, row.Experience);
            Assert.DoesNotContain(f.ActorFrames, bytes => OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode == LegacyProficiencyPacket.Opcode);
            Assert.DoesNotContain(f.ActorFrames, bytes => bytes.AsSpan().SequenceEqual(Update(EarnedActor, LegacyMsgUpdateDataType.Level, 138)));
            Assert.Contains("ownedProficiencyRefused:", f.Log.ToString(), StringComparison.Ordinal);
            // The monster HP hit was already legitimately accepted before
            // this phase. Existing committed-hit failure handling retires it.
            Assert.True(f.Recovery.IsBlocked); Assert.True(binding.IsRetired);
            Assert.False(binding.Client.OnAutoAttack);
        }, opaqueProficiencies: true);

    [Fact]
    public Task Actual2041SendLossRetainsCommittedBookAndOriginAndFencesBeforeXpContinuation() =>
        RunEarnedPhysicalSerialization(async (f, monster, clock) =>
        {
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Current().NativeGameClient!.OrdinarySpellBinding);
            var admitted = binding.SavedProficiencies;
            var injected = new IOException("owned actual monster proficiency output loss"); int failures = 0;
            f.BeforeSend = (uid, bytes) =>
            {
                if (uid != EarnedActor || OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode != LegacyProficiencyPacket.Opcode) return;
                failures++; AssertOwnedMonsterProficiency(f, 20_100, opaque: true);
                Assert.NotSame(admitted, binding.SavedProficiencies); throw injected;
            };
            try { Assert.Same(injected, await Record.ExceptionAsync(() => Hit(f, monster))); }
            finally { f.BeforeSend = null; }
            Assert.Equal(1, failures); Assert.True(f.Recovery.IsBlocked); Assert.True(binding.IsRetired);
            Assert.NotSame(admitted, binding.SavedProficiencies);
            var saved = PlayerProficiencySnapshotLoader.Parse(File.ReadAllBytes(OwnedMonsterProficiencyPath(f)));
            Assert.Equal(20_100U, Assert.Single(saved.Proficiencies).Experience);
            Assert.Equal(20_100U, binding.Proficiencies.ClientProf[410].Experience);
            Assert.Equal(new byte[] { 0xA5, 0xC7, 0xD9 }, File.ReadAllBytes(OwnedMonsterProficiencyPath(f)).AsSpan(13, 3).ToArray());
            Assert.True(binding.SavedProficiencies.Matches(File.ReadAllBytes(OwnedMonsterProficiencyPath(f))));
            Assert.False(binding.Client.OnAutoAttack); Assert.False(f.Store.TryGet(f.Connection(EarnedActor), out _));
            Assert.DoesNotContain(f.ActorFrames, bytes => OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode == LegacyProficiencyPacket.Opcode);
            Assert.DoesNotContain(f.ActorFrames, bytes => bytes.AsSpan().SequenceEqual(Update(EarnedActor, LegacyMsgUpdateDataType.Level, 138)));
        }, opaqueProficiencies: true);

    private static string OwnedMonsterProficiencyPath(OwnedEquipmentBehaviorFixture f) => Path.Combine(f.Data.Proficiencies,
        EarnedActor.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".bin");

    private static void AssertOwnedMonsterProficiency(OwnedEquipmentBehaviorFixture f, uint experience, bool opaque)
    {
        var current = f.Current(); var binding = Assert.IsType<LegacyOrdinarySpellBinding>(current.NativeGameClient!.OrdinarySpellBinding);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
        var row = Assert.Single(current.PlayerProficiencies.Proficiencies);
        Assert.Equal(410U, row.Id); Assert.Equal(5U, row.Level); Assert.Equal(experience, row.Experience);
        Assert.Equal(1_600_000U, row.NeededExperience);
        Assert.Equal(experience, binding.Proficiencies.ClientProf[410].Experience);
        var bytes = File.ReadAllBytes(OwnedMonsterProficiencyPath(f));
        Assert.Equal(experience, Assert.Single(PlayerProficiencySnapshotLoader.Parse(bytes).Proficiencies).Experience);
        Assert.True(binding.SavedProficiencies.Matches(bytes));
        if (opaque) Assert.Equal(new byte[] { 0xA5, 0xC7, 0xD9 }, bytes.AsSpan(13, 3).ToArray());
    }

    // Equal137 levels apply the established120% monster damage:100 ->120.
    // Profs:100 saved + (120 *1000)/(5+1) =20100; XP120 clears two1-point levels.
    private static byte[] OwnedMonsterFirstProficiencyGolden() =>
        [0x14, 0x00, 0xF9, 0x07, 0x9A, 0x01, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00,
         0x84, 0x4E, 0x00, 0x00, 0x00, 0x6A, 0x18, 0x00];
}
