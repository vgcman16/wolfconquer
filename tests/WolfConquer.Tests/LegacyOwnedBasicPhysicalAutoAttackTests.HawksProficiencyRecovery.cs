using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedBasicPhysicalAutoAttackTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public Task RealHawksZeroDamageRetainsCharacterImageWhileProficiencyRecovers(int prefix) =>
        RunHawksProficiencyAsync(async f =>
        {
            // The target obtains its real 100% Hawk through the normal magic
            // request, not a seeded flag or fabricated protection result.
            await f.MintHawksAsync(); await f.SitAsync(); f.Clear();
            var before = f.Files(); var after = before.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
            string characterKey = Path.GetRelativePath(f.Owner.Data.Root, f.Owner.CharacterPath(Target));
            string proficiencyKey = Path.GetRelativePath(f.Owner.Data.Root, ProficiencyPath(f));
            after[proficiencyKey] = ProficiencyAfterImage(before[proficiencyKey], existing: true);
            using (var interruption = LegacyJournalInterruption.StopAfter(prefix))
            {
                f.Owner.Handler.PhysicalDeathJournalInterruption = interruption;
                try { await f.AttackAsync(); }
                finally { f.Owner.Handler.PhysicalDeathJournalInterruption = null; }
                Assert.True(interruption.PendingObserved, f.Owner.Log.ToString());
                Assert.Equal(Enumerable.Range(0, prefix + 1), interruption.Visited);
            }
            Assert.True(f.Owner.Recovery.IsBlocked); Assert.Empty(f.Order);
            string pendingPath = Path.Combine(f.Owner.Data.Users, ".managed-trade-journal", "pending.json");
            var pending = JsonNode.Parse(File.ReadAllBytes(pendingPath))!;
            Assert.Equal(24, pending["Version"]!.GetValue<int>());
            var op = pending["PhysicalHitProgression"]!;
            Assert.Equal(300U, op["RawExperience"]!.GetValue<uint>());
            Assert.Equal(0U, op["AppliedDamage"]!.GetValue<uint>());
            var changes = pending["Changes"]!.AsArray(); Assert.Equal(2, changes.Count);
            Assert.Equal(before[characterKey], Convert.FromBase64String(changes[0]!["Before"]!.GetValue<string>()));
            Assert.Equal(before[characterKey], Convert.FromBase64String(changes[0]!["After"]!.GetValue<string>()));
            Assert.Equal(before[proficiencyKey], Convert.FromBase64String(changes[1]!["Before"]!.GetValue<string>()));
            Assert.Equal(after[proficiencyKey], Convert.FromBase64String(changes[1]!["After"]!.GetValue<string>()));
            Assert.Equal(before[characterKey], File.ReadAllBytes(f.Owner.CharacterPath(Target)));
            Assert.Equal(prefix >= 2 ? after[proficiencyKey] : before[proficiencyKey], File.ReadAllBytes(ProficiencyPath(f)));
            await f.Owner.Runtime.DisposeAsync();
            for (int restart = 0; restart < 2; restart++)
            {
                Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(f.Owner.Data.Loader, f.Owner.Data.Options(), out var value, out var reason), reason);
                await using var recovered = Assert.IsType<LegacyOwnedAdmissionRuntime>(value);
                f.AssertFiles(after); Assert.Empty(f.Order); Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
            }
        });

    private static Task RunHawksProficiencyAsync(Func<AttackFixture, Task> body)
    {
        var clock = new AttackClock(); var hawks = HawksCatalog();
        var actor = Saved(Actor, "HawksProficiencyActor", 830, 5000, 300, false);
        var target = Saved(Target, "HawksProficiencyTarget", 831, 2000, 0, false);
        var items = new PlayerItemSnapshot([], [], [],
            [new() { Uid = ProficiencyWeaponUid, ItemId = ProficiencyWeaponType, Position = 4,
                Durability = 100, MaximumDurability = 100, StackSize = 1 }], []);
        var profImage = ProficiencyBeforeImage(existing: true);
        var spells = new PlayerSpellSnapshot([new(15920, 9, 0, 0, 0, 0)]);
        var options = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock,
            LegacySignedMillisecondClock.FromTimeProvider(clock, 10000));
        var catalog = NativeCallerWorldDataFixture.Create(new ItemType[] { OwnedEquipmentBehaviorFixture.Item(ProficiencyWeaponType) }, [],
            magic: hawks.Magic.ToArray(), magicEffects: hawks.MagicEffects.ToArray());
        return OwnedEquipmentBehaviorFixture.RunWithProficiencyImageAsync(
            new(actor, items, spells, PlayerProficiencySnapshotLoader.Parse(profImage)), catalog, options, profImage,
            async owner => { var fixture = new AttackFixture(owner, clock); fixture.AssertCoherent(2000); fixture.Clear(); await body(fixture); },
            new OwnedEquipmentBehaviorInput(target, PlayerItemSnapshot.Empty, spells));
    }
}
