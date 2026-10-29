using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicClassGatePreparationTests
{
    [Theory]
    [InlineData(false, "mana")]
    [InlineData(true, "mana")]
    [InlineData(false, "stamina")]
    [InlineData(true, "stamina")]
    [InlineData(false, "cold")]
    [InlineData(true, "cold")]
    [InlineData(false, "cadence")]
    [InlineData(true, "cadence")]
    [InlineData(false, "negativeCold")]
    [InlineData(true, "negativeCold")]
    public async Task GenericAdmissionKnownDenialPreservesCompleteCurrentOwner(bool sixArguments, string denial)
    {
        var input = GenericAdmissionInput(mana: denial == "mana" ? 20 : 1_000,
            stamina: denial == "stamina" ? (ushort)9 : (ushort)100);
        var spell = GenericAdmissionSpell(cold: denial == "negativeCold" ? -1 : 60_000);
        if (denial == "cold")
            input = input with { MagicCooldowns = new Dictionary<ushort, DateTimeOffset>
                { [spell.Id] = DateTimeOffset.UtcNow.AddMinutes(5) } };
        if (denial == "cadence")
            input = input with { Character = input.Character with
            {
                Attributes = new Dictionary<string, string>(input.Character.Attributes)
                    { ["LegacyAttackStampUtc"] = DateTimeOffset.UtcNow.AddMinutes(5).ToString("O", CultureInfo.InvariantCulture) },
            } };
        await GenericAdmissionFixture.RunAsync(new(input), async fixture =>
        {
            var before = fixture.Capture();
            Assert.Null(await fixture.InvokeAsync(spell, sixArguments));
            fixture.AssertUnchanged(before);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenericAdmissionFirstCostCallbackAlreadySeesCompleteCostsAndDeadline(bool sixArguments)
    {
        WorldSession? firstCommitted = null;
        var spell = GenericAdmissionSpell();
        await GenericAdmissionFixture.RunAsync(new(GenericAdmissionInput(), onSend: (fixture, _) =>
        {
            if (fixture.Frames.Count == 1) firstCommitted = fixture.Current;
        }), async fixture =>
        {
            var started = DateTimeOffset.UtcNow;
            var result = await fixture.InvokeAsync(spell, sixArguments, supplyIdentity: sixArguments);
            Assert.Same(fixture.Current, result);
            var observed = Assert.IsType<WorldSession>(firstCommitted);
            Assert.Equal("90", observed.Character.Attributes["Stamina"]);
            Assert.Equal(960L, observed.Character.MinMana);
            var stamp = DateTimeOffset.Parse(observed.Character.Attributes["LegacyAttackStampUtc"],
                CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            Assert.InRange(stamp, started, DateTimeOffset.UtcNow);
            Assert.InRange(observed.MagicCooldowns![spell.Id], started.AddMilliseconds(spell.ColdTime),
                DateTimeOffset.UtcNow.AddMilliseconds(spell.ColdTime));
            AssertGenericAdmissionWire(fixture, spell);
            Assert.Same(fixture.Original.PlayerItems, fixture.Current.PlayerItems);
            Assert.Same(fixture.Original.PlayerSpells, fixture.Current.PlayerSpells);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenericAdmissionPostcommitSameClientEditStopsStaleOutputWithoutRepricing(bool sixArguments)
    {
        WorldSession? committed = null;
        var spell = GenericAdmissionSpell();
        await GenericAdmissionFixture.RunAsync(new(GenericAdmissionInput(), onSend: (fixture, _) =>
        {
            if (fixture.Frames.Count != 1) return;
            var owner = fixture.Current;
            committed = owner;
            fixture.Store!.Set(fixture.Connection, owner with
            {
                Character = owner.Character with
                {
                    MinMana = 7,
                    Attributes = new Dictionary<string, string>(owner.Character.Attributes) { ["AfterAdmission"] = "retained" },
                },
            });
        }), async fixture =>
        {
            var result = await fixture.InvokeAsync(spell, sixArguments);
            Assert.Null(result);
            var accepted = Assert.IsType<WorldSession>(committed);
            Assert.Equal(960L, accepted.Character.MinMana);
            Assert.Equal(7L, fixture.Current.Character.MinMana);
            Assert.Equal("90", fixture.Current.Character.Attributes["Stamina"]);
            Assert.Equal("retained", fixture.Current.Character.Attributes["AfterAdmission"]);
            Assert.True(fixture.Current.MagicCooldowns!.ContainsKey(spell.Id));
            Assert.Equal(accepted.MagicCooldowns![spell.Id], fixture.Current.MagicCooldowns[spell.Id]);
            Assert.Equal(LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(
                checked((uint)fixture.Original.Character.Uid), 90).ToFrameBytes(), Assert.Single(fixture.Frames));
            // This is a component-only Store: current7 remains without a
            // fabricated native fence, stale960, refund or a second debit.
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenericAdmissionRetiredGenerationIsNotOverwrittenAfterFirstCommittedCost(bool sixArguments)
    {
        WorldSession? firstCommitted = null;
        WorldSession? replacement = null;
        string? replacementValues = null;
        var spell = GenericAdmissionSpell();
        await GenericAdmissionFixture.RunAsync(new(GenericAdmissionInput(), onSend: (fixture, frame) =>
        {
            if (fixture.Frames.Count != 1) return;
            firstCommitted = fixture.Current;
            Assert.True(fixture.Store!.TryRemove(fixture.Connection, out _));
            replacement = fixture.Original with { Character = fixture.Original.Character with { Name = "NewGeneration" } };
            replacementValues = System.Text.Json.JsonSerializer.Serialize(replacement);
            fixture.Store.Set(fixture.Connection, replacement);
        }), async fixture =>
        {
            var before = fixture.Capture();
            Assert.Null(await fixture.InvokeAsync(spell, sixArguments)); // Accepted publication stops; no refund claim.
            var committed = Assert.IsType<WorldSession>(firstCommitted);
            Assert.Equal("90", committed.Character.Attributes["Stamina"]);
            Assert.Equal(960L, committed.Character.MinMana);
            Assert.True(committed.MagicCooldowns!.ContainsKey(spell.Id));
            Assert.Same(replacement, fixture.Current);
            Assert.Equal(replacementValues, System.Text.Json.JsonSerializer.Serialize(fixture.Current));
            Assert.False(LegacyNativeClientPublication.TryCurrent(fixture.Store!, fixture.Connection,
                before.Owner.Character.Uid, before.Identity!, out _));
            Assert.Equal(LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(
                checked((uint)before.Owner.Character.Uid), 90).ToFrameBytes(), Assert.Single(fixture.Frames));
        });
    }

    [Theory]
    [InlineData(false, (ushort)0)]
    [InlineData(true, (ushort)0)]
    [InlineData(false, (ushort)40)]
    [InlineData(true, (ushort)40)]
    public async Task GenericAdmissionZeroManaSkipsOnlyIrrelevantRuneMetadata(bool sixArguments, ushort mana)
    {
        var fields = new GenericAdmissionRuneFields();
        var catalog = CreateCatalogWithItems(new ItemType(4_030_809, "MPMaster(+9)", fields));
        var input = GenericAdmissionInput();
        input = input with
        {
            Character = input.Character with
            {
                Reborn = 2,
                Attributes = new Dictionary<string, string>
                {
                    ["Stamina"] = "100", ["Reborn"] = "2", ["Level"] = "120",
                    ["StatusFlags"] = BuildStatusFlags(0x60, LegacyDragonPierceStaminaFlag),
                    ["DragonPierceStamina"] = "4",
                },
            },
            PlayerItems = CreatePlayerItems(new PlayerItemSnapshotItem
                { Uid = 98_400, ItemId = 4_030_809, Position = 103, Durability = 100, MaximumDurability = 100 }),
        };
        fields.Armed = true; // Only after real finite catalog construction.
        var spell = GenericAdmissionSpell(mana);
        await GenericAdmissionFixture.RunAsync(new(input, catalog), async fixture =>
        {
            var before = fixture.Capture();
            var result = await fixture.InvokeAsync(spell, sixArguments);
            if (mana != 0)
            {
                Assert.Null(result);
                Assert.True(fields.Reads > 0);
                fixture.AssertUnchanged(before); // Required metadata fault is not a partial resource debit.
            }
            else
            {
                Assert.Same(fixture.Current, result);
                Assert.Equal(0, fields.Reads);
                Assert.Equal("80", fixture.Current.Character.Attributes["Stamina"]); //10+Scurvy6+DragonPierce4.
                Assert.Equal(1_000L, fixture.Current.Character.MinMana);
                Assert.True(fixture.Current.MagicCooldowns!.ContainsKey(spell.Id));
                AssertGenericAdmissionWire(fixture, spell, stamina: 80, mana: null);
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenericAdmissionNoStoreRemainsExplicitLocalRecordCompatibility(bool sixArguments)
    {
        await GenericAdmissionFixture.RunAsync(new(GenericAdmissionInput(), registered: false), async fixture =>
        {
            var before = fixture.Capture();
            var spell = GenericAdmissionSpell();
            var result = Assert.IsType<WorldSession>(await fixture.InvokeAsync(spell, sixArguments));
            Assert.Null(fixture.Store);
            Assert.Null(before.Identity);
            Assert.Same(before.Owner, fixture.Current); // No fabricated live Store publication.
            Assert.Equal(before.Values, System.Text.Json.JsonSerializer.Serialize(fixture.Original));
            Assert.Equal("90", result.Character.Attributes["Stamina"]);
            Assert.Equal(960L, result.Character.MinMana);
            Assert.True(result.MagicCooldowns!.ContainsKey(spell.Id));
            AssertGenericAdmissionWire(fixture, spell);
        });
    }

    [Theory]
    [InlineData("generic")]
    [InlineData("recruit")]
    public async Task GenericAdmissionExplicitIgnoreCadenceRetainsExistingStamp(string entry)
    {
        var input = GenericAdmissionInput();
        var existing = DateTimeOffset.UtcNow.AddMinutes(5).ToString("O", CultureInfo.InvariantCulture);
        input = input with { Character = input.Character with
            { Attributes = new Dictionary<string, string>(input.Character.Attributes) { ["LegacyAttackStampUtc"] = existing } } };
        var spell = GenericAdmissionSpell(cold: 0);
        if (entry == "recruit") spell = spell with { Sort = LegacyMagicSort.Recruit };
        await GenericAdmissionFixture.RunAsync(new(input), async fixture =>
        {
            var result = await fixture.InvokeAsync(spell, sixArguments: true,
                ignoreAttackStamp: true, recruit: entry == "recruit");
            Assert.Same(fixture.Current, result);
            Assert.Equal(existing, fixture.Current.Character.Attributes["LegacyAttackStampUtc"]);
            Assert.Null(fixture.Current.MagicCooldowns);
            AssertGenericAdmissionWire(fixture, spell);
        });
    }

    [Theory]
    [InlineData("mana")]
    [InlineData("cold")]
    public async Task GenericAdmissionRecruitHelperRefusalPreservesCompleteCurrentOwner(string denial)
    {
        var spell = GenericAdmissionSpell() with { Sort = LegacyMagicSort.Recruit };
        var input = GenericAdmissionInput(mana: denial == "mana" ? 20 : 1_000);
        if (denial == "cold")
            input = input with { MagicCooldowns = new Dictionary<ushort, DateTimeOffset>
                { [spell.Id] = DateTimeOffset.UtcNow.AddMinutes(5) } };
        await GenericAdmissionFixture.RunAsync(new(input), async fixture =>
        {
            var before = fixture.Capture();
            Assert.Null(await fixture.InvokeAsync(spell, sixArguments: false, recruit: true));
            fixture.AssertUnchanged(before);
        });
    }
}
