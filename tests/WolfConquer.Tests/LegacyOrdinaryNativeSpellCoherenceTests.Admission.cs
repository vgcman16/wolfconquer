using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryNativeSpellCoherenceTests
{
    [Fact]
    public async Task OrdinaryJoinGivesMissingSavedSpellsTheirRealEmptyOwnerWithoutCreatingAnUnusedFile()
    {
        await RunAsync(async fixture =>
        {
            Assert.False(File.Exists(fixture.SpellPath));
            await fixture.LoginAsync();
            fixture.RequireCommitObserved();
            Assert.Empty(fixture.Current.PlayerSpells.Spells);
            Assert.Empty(RequireCoherent(fixture).ClientSpells);
            Assert.False(File.Exists(fixture.SpellPath));
        }, [], lineage: false, missingSpellFile: true);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    public async Task OrdinaryJoinAdmitsSavedMembershipAndLineageWithoutReencodingOpaqueRows(byte selectedSoul)
    {
        var saved = new PlayerSpellSnapshotEntry(6001, 4, 1111, 2, 255, selectedSoul);
        await RunAsync(async fixture =>
        {
            var player = fixture.Player;
            Assert.Null(fixture.Native.MySpells);
            await fixture.LoginAsync();

            fixture.RequireCommitObserved();
            Assert.Same(player, fixture.Player);
            Assert.Equal(new[] { saved, new PlayerSpellSnapshotEntry(1110, 0, 0, 0, 0, 0) }, fixture.Current.PlayerSpells.Spells);
            var storage = RequireCoherent(fixture); // Outcome assertion, never an old-source fixture prerequisite.
            var row = Assert.Single(storage.ClientSpells.ToArray(), pair => pair.Key == 6001).Value;
            Assert.Equal(65810, row.HashLevelSoul); // Genuine SoulLevel255 setter ORs2|16|256|65536 into a new row.
            var expected = Convert.FromHexString("02000000").Concat(fixture.SavedSpellsBefore.Skip(4))
                .Concat(Convert.FromHexString("560400000000000000000000")).ToArray();
            Assert.Equal(expected, File.ReadAllBytes(fixture.SpellPath));
            Assert.Equal(0xA5, expected[15]);
            Assert.Equal(LegacyMsgMessagePacket.AnswerOk().ToFrameBytes(), fixture.Sent[0]);
            var grants = fixture.Sent.Where(frame => LegacySpellPacket.TryParse(ReadFrame(frame), out _)).ToArray();
            Assert.Single(grants);
            Assert.True(LegacySpellPacket.TryParse(ReadFrame(grants[0]), out var granted));
            Assert.Equal(1110, granted.Id);
            Assert.Equal(0, granted.Level); Assert.Equal(0, granted.Experience);

            var current = fixture.Current;
            var map = storage.ClientSpells;
            var count = fixture.Sent.Count;
            await fixture.LoginAsync();
            Assert.Same(current, fixture.Current);
            Assert.Same(storage, fixture.Native.MySpells);
            Assert.Same(map, storage.ClientSpells);
            Assert.Same(row, Assert.Single(storage.ClientSpells.ToArray(), pair => pair.Key == 6001).Value);
            Assert.Equal(count, fixture.Sent.Count);
            Assert.Equal(expected, File.ReadAllBytes(fixture.SpellPath));
        }, [saved]);
    }

    [Theory]
    [InlineData((byte)5)]
    [InlineData(byte.MaxValue)]
    public async Task OrdinaryJoinRefusesUnrepresentableSelectedSoulBeforeAnyDurableOrNativePrefix(byte selectedSoul)
    {
        await RunAsync(async fixture =>
        {
            var files = fixture.Files();
            var before = new NativeAdmissionSpellWitness(fixture);
            await fixture.LoginAsync();

            Assert.Null(fixture.CurrentOrNull);
            before.RequireUnchanged(fixture);
            fixture.RequireFiles(files);
            Assert.Empty(fixture.Sent);
            Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
            Assert.DoesNotContain("WorldServer ordinary admission committed", fixture.Log.ToString());
        }, [new(6001, 4, 1111, 2, 3, selectedSoul)]);
    }

    [Theory]
    [InlineData("zero-id")]
    [InlineData("negative-xp")]
    [InlineData("duplicate-id")]
    public async Task OrdinaryJoinKeepsExistingMalformedSavedMembershipRefusals(string malformed)
    {
        var row = new PlayerSpellSnapshotEntry(6001, 4, 1111, 2, 3, 1);
        var rows = malformed switch
        {
            "zero-id" => new[] { row with { Id = 0 } },
            "negative-xp" => new[] { row with { Experience = -1 } },
            _ => new[] { row, row with { Level = 5 } },
        };
        await RunAsync(async fixture =>
        {
            var files = fixture.Files();
            var before = new NativeAdmissionSpellWitness(fixture);
            await fixture.LoginAsync();
            Assert.Null(fixture.CurrentOrNull);
            before.RequireUnchanged(fixture);
            fixture.RequireFiles(files);
            Assert.Empty(fixture.Sent);
            Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
        }, rows);
    }
}
