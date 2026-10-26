using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData("PlayersItems", ".bin")]
    [InlineData("PlayersSpells", ".bin")]
    [InlineData("PlayersProfs", ".bin")]
    [InlineData("Quests", ".bin")]
    [InlineData("PlayersCombatGear", ".ini")]
    public async Task RequiredMalformedStartupFileRefusesBeforeOnlineSpellRepairOrNativePublication(string folder, string extension)
    {
        using var input = new LoginInput();
        PrepareAdmissionInput(input);
        File.WriteAllText(input.FilePath, Encoding.UTF8.GetString(input.InitialBytes)
            .Replace("FirstClass=0\r\n", "FirstClass=1000\r\n", StringComparison.Ordinal), new UTF8Encoding(false));
        File.WriteAllBytes(Path.Combine(AdmissionRoot(input), folder, Uid + extension), [1, 0, 0, 0]);
        var files = CaptureAdmissionFiles(input);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var native = birth.Connection.NativeGameClient!;
            var before = ObservePrefixPlayer(native.Player);
            var raw = native.RawEquipmentObservation;
            Assert.Null((await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(before, ObservePrefixPlayer(native.Player));
            Assert.Null(native.HairfaceStorage);
            Assert.Null(native.Beasts);
            Assert.Same(raw, native.RawEquipmentObservation);
            AssertAdmissionFiles(files);
            Assert.False(LegacyTradeJournal.HasPending(input.Users));
            Assert.DoesNotContain("ordinary admission committed", log.Text);
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedJointAdmissionRollsBackEarlierFileImagesAndCanRetryAfterRealShareBlockerCloses(bool denySpells)
    {
        using var input = new LoginInput();
        PrepareAdmissionInput(input);
        File.WriteAllText(input.FilePath, Encoding.UTF8.GetString(input.InitialBytes)
            .Replace("FirstClass=0\r\n", "FirstClass=1000\r\n", StringComparison.Ordinal), new UTF8Encoding(false));
        var spellsPath = Path.Combine(AdmissionRoot(input), "PlayersSpells", Uid + ".bin");
        File.WriteAllBytes(spellsPath, new byte[4]);
        var files = CaptureAdmissionFiles(input);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var native = birth.Connection.NativeGameClient!;
            var before = ObservePrefixPlayer(native.Player);
            using (var held = new FileStream(denySpells ? spellsPath : input.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.Null((await SendLoginAsync(fixture, wire)).After);
                Assert.Equal(before, ObservePrefixPlayer(native.Player));
                Assert.Null(native.HairfaceStorage);
                Assert.Null(native.Beasts);
                Assert.Null(native.RawEquipmentObservation);
                AssertAdmissionFiles(files);
                Assert.False(LegacyTradeJournal.HasPending(input.Users));
                Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
            }
            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            Assert.Contains(accepted.PlayerSpells.Spells, row => row.Id == 1110);
            Assert.Equal(PlayerSpellSnapshotLoader.ToLegacyBytes(accepted.PlayerSpells), File.ReadAllBytes(spellsPath));
            Assert.Equal(1L, ReadPrefixSnapshot(input).Online);
            Assert.Equal(1U, native.Player.Online);
            Assert.False(LegacyTradeJournal.HasPending(input.Users));
        });
    }

    [Theory]
    [InlineData("negative-money")]
    [InlineData("invalid-level")]
    public async Task InvalidSavedFundsOrLevelCannotChangeFilesOrBurnUidState(string invalid)
    {
        using var input = new LoginInput();
        PrepareAdmissionInput(input, starter: true);
        if (invalid == "negative-money")
            File.WriteAllText(input.FilePath, File.ReadAllText(input.FilePath)
                .Replace("Money=1000\r\n", "Money=-1\r\n", StringComparison.Ordinal), new UTF8Encoding(false));
        else File.WriteAllText(input.FilePath, File.ReadAllText(input.FilePath)
            .Replace("Level=1\r\n", "Level=0\r\n", StringComparison.Ordinal), new UTF8Encoding(false));
        using var uids = new AdmissionUidFixture(input);
        var files = CaptureAdmissionFiles(input);
        var uidBefore = uids.StateBytes();
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var before = ObservePrefixPlayer(birth.Connection.NativeGameClient!.Player);
            Assert.Null((await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(before, ObservePrefixPlayer(birth.Connection.NativeGameClient.Player));
            AssertAdmissionFiles(files);
            Assert.Equal(uidBefore, uids.StateBytes());
            Assert.False(LegacyTradeJournal.HasPending(input.Users));
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
        }, admissionUids: uids.Owner);
    }
}
