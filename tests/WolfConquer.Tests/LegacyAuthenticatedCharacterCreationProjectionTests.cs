using System.Globalization;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// DATA ONLY: hypothetical item UIDs here feed the existing pure HP calculator.
// These tests issue no authenticated permit, registry receipt, producer work or WAL.
public sealed partial class LegacyAuthenticatedCharacterCreationProjectionTests
{
    private static readonly DateTime Prepared = new(2026, 10, 30, 12, 34, 56, DateTimeKind.Unspecified);
    private const uint Account = 2_399_701;

    [Theory]
    [InlineData((ushort)0, 10000L)] [InlineData((ushort)2, 1000L)] [InlineData((ushort)4, 4000L)]
    [InlineData((ushort)6, 2000L)] [InlineData((ushort)8, 5000L)] [InlineData((ushort)10, 6000L)]
    [InlineData((ushort)12, 7000L)] [InlineData((ushort)14, 8000L)] [InlineData((ushort)16, 16000L)] [InlineData((ushort)20, 9000L)]
    public void EveryFreshProfileMatchesTheExistingFullHpCalculatorIncludingAdoptedItemProfessionAndScoreZeroRuneRows(ushort selection, long code)
    {
        var catalog = Catalog(code); var projected = Capture(catalog, selection);
        var character = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(projected.CopyCharacterImage(), out _));
        Assert.True(LegacyFirstCharacterStarterProfiles.TryGet(code, out var profile));
        var sourceItems = ProfileRoots(profile).Select((row, index) => new PlayerItemSnapshotItem
        {
            Uid = checked((uint)index + 1), ItemId = row.Id, Position = row.Position,
            Durability = 321, MaximumDurability = 321,
        }).ToArray();
        var snapshots = new PlayerItemSnapshot([], [], [], sourceItems, []);
        var neutral = character with { MinHitPoints = 0,
            Attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase) { ["MinHitPoints"] = "0" } };
        uint actualFullCalculator = LegacyMaxHitpointsCalculator.CalculateRuntimeMaxHitpoints(neutral, 0, snapshots, CombatGearSnapshot.Empty, catalog);
        uint sourceBase = LegacyFirstCharacterStarterProfiles.CalculateHitpoints(code, profile.Strength, profile.Agility, profile.Vitality, profile.Spirit);
        Assert.Equal(Math.Max(sourceBase, actualFullCalculator), checked((uint)character.MinHitPoints));
        Assert.True(character.MinHitPoints > sourceBase + 17 + 23); // Nonzero physical item HP also reached the independent calculator.
        Assert.Equal((code, 1L, 0L, 0L, 100_000L, 0L),
            (character.Class, character.Level, character.Reborn, character.Experience, character.Money, character.ConquerPoints));
        Assert.Equal((profile.Strength, profile.Agility, profile.Vitality, profile.Spirit),
            (character.Strength, character.Agility, character.Vitality, character.Spirit));
        Assert.Equal(ProfileRoots(profile).Select(row => new LegacyOrdinaryStarterTemplate(row.Id, row.Position, 321, 321)), projected.Items);
        ushort[] spells = code switch { 1000 => [1046], 2000 => [1020, 1015, 1040], 4000 => [8002], 5000 => [6001],
            6000 => [10390], 7000 => [11050], 8000 => [12210], 9000 => [15670], 10000 => [1000, 1005], 16000 => [12870], _ => [] };
        Assert.Equal(PlayerSpellSnapshotLoader.ToLegacyBytes(new PlayerSpellSnapshot(spells.Select(id => new PlayerSpellSnapshotEntry(id, 0, 0, 0, 0, 0)).ToArray())), projected.CopySpellsImage());
        Assert.Equal(new byte[4], projected.CopyProficienciesImage()); Assert.Equal(new byte[4], projected.CopyQuestsImage());
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes(CombatGearSnapshotLoader.ToLegacyText(CombatGearSnapshot.Empty)), projected.CopyCombatGearImage());
        Assert.True(projected.IsCurrent()); Assert.Single(projected.Metadata.RuneStorageZeroScore);
    }

    [Fact]
    public void ActualOptionalAbsenceRoundTripsWithoutInventingAProfessionOrScoreZeroRuneRow()
    {
        var catalog = Catalog(6000, optionalRows: false); var captured = Capture(catalog);
        Assert.Null(captured.Metadata.Profession); Assert.Empty(captured.Metadata.RuneStorageZeroScore); Assert.Empty(captured.Metadata.InnerPowerRows);
        using var document = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(captured.CopyMetadata()));
        var decoded = LegacyAuthenticatedCharacterCreationMetadata.Read(document.RootElement);
        var rebuilt = LegacyAuthenticatedCharacterCreationProjection.Rebuild(Account, "FreshOwner", 1008, 10, Prepared, decoded);
        Assert.False(rebuilt.IsCurrent()); Assert.Equal(captured.CopyCharacterImage(), rebuilt.CopyCharacterImage());
        Assert.Equal(captured.CopySpellsImage(), rebuilt.CopySpellsImage()); Assert.Equal(captured.Items, rebuilt.Items);
        var bytes = captured.CopyCharacterImage(); bytes[0] ^= 1; Assert.NotEqual(bytes, captured.CopyCharacterImage());
        var templates = captured.Items; templates[0] = templates[0] with { Durability = 1 }; Assert.NotEqual(templates, captured.Items);
    }

    private static LegacyAuthenticatedCharacterCreationProjection Capture(WorldDataCatalog catalog, ushort selection = 10) =>
        LegacyAuthenticatedCharacterCreationProjection.Capture(catalog, Account, "FreshOwner", 1008, selection, Prepared);

    private static (uint Id, ushort Position)[] ProfileRoots(LegacyFirstCharacterStarterProfile profile) =>
        new (uint Id, ushort Position)[] { (profile.ArmorItemId, 3), (profile.RightWeaponItemId, 4),
            (profile.LeftWeaponItemId, 5), (profile.RingItemId, 6) }.Where(row => row.Id != 0).ToArray();

    private static WorldDataCatalog Catalog(long code, bool optionalRows = true)
    {
        Assert.True(LegacyFirstCharacterStarterProfiles.TryGet(code, out var profile));
        var items = ProfileRoots(profile).Select(row => row.Id).Distinct().Select(id => OwnedEquipmentBehaviorFixture.Item(id, "FreshStarter" + id,
            new Dictionary<int, string> { [ItemType.DurabilityFieldIndex] = "321", [ItemType.ItemHPFieldIndex] = (id % 31 + 7).ToString(CultureInfo.InvariantCulture) })).ToArray();
        ProfessionLevelBenefit[] profession = optionalRows
            ? [ProfessionParser.ParseLevelBenefitRow("owned-profession", 1, "1@@" + code.ToString(CultureInfo.InvariantCulture) + "@@0@@60017@@0@@0@@0@@0@@")]
            : Array.Empty<ProfessionLevelBenefit>();
        RuneStorageAttribute[] runes = optionalRows ? new[]
        {
            RuneParser.ParseStorageAttributeRow("owned-rune-storage", 1, "1@@0@@1@@23@@2@@99@@0@@0@@"),
            RuneParser.ParseStorageAttributeRow("owned-rune-storage", 2, "2@@1@@1@@999@@0@@0@@0@@0@@"),
        } : Array.Empty<RuneStorageAttribute>();
        return NativeCallerWorldDataFixture.Create(items, [], professionLevelBenefits: profession, runeStorageAttributes: runes);
    }
}
