using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Literal Framework4/x86 default AND optimized observations, exercised against
// actual modern production owners. Not an original-binary or general cast proof.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeEquipmentArithmeticTests
{
    public static IEnumerable<object[]> SourceObservations()
    {
        yield return [double.NaN, 0u, -21474836d, 0u, 0u, 0u];
        yield return [double.PositiveInfinity, 0u, -21474836d, 0u, 0u, 0u];
        yield return [double.NegativeInfinity, 0u, -21474836d, 0u, 0u, 0u];
        yield return [-1.5, 4294967295u, 0d, 4294967146u, 4294967196u, 4294323051u];
        yield return [-0.59, 0u, 0d, 4294967237u, 0u, 4294713893u];
        yield return [0d, 0u, 0d, 0u, 0u, 0u];
        yield return [0.29, 0u, 0d, 28u, 0u, 124554u];
        yield return [0.59, 0u, 0d, 59u, 0u, 253403u];
        yield return [25.9, 25u, 0d, 2590u, 2500u, 11123965u];
        yield return [99.999, 99u, 0d, 9999u, 9900u, 42949243u];
        yield return [100.1, 100u, 1d, 10010u, 0u, 42992622u];
        yield return [2147483647d, 2147483647u, 21474836d, 4294967196u, 4700u, 1566159824u];
        yield return [2147483648d, 2147483648u, -21474836d, 0u, 4800u, 1566589321u];
        yield return [-2147483649d, 2147483647u, -21474836d, 4294967196u, 4294962396u, 2727948479u];
        yield return [4294967295d, 4294967295u, -21474836d, 4294967196u, 9500u, 3132749145u];
        yield return [9223372036854774784d, 4294966272u, -21474836d, 0u, 8400u, 0u];
        yield return [9223372036854775808d, 0u, -21474836d, 0u, 800u, 0u];
        yield return [-9223372036854775808d, 0u, -21474836d, 0u, 4294966496u, 0u];
        yield return [-9223372036854777856d, 0u, -21474836d, 0u, 4294961696u, 0u];
        yield return [double.MaxValue, 0u, -21474836d, 0u, 6800u, 0u];
        yield return [double.Epsilon, 0u, 0d, 0u, 0u, 0u];
    }

    [Theory]
    [MemberData(nameof(SourceObservations))]
    public Task WholeOwnersMatchObservedRawPercentScaleModuloAndDodgeExpressions(
        double value, uint raw, double percent, uint scale, uint modulo, uint dodge) =>
        NativePacketTestThread.Run(() =>
        {
            var priorRandom = LegacyNativePoolRandom.GetRandom;
            var priorReset = LegacyNativePoolRandom.ResetRandom;
            try
            {
                LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(67);
                LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
                using var metadata = new NativeCombatGearQueryScope();
                using var fixture = new NativeSpellTestFixture();
                var gear = new LegacyNativeMyCombatGear(fixture.Owner);
                gear.A(LegacyNativeDaoqiDictTypeTable.AttrType.MaxHP, value);
                gear.A(LegacyNativeDaoqiDictTypeTable.AttrType.Parry, value);
                Assert.Equal(raw, fixture.Owner.Status.MaxHitpoints);
                Assert.Equal(percent, fixture.Owner.PerfectionStatus.StrikeLock);

                gear.SetValue("NobleSpirit", value);
                gear.SetValue("DivineEmptiness", value);
                fixture.Owner.Status.Dodge = uint.MaxValue;
                Assert.True(fixture.Owner.Player.BitVector.TryAdd(372, 60, false, 0));
                Assert.True(fixture.Owner.Player.BitVector.TryAdd(381, 60, false, 0));
                gear.Status();
                Assert.Equal(raw / 100, fixture.Owner.Status.MagicDefence);
                Assert.Equal(modulo, fixture.Owner.Status.Penetration);
                Assert.Equal(dodge, fixture.Owner.Status.DodgeRate);

                fixture.Owner.PerfectionStatus.LuckyStrike = value;
                fixture.Owner.PerfectionStatus.StrikeLock = value;
                fixture.Packet.StatusCreate(fixture.Owner);
                byte[] bytes = NativeSpellTestFixture.Bytes(fixture.Packet);
                Assert.Equal(scale, BitConverter.ToUInt32(bytes, 148));
                Assert.Equal(scale, BitConverter.ToUInt32(bytes, 152));
                Assert.Empty(fixture.Queue);
            }
            finally
            {
                LegacyNativePoolRandom.GetRandom = priorRandom;
                LegacyNativePoolRandom.ResetRandom = priorReset;
            }
        });
}
