using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyRuneCastEligibility
{
    private sealed class RuneRequirementWitness
    {
        private readonly ItemType item;
        private readonly IReadOnlyList<string> source;
        private readonly string[]? array;
        private readonly List<string>? list;
        private readonly string job, level, spell;
        private readonly int count;
        internal uint Class { get; }
        internal uint Level { get; }
        internal uint Encoded { get; }
        internal RuneRequirementWitness(ItemType item)
        {
            this.item = item; source = item.Fields; array = source as string[]; list = source as List<string>;
            if (array is null && list is null) throw new InvalidOperationException("Rune requirements need actual plain fields.");
            count = array?.Length ?? list!.Count;
            if (count <= ItemType.RuneSpellIdFieldIndex) throw new InvalidOperationException("Rune requirements are incomplete.");
            job = Read(ItemType.RequiredClassFieldIndex); level = Read(ItemType.RequiredLevelFieldIndex); spell = Read(ItemType.RuneSpellIdFieldIndex);
            if (!uint.TryParse(job, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedJob) ||
                !uint.TryParse(level, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLevel) ||
                !uint.TryParse(spell, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSpell))
                throw new InvalidOperationException("Rune requirements are malformed.");
            Class = parsedJob; Level = parsedLevel; Encoded = parsedSpell;
        }
        private string Read(int index) => array is not null ? array[index] : list![index];
        internal bool IsCurrentPlain() => ReferenceEquals(item.Fields, source) && (array?.Length ?? list!.Count) == count &&
            Read(ItemType.RequiredClassFieldIndex) == job && Read(ItemType.RequiredLevelFieldIndex) == level && Read(ItemType.RuneSpellIdFieldIndex) == spell;
    }
    private bool RequirementsCurrentPlain()
    {
        foreach (var requirement in requirements) if (!requirement.IsCurrentPlain()) return false;
        return true;
    }
}
