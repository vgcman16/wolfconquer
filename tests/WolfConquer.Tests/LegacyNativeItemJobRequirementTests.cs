using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeItemJobRequirementTests
{
    [Theory]
    [InlineData(1000u, 1051u)]
    [InlineData(2000u, 2051u)]
    [InlineData(4000u, 4051u)]
    [InlineData(5000u, 5051u)]
    [InlineData(6000u, 6051u)]
    [InlineData(7000u, 7051u)]
    [InlineData(8000u, 8049u)]
    [InlineData(9000u, 9051u)]
    [InlineData(13000u, 13051u)]
    [InlineData(14000u, 14051u)]
    [InlineData(16000u, 16051u)]
    public void AllSixRecognizedRanksKeepTheFamilyCapAndTheirOwnLowerBound(uint family, uint cap)
    {
        var client = new LegacyNativeGameClientRuntime();
        for (uint rank = 0; rank < 6; rank++)
        {
            uint required = family + rank;
            var row = new LegacyNativeItemRuneMetadata { Class = required };
            foreach (uint candidate in new[] { 0u, required - 1, required, cap, cap + 1, uint.MaxValue })
            {
                SetClass(client.Player, candidate);
                Assert.Equal(candidate >= required && candidate <= cap,
                    LegacyNativeItemTypeRuneMetadata.EquipPassJobReq(row, client));
            }
        }
    }

    [Theory]
    [InlineData(10000u)]
    [InlineData(19000u)]
    public void BothSpecialRequirementsAcceptEveryClassAtLeastTenThousand(uint requirement)
    {
        var client = new LegacyNativeGameClientRuntime();
        var row = new LegacyNativeItemRuneMetadata { Class = requirement };
        foreach (uint candidate in new[] { 0u, 9999u, 10000u, 10001u, 16051u, 19000u, uint.MaxValue })
        {
            SetClass(client.Player, candidate);
            Assert.Equal(candidate >= 10000,
                LegacyNativeItemTypeRuneMetadata.EquipPassJobReq(row, client));
        }
    }

    [Fact]
    public void ZeroAndUnknownRequirementsDoNotDereferenceTheClient()
    {
        Assert.True(LegacyNativeItemTypeRuneMetadata.EquipPassJobReq(new() { Class = 0 }, null!));
        foreach (uint unknown in new[] { 1u, 1006u, 3000u, 8006u, 18000u, 19001u, uint.MaxValue })
            Assert.False(LegacyNativeItemTypeRuneMetadata.EquipPassJobReq(new() { Class = unknown }, null!));
        Assert.Throws<NullReferenceException>(() =>
            LegacyNativeItemTypeRuneMetadata.EquipPassJobReq(null!, null!));
    }

    [Fact]
    public void RecognizedRequirementsReadTheCurrentPhysicalPlayerWithoutRepair()
    {
        var client = new LegacyNativeGameClientRuntime();
        var foreign = new LegacyNativeGameClientRuntime();
        SetClass(client.Player, 1000);
        SetClass(foreign.Player, 8000);
        var row = new LegacyNativeItemRuneMetadata { Class = 1000 };
        Assert.True(LegacyNativeItemTypeRuneMetadata.EquipPassJobReq(row, client));
        client.Player = foreign.Player;
        Assert.False(LegacyNativeItemTypeRuneMetadata.EquipPassJobReq(row, client));
        row.Class = 8000;
        Assert.True(LegacyNativeItemTypeRuneMetadata.EquipPassJobReq(row, client));
        Assert.Same(foreign, client.Player.Owner);
        Assert.Throws<NullReferenceException>(() => LegacyNativeItemTypeRuneMetadata.EquipPassJobReq(row, null!));
        client.Player = null!;
        Assert.Throws<NullReferenceException>(() => LegacyNativeItemTypeRuneMetadata.EquipPassJobReq(row, client));
    }

    private static void SetClass(LegacyNativePlayerRuntime player, uint value) =>
        typeof(LegacyNativePlayerRuntime).GetField("_class", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(player, value); // Physical test prestate, not a cold-only setter.
}
