namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeItemLoadOwners
{
    private bool attempted;
    private LegacyNativeDemonExterminatorRuntime? quest;
    private uint questBefore;
    private uint? questUid;

    internal void Prepare(Placement[] placements, uint? questUid)
    {
        RequireLease(); ArgumentNullException.ThrowIfNull(placements);
        if (attempted || published) throw new InvalidOperationException("Item-load preparation is single use.");
        attempted = true; validated = false;
        var rows = (Placement[])placements.Clone();
        CaptureIncoming(rows);
        this.questUid = questUid;
        if (questUid is { } marker)
        {
            if (marker == 0 || !newItems.TryGetValue(marker, out var jar) || jar.Id != 750000) throw Invalid("quest marker has no loaded jar UID");
            quest = client.DemonExterminator ?? throw Invalid("quest owner"); questBefore = quest.ItemUID;
        }
        PrepareMaps(rows);
        var runes = rows.Where(row => row.Destination == Destination.Rune).Select(row => row.Item).ToArray();
        if (runes.Length > 0) runeState.Prepare(runes);
        var relics = rows.Where(row => row.Destination == Destination.Relic).Select(row => new KeyValuePair<uint, LegacyNativeGameItemRuntime>(row.Item.Uid, row.Item)).ToArray();
        if (relics.Length > 0) relicState.Prepare(relics);
        var gears = rows.Where(row => row.Destination == Destination.Gear).Select(row => new KeyValuePair<uint, LegacyNativeGameItemRuntime>(row.Item.Uid, row.Item)).ToArray();
        if (gears.Length > 0) gearState.Prepare(gears);
        prepared = true;
    }

    internal void RequireCurrent()
    {
        RequireLease(); validated = false;
        if (published || !OwnersCurrent() || !MapsCurrent() || !runeState.IsCurrent() || !relicState.IsCurrent() || !gearState.IsCurrent())
            throw Invalid("stale owner or membership");
        foreach (var state in states) if (!state.IsCurrent()) throw Invalid("stale item identity or routing");
        if (quest is not null && (!ReferenceEquals(client.DemonExterminator, quest) || quest.ItemUID != questBefore)) throw Invalid("stale quest owner");
        validated = true;
    }

    internal void PublishPrepared()
    {
        RequireLease();
        if (!prepared || !validated || published) throw new InvalidOperationException("Item-load publication has not been validated.");
        // From the first assignment: no allocation, lookup/comparer, lock acquisition,
        // domain setter, callback, send or persistence operation. Actual owners remain.
        if (preparedInventory is not null) inventory.ClientItems = preparedInventory;
        if (preparedEquipment is not null) equipment.ClientItems = preparedEquipment;
        if (preparedBanks is not null) warehouse.ClientItems = preparedBanks;
        if (preparedWardrobe is not null) wardrobe.Items = preparedWardrobe;
        runeState.PublishUnchecked(); relicState.PublishUnchecked(); gearState.PublishUnchecked();
        if (questUid is { } marker) quest!.ItemUID = marker;
        published = true;
    }
}
