using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeInteractionContractTests
{
    // Actual outputs from pinned protobuf-net2.0.0.668 and the complete thirteen-field
    // source-attributed contract, not original server methods or a generic Core serializer.
    [Theory]
    [InlineData("interaction-default", "200028003800400068007800880100A80100C80100D00100")]
    [InlineData("interaction-max", "20FFFFFFFF0F28FFFFFFFF0F38FFFF0340FFFF0368FFFF0378FFFFFFFF078001FFFFFFFFFFFFFFFFFF018801FFFFFFFF079001FFFF039801FFFF03A801FFFFFFFF0FC801FFFFFFFF0FD001FFFFFFFF0F")]
    [InlineData("interaction-negative-one", "2000280038004000680078FFFFFFFFFFFFFFFFFF018801FFFFFFFFFFFFFFFFFF01A80100C80100D00100")]
    [InlineData("interaction-mins", "200028003800400068007880808080F8FFFFFFFF01880180808080F8FFFFFFFF01A80100C80100D00100")]
    [InlineData("interaction-options-only", "2000280038004000680078008001FFFFFFFFFFFFFFFFFF018801009001FFFF039801FFFF03A80100C80100D00100")]
    [InlineData("interaction-optional-zero", "201128003800400068007800880100A80100C80100D00100")]
    [InlineData("interaction-required-zero", "200028003800400068007800800101880100900101980101A80100C80100D00100")]
    [InlineData("interaction-sparse-tags", "2004280538074008680D780F800110880111900112980113A80115C80119D0011A")]
    [InlineData("interaction-mixed-sign", "2000280038004000680078FFFFFFFF07880180808080F8FFFFFFFF01A80100C80100D00100")]
    [InlineData("interaction-null-outer", "")]
    public void FullConcreteContractMatchesThePreservedSerializerBytes(string name, string expected)
    {
        var query = Query(name);
        var before = query is null ? null : LegacyNativeInteractQuery.ShallowCopy(query);
        using var output = new MemoryStream();
        typeof(LegacyNativeMsgAttackPacket).GetMethod("SerializeTypedInteractQuery", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [output, query]);
        Assert.Equal(expected, Convert.ToHexString(output.ToArray()));
        if (query != null)
            foreach (var field in typeof(LegacyNativeInteractQuery).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Assert.Equal(field.GetValue(before), field.GetValue(query));
    }

    private static LegacyNativeInteractQuery? Query(string name) => name switch
    {
        "interaction-default" => new(),
        "interaction-max" => new()
        {
            UID = uint.MaxValue, OpponentUID = uint.MaxValue, X = ushort.MaxValue, Y = ushort.MaxValue,
            AtkType = ushort.MaxValue, Damage = int.MaxValue, CombatGear = ulong.MaxValue,
            KilledMonster = int.MaxValue, SpellLevel = ushort.MaxValue, SpellID = ushort.MaxValue,
            ResponseDamage = uint.MaxValue, Effect = uint.MaxValue, Unknown = uint.MaxValue
        },
        "interaction-negative-one" => new() { Damage = -1, KilledMonster = -1 },
        "interaction-mins" => new() { Damage = int.MinValue, KilledMonster = int.MinValue },
        "interaction-options-only" => new() { CombatGear = ulong.MaxValue, SpellLevel = ushort.MaxValue, SpellID = ushort.MaxValue },
        "interaction-optional-zero" => new() { UID = 17 },
        "interaction-required-zero" => new() { CombatGear = 1, SpellLevel = 1, SpellID = 1 },
        "interaction-sparse-tags" => new()
        {
            UID = 4, OpponentUID = 5, X = 7, Y = 8, AtkType = 13, Damage = 15, CombatGear = 16,
            KilledMonster = 17, SpellLevel = 18, SpellID = 19, ResponseDamage = 21, Effect = 25, Unknown = 26
        },
        "interaction-mixed-sign" => new() { Damage = int.MaxValue, KilledMonster = int.MinValue },
        "interaction-null-outer" => null,
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };
}
