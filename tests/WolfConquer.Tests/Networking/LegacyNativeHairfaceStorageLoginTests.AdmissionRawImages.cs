using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData("utf8")]
    [InlineData("utf8bom")]
    [InlineData("1252")]
    public async Task RealAdmissionRetainsUnrelatedCharacterBytesEncodingMixedLineEndingsAndNoFinalNewline(string format)
    {
        using var input = new LoginInput();
        PrepareAdmissionInput(input);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = format == "1252" ? Encoding.GetEncoding(1252) : new UTF8Encoding(format == "utf8bom");
        var text = Encoding.UTF8.GetString(input.InitialBytes).TrimEnd('\r', '\n') + "\n; untouched café\r\n[Unrelated]\nRaw =  exact  ";
        text = text.Replace("\r\nOnline=7\r\n", "\nOnline = 7  \r\n", StringComparison.Ordinal);
        var before = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        var expected = encoding.GetPreamble().Concat(encoding.GetBytes(text.Replace("Online = 7  \r\n", "Online = 1  \r\n",
            StringComparison.Ordinal))).ToArray();
        File.WriteAllBytes(input.FilePath, before);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(expected, File.ReadAllBytes(input.FilePath));
            Assert.Equal(1U, birth.Connection.NativeGameClient!.Player.Online);
            Assert.False(LegacyTradeJournal.HasPending(input.Users));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrdinaryCycloneGrantPreservesEverySavedSpellRecordAndItsOpaqueByte(bool alreadyLearned)
    {
        using var input = new LoginInput();
        PrepareAdmissionInput(input, starter: true);
        var rows = new List<PlayerSpellSnapshotEntry> { new(8002, 0, 0, 0, 0, 0), new(1046, 3, 123, 2, 1, 1) };
        if (alreadyLearned) rows.Add(new(1110, 4, 456, 3, 2, 1));
        var before = PlayerSpellSnapshotLoader.ToLegacyBytes(rows);
        before[15] = 0xa7; // Actual unmodeled final byte of the first twelve-byte record.
        var expected = alreadyLearned ? before.ToArray() : before.Concat(new byte[] { 0x56, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }).ToArray();
        if (!alreadyLearned) expected[0] = 3; // Independent count2→3, original records retain every byte.
        var path = Path.Combine(AdmissionRoot(input), "PlayersSpells", Uid + ".bin");
        File.WriteAllBytes(path, before);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, _) = await ConnectBornAsync(fixture);
            var admitted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(expected, File.ReadAllBytes(path));
            Assert.Equal(rows[0], admitted.PlayerSpells.Spells[0]);
            Assert.Equal(rows[1], admitted.PlayerSpells.Spells[1]);
            Assert.Equal(new ushort[] { 8002, 1046, 1110 }, admitted.PlayerSpells.Spells.Select(row => row.Id));
            Assert.Equal(alreadyLearned ? new PlayerSpellSnapshotEntry(1110, 4, 456, 3, 2, 1) :
                new PlayerSpellSnapshotEntry(1110, 0, 0, 0, 0, 0), admitted.PlayerSpells.Spells[2]);
            var after = CaptureAdmissionFiles(input);
            Assert.Same(admitted, (await SendLoginAsync(fixture, wire)).After);
            AssertAdmissionFiles(after);
        });
    }
}
