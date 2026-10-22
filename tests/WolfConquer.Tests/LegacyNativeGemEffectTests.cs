using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeGemEffectTests
{
    [Fact]
    public void NativeGemEffectEnumRetainsAllNineByteValuesAndSpellings()
    {
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(LegacyNativeCheckGemEffects.GemEffect)));
        Assert.Equal(new[] { "moon", "phoenix", "rainbow", "purpleray", "goldendragon", "fastflash",
            "goldenkylin", "recovery", "Aegis4" }, Enum.GetNames<LegacyNativeCheckGemEffects.GemEffect>());
        Assert.Equal(Enumerable.Range(0, 9).Select(i => (byte)i),
            Enum.GetValues<LegacyNativeCheckGemEffects.GemEffect>().Select(value => (byte)value));
    }

    [Fact]
    public Task DisabledAndUnusedEquipmentFlagsDoNotConsultEvenANullRandom() => NativePacketTestThread.Run(() =>
    {
        using var fixture = new GemFixture(2099);
        fixture.Equip.SuperKylinGem = fixture.Equip.SuperFuryGem = true;
        LegacyNativeAttackBase.MyRandom = null!;

        LegacyNativeCheckGemEffects.TryngEffect(fixture.Client);
        LegacyNativeCheckGemEffects.CheckRespouseDamage(fixture.Client);

        Assert.Empty(fixture.Queue);
        Assert.Null(LegacyNativeAttackBase.MyRandom);
        Assert.True(fixture.Equip.SuperKylinGem && fixture.Equip.SuperFuryGem);
    });

    [Theory]
    [InlineData(0, "goldendragon")]
    [InlineData(1, "phoenix")]
    [InlineData(2, "rainbow")]
    [InlineData(3, "moon")]
    [InlineData(4, "purpleray")]
    public Task EachAttackGemIndependentlySendsItsLiteralEffect(int gem, string effect) => NativePacketTestThread.Run(() =>
    {
        var threshold = gem == 0 ? 4 : 5;
        using var fixture = new GemFixture(NativeAttackRandomScope.FindSeed(1, r => r[0] < threshold));
        EnableAttackGem(fixture.Equip, gem);

        LegacyNativeCheckGemEffects.TryngEffect(fixture.Client);

        fixture.AssertEffects(effect);
        fixture.Random.AssertDrawCount(1);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public Task EachAttackGateRejectsEqualityButStillAdvancesOneDraw(int gem) => NativePacketTestThread.Run(() =>
    {
        var threshold = gem == 0 ? 4 : 5;
        using var fixture = new GemFixture(NativeAttackRandomScope.FindSeed(1, r => r[0] == threshold));
        EnableAttackGem(fixture.Equip, gem);

        LegacyNativeCheckGemEffects.TryngEffect(fixture.Client);

        Assert.Empty(fixture.Queue);
        fixture.Random.AssertDrawCount(1);
    });

    [Fact]
    public Task AllAttackGatesConsumeInSourceOrderAndPublishSeparatePackets() => NativePacketTestThread.Run(() =>
    {
        var seed = NativeAttackRandomScope.FindSeed(5,
            r => r[0] < 4 && r[1] >= 5 && r[2] < 5 && r[3] >= 5 && r[4] < 5);
        using var fixture = new GemFixture(seed);
        for (var gem = 0; gem < 5; gem++) EnableAttackGem(fixture.Equip, gem);

        LegacyNativeCheckGemEffects.TryngEffect(fixture.Client);

        fixture.AssertEffects("goldendragon", "rainbow", "purpleray");
        fixture.Random.AssertDrawCount(5);
    });

    [Theory]
    [InlineData(false, "recovery")]
    [InlineData(true, "Aegis4")]
    public Task EachDefenseGateRequiresBothRollsAndUsesItsExactEffect(bool bless, string effect) => NativePacketTestThread.Run(() =>
    {
        using var fixture = new GemFixture(NativeAttackRandomScope.FindSeed(2, r => r[0] < 10 && r[1] < 5));
        fixture.Equip.SuprtTortoiseGem = !bless;
        fixture.Equip.HaveBless = bless;

        LegacyNativeCheckGemEffects.CheckRespouseDamage(fixture.Client);

        fixture.AssertEffects(effect);
        fixture.Random.AssertDrawCount(2);
    });

    [Theory]
    [InlineData(false, false, 1)]
    [InlineData(false, true, 2)]
    [InlineData(true, false, 1)]
    [InlineData(true, true, 2)]
    public Task FailedDefenseFirstRollSkipsTheSecondAndEqualityAtEitherGateFails(
        bool bless, bool secondGate, int draws) => NativePacketTestThread.Run(() =>
    {
        var seed = NativeAttackRandomScope.FindSeed(draws,
            r => secondGate ? r[0] < 10 && r[1] == 5 : r[0] == 10);
        using var fixture = new GemFixture(seed);
        fixture.Equip.SuprtTortoiseGem = !bless;
        fixture.Equip.HaveBless = bless;

        LegacyNativeCheckGemEffects.CheckRespouseDamage(fixture.Client);

        Assert.Empty(fixture.Queue);
        fixture.Random.AssertDrawCount(draws);
    });

    [Fact]
    public Task FailedTortoiseFirstRollLeavesTheNextTwoDrawsForBless() => NativePacketTestThread.Run(() =>
    {
        using var fixture = new GemFixture(NativeAttackRandomScope.FindSeed(3,
            r => r[0] >= 10 && r[1] < 10 && r[2] < 5));
        fixture.Equip.SuprtTortoiseGem = fixture.Equip.HaveBless = true;

        LegacyNativeCheckGemEffects.CheckRespouseDamage(fixture.Client);

        fixture.AssertEffects("Aegis4");
        fixture.Random.AssertDrawCount(3);
    });

    [Fact]
    public Task BothDefenseEffectsPublishRecoveryBeforeAegisAfterFourDraws() => NativePacketTestThread.Run(() =>
    {
        using var fixture = new GemFixture(NativeAttackRandomScope.FindSeed(4,
            r => r[0] < 10 && r[1] < 5 && r[2] < 10 && r[3] < 5));
        fixture.Equip.SuprtTortoiseGem = fixture.Equip.HaveBless = true;

        LegacyNativeCheckGemEffects.CheckRespouseDamage(fixture.Client);

        fixture.AssertEffects("recovery", "Aegis4");
        fixture.Random.AssertDrawCount(4);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MissingEquipmentFaultsBeforeAnyDrawWithoutAllocatingIt(bool defense) => NativePacketTestThread.Run(() =>
    {
        using var fixture = new GemFixture(3169);
        fixture.Client.Equipment = null;

        Assert.Throws<NullReferenceException>(() => Invoke(fixture.Client, defense));

        Assert.Null(fixture.Client.Equipment);
        Assert.Empty(fixture.Queue);
        fixture.Random.AssertDrawCount(0);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingClientIsNotGuardedAndDoesNotDraw(bool defense)
    {
        using var random = new NativeAttackRandomScope(1997);
        Assert.Throws<NullReferenceException>(() => Invoke(null!, defense));
        random.AssertDrawCount(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task EnabledGateDoesNotRepairAMissingRandom(bool defense) => NativePacketTestThread.Run(() =>
    {
        using var fixture = new GemFixture(3343);
        fixture.Equip.SuperDragonGem = fixture.Equip.SuprtTortoiseGem = true;
        LegacyNativeAttackBase.MyRandom = null!;

        Assert.Throws<NullReferenceException>(() => Invoke(fixture.Client, defense));

        Assert.Null(LegacyNativeAttackBase.MyRandom);
        Assert.Empty(fixture.Queue);
        Assert.Same(fixture.Equip, fixture.Client.Equipment);
    });

    [Fact]
    public Task MissingPlayerViewStopsLaterGatesAfterOnlyTheSuccessfulDragonRoll() => NativePacketTestThread.Run(() =>
    {
        using var fixture = new GemFixture(NativeAttackRandomScope.FindSeed(1, r => r[0] < 4));
        fixture.Equip.SuperDragonGem = fixture.Equip.SuperPheonixGem = true;
        fixture.Client.Player.View = null!;

        Assert.Throws<NullReferenceException>(() => LegacyNativeCheckGemEffects.TryngEffect(fixture.Client));

        Assert.Null(fixture.Client.Player.View);
        Assert.Empty(fixture.Queue);
        fixture.Random.AssertDrawCount(1);
    });

    [Fact]
    public Task MissingMapViewFaultOccursAfterOwnerQueueingButBeforeLaterGemDraws() => NativePacketTestThread.Run(() =>
    {
        using var fixture = new GemFixture(NativeAttackRandomScope.FindSeed(1, r => r[0] < 4));
        fixture.Equip.SuperDragonGem = fixture.Equip.SuperPheonixGem = true;
        fixture.Client.Map = new LegacyNativeGameMap(72, 72, 1002);

        Assert.Throws<NullReferenceException>(() => LegacyNativeCheckGemEffects.TryngEffect(fixture.Client));

        fixture.AssertEffects("goldendragon");
        Assert.Null(fixture.Client.Map.View);
        fixture.Random.AssertDrawCount(1);
    });

    private static void Invoke(LegacyNativeGameClientRuntime client, bool defense)
    {
        if (defense) LegacyNativeCheckGemEffects.CheckRespouseDamage(client);
        else LegacyNativeCheckGemEffects.TryngEffect(client);
    }

    private static void EnableAttackGem(LegacyNativeEquipItemStorage equipment, int gem)
    {
        switch (gem)
        {
            case 0: equipment.SuperDragonGem = true; break;
            case 1: equipment.SuperPheonixGem = true; break;
            case 2: equipment.SuperRaibowGem = true; break;
            case 3: equipment.SuperMoonGem = true; break;
            case 4: equipment.SuperVioletGem = true; break;
            default: throw new ArgumentOutOfRangeException(nameof(gem));
        }
    }

    private sealed class GemFixture : IDisposable
    {
        private static readonly FieldInfo Seal = typeof(LegacyNativePacket)
            .GetField("seal", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? priorSeal;
        internal NativeAttackRandomScope Random { get; }
        internal Queue<byte[]> Queue { get; } = new();
        internal LegacyNativeGameClientRuntime Client { get; }
        internal LegacyNativeEquipItemStorage Equip { get; }

        internal GemFixture(int seed)
        {
            Random = new NativeAttackRandomScope(seed);
            priorSeal = Seal.GetValue(null);
            Seal.SetValue(null, null);
            // Owned component MODEL: actual socket queue, but no Create/pump or host binding.
            Client = NativeSpellTestFixture.CreateOwner(Queue);
            Client.Player.UID = 0x12345678;
            Equip = new LegacyNativeEquipItemStorage(Client);
            Client.Equipment = Equip;
        }

        internal void AssertEffects(params string[] names)
        {
            var packets = Queue.ToArray();
            Assert.Equal(names.Length, packets.Length);
            for (var i = 0; i < names.Length; i++)
            {
                var text = Encoding.ASCII.GetBytes(names[i]);
                var expected = new byte[12 + text.Length];
                BinaryPrimitives.WriteUInt16LittleEndian(expected, (ushort)expected.Length);
                BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(2), 2036);
                BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(4), 0x12345678);
                expected[8] = 10; // Native StringID.Effect, one UTF8 string with a UInt16 byte length.
                expected[9] = 1;
                BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(10), (ushort)text.Length);
                text.CopyTo(expected, 12);
                Assert.Equal(expected, packets[i]);
                if (i > 0) Assert.NotSame(packets[i - 1], packets[i]);
            }
            Assert.Same(Equip, Client.Equipment);
            Assert.Null(Client.Socket!.Connection);
            Assert.Null(Client.Socket.Crypto);
        }

        public void Dispose()
        {
            Seal.SetValue(null, priorSeal);
            Random.Dispose();
        }
    }
}
