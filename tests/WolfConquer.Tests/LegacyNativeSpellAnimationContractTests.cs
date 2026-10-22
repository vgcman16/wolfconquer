using System.Reflection;
using WolfConquer.Core.World;
using Animation = WolfConquer.Core.World.LegacyNativeMsgSpellAnimation;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeSpellAnimationContractTests
{
    // Literal outputs from the preserved protobuf-net2.0.0.668, full source-shaped
    // attributed contracts, statically object-typed Serialize; not original server execution.
    [Theory]
    [InlineData("animation-default", "080028003000380040004800")]
    [InlineData("animation-max", "08FFFFFFFF0F10FFFFFFFF0F18FFFF0320FFFF0328FFFF0330FF0138FFFF0340FF0148FF01522808FFFFFFFF0F10FFFFFFFF0F180120FFFFFFFF0F30FFFF0338FFFF0340FFFFFFFF0F48FFFFFFFF0F")]
    [InlineData("animation-optional-zero", "081128003000380040004800")]
    [InlineData("animation-optionals", "081110131817201D28003000380040004800")]
    [InlineData("animation-required-zero", "080028003000380040004800")]
    [InlineData("animation-null-array", "081328003000380040004800")]
    [InlineData("animation-empty-array", "081328003000380040004800")]
    [InlineData("animation-null-entries", "0817280030003800400048005210081D1000180020003000380040004800")]
    [InlineData("animation-target-required-zeros", "081F28003000380040004800521008001000180020003000380040004800")]
    [InlineData("animation-multi", "0825282A30063807400848095210080210031801200A3004380540064807521408081009180020EFFDB6F50D300A380B400C480D")]
    [InlineData("animation-null-outer", "")]
    public void CompleteConcreteContractMatchesActualPinnedPayloads(string name, string expected)
    {
        var value = Query(name);
        var targets = value?.Targets;
        using var output = new MemoryStream();
        Serialize(output, value);
        Assert.Equal(expected, Convert.ToHexString(output.ToArray()));
        if (value != null) Assert.Same(targets, value.Targets);
        if (name == "animation-null-entries")
        {
            Assert.Equal(3, value!.Targets.Length);
            Assert.Null(value.Targets[0]);
            Assert.Null(value.Targets[2]);
        }
    }

    [Theory]
    [InlineData("default", "08001000180020003000380040004800")]
    [InlineData("max", "08FFFFFFFF0F10FFFFFFFF0F180120FFFFFFFF0F30FFFF0338FFFF0340FFFFFFFF0F48FFFFFFFF0F")]
    [InlineData("unknown-flag", "08001000180020EFFDB6F50D3000380040004800")]
    public void EveryTargetFieldMatchesItsPinnedStandaloneBytesWithinTheRepeatedEnvelope(string name, string expectedTarget)
    {
        var target = name switch
        {
            "max" => MaxTarget(),
            "unknown-flag" => new Animation.Target { Flag = 0xDEADBEEF },
            _ => new Animation.Target()
        };
        using var output = new MemoryStream();
        Serialize(output, new() { Targets = [target] });
        byte[] nested = Convert.FromHexString(expectedTarget);
        byte[] expected = Convert.FromHexString("08002800300038004000480052")
            .Concat(new[] { (byte)nested.Length }).Concat(nested).ToArray();
        Assert.Equal(expected, output.ToArray());
    }

    [Fact]
    public void ContractStorageKeepsAllMutableFieldTypesAndNullArrayDefault()
    {
        Assert.Equal(10, typeof(Animation.SpellUseProto).GetFields(BindingFlags.Public | BindingFlags.Instance).Length);
        Assert.Equal(8, typeof(Animation.Target).GetFields(BindingFlags.Public | BindingFlags.Instance).Length);
        Assert.Equal(typeof(uint), typeof(Animation.Target).GetField("Flag")!.FieldType);
        Assert.Equal(typeof(byte), typeof(Animation.SpellUseProto).GetField("SpellLevel")!.FieldType);
        Assert.Equal(typeof(ushort), typeof(Animation.Target).GetField("newX")!.FieldType);
        Assert.Null(new Animation.SpellUseProto().Targets);
        Assert.All(typeof(Animation.Target).GetFields(BindingFlags.Public | BindingFlags.Instance), field => Assert.False(field.IsInitOnly));
    }

    private static void Serialize(Stream output, Animation.SpellUseProto? value) =>
        typeof(Animation).GetMethod("SerializeTypedSpellUseProto", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [output, value]);

    private static Animation.SpellUseProto? Query(string name) => name switch
    {
        "animation-default" => new(),
        "animation-max" => new()
        {
            Attacker = uint.MaxValue, Attacked = uint.MaxValue, X = ushort.MaxValue, Y = ushort.MaxValue,
            SpellID = ushort.MaxValue, SpellLevel = byte.MaxValue, LevelHu = byte.MaxValue,
            NextSpell = ushort.MaxValue, SpellEffect = byte.MaxValue, Targets = [MaxTarget()]
        },
        "animation-optional-zero" => new() { Attacker = 17 },
        "animation-optionals" => new() { Attacker = 17, Attacked = 19, X = 23, Y = 29 },
        "animation-required-zero" => new() { Targets = [] },
        "animation-null-array" => new() { Attacker = 19 },
        "animation-empty-array" => new() { Attacker = 19, Targets = [] },
        "animation-null-entries" => new() { Attacker = 23, Targets = [null!, new() { UID = 29 }, null!] },
        "animation-target-required-zeros" => new() { Attacker = 31, Targets = [new()] },
        "animation-multi" => new()
        {
            Attacker = 37, SpellID = 42, SpellLevel = 6, LevelHu = 8, NextSpell = 7, SpellEffect = 9,
            Targets = [new() { UID = 2, Damage = 3, Hit = true, Flag = 10, newX = 4, newY = 5, Unknown = 6, TailedBeast = 7 },
                new() { UID = 8, Damage = 9, Hit = false, Flag = 0xDEADBEEF, newX = 10, newY = 11, Unknown = 12, TailedBeast = 13 }]
        },
        "animation-null-outer" => null,
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    private static Animation.Target MaxTarget() => new()
    {
        UID = uint.MaxValue, Damage = uint.MaxValue, Hit = true, Flag = uint.MaxValue,
        newX = ushort.MaxValue, newY = ushort.MaxValue, Unknown = uint.MaxValue, TailedBeast = uint.MaxValue
    };
}
