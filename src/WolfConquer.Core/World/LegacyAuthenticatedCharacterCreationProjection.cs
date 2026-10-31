using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Data projection only. The actual authenticated producer owns birth permission,
// registry materialization, durable creation and eventual native publication.
internal sealed partial class LegacyAuthenticatedCharacterCreationProjection
{
    private readonly LegacyAuthenticatedCharacterCreationCatalog? catalog;
    private readonly LegacyAuthenticatedCharacterCreationMetadata metadata;
    private readonly LegacyOrdinaryStarterTemplate[] items;
    private readonly byte[] character, spells, proficiencies, quests, combatGear;
    internal uint Account { get; }
    internal string Name { get; }
    internal ushort Body { get; }
    internal ushort ClassSelection { get; }
    internal DateTime PreparedAt { get; }
    internal LegacyOrdinaryStarterTemplate[] Items => items.ToArray();
    internal LegacyAuthenticatedCharacterCreationMetadata Metadata => metadata.Copy();
    internal LegacyAuthenticatedCharacterCreationMetadata CopyMetadata() => metadata.Copy();
    internal byte[] CopyCharacterImage() => character.ToArray();
    internal byte[] CopySpellsImage() => spells.ToArray();
    internal byte[] CopyProficienciesImage() => proficiencies.ToArray();
    internal byte[] CopyQuestsImage() => quests.ToArray();
    internal byte[] CopyCombatGearImage() => combatGear.ToArray();
    internal bool IsCurrent() => catalog?.IsCurrent() == true;

    internal static LegacyAuthenticatedCharacterCreationProjection Capture(WorldDataCatalog catalog, uint account,
        string name, ushort body, ushort classSelection, DateTime preparedAt)
    {
        long code = ClassCode(classSelection);
        RequireIdentity(account, name, body);
        if (!LegacyFirstCharacterStarterProfiles.TryGet(code, out var profile)) throw Invalid();
        var actual = new LegacyAuthenticatedCharacterCreationCatalog(catalog, account, code, ProfileItems(profile).Select(row => row.Id).Distinct().ToArray());
        var result = new LegacyAuthenticatedCharacterCreationProjection(account, name, body, classSelection, preparedAt, actual.Metadata, actual);
        if (!result.IsCurrent()) throw Invalid();
        return result;
    }

    internal static LegacyAuthenticatedCharacterCreationProjection Rebuild(uint account, string name, ushort body,
        ushort classSelection, DateTime preparedAt, LegacyAuthenticatedCharacterCreationMetadata metadata) =>
        new(account, name, body, classSelection, preparedAt, metadata, null);

    private LegacyAuthenticatedCharacterCreationProjection(uint account, string name, ushort body, ushort classSelection,
        DateTime preparedAt, LegacyAuthenticatedCharacterCreationMetadata metadata, LegacyAuthenticatedCharacterCreationCatalog? catalog)
    {
        RequireIdentity(account, name, body); ArgumentNullException.ThrowIfNull(metadata);
        Account = account; Name = name; Body = body; ClassSelection = classSelection;
        PreparedAt = DateTime.SpecifyKind(preparedAt, DateTimeKind.Unspecified);
        this.catalog = catalog; this.metadata = metadata.Copy(); this.metadata.Validate();
        long code = ClassCode(classSelection);
        if (!LegacyFirstCharacterStarterProfiles.TryGet(code, out var profile)) throw Invalid();
        var required = ProfileItems(profile);
        if (!this.metadata.Items.Select(row => row.ItemId).SequenceEqual(required.Select(row => row.Id).Distinct().Order())) throw Invalid();
        items = required.Select(row =>
        {
            var dependency = this.metadata.Items.Single(item => item.ItemId == row.Id);
            var durability = checked((ushort)LegacyAuthenticatedCharacterCreationMetadata.ReadUInt(dependency.RawDurability));
            return new LegacyOrdinaryStarterTemplate(row.Id, row.Position, durability, durability);
        }).ToArray();
        uint baseHp = LegacyFirstCharacterStarterProfiles.CalculateHitpoints(code, profile.Strength, profile.Agility, profile.Vitality, profile.Spirit);
        uint hp = this.metadata.Hitpoints(baseHp, code, items);
        character = CharacterImage(account, name, body, code, PreparedAt, profile, hp);
        spells = SpellImage(code);
        proficiencies = PlayerProficiencySnapshotLoader.ToLegacyBytes(PlayerProficiencySnapshot.Empty);
        quests = PlayerQuestSnapshotLoader.Serialize(PlayerQuestSnapshot.Empty);
        combatGear = System.Text.Encoding.UTF8.GetBytes(CombatGearSnapshotLoader.ToLegacyText(CombatGearSnapshot.Empty));
    }

    private static (uint Id, ushort Position)[] ProfileItems(LegacyFirstCharacterStarterProfile profile) =>
        new (uint Id, ushort Position)[] { (profile.ArmorItemId, 3), (profile.RightWeaponItemId, 4),
            (profile.LeftWeaponItemId, 5), (profile.RingItemId, 6) }.Where(row => row.Id != 0).ToArray();
    private static long ClassCode(ushort selection) => selection switch
    {
        0 or 1 => 10000, 2 or 3 => 1000, 4 or 5 => 4000, 6 or 7 => 2000, 8 or 9 => 5000,
        10 or 11 => 6000, 12 or 13 => 7000, 14 or 15 => 8000, >= 16 and <= 19 => 16000, 20 or 21 => 9000,
        _ => throw Invalid(),
    };
    private static InvalidDataException Invalid() => new("Authenticated creation projection lacks complete canonical fresh inputs.");
}
