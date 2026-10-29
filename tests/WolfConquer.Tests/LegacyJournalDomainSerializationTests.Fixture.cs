using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyJournalDomainSerializationTests
{
    private sealed record OwnerFiles(uint Uid, string Character, string Items, string Spells);

    private static OwnerFiles SeedOwner(LegacyOwnedAdmissionRuntimeFixture data, uint offset, uint stamina = 100)
    {
        var uid = checked(LegacyOwnedAdmissionRuntimeFixture.Uid + offset);
        var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(data.UserPath), out var encoding);
        text = LegacyOrdinaryAdmissionCharacterText.Project(text, new Dictionary<string, string>
        {
            ["UID"] = uid.ToString(CultureInfo.InvariantCulture),
            ["Stamina"] = stamina.ToString(CultureInfo.InvariantCulture),
        }, paid: null);
        var character = Path.Combine(data.Users, uid.ToString(CultureInfo.InvariantCulture) + ".ini");
        var items = Path.Combine(data.Items, uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        var spells = Path.Combine(data.Spells, uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        File.WriteAllBytes(character, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
        new PlayerItemSnapshotLoader(data.Items).SaveByCharacterUid(uid, PlayerItemSnapshot.Empty);
        new PlayerSpellSnapshotLoader(data.Spells).SaveByCharacterUid(uid, PlayerSpellSnapshot.Empty);
        return new(uid, character, items, spells);
    }

    private static LegacyTradeJournalChange CharacterChange(OwnerFiles owner, string key, long delta)
    {
        var before = File.ReadAllBytes(owner.Character);
        var text = CharacterSnapshotFileEncoding.Decode(before, out var encoding);
        var parsed = CharacterSnapshotParser.ParseText(text);
        var value = key == "Money" ? parsed.Money : long.Parse(parsed.Attributes[key], CultureInfo.InvariantCulture);
        var after = LegacyOrdinaryAdmissionCharacterText.Project(text,
            new Dictionary<string, string> { [key] = checked(value + delta).ToString(CultureInfo.InvariantCulture) }, paid: null);
        return new(LegacyTradeJournalFileKind.Character, owner.Uid, before,
            encoding.GetPreamble().Concat(encoding.GetBytes(after)).ToArray());
    }

    private static LegacyTradeJournalChange[] OwnerChanges(OwnerFiles owner, int version)
    {
        var changes = new List<LegacyTradeJournalChange> { CharacterChange(owner, "Money", 1) };
        if (version is 2 or 9)
        {
            var items = File.ReadAllBytes(owner.Items);
            // A real existing empty Items file, not a fabricated participant.
            changes.Add(new(LegacyTradeJournalFileKind.Items, owner.Uid, items, items));
        }
        if (version is 8 or 9)
        {
            var before = File.ReadAllBytes(owner.Spells);
            var rows = PlayerSpellSnapshotLoader.Parse(before).Spells;
            var added = new PlayerSpellSnapshotEntry(checked((ushort)(1000 + rows.Count)), 0, 0, 0, 0, 0);
            var after = PlayerSpellSnapshotLoader.ToLegacyBytes(new PlayerSpellSnapshot(rows.Concat([added]).ToArray()));
            changes.Add(new(LegacyTradeJournalFileKind.Spells, owner.Uid, before, after));
        }
        return changes.ToArray();
    }

    private static bool CommitOwner(LegacyOwnedAdmissionRuntimeFixture data, OwnerFiles owner, int version, out string reason)
    {
        var changes = OwnerChanges(owner, version);
        return version switch
        {
            2 => LegacyTradeJournal.TryCommitSingleOwner(data.Users, data.Items, changes, out reason),
            8 => LegacyTradeJournal.TryCommitCharacterSpells(data.Users, data.Spells, changes, out reason),
            9 => LegacyTradeJournal.TryCommitCharacterItemsSpells(data.Users, data.Items, data.Spells, changes, out reason),
            _ => throw new ArgumentOutOfRangeException(nameof(version)),
        };
    }

    private static LegacyTradeJournalChange[] PairChanges(OwnerFiles first, OwnerFiles second, bool reverse = false) =>
        [CharacterChange(first, "Stamina", reverse ? -20 : 20), CharacterChange(second, "Stamina", reverse ? 20 : -20)];

    private static void AssertCharacterPreserved(OwnerFiles owner)
    {
        var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(owner.Character), out _);
        Assert.Contains("KeepBytes=startup-owned-value", text, StringComparison.Ordinal);
        Assert.Contains("Sentinel=retain", text, StringComparison.Ordinal);
        Assert.Equal((long)owner.Uid, CharacterSnapshotParser.ParseText(text).Uid);
    }
}
