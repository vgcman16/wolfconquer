using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeProficiencyStorage
{
    internal readonly record struct ProgressInstruction(uint Id, ulong Experience, bool Add,
        uint Level = 0, uint InitialExperience = 0, byte PreviousLevel = 0, bool ClearExperience = false);

    internal sealed class ProgressProjection
    {
        private readonly Dictionary<uint, PlayerProficiencySnapshotEntry> rows;
        private readonly List<uint> order;
        private readonly LegacyNativeGameClientRuntime client;
        private ushort[]? gems;
        private int violetIndex;
        private ushort violet;
        private bool hasGemWitness;
        private readonly string? seal;
        private readonly List<byte[]> frames = [];
        internal PlayerProficiencySnapshot FinalSnapshot => new(new CanonicalRows(order.Select(id => rows[id]).ToArray()));
        internal byte[][] Frames => frames.ToArray();

        internal ProgressProjection(LegacyNativeGameClientRuntime client, PlayerProficiencySnapshot source, string? seal)
        {
            this.client = client; this.seal = seal;
            var frozen = FreezeRows(source);
            rows = frozen.ToDictionary(row => row.Id);
            order = frozen.Select(row => row.Id).ToList();
        }

        internal bool InputsAreCurrentPlain() => !hasGemWitness || ReferenceEquals(client.Gems, gems) &&
            gems!.Length > violetIndex && gems[violetIndex] == violet;

        private void CaptureViolet()
        {
            if (hasGemWitness) return;
            gems = client.Gems ?? throw UnavailableProficiencies();
            violetIndex = (byte)((int)LegacyNativeGameItemFlags.Gem.NormalVioletGem / 10);
            if (gems.Length <= violetIndex) throw UnavailableProficiencies();
            violet = gems[violetIndex]; hasGemWitness = true;
        }

        internal void Apply(ProgressInstruction instruction)
        {
            if (instruction.Id == 0) throw UnavailableProficiencies();
            if (instruction.Add) { ApplyAdd(instruction); return; }
            if (instruction.Experience == 0 || !Enum.IsDefined(typeof(LegacyNativeMagicType.WeaponsType),
                    (LegacyNativeMagicType.WeaponsType)instruction.Id)) return;
            if (!rows.TryGetValue(instruction.Id, out var row))
            {
                ApplyAdd(new(instruction.Id, 0, Add: true));
                return; // Preserve the actual first-call initialization rule.
            }
            if (row.Level >= 20) return;
            CaptureViolet();
            UInt128 awarded = instruction.Experience;
            if (row.Level < 5) awarded *= 100;
            else if (row.Level > 13) awarded /= 100;
            awarded /= row.Level + 1;
            UInt128 experience = row.Experience + awarded + awarded * violet / 100;
            uint level = row.Level;
            bool changed = false;
            while (experience >= LegacyNativeProgressionFrames.Threshold(level))
            {
                experience -= LegacyNativeProgressionFrames.Threshold(level);
                level++;
                if (level == 20)
                {
                    rows[row.Id] = row with { Level = level, Experience = 0 };
                    frames.Add(LegacyNativeProgressionFrames.Proficiency(row.Id, level, 0, seal));
                    AddLevelMessage();
                    frames.Add(LegacyNativeProgressionFrames.Experience(row.Id, level, 0, seal));
                    return;
                }
                changed = true;
                AddLevelMessage();
                if (row.PreviousLevel != 0 && level >= row.PreviousLevel / 2 && level < row.PreviousLevel)
                {
                    // The real progression stops at20. A malformed saved
                    // previous level must not jump past that guard.
                    if (row.PreviousLevel > 20) throw UnavailableProficiencies();
                    level = row.PreviousLevel; experience = 0;
                }
            }
            if (experience > uint.MaxValue) throw UnavailableProficiencies();
            uint finalExperience = (uint)experience;
            rows[row.Id] = row with { Level = level, Experience = finalExperience };
            if (changed) frames.Add(LegacyNativeProgressionFrames.Proficiency(row.Id, level, finalExperience, seal));
            frames.Add(LegacyNativeProgressionFrames.Experience(row.Id, level, finalExperience, seal));
        }

        private void ApplyAdd(ProgressInstruction instruction)
        {
            uint experience = instruction.Level == 20 || instruction.ClearExperience ? 0 : instruction.InitialExperience;
            var next = new PlayerProficiencySnapshotEntry(instruction.Id, instruction.Level,
                instruction.PreviousLevel, experience, LegacyNativeProgressionFrames.Threshold(instruction.Level));
            if (!rows.ContainsKey(next.Id)) order.Add(next.Id);
            rows[next.Id] = next;
            frames.Add(LegacyNativeProgressionFrames.Proficiency(next.Id, next.Level, next.Experience, seal));
            frames.Add(LegacyNativeProgressionFrames.Experience(next.Id, next.Level, next.Experience, seal));
        }

        private void AddLevelMessage() => frames.Add(LegacyNativeProgressionFrames.SystemMessage(
            "You increased the weapon proficiency!", seal));
    }
}
