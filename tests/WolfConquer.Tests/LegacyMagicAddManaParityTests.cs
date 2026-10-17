using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMagicAddManaParityTests
{
    [Fact]
    public void ClassifyMeditationAddManaAsModeledSelfManaSupport()
    {
        var spell = Spell(1_195, sort: 20, power: 300);

        var info = LegacyMagicSupportRuntime.Classify(spell);

        Assert.True(info.Supported);
        Assert.Equal(LegacyMagicSupportModelingStatus.Modeled, info.Status);
        Assert.NotEqual(LegacyMagicSupportKind.Unsupported, info.Kind);
        Assert.Contains("AddMana.cs", info.SourceEvidence);
        Assert.Contains("1195", info.Reason);
        Assert.Contains("mana", info.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("self", info.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryCreateSupportSpellModelsLegacyLevelTwoMeditationDamageOverride()
    {
        Assert.True(LegacyMagicSupportRuntime.TryCreateSupportSpell(
            CreateMagicRow(rowId: 1_196, typeId: 1_195, sort: 20, name: "Meditation", level: 2, damage: 300),
            out var spell));

        Assert.Equal(1_195, spell.Id);
        Assert.Equal((LegacyMagicSort)20, spell.Sort);
        Assert.Equal(300U, spell.RawDamage);
        Assert.Equal(1_020U, spell.Power);
    }

    private static LegacyMagicSupportSpell Spell(
        ushort id,
        int sort,
        uint power,
        byte level = 0)
    {
        return new LegacyMagicSupportSpell(
            id,
            (LegacyMagicSort)sort,
            $"Spell{id}",
            level,
            UseMana: 0,
            RawDamage: power,
            power,
            Rate: 100,
            Duration: 0,
            Experience: 0,
            MaxTargets: 0,
            Range: 0,
            Status: 0,
            NeedLevel: 0,
            WeaponType: 0,
            UseStamina: 0,
            CoolDown: 0,
            ColdTime: 0);
    }

    private static MagicType CreateMagicRow(
        int rowId,
        ushort typeId,
        byte sort,
        string name,
        byte level = 0,
        uint damage = 0)
    {
        var fields = Enumerable.Repeat("0", 53).ToArray();
        fields[0] = rowId.ToString();
        fields[1] = typeId.ToString();
        fields[2] = sort.ToString();
        fields[3] = name;
        fields[8] = level.ToString();
        fields[10] = damage.ToString();
        return new MagicType(rowId, typeId, name, fields);
    }
}
