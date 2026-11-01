using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOwnedPhysicalMetadataWitness
{
    private sealed class Row : ICurrent
    {
        private readonly object source, copy;
        private readonly List<ICurrent> children = [];
        internal Row(object source)
        {
            this.source = source;
            copy = source switch
            {
                ItemType row => row with { }, ItemAddBonus row => row with { }, RefineryMaterial row => row with { },
                FateExperience row => row with { }, ItemRefineEffect row => row with { }, CombatGear row => row with { },
                DaoqiDictType row => row with { }, CollectionRewardTier row => row with { }, GouyuType row => row with { },
                MagicType row => row with { }, SpecialEffectAttribute row => row with { }, BeastAttribute row => row with { },
                ProfessionLevelBenefit row => row with { }, JiangHuAttribute row => row with { }, HundredWeapon row => row with { },
                SubProfessionEntry row => row with { }, RuneStorageAttribute row => row with { }, NeiGongSecret row => row with { },
                NeiGongType row => row with { }, InnerPowerRankEntry row => row with { }, CombatGearRankEntry row => row with { },
                _ => throw Invalid(),
            };
            CaptureChildren(source);
        }

        private void CaptureChildren(object source)
        {
            switch (source)
            {
                case ItemType row: Add(row.Fields); break;
                case ItemAddBonus row: Add(row.Values); break;
                case RefineryMaterial row: Add(row.Fields); Add(row.Parameters); Add(row.TailValues); break;
                case FateExperience row: Add(row.Fields); Add(row.Values); break;
                case ItemRefineEffect row: Add(row.Fields); Add(row.Values); break;
                case CombatGear row: Add(row.Fields); Add(row.MagicTypeIds); break;
                case DaoqiDictType row: Add(row.Fields); Add(row.Values); break;
                case CollectionRewardTier row: Add(row.Fields); break;
                case GouyuType row: Add(row.Fields); Add(row.Values); break;
                case MagicType row: Add(row.Fields); break;
                case SpecialEffectAttribute row: Add(row.Fields); Add(row.Values); break;
                case BeastAttribute row: Add(row.Fields); Add(row.Parameters); break;
                case ProfessionLevelBenefit row: Add(row.Fields); Add(row.BonusValues); break;
                case JiangHuAttribute row: Add(row.Fields); break;
                case HundredWeapon row: Add(row.Fields); Add(row.Values); break;
                case SubProfessionEntry row: Add(row.RequiredProfessions); Add(row.Entries); break;
                case RuneStorageAttribute row: Add(row.Fields); Add(row.Bonuses); break;
                case NeiGongSecret row:
                    Add(row.AttributeTypes); Add(row.AttributeValues); children.Add(new AttributeRows(row.Fields)); break;
                case NeiGongType row:
                    Add(row.AttributeTypes); Add(row.AttributeValues); Add(row.CultureValues);
                    children.Add(new AttributeRows(row.Fields)); break;
                case InnerPowerRankEntry row: Add(row.Fields); Add(row.RankingFields); break;
                case CombatGearRankEntry row: Add(row.Fields); Add(row.RankingFields); break;
                default: throw Invalid();
            }
        }

        private void Add<T>(IReadOnlyList<T> values) => children.Add(new PlainList<T>(values));
        public bool IsCurrentPlain() => source.Equals(copy) && children.All(child => child.IsCurrentPlain());
    }

    private sealed class AttributeRows : ICurrent
    {
        private readonly LegacyNativeAttributeMapProof fields;
        internal AttributeRows(IReadOnlyDictionary<string, string> source) => fields = new(source);
        public bool IsCurrentPlain() => fields.IsCurrentPlain();
    }
}
