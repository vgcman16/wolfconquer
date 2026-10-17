using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMsgUpdatePacketTests
{
    [Fact]
    public void LegacyMsgUpdateOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal((ushort)2176, WorldPacketOpcodes.LegacyMsgUpdate);
        Assert.Equal(WorldPacketOpcodes.LegacyMsgUpdate, LegacyMsgUpdatePacket.Opcode);
    }

    [Fact]
    public void LegacyMsgUpdateScalarDataTypesMatchLegacyValues()
    {
        Assert.Equal((uint)2, (uint)LegacyMsgUpdateDataType.Mana);
        Assert.Equal((uint)3, (uint)LegacyMsgUpdateDataType.MaxMana);
        Assert.Equal((uint)4, (uint)LegacyMsgUpdateDataType.Money);
        Assert.Equal((uint)5, (uint)LegacyMsgUpdateDataType.Experience);
        Assert.Equal((uint)6, (uint)LegacyMsgUpdateDataType.Conquer);
        Assert.Equal((uint)6, (uint)LegacyMsgUpdateDataType.PKPoints);
        Assert.Equal((uint)7, (uint)LegacyMsgUpdateDataType.Class);
        Assert.Equal((uint)8, (uint)LegacyMsgUpdateDataType.Stamina);
        Assert.Equal((uint)9, (uint)LegacyMsgUpdateDataType.WarehouseMoney);
        Assert.Equal((uint)0x0C, (uint)LegacyMsgUpdateDataType.Level);
        Assert.Equal((uint)0x1B, (uint)LegacyMsgUpdateDataType.XPCircle);
        Assert.Equal((uint)0x26, (uint)LegacyMsgUpdateDataType.Merchant);
        Assert.Equal((uint)0x27, (uint)LegacyMsgUpdateDataType.VIPLevel);
        Assert.Equal((uint)0x29, (uint)LegacyMsgUpdateDataType.EnlightPoints);
        Assert.Equal((uint)44, (uint)LegacyMsgUpdateDataType.LegacyLoginFlag);
        Assert.Equal((uint)0x2F, (uint)LegacyMsgUpdateDataType.RaceShopPoints);
        Assert.Equal((uint)0x31, (uint)LegacyMsgUpdateDataType.AzureShield);
        Assert.Equal((uint)0x34, (uint)LegacyMsgUpdateDataType.Team);
        Assert.Equal((uint)0x4D, (uint)LegacyMsgUpdateDataType.InnerPowerPotency);
        Assert.Equal((uint)0x4F, (uint)LegacyMsgUpdateDataType.InventorySash);
        Assert.Equal((uint)0x50, (uint)LegacyMsgUpdateDataType.InventorySashMax);
        Assert.Equal((uint)0x65, (uint)LegacyMsgUpdateDataType.XPList);
        Assert.Equal((uint)0x78, (uint)LegacyMsgUpdateDataType.ArchiveSkill);
        Assert.Equal((uint)0x79, (uint)LegacyMsgUpdateDataType.ClassExperience);
        Assert.Equal((uint)125, (uint)LegacyMsgUpdateDataType.GuildDonation);
        Assert.Equal((uint)0x7D, (uint)LegacyMsgUpdateDataType.MyDontion);
        Assert.Equal((uint)126, (uint)LegacyMsgUpdateDataType.GuildDonationMirror);
        Assert.Equal((uint)0x7E, (uint)LegacyMsgUpdateDataType.DonationPoints);
    }

    [Fact]
    public void LegacyPostRuneStartupUpdatesUsePkPointsAndClassExperienceValues()
    {
        var snapshot = CharacterSnapshotParser.ParseText(
            """
            [Character]
            UID=1400123
            Body=1008
            Face=154
            Name=FixturePlayer
            Class=7051
            Map=1015
            X=830
            Y=584
            FirstClass=0
            SecoundeClass=7050
            Reborn=2
            Level=140
            Haire=27
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=36
            Strength=0
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            ConquerPoints=0
            BankCps=0
            BoundConquerPoints=0
            Money=0
            PkPoints=7
            ClassExperience=5500
            Online=0
            """);

        var pkPoints = LegacyMsgUpdatePacket.CreateLegacyPKPointsStartupUpdate(snapshot);
        var classExperience = LegacyMsgUpdatePacket.CreateLegacyClassExperienceStartupUpdate(snapshot);

        AssertSingleUpdate(pkPoints, LegacyMsgUpdateDataType.PKPoints, 7);
        AssertSingleUpdate(classExperience, LegacyMsgUpdateDataType.ClassExperience, 5_500);
    }

    [Fact]
    public void LegacyInventorySashStartupUpdatesSendMaxThenCount()
    {
        var snapshot = CharacterSnapshotParser.ParseText(
            """
            [Character]
            UID=1400123
            Body=1008
            Face=154
            Name=FixturePlayer
            Class=7051
            Map=1015
            X=830
            Y=584
            FirstClass=0
            SecoundeClass=7050
            Reborn=2
            Level=140
            Haire=27
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=36
            Strength=0
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            ConquerPoints=0
            BankCps=0
            BoundConquerPoints=0
            Money=0
            PkPoints=0
            InventorySashCount=1
            Online=0
            """);

        var updates = LegacyMsgUpdatePacket.CreateLegacyInventorySashStartupUpdates(snapshot);

        Assert.Equal(2, updates.Count);
        AssertSingleUpdate(updates[0], LegacyMsgUpdateDataType.InventorySashMax, 300);
        AssertSingleUpdate(updates[1], LegacyMsgUpdateDataType.InventorySash, 1);
    }

    [Fact]
    public void LegacyDonationStartupUpdatesUseGuildDonationValueForBothFinalTypes()
    {
        var snapshot = CharacterSnapshotParser.ParseText(
            """
            [Character]
            UID=1400123
            Body=1008
            Face=154
            Name=FixturePlayer
            Class=7051
            Map=1015
            X=830
            Y=584
            FirstClass=0
            SecoundeClass=7050
            Reborn=2
            Level=140
            Haire=27
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=36
            Strength=0
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            ConquerPoints=0
            BankCps=0
            BoundConquerPoints=0
            Money=0
            PkPoints=0
            GuildDontion=900
            Online=0
            """);

        var updates = LegacyMsgUpdatePacket.CreateLegacyDonationStartupUpdates(snapshot);

        Assert.Equal(2, updates.Count);
        AssertSingleUpdate(updates[0], LegacyMsgUpdateDataType.MyDontion, 900);
        AssertSingleUpdate(updates[1], LegacyMsgUpdateDataType.DonationPoints, 900);
    }

    [Fact]
    public void LegacyMsgUpdatePacketWritesLegacyMaxHitpointsProtobufSlice()
    {
        var packet = new LegacyMsgUpdatePacket(
            Uid: 601000,
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, 1_167_300)]);

        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(LegacyMsgUpdatePacket.Opcode, frame.Header.Opcode);
        Assert.Equal(
        [
            0x08, 0xA8, 0xD7, 0x24,
            0x12, 0x06,
            0x08, 0x01,
            0x10, 0xC4, 0x9F, 0x47,
        ], frame.Payload.ToArray());
    }

    [Fact]
    public void LegacyMsgUpdatePacketRoundTripsMultipleScalarUpdates()
    {
        var packet = new LegacyMsgUpdatePacket(
            Uid: 601000,
            [
                new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, 1_167_300),
                new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, 1_167_300),
            ]);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyMsgUpdatePacket.TryParse(frame, out var parsed));

        Assert.Equal(601000U, parsed.Uid);
        Assert.Equal(
        [
            new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, 1_167_300),
            new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, 1_167_300),
        ], parsed.Updates);
    }

    [Fact]
    public void LegacyMsgUpdatePacketRoundTripsAzureShieldRemovalEntry()
    {
        const uint magicDefender = 0x80;
        var packet = new LegacyMsgUpdatePacket(
            Uid: 1001,
            [
                new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.AzureShield,
                    Value: 0,
                    Flag: magicDefender,
                    Time: 0,
                    Damage: 0,
                    SpellLevel: 0),
            ]);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyMsgUpdatePacket.TryParse(frame, out var parsed));

        var update = Assert.Single(parsed.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.AzureShield, update.Type);
        Assert.Equal(magicDefender, update.Flag);
        Assert.Equal(0UL, update.Value);
        Assert.Equal(0U, update.Time);
        Assert.Equal(0U, update.Damage);
        Assert.Equal(0U, update.SpellLevel);
    }

    [Fact]
    public void LegacyStartupStatusUpdatesUseIndependentInnerPowerVipAndMerchantWireLayouts()
    {
        var dbRoot = Path.Combine(Path.GetTempPath(), "wolf-msgupdate-startup-status-" + Guid.NewGuid());
        try
        {
            var usersRoot = Path.Combine(dbRoot, "Users");
            Directory.CreateDirectory(usersRoot);
            File.WriteAllText(
                Path.Combine(dbRoot, "InnerPower.txt"),
                "Count=1\r\nFixturePlayer/1400123/2375/0/\r\n");
            var snapshot = CharacterSnapshotParser.ParseText(
                """
                [Character]
                UID=1400123
                Name=FixturePlayer
                Body=1003
                Face=1
                Haire=0
                Class=10
                FirstClass=0
                SecoundeClass=0
                Reborn=0
                Map=1000
                X=50
                Y=60
                Level=15
                Experience=0
                PMap=1000
                PMapX=50
                PMapY=60
                Strength=10
                Agility=10
                Vitaliti=10
                Spirit=10
                Atributes=0
                MinHitPoints=100
                MinMana=80
                Money=0
                ConquerPoints=0
                BoundConquerPoints=0
                BankCps=0
                PkPoints=0
                Online=0
                VipLevel=3
                """);

            var innerPower = LegacyMsgUpdatePacket.CreateLegacyInnerPowerPotencyStartupUpdate(snapshot, usersRoot);
            var vipLevel = LegacyMsgUpdatePacket.CreateLegacyVipLevelStartupUpdate(snapshot);
            var merchant = LegacyMsgUpdatePacket.CreateLegacyMerchantStartupUpdate(snapshot);

            Assert.Equal(ExpectedUpdateFrame(1_400_123, ProtoFields((1, 0x4D), (2, 2375))), innerPower.ToFrameBytes());
            Assert.Equal(ExpectedUpdateFrame(1_400_123, ProtoFields((1, 0x27), (2, 3))), vipLevel.ToFrameBytes());
            Assert.Equal(ExpectedUpdateFrame(1_400_123, ProtoFields((1, 0x26), (2, 255))), merchant.ToFrameBytes());
            AssertSingleUpdate(innerPower, LegacyMsgUpdateDataType.InnerPowerPotency, 2375);
            AssertSingleUpdate(vipLevel, LegacyMsgUpdateDataType.VIPLevel, 3);
            AssertSingleUpdate(merchant, LegacyMsgUpdateDataType.Merchant, 255);
        }
        finally
        {
            if (Directory.Exists(dbRoot))
            {
                Directory.Delete(dbRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void LegacyWorldLoginPreAnswerUpdatesPreserveRequiredOrderAndSyntheticValues()
    {
        var snapshot = CharacterSnapshotParser.ParseText(
            """
            [Character]
            UID=1400123
            Body=1008
            Face=154
            Name=FixturePlayer
            Class=7051
            Map=1015
            X=830
            Y=584
            FirstClass=0
            SecoundeClass=7050
            Reborn=2
            Level=140
            Haire=27
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=36
            Strength=0
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            Conquer=0
            ConquerPoints=639778904
            BankCps=123
            BoundConquerPoints=456
            Money=98602439956
            GuildDontion=900
            PkPoints=0
            Online=7
            """);

        var packets = LegacyMsgUpdatePacket.CreateLegacyWorldLoginPreAnswerUpdates(snapshot);

        Assert.Equal(5, packets.Count);
        AssertSingleUpdate(packets[0], LegacyMsgUpdateDataType.Money, 98_602_439_956);
        AssertSingleUpdate(packets[1], LegacyMsgUpdateDataType.Conquer, 0);
        AssertSingleUpdate(packets[2], LegacyMsgUpdateDataType.LegacyLoginFlag, 15);
        AssertSingleUpdate(packets[3], LegacyMsgUpdateDataType.GuildDonation, 900);
        AssertSingleUpdate(packets[4], LegacyMsgUpdateDataType.GuildDonationMirror, 900);
    }

    [Fact]
    public void LegacyWorldLoginPreAnswerUpdatesWriteOpcodeForEveryFrame()
    {
        var snapshot = CharacterSnapshotParser.ParseText(
            """
            [Character]
            UID=1400123
            Body=1008
            Face=154
            Name=FixturePlayer
            Class=7051
            Map=1015
            X=830
            Y=584
            FirstClass=0
            SecoundeClass=7050
            Reborn=2
            Level=140
            Haire=27
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=36
            Strength=0
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            Conquer=0
            ConquerPoints=639778904
            BankCps=123
            BoundConquerPoints=456
            Money=98602439956
            GuildDontion=900
            PkPoints=0
            Online=7
            """);

        var packets = LegacyMsgUpdatePacket.CreateLegacyWorldLoginPreAnswerUpdates(snapshot);

        foreach (var packet in packets)
        {
            var frame = ReadFrame(packet.ToFrameBytes());

            Assert.Equal(LegacyMsgUpdatePacket.Opcode, frame.Header.Opcode);
            Assert.True(LegacyMsgUpdatePacket.TryParse(frame, out var parsed));
            Assert.Equal(packet.Uid, parsed.Uid);
            Assert.Equal(packet.Updates, parsed.Updates);
        }
    }

    [Fact]
    public void LegacyMsgUpdateMonsterHitpointSequenceKeepsLegacyCumulativeSecondPacket()
    {
        var monster = new LegacyMonsterInstance(
            Uid: 601000,
            MapId: 1000,
            X: 215,
            Y: 306,
            Mesh: 37,
            Level: 97,
            Hitpoints: 1_167_300,
            MaxLife: 1_167_300,
            MonsterId: 37,
            Name: "BladeMonster",
            Boss: true,
            GeneratorId: 201,
            SpawnIndex: 0);

        var packets = LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(monster);

        Assert.Equal(2, packets.Count);
        Assert.Equal(
        [
            new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, 1_167_300),
        ], packets[0].Updates);
        Assert.Equal(
        [
            new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, 1_167_300),
            new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, 1_167_300),
        ], packets[1].Updates);
    }

    [Fact]
    public void LegacyMsgUpdateMonsterHitpointSequenceSkipsNormalLowHitpointMonsters()
    {
        var monster = new LegacyMonsterInstance(
            Uid: 470000,
            MapId: 1000,
            X: 192,
            Y: 345,
            Mesh: 200,
            Level: 83,
            Hitpoints: 23_346,
            MaxLife: 23_346,
            MonsterId: 76,
            Name: "BladeGhostL83",
            Boss: false,
            GeneratorId: 70,
            SpawnIndex: 0);

        Assert.Empty(LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(monster));
    }

    [Fact]
    public void LegacyMsgUpdatePacketRoundTripsStatusFlagVectorValues()
    {
        var values = new ulong[18];
        values[2] = 1UL << 9;
        values[15] = 1UL << 31;
        var packet = new LegacyMsgUpdatePacket(
            1_400_123,
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.StatusFlag, Value: 0, Values: values)]);

        var bytes = packet.ToFrameBytes();

        Assert.Equal(ExpectedStatusFlagFrame(1_400_123, values), bytes);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var parsed));
        var update = Assert.Single(parsed.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, update.Type);
        Assert.Equal(values, update.Values!.ToArray());
    }

    [Fact]
    public void LegacyRuntimeMeshUpdateRoundTripsGhostTransformationMesh()
    {
        var packet = LegacyMsgUpdatePacket.CreateLegacyRuntimeMeshUpdate(1_400_123, 991_541_007);
        var bytes = packet.ToFrameBytes();

        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(1_400_123U, parsed.Uid);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mesh, 991_541_007)],
            parsed.Updates);
    }

    [Fact]
    public void LegacyRuntimeXPCircleTickUpdateUsesIndependentScalarWireLayout()
    {
        var packet = LegacyMsgUpdatePacket.CreateLegacyRuntimeXPCircleTickUpdate(1_400_123, 7);
        var bytes = packet.ToFrameBytes();

        Assert.Equal(ExpectedUpdateFrame(1_400_123, ProtoFields((1, 0x1B), (2, 7))), bytes);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(1_400_123U, parsed.Uid);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPCircle, 7)],
            parsed.Updates);
    }

    [Fact]
    public void LegacyRuntimeXPListUpdateMatchesLegacyActivationFrame()
    {
        var packet = LegacyMsgUpdatePacket.CreateLegacyRuntimeXPListUpdate(1_400_123);
        var bytes = packet.ToFrameBytes();

        Assert.Equal(ExpectedUpdateFrame(1_400_123, ProtoFields((1, 0x65), (2, 1))), bytes);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(1_400_123U, parsed.Uid);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPList, 1)],
            parsed.Updates);
    }

    [Fact]
    public void LegacyRuntimeXPListRemovalUpdateMatchesLegacyRemovalFrame()
    {
        var packet = LegacyMsgUpdatePacket.CreateLegacyRuntimeXPListUpdate(1_400_123, value: 0);
        var bytes = packet.ToFrameBytes();

        Assert.Equal(ExpectedUpdateFrame(1_400_123, ProtoFields((1, 0x65))), bytes);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(1_400_123U, parsed.Uid);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPList, 0)],
            parsed.Updates);
    }

    [Fact]
    public void LegacyRuntimeStaminaTickUpdateUsesIndependentScalarWireLayout()
    {
        var packet = LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(1_400_123, 19);
        var bytes = packet.ToFrameBytes();

        Assert.Equal(ExpectedUpdateFrame(1_400_123, ProtoFields((1, 8), (2, 19))), bytes);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(1_400_123U, parsed.Uid);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 19)],
            parsed.Updates);
    }

    [Fact]
    public void LegacyRuntimeStatusFlagVectorUpdateUsesIndependentRepeatedFieldLayout()
    {
        var values = new ulong[18];
        values[1] = 1UL << 3;
        values[16] = 1UL << 27;
        Assert.Equal(LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength, values.Length);
        Assert.True(IsLegacyStatusFlagSet(values, 35));
        Assert.True(IsLegacyStatusFlagSet(values, 539));
        var packet = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(1_400_123, values);
        var bytes = packet.ToFrameBytes();

        Assert.Equal(ExpectedStatusFlagFrame(1_400_123, values), bytes);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(1_400_123U, parsed.Uid);
        var update = Assert.Single(parsed.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, update.Type);
        Assert.Equal(0UL, update.Value);
        Assert.Equal(values, update.Values!.ToArray());
    }

    [Fact]
    public void LegacyRuntimeStatusFlagVectorUpdateRequiresEighteenWords()
    {
        var values = new ulong[8];

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(1_400_123, values));

        Assert.Equal("values", exception.ParamName);
    }

    [Fact]
    public void LegacyMsgUpdatePacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(LegacyMsgUpdatePacket.Opcode + 1, payloadLength: 0);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyMsgUpdatePacket.TryParse(frame, out _));
    }

    // These small wire builders use the documented tag/length layout, not the production packet serializer.
    private static byte[] ExpectedUpdateFrame(uint uid, byte[] update)
    {
        var payload = ProtoFields((1, uid)).ToList();
        payload.Add(0x12); // field 2: one length-delimited update
        AddVarint(payload, (ulong)update.Length);
        payload.AddRange(update);
        var frame = new byte[4 + payload.Count];
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(0, 2), checked((ushort)frame.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2, 2), 2176);
        payload.CopyTo(frame, 4);
        return frame;
    }

    private static byte[] ExpectedStatusFlagFrame(uint uid, IReadOnlyList<ulong> values)
    {
        var update = ProtoFields((1, 0x19)).ToList();
        foreach (var value in values)
        {
            update.Add(0x18); // field 3 is repeated, including zero words
            AddVarint(update, value);
        }

        return ExpectedUpdateFrame(uid, update.ToArray());
    }

    private static byte[] ProtoFields(params (int Number, ulong Value)[] fields)
    {
        var payload = new List<byte>();
        foreach (var field in fields)
        {
            AddVarint(payload, checked((ulong)field.Number << 3));
            AddVarint(payload, field.Value);
        }

        return payload.ToArray();
    }

    private static void AddVarint(List<byte> bytes, ulong value)
    {
        while (value >= 128)
        {
            bytes.Add((byte)((value & 127) | 128));
            value >>= 7;
        }

        bytes.Add((byte)value);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static void AssertSingleUpdate(
        LegacyMsgUpdatePacket packet,
        LegacyMsgUpdateDataType type,
        ulong value)
    {
        Assert.Equal(1_400_123U, packet.Uid);
        Assert.Equal([new LegacyMsgUpdateEntry(type, value)], packet.Updates);
    }

    private static bool IsLegacyStatusFlagSet(IReadOnlyList<ulong> values, int flag)
    {
        var index = flag / 32;
        var bit = flag % 32;
        return index < values.Count && (values[index] & (1UL << bit)) != 0;
    }
}
