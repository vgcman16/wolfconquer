using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// DATA: owned-file v7 journal checks; these do not grant runtime admission.
public sealed class LegacyExistingAdmissionIdentityDataTests
{
    private const uint Account = LegacyOwnedAdmissionRuntimeFixture.Uid + 19;
    private const uint Player = LegacyOwnedAdmissionRuntimeFixture.Uid;
    private static readonly DateTime Prepared = new(2026, 10, 31, 12, 0, 0);

    [Fact]
    public void ConfiguredRawLoaderSeparatesIdentitiesWhileDefaultLoaderStillRejectsMismatch()
    {
        using var f = MixedFixture();
        var before = f.Files();
        Assert.Equal(CharacterSnapshotLoadStatus.UidMismatch, f.Loader.TryLoadByAccountId(Account).Status);
        var raw = f.Loader.TryLoadConfiguredAccountForAdmission(Account);
        Assert.Equal(CharacterSnapshotLoadStatus.Success, raw.Status);
        Assert.Equal(Player, raw.Snapshot!.Uid);
        f.AssertFiles(before);
    }

    [Theory]
    [InlineData((int)LegacyOrdinaryAdmissionInterruption.AfterPending)]
    [InlineData((int)LegacyOrdinaryAdmissionInterruption.AfterCharacter)]
    [InlineData((int)LegacyOrdinaryAdmissionInterruption.AfterItems)]
    [InlineData((int)LegacyOrdinaryAdmissionInterruption.AfterSpells)]
    [InlineData((int)LegacyOrdinaryAdmissionInterruption.AfterProficiencies)]
    public void MixedIdentityRealV7JournalRecoversOwnedParticipants(int interruption)
    {
        using var f = MixedFixture();
        var (roots, changes, inputs) = Images(f);
        Assert.False(LegacyTradeJournal.TryCommitOrdinaryAdmission(roots, changes, inputs, Prepared, out _,
            (LegacyOrdinaryAdmissionInterruption)interruption));
        Assert.True(LegacyTradeJournal.HasPending(f.Users));
        var record = JsonNode.Parse(File.ReadAllBytes(Pending(f)))!;
        Assert.Equal(7, record["Version"]!.GetValue<int>());
        Assert.Equal((long)Account, record["Changes"]![0]!["FileId"]!.GetValue<long>());
        Assert.All(record["Changes"]!.AsArray().Skip(1), row => Assert.Equal((long)Player, row!["FileId"]!.GetValue<long>()));
        Assert.All(record["AdmissionInputs"]!.AsArray(), row => Assert.Equal((long)Player, row!["FileId"]!.GetValue<long>()));
        Assert.True(Recovery(f).TryEnsureInitialized(out var reason), reason);
        Assert.False(LegacyTradeJournal.HasPending(f.Users));
        foreach (var change in changes)
            Assert.Equal(change.After, File.ReadAllBytes(roots.PathFor(change.Kind, change.FileId)));
        foreach (var input in inputs)
            Assert.Equal(input.Image, File.ReadAllBytes(roots.PathFor(input.Kind, input.FileId)));
        Assert.False(File.Exists(Path.Combine(f.Users, Player.ToString(CultureInfo.InvariantCulture) + ".ini")));
    }

