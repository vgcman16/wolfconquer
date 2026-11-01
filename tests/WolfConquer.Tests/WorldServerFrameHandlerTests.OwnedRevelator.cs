using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;
using Xunit.Abstractions;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    internal static Task RunOwnedRevelatorStartupAsync(LegacyNativeCombatGearRuntime runtime,
        IReadOnlyList<CombatGear> gearRows, IReadOnlyList<DaoqiDictType> jadeRows) =>
        LegacyAuthenticatedOrdinaryCreationTests.Run(async fixture =>
        {
            const uint uid = LegacyAuthenticatedOrdinaryCreationTests.CreationFixture.RuneActor;
            var connection = fixture.NewConnection(uid);
            await fixture.SendOnAsync(connection, new LegacyMsgLoginGamePacket(uid).ToFrameBytes());
            var current = fixture.CurrentFor(connection);
            var client = Assert.IsType<LegacyNativeGameClientRuntime>(connection.NativeGameClient);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(client.OrdinarySpellBinding);
            var gear = Assert.IsType<LegacyNativeMyCombatGear>(client.CombatGear);
            Assert.True(runtime.IsCurrent);
            Assert.True(fixture.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.True(gear.IsCombatOwnedBy(client));
            Assert.True(gear.IsAdmissionOriginCurrent(binding, current));
            Assert.Single(current.CombatGear!.Items);
            var sent = fixture.Frames(uid);
            var index = FindOpcodeIndex(sent, WorldPacketOpcodes.LegacyCombatGear, startIndex: 1);
            Assert.True(LegacySpellPacket.TryParse(ReadFrame(sent[index + 1]), out var first));
            Assert.Equal(21_540, first.Id);
            Assert.True(LegacySpellPacket.TryParse(ReadFrame(sent[index + 2]), out var second));
            Assert.Equal(21_920, second.Id);
            var expected = CreateSyntheticRevelatorFrames(uid, checked((ushort)current.Character.X),
                checked((ushort)current.Character.Y), 9);
            for (var i = 0; i < expected.Count; i++) Assert.Equal(expected[i], sent[index + 3 + i]);
            var air = FindOpcodeIndex(sent, WorldPacketOpcodes.LegacyAirPowerUp, startIndex: index + 7);
            Assert.True(air > index + 6);
            Assert.True(LegacyAirPowerUpPacket.TryParse(ReadFrame(sent[air]), out _));
            Assert.False(fixture.Runtime.Recovery.IsBlocked);
        }, beforeBootstrap: fixture =>
        {
            fixture.ClientCompatibility = LegacyClientCompatibilityOptions.NewerClient;
            fixture.ConfigureNativeGear(runtime, gearRows, jadeRows);
            var character = File.ReadAllText(fixture.Data.UserPath);
            Assert.Contains("Class=6000", character);
            File.WriteAllText(fixture.Data.UserPath, character.Replace("Class=6000", "Class=7051", StringComparison.Ordinal));
            var row = new CombatGearSnapshotItem(701, 2, 0, 1, 0, 0,
                [new uint[7], new uint[7]], new uint[6]);
            var saved = CombatGearSnapshot.Empty with { Count = 1, Items = [row] };
            new CombatGearSnapshotLoader(fixture.Data.CombatGear).SaveByCharacterUid(
                LegacyAuthenticatedOrdinaryCreationTests.CreationFixture.RuneActor, saved);
            new PlayerSpellSnapshotLoader(fixture.Data.Spells).SaveByCharacterUid(
                LegacyAuthenticatedOrdinaryCreationTests.CreationFixture.RuneActor,
                new([new PlayerSpellSnapshotEntry(21_520, 9, 0, 0, 0, 0)]));
        }, enter: false);
}

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class OwnedRevelatorStartupChildTests(ITestOutputHelper output)
{
    [Fact]
    public void ConfiguredLegacy19OwnerPublishesRevelatorBeforeAirPower()
    {
        var result = LegacyNativeCombatGearLoadChildFixture.Run("OrdinaryGearRevelator", output,
            knownEntry: "ExecutePirateRecovery");
        Assert.True(result.GetProperty("Passed").GetBoolean());
        Assert.True(result.GetProperty("Loaded").GetBoolean());
    }
}
