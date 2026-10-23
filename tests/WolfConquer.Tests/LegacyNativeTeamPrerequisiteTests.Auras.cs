using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using AuraFlag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTeamPrerequisiteTests
{
    [Theory]
    [InlineData((int)AuraFlag.FeandAura, 0, 125u)]
    [InlineData((int)AuraFlag.TyrantAura, 1, 125u)]
    [InlineData((int)AuraFlag.MetalAura, 2, 1u)]
    [InlineData((int)AuraFlag.WoodAura, 3, 1u)]
    [InlineData((int)AuraFlag.WaterAura, 4, 1u)]
    [InlineData((int)AuraFlag.FireAura, 5, 1u)]
    [InlineData((int)AuraFlag.EartAura, 6, 1u)]
    public Task AuraIncreaseAndDecreaseMutateOnlyTheirActualStatusFieldAndUseCurrentAuraForDecrease(int flagValue, int field, uint amount) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var flag = (AuraFlag)flagValue;
        var input = new LegacyNativeMagicType.Magic { Damage = 1.25f };
        scope.Player.Aura = new LegacyNativeMagicType.Magic { Damage = 2.5f };
        InvokeAura(scope.Player, "IncreaseStatusAura", flag, input);
        var expected = new uint[7]; expected[field] = amount;
        Assert.Equal(expected, AuraValues(scope.Client.Status));
        InvokeAura(scope.Player, "DecreaseStatusAura", flag);
        expected[field] = unchecked(amount - (field < 2 ? 250u : 2u));
        Assert.Equal(expected, AuraValues(scope.Client.Status));
        Assert.Equal(2.5f, scope.Player.Aura.Damage); Assert.Equal(1.25f, input.Damage);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task CheckAuraIsActuallyAdditiveAndReadsTheSamePhysicalAuraWithoutAddingFlagsOrPublishingPackets() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.Equal(AuraFlag.Normal, scope.Player.UseAura); Assert.Null(scope.Player.Aura); Assert.Equal(0, scope.Player.AuraTimer);
        scope.Player.CheckAura(); Assert.Equal(new uint[7], AuraValues(scope.Client.Status));
        scope.Player.UseAura = AuraFlag.FeandAura;
        scope.Player.Aura = new LegacyNativeMagicType.Magic { Damage = 1.25f };
        scope.Player.AuraTimer = -1;
        scope.Player.CheckAura(); scope.Player.CheckAura();
        Assert.Equal(250u, scope.Client.Status.Immunity);
        scope.Player.Aura.Damage = 2.5f; scope.Player.CheckAura(); Assert.Equal(500u, scope.Client.Status.Immunity);
        Assert.Equal(-1, scope.Player.AuraTimer); Assert.False(scope.Player.ContainFlag(AuraFlag.FeandAura)); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData((int)AuraFlag.Normal)] [InlineData((int)AuraFlag.TeamLeader)]
    [InlineData((int)AuraFlag.MagicShield)] [InlineData(2147483647)]
    public Task UnknownAndNormalAuraTypesDoNotReadNullAuraOrStatus(int flagValue) => NativePacketTestThread.Run(() =>
    {
        var flag = (AuraFlag)flagValue;
        using var scope = new Scope(); scope.Client.Status = null!; scope.Player.Aura = null;
        InvokeAura(scope.Player, "IncreaseStatusAura", flag, null);
        InvokeAura(scope.Player, "DecreaseStatusAura", flag);
        scope.Player.UseAura = flag; scope.Player.CheckAura();
        Assert.Null(scope.Client.Status); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task MatchingAuraTypesRetainNullFaultsAndUncheckedStatusWrapInsteadOfRepairingOwners() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Player.UseAura = AuraFlag.FeandAura;
        Assert.Throws<NullReferenceException>(() => scope.Player.CheckAura());
        Assert.Throws<NullReferenceException>(() => InvokeAura(scope.Player, "DecreaseStatusAura", AuraFlag.FeandAura));
        scope.Player.Aura = new LegacyNativeMagicType.Magic { Damage = 1.25f };
        scope.Client.Status.Immunity = uint.MaxValue - 20;
        scope.Player.CheckAura(); Assert.Equal(104u, scope.Client.Status.Immunity);
        scope.Client.Status.Immunity = 0;
        InvokeAura(scope.Player, "DecreaseStatusAura", AuraFlag.FeandAura);
        Assert.Equal(4294967171u, scope.Client.Status.Immunity);
        scope.Client.Status = null!;
        Assert.Throws<NullReferenceException>(() => scope.Player.CheckAura());
        Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(-1f, 4294967295u, 4294967196u)]
    [InlineData(-0.5f, 0u, 4294967246u)]
    [InlineData(1.25f, 1u, 125u)]
    [InlineData(4294967296f, 0u, 0u)]
    [InlineData(4294967040f, 4294967040u, 4294941696u)]
    [InlineData(-4294967808f, 4294966784u, 4294916096u)]
    [InlineData(float.NaN, 0u, 0u)]
    [InlineData(float.PositiveInfinity, 0u, 0u)]
    [InlineData(float.NegativeInfinity, 0u, 0u)]
    [InlineData(float.MaxValue, 0u, 0u)]
    [InlineData(float.MinValue, 0u, 0u)]
    [InlineData(9223372036854775808f, 0u, 0u)]
    [InlineData(-9223372036854775808f, 0u, 0u)]
    [InlineData(float.Epsilon, 0u, 0u)]
    public Task AuraActualMutationPreservesFrameworkX86DirectCastAndExtendedScaledProduct(float damage, uint scalar, uint scaled) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var magic = new LegacyNativeMagicType.Magic { Damage = damage };
        scope.Player.Aura = magic;
        InvokeAura(scope.Player, "IncreaseStatusAura", AuraFlag.MetalAura, magic);
        InvokeAura(scope.Player, "IncreaseStatusAura", AuraFlag.FeandAura, magic);
        Assert.Equal(scalar, scope.Client.Status.MetalResistance); Assert.Equal(scaled, scope.Client.Status.Immunity);
        scope.Client.Status.MetalResistance = 0; scope.Client.Status.Immunity = 0;
        InvokeAura(scope.Player, "DecreaseStatusAura", AuraFlag.MetalAura);
        InvokeAura(scope.Player, "DecreaseStatusAura", AuraFlag.FeandAura);
        Assert.Equal(unchecked(0u - scalar), scope.Client.Status.MetalResistance);
        Assert.Equal(unchecked(0u - scaled), scope.Client.Status.Immunity);
        Assert.Empty(scope.Queue);
    });
}
