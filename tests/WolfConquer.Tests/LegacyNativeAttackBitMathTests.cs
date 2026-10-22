using System.Numerics;
using System.Reflection;
using WolfConquer.Core.World;
using Attack = WolfConquer.Core.World.LegacyNativeMsgAttackPacket;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeAttackBitMathTests
{
    private static readonly MethodInfo ShortExchange = typeof(Attack).GetMethod("ExchangeShortBits", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo Decode = typeof(Attack).GetMethod("DecodeMagicAttack", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Fact]
    public void FoldDoesNotMaskTheLowerArgumentAndTruncatesShiftedHighBits()
    {
        foreach (var (lower, upper, expected) in new (int, int, int)[]
        {
            (0, 0, 0), (0x1234, 0x5678, 0x56781234), (-1, 0x1234, -1),
            (int.MinValue, 0, int.MinValue), (0x12345678, 0, 0x12345678),
            (0, 0x12345678, 0x56780000), (0, int.MinValue, 0), (0, -1, -65536)
        })
            Assert.Equal(expected, Attack.BitFold32(lower, upper));
    }

    [Fact]
    public void Unfold32KeepsUnsignedLow16ButArithmeticSignedUpper16()
    {
        foreach (var (value, lower, upper) in new (int, int, int)[]
        {
            (0, 0, 0), (-1, 65535, -1), (int.MinValue, 0, -32768),
            (int.MaxValue, 65535, 32767), (0x12345678, 0x5678, 0x1234), (unchecked((int)0xFEDCBA98), 0xBA98, -292)
        })
        {
            Attack.BitUnfold32(value, out var actualLower, out var actualUpper);
            Assert.Equal(lower, actualLower);
            Assert.Equal(upper, actualUpper);
            Assert.Equal(value, Attack.BitFold32(actualLower, actualUpper));
        }
    }

    [Fact]
    public void Unfold64CastsBothHalvesToSignedInt32WithoutNormalization()
    {
        foreach (var (value, lower, upper) in new (ulong, int, int)[]
        {
            (0, 0, 0), (ulong.MaxValue, -1, -1), (0x8000000000000000, 0, int.MinValue),
            (0x0000000080000000, int.MinValue, 0), (0xFEDCBA9876543210, 0x76543210, unchecked((int)0xFEDCBA98))
        })
        {
            Attack.BitUnfold64(value, out var actualLower, out var actualUpper);
            Assert.Equal(lower, actualLower);
            Assert.Equal(upper, actualUpper);
        }
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(0x89ABCDEFu)]
    [InlineData(uint.MaxValue)]
    public void LongExchangeRetainsCSharpMaskedShiftCountsIncludingNegativeAndBeyond32(uint value)
    {
        foreach (int shift in Shifts())
            Assert.Equal(BitOperations.RotateRight(value, shift), Attack.ExchangeLongBits(value, shift));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(0x89ABCDEFu)]
    [InlineData(uint.MaxValue)]
    public void ShortExchangeMasksDataButKeeps32BitShiftRulesRatherThanNormalizingModulo16(uint value)
    {
        foreach (int shift in Shifts())
        {
            uint expected = 0;
            int normalized = shift & 31;
            if (normalized <= 16)
            {
                // Independent bit-position permutation, not the native two-shift expression.
                for (int bit = 0; bit < 16; bit++)
                    if ((value & (1u << bit)) != 0)
                        expected |= 1u << ((bit - normalized + 16) % 16);
            }
            Assert.Equal(expected, (uint)ShortExchange.Invoke(null, [value, shift])!);
        }
        Assert.Equal(0u, (uint)ShortExchange.Invoke(null, [value, 17])!);
        Assert.Equal(0u, (uint)ShortExchange.Invoke(null, [value, -1])!);
    }

    // Literal source-calculated arithmetic vectors; these are not claimed pinned-DLL observations.
    [Theory]
    [InlineData(0, 924961845, 1305519464u, 16289, 44498)]
    [InlineData(1, -1222534579, 1297733216u, 59864, 65450)]
    [InlineData(2, -924953654, 2989439639u, 16479, 18989)]
    [InlineData(3, 622188923, 1305560424u, 48967, 3523)]
    public void EncodeMutatesDamageOpponentAndCoordinatesButNotSpellIdOrOtherFields(
        int index, int damage, uint opponent, int x, int y)
    {
        var query = Source(index);
        var holder = new Attack.AttackObj { Attack = query };
        var before = Snapshot(query, "Damage", "OpponentUID", "X", "Y");
        ushort spell = query.SpellID;

        Attack.EncodeMagicAttack(query);

        Assert.Equal(damage, query.Damage);
        Assert.Equal(opponent, query.OpponentUID);
        Assert.Equal((ushort)x, query.X);
        Assert.Equal((ushort)y, query.Y);
        Assert.Equal(spell, query.SpellID);
        Assert.Equal(before, Snapshot(query, "Damage", "OpponentUID", "X", "Y"));
        Assert.Same(query, holder.Attack);
    }

    [Theory]
    [InlineData(0, 40874, 3938310525u, 15038, 43605)]
    [InlineData(1, 56207, 2918577247u, 60341, 29515)]
    [InlineData(2, 63857, 3938310525u, 15038, 43605)]
    [InlineData(3, 37335, 3947747724u, 15034, 43701)]
    public void DecodeWithNullUnusedUserMutatesSpellIdNotDamageAndPreservesRawOtherFields(
        int index, int spell, uint opponent, int x, int y)
    {
        var query = Source(index);
        var holder = new Attack.AttackObj { Attack = query };
        var before = Snapshot(query, "SpellID", "OpponentUID", "X", "Y");
        int damage = query.Damage;

        Decode.Invoke(null, [query, null]); // The genuine private method never reads its GameClient parameter.

        Assert.Equal((ushort)spell, query.SpellID);
        Assert.Equal(opponent, query.OpponentUID);
        Assert.Equal((ushort)x, query.X);
        Assert.Equal((ushort)y, query.Y);
        Assert.Equal(damage, query.Damage);
        Assert.Equal(before, Snapshot(query, "SpellID", "OpponentUID", "X", "Y"));
        Assert.Same(query, holder.Attack);
    }

    [Fact]
    public void NullQueriesFaultAtTheFirstFieldReadWithoutAddingGuardsOrReadingUnusedUser()
    {
        Assert.Throws<NullReferenceException>(() => Attack.EncodeMagicAttack(null!));
        var error = Assert.Throws<TargetInvocationException>(() => Decode.Invoke(null, [null, null]));
        Assert.IsType<NullReferenceException>(error.InnerException);
    }

    private static int[] Shifts() => [-65, -33, -32, -31, -17, -1, 0, 1, 13, 16, 17, 19, 31, 32, 33, 63, 64, 65, int.MinValue, int.MaxValue];

    private static object?[] Snapshot(LegacyNativeInteractQuery query, params string[] changed) =>
        typeof(LegacyNativeInteractQuery).GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Where(field => !changed.Contains(field.Name)).OrderBy(field => field.MetadataToken)
            .Select(field => field.GetValue(query)).ToArray();

    private static LegacyNativeInteractQuery Source(int index)
    {
        var query = new LegacyNativeInteractQuery { AtkType = 24, CombatGear = 0xFEDCBA9876543210,
            KilledMonster = -7, SpellLevel = 77, ResponseDamage = 123, Effect = uint.MaxValue, Unknown = 99 };
        (query.UID, query.OpponentUID, query.X, query.Y, query.Damage, query.SpellID) = index switch
        {
            0 => (0u, 0u, (ushort)0, (ushort)0, 0, (ushort)0),
            1 => (0x12345678u, 0x89ABCDEFu, ushort.MaxValue, (ushort)32768, int.MinValue, ushort.MaxValue),
            2 => (uint.MaxValue, uint.MaxValue, ushort.MaxValue, ushort.MaxValue, -1, (ushort)0x1234),
            3 => (17u, 18u, (ushort)19, (ushort)20, 0x12345678, (ushort)0xBEEF),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };
        return query;
    }
}
