using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMailboxOwnerJournalTests
{
    private static string GearRoot(Fixture fixture) => Path.Combine(fixture.Root, "PlayersCombatGear");

    private static CombatGearSnapshot GearSnapshot() => CombatGearSnapshot.Empty with
    {
        MasteryPoints = 43, TaoistReiki = 23, PirateExp = 17, Lottery = 7, Count = 1,
        AirPowers = new(1, 2, 3, 19, 4, 5, 6),
        Items = [new CombatGearSnapshotItem(5001, 2, 31, 1, 3, 41,
            [new uint[] { 11, 12, 13, 14, 15, 16, 17 }, new uint[] { 21, 22, 23, 24, 25, 26, 27 }],
            new uint[] { 101, 102, 103, 104, 105, 106 })],
        BlackJadeBag = new(2, new uint[] { 301, 302 }),
    };

    private static byte[] GearBytes(CombatGearSnapshot gear) => Encoding.UTF8.GetBytes(CombatGearSnapshotLoader.ToLegacyText(gear));

    private static LegacyTradeJournalChange[] SeedGearJournal(Fixture fixture, bool emptyGear = false)
    {
        // Reuse the genuine complete character/seven-store item/mailbox fixture.
        // All writes here precede the journal under test; no gameplay/UID owner is supplied.
        var previous = fixture.Seed();
        var gear = emptyGear ? CombatGearSnapshot.Empty : GearSnapshot();
        var next = gear with { PirateExp = gear.PirateExp + 20, AirPowers = gear.AirPowers with { Points = gear.AirPowers.Points + 50 } };
        var loader = new CombatGearSnapshotLoader(GearRoot(fixture));
        loader.SaveByCharacterUid(Fixture.CharacterUid, gear);
        var before = File.ReadAllBytes(Path.Combine(GearRoot(fixture), Fixture.CharacterUid.ToString(CultureInfo.InvariantCulture) + ".ini"));
        Assert.Equal(GearBytes(gear), before);
        Assert.Equal(before, GearBytes(loader.LoadByCharacterUid(Fixture.CharacterUid)));
        return [.. previous, new(LegacyTradeJournalFileKind.CombatGear, Fixture.CharacterUid, before, GearBytes(next))];
    }

    private static string GearTarget(Fixture fixture, LegacyTradeJournalChange change) =>
        change.Kind == LegacyTradeJournalFileKind.CombatGear
            ? Path.Combine(GearRoot(fixture), change.FileId.ToString(CultureInfo.InvariantCulture) + ".ini")
            : fixture.Target(change);

    private static LegacyTradeRecoveryState GearRecovery(Fixture fixture, string? root) =>
        new(fixture.Users, fixture.Items, null, fixture.Mailbox, root);

    private static void AssertGearImages(Fixture fixture, LegacyTradeJournalChange[] changes, bool after)
    {
        foreach (var change in changes)
            Assert.Equal(after ? change.After : change.Before, File.ReadAllBytes(GearTarget(fixture, change)));
        if (!after) return;
        fixture.AssertClaimedImages();
        var gearChange = Assert.Single(changes, change => change.Kind == LegacyTradeJournalFileKind.CombatGear);
        var gear = new CombatGearSnapshotLoader(GearRoot(fixture)).LoadByCharacterUid(Fixture.CharacterUid);
        Assert.Equal(gearChange.After, GearBytes(gear));
        Assert.Equal(CombatGearSnapshotLoader.Parse(Encoding.UTF8.GetString(gearChange.After)).PirateExp, gear.PirateExp);
    }
}