    [Theory]
    [InlineData("player-target")]
    [InlineData("player-input")]
    [InlineData("character-account")]
    [InlineData("character-after-uid")]
    [InlineData("duplicate-owner")]
    public void TamperedRealPendingIdentityRefusesRecoveryWithoutParticipantWrites(string fault)
    {
        using var f = MixedFixture();
        var (roots, changes, inputs) = Images(f);
        Assert.False(LegacyTradeJournal.TryCommitOrdinaryAdmission(roots, changes, inputs, Prepared, out _,
            LegacyOrdinaryAdmissionInterruption.AfterPending));
        var pending = Pending(f);
        var legitimate = File.ReadAllBytes(pending);
        var record = JsonNode.Parse(legitimate)!;
        string? duplicate = null;
        switch (fault)
        {
            case "player-target": record["Changes"]![1]!["FileId"] = Account; break;
            case "player-input": record["AdmissionInputs"]![0]!["FileId"] = Account; break;
            case "character-account": record["Changes"]![0]!["FileId"] = Account + 1; break;
            case "character-after-uid":
                var after = Convert.FromBase64String(record["Changes"]![0]!["After"]!.GetValue<string>());
                var text = Encoding.UTF8.GetString(after).Replace($"UID={Player}", $"UID={Player + 1}", StringComparison.Ordinal);
                record["Changes"]![0]!["After"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
                break;
            case "duplicate-owner":
                duplicate = Path.Combine(f.Users, (Account + 1).ToString(CultureInfo.InvariantCulture) + ".ini");
                File.Copy(roots.PathFor(LegacyTradeJournalFileKind.Character, Account), duplicate);
                break;
        }
        if (duplicate is null) File.WriteAllText(pending, record.ToJsonString(), new UTF8Encoding(false));
        var before = f.Files();
        Assert.False(Recovery(f).TryEnsureInitialized(out var reason));
        Assert.Contains(LegacyTradeJournal.RecoveryRequired, reason);
        f.AssertFiles(before);
        if (duplicate is not null) File.Delete(duplicate);
        File.WriteAllBytes(pending, legitimate);
        Assert.True(Recovery(f).TryEnsureInitialized(out reason), reason);
        Assert.False(LegacyTradeJournal.HasPending(f.Users));
    }

    private static LegacyOwnedAdmissionRuntimeFixture MixedFixture()
    {
        var f = new LegacyOwnedAdmissionRuntimeFixture(bootstrap: false);
        File.Move(f.UserPath, Path.Combine(f.Users, Account.ToString(CultureInfo.InvariantCulture) + ".ini"));
        return f;
    }

    private static (LegacyOrdinaryAdmissionRoots Roots, LegacyTradeJournalChange[] Changes, LegacyOrdinaryAdmissionInput[] Inputs)
        Images(LegacyOwnedAdmissionRuntimeFixture f)
    {
        var roots = LegacyOrdinaryAdmissionRoots.FromLoader(f.Loader);
        var captured = LegacyTradeJournal.CaptureOrdinaryAdmissionInputs(roots, Account, Player);
        var character = captured[LegacyTradeJournalFileKind.Character]!;
        var snapshot = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(character, out _));
        var items = captured[LegacyTradeJournalFileKind.Items]!;
        var spells = captured[LegacyTradeJournalFileKind.Spells]!;
        var profs = captured[LegacyTradeJournalFileKind.Proficiencies]!;
        LegacyTradeJournalChange[] changes =
        [
            new(LegacyTradeJournalFileKind.Character, Account, character, LegacyOrdinaryAdmissionImages.ProjectCharacter(character, Prepared)),
            new(LegacyTradeJournalFileKind.Items, Player, items, items),
            new(LegacyTradeJournalFileKind.Spells, Player, spells, LegacyOrdinaryAdmissionSpellImages.Project(snapshot, spells)),
            new(LegacyTradeJournalFileKind.Proficiencies, Player, profs, profs),
        ];
        LegacyOrdinaryAdmissionInput[] inputs =
        [
            new(LegacyTradeJournalFileKind.Quests, Player, captured[LegacyTradeJournalFileKind.Quests]),
            new(LegacyTradeJournalFileKind.CombatGear, Player, captured[LegacyTradeJournalFileKind.CombatGear]),
        ];
        return (roots, changes, inputs);
    }

    private static string Pending(LegacyOwnedAdmissionRuntimeFixture f) =>
        Path.Combine(f.Users, ".managed-trade-journal", "pending.json");

    private static LegacyTradeRecoveryState Recovery(LegacyOwnedAdmissionRuntimeFixture f) =>
        new(f.Users, f.Items, f.Quests, f.Mailbox, f.CombatGear, f.Spells, f.Proficiencies);
}
