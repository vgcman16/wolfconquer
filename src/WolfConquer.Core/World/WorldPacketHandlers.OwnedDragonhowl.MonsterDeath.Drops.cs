using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Concrete source preparation only. The Store registers each attempt;
    // neither this recipe nor its frames confer floor/death authority.
    internal sealed class DragonhowlDropRecipe
    {
        internal readonly WorldPacketHandlerContext Context;
        internal readonly WorldSession Actor;
        internal readonly LegacyMonsterInstance Monster;
        private readonly Monster family;
        private readonly LegacyNativeAttributeMapProof attributes;
        internal readonly int Roll, Count;
        internal readonly string? Kind;
        private readonly DragonhowlSpecialDrop[] specials;
        private readonly List<DragonhowlDropItemEvidence> items = [];

        private DragonhowlDropRecipe(WorldPacketHandlerContext context, WorldSession actor, LegacyMonsterInstance monster,
            Monster family, LegacyNativeAttributeMapProof attributes, int roll, int count, string? kind, DragonhowlSpecialDrop[] specials)
        { Context = context; Actor = actor; Monster = monster; this.family = family; this.attributes = attributes;
            Roll = roll; Count = checked(count + specials.Length); Kind = kind; this.specials = specials; }

        internal static bool TryPrepare(WorldPacketHandlerContext context, WorldSession actor, LegacyMonsterInstance monster,
            Monster family, out DragonhowlDropRecipe? recipe, out string reason)
        {
            recipe = null; reason = "ownedDragonhowlDropInputsUnavailable";
            if (context.WorldData is null || actor.Character.Uid is <= 0 or > uint.MaxValue || monster.MapId < 0) return false;
            var attributes = LegacyNativeAttributeMapProof.CaptureRaw(actor.Character.Attributes);
            foreach (var field in new[] { RuntimeLegacyMonsterDropRollAttribute, RuntimeLegacyMonsterDropMoneyAmountAttribute,
                RuntimeLegacyMonsterDropItemLevelAttribute, RuntimeLegacyMonsterDropItemSortAttribute, RuntimeLegacyMonsterDropItemQualityAttribute,
                RuntimeLegacyMapSpecialDropRateAttribute, RuntimeLegacyMapSpecialDropItemIndexAttribute,
                RuntimeLegacyBoss4212RareDropAttribute, RuntimeLegacyBoss4171RareDropAttribute, RuntimeLegacyBoss4220RareDropAttribute,
                RuntimeLegacyBoss20055Add711679Attribute, RuntimeLegacyBoss20055Add711188Attribute })
                if (attributes.Fields.Keys.Count(key => key.Equals(field, StringComparison.OrdinalIgnoreCase)) > 1)
                    throw new InvalidDataException("ownedDragonhowlDropInputAmbiguous");
            var roll = ReadLegacyMonsterDeathDropRoll(actor.Character, out var forced);
            if (!TryPrepareDragonhowlSpecialDrops(context, actor, monster, family, roll, forced,
                out var specials, out bool suppressOrdinary, out reason)) return false;
            bool blocked = IsLegacyMonsterDropBlockedMap(monster.MapId);
            // Source draws the count even when this roll has no selected kind,
            // but returns before that draw for a blocked drop map.
            int drawnCount = blocked || suppressOrdinary ? 0 : GetLegacyMonsterDeathDropCount(roll, forced);
            string? kind = blocked || suppressOrdinary ? null : GetLegacyMonsterDeathDropKind(roll);
            int count = kind is null ? 0 : drawnCount;
            recipe = new(context, actor, monster, family, attributes, roll, count, kind, specials);
            foreach (var special in specials)
            {
                var evidence = new DragonhowlDropItemEvidence(context.WorldData, special.ItemId);
                recipe.items.Add(evidence);
                if (evidence.Present == special.SelectionAbsent)
                { recipe = null; reason = "ownedDragonhowlSpecialDropItemUnavailable"; return false; }
            }
            reason = string.Empty; return recipe.InputsAreCurrentPlain();
        }

        internal bool TryCreateAttempt(int index, ushort x, ushort y, WorldSessionStore.OwnedDragonhowlDropQuality quality,
            out DragonhowlDropAttemptData? attempt, out string reason)
        {
            attempt = null; reason = "ownedDragonhowlDropSelectionUnavailable";
            if (index < 0 || index >= Count || !InputsAreCurrentPlain()) return false;
            if (index < specials.Length)
            {
                var special = specials[index];
                if (special.SelectionAbsent)
                {
                    attempt = new(this, Monster.X, Monster.Y, 0, selectionAbsent: true, special: true);
                    reason = string.Empty; return true;
                }
                attempt = new(this, Monster.X, Monster.Y, special.ItemId,
                    special: true, scatterRange: special.ScatterRange);
                reason = string.Empty; return InputsAreCurrentPlain();
            }
            if (Kind == "money")
            { attempt = new(this, x, y, 0); reason = string.Empty; return true; }
            uint id;
            if (Kind is "hp-item" or "mp-item")
            {
                if (!TryReadKnownLegacyDropItemId(Context.WorldData!, family, Kind == "hp-item" ? "drop_hp" : "drop_mp",
                    out id, out reason)) return false;
                if (!RequireItem(id)) return false;
            }
            else
            {
                id = 0;
                foreach (var drop in family.SpecialDrops)
                {
                    if (drop.ItemTypeId <= 0) continue;
                    var evidence = new DragonhowlDropItemEvidence(Context.WorldData!, checked((uint)drop.ItemTypeId));
                    items.Add(evidence);
                    if (evidence.Present) { id = checked((uint)drop.ItemTypeId); break; }
                }
                if (id == 0)
                {
                    bool selected = TryGenerateLegacyMonsterDeathDropItemId(quality, Actor.Character, Context.WorldData!, family,
                        out var generated, out var candidate, out reason);
                    if (candidate != 0) items.Add(new DragonhowlDropItemEvidence(Context.WorldData!, candidate));
                    if (!selected)
                    {
                        attempt = new(this, x, y, 0, selectionAbsent: true);
                        reason = string.Empty; return InputsAreCurrentPlain();
                    }
                    id = generated.ItemId;
                }
            }
            // Selection is DATA. The actual killed phase draws the item UID,
            // including a selected item whose planned ground result is absent.
            attempt = new(this, x, y, id);
            reason = string.Empty; return InputsAreCurrentPlain();
        }

        private bool RequireItem(uint id)
        {
            var evidence = new DragonhowlDropItemEvidence(Context.WorldData!, id); items.Add(evidence);
            return evidence.Present && evidence.IsCurrentPlain();
        }

        internal bool InputsAreCurrentPlain() => attributes.IsCurrentPlain() && items.All(row => row.IsCurrentPlain());
        internal bool MatchesActor(WorldSession actual)
        {
            if (!InputsAreCurrentPlain() || (actual with { Character = Actor.Character }) != Actor ||
                (actual.Character with { Attributes = Actor.Character.Attributes }) != Actor.Character) return false;
            var current = LegacyNativeAttributeMapProof.CaptureRaw(actual.Character.Attributes);
            return ReferenceEquals(current.CapturedComparer, attributes.CapturedComparer) &&
                current.Fields.SequenceEqual(attributes.Fields) && current.IsCurrentPlain();
        }

        internal sealed class DragonhowlDropAttemptData
        {
            internal readonly DragonhowlDropRecipe Recipe;
            internal readonly ushort OriginX, OriginY;
            internal readonly bool SelectionAbsent;
            internal readonly bool Special;
            internal readonly byte ScatterRange;
            internal bool IsMoney => !Special && Recipe.Kind == "money";
            private uint itemId, amount;
            private bool planned, hasGround, completed;
            private byte[]? frame;
            internal DragonhowlDropAttemptData(DragonhowlDropRecipe recipe, ushort x, ushort y, uint itemId,
                bool selectionAbsent = false, bool special = false, byte scatterRange = 0)
            { Recipe = recipe; OriginX = x; OriginY = y; this.itemId = itemId; SelectionAbsent = selectionAbsent;
                Special = special; ScatterRange = scatterRange; }

            internal bool TryPreparePayload(bool placed)
            {
                if (planned || completed || SelectionAbsent || !Recipe.InputsAreCurrentPlain()) return false;
                planned = true; hasGround = placed;
                if (IsMoney && placed)
                {
                    // Draw once only for the selected grounded DATA outcome.
                    // No floor identity, reservation or packet is issued here.
                    amount = ReadLegacyMonsterDeathDropMoneyAmount(Recipe.Actor.Character);
                    itemId = GetLegacyMoneyItemId(amount);
                    if (amount == 0 || !Recipe.RequireItem(itemId)) return false;
                }
                return (IsMoney && !placed || itemId != 0) && Recipe.InputsAreCurrentPlain();
            }

            internal bool TryMaterialize(WorldSessionStore.OwnedDragonhowlGroundAttempt purpose,
                FloorItemRuntimeStateStore.PreparedDragonhowlGround ground, ushort x, ushort y,
                out FloorItemRuntimeEntry? entry)
            {
                entry = null;
                if (!planned || completed || SelectionAbsent || !Recipe.InputsAreCurrentPlain() ||
                    !ground.CoversPayload(this, hasGround) || !purpose.TryReadCommit(ground, out _)) return false;
                completed = true;
                if (IsMoney && !hasGround) return true;
                // A consumed monotonic UID is never rewound, including a source
                // item no-ground outcome or a later real publication failure.
                uint uid = NextLegacyFloorItemUid();
                if (!hasGround) return true;
                var packet = LegacyFloorItemPacket.FromMonsterDrop(itemId, x, y, Recipe.Actor.Character, uid);
                var prepared = IsMoney
                    ? FloorItemRuntimeEntry.ForMoney(Recipe.Monster.MapId, packet, amount, ownerOnly: true, dynamicId: Recipe.Monster.DynamicId)
                    : FloorItemRuntimeEntry.ForItem(Recipe.Monster.MapId, packet, ownerOnly: true, dynamicId: Recipe.Monster.DynamicId);
                frame = packet.ToFrameBytes(); // FINAL real UID bytes, before live floor publication.
                entry = prepared;
                return Recipe.InputsAreCurrentPlain();
            }

            internal byte[] CopyFrame() => frame?.ToArray() ?? throw new InvalidOperationException("ownedDragonhowlDropNotPrepared");
        }
    }

    // Only item existence/index membership is used by these ordinary drop
    // packets. Unused item fields/rows are not normalized or turned into owners.
    private sealed class DragonhowlDropItemEvidence
    {
        private readonly WorldDataCatalog catalog;
        private readonly uint id;
        private readonly IReadOnlyList<ItemType> raw;
        private readonly Dictionary<int, IReadOnlyList<ItemType>> index;
        private readonly IReadOnlyList<ItemType>? group;
        private readonly ItemType[] selected;
        internal bool Present => selected.Length != 0;
        internal DragonhowlDropItemEvidence(WorldDataCatalog catalog, uint id)
        {
            this.catalog = catalog; this.id = id; raw = catalog.Items;
            index = catalog.ItemsById as Dictionary<int, IReadOnlyList<ItemType>> ?? throw new InvalidDataException("ownedDragonhowlDropIndexUnsupported");
            if (id == 0 || id > int.MaxValue || !IsPlain(raw) || !ReferenceEquals(index.Comparer, EqualityComparer<int>.Default))
                throw new InvalidDataException("ownedDragonhowlDropItemInvalid");
            selected = raw.Where(row => row is not null && row.Id == (int)id).ToArray();
            index.TryGetValue((int)id, out group);
            if (!IsCurrentPlain()) throw new InvalidDataException("ownedDragonhowlDropItemIndexChanged");
        }
        internal bool IsCurrentPlain()
        {
            if (!ReferenceEquals(catalog.Items, raw) || !ReferenceEquals(catalog.ItemsById, index) || !IsPlain(raw)) return false;
            var current = raw.Where(row => row is not null && row.Id == (int)id).ToArray();
            if (!current.SequenceEqual(selected, ReferenceEqualityComparer.Instance)) return false;
            if (group is null) return selected.Length == 0 && !index.ContainsKey((int)id);
            return index.TryGetValue((int)id, out var actual) && ReferenceEquals(group, actual) && IsPlain(group) &&
                group.SequenceEqual(selected, ReferenceEqualityComparer.Instance);
        }
        private static bool IsPlain(IReadOnlyList<ItemType> rows) => rows.GetType() == typeof(ItemType[]) || rows.GetType() == typeof(List<ItemType>);
    }
}
