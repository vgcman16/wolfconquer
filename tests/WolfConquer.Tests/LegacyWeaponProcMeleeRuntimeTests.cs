using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyWeaponProcMeleeRuntimeTests
{
    private const uint PlayerUid = 1_293_600;
    private const uint WeaponItemType = 671_439;
    private const ushort WindstormSpellId = 11_140;
    private const ushort LeftChopSpellId = 14_570;
    private const ushort RightChopSpellId = 14_580;
    private const string RuntimeLegacyAttackStampUtcAttribute = "LegacyAttackStampUtc";

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    public static TheoryData<ushort, uint, bool, uint, bool, string?> CheckMeleeCases => new()
    {
        { LeftChopSpellId, 45, false, 99, true, "Primary" },
        { LeftChopSpellId, 46, true, 24, true, "DeadlySight" },
        { LeftChopSpellId, 46, true, 25, false, null },
        { LeftChopSpellId, 46, false, 0, false, null },
        { RightChopSpellId, 45, false, 99, true, "Primary" },
        { RightChopSpellId, 46, true, 24, true, "DeadlySight" },
        { RightChopSpellId, 46, true, 25, false, null },
        { RightChopSpellId, 46, false, 0, false, null },
    };

    public static TheoryData<ConquerProtocolProfile, bool, int, uint> D13SelectorGuardCases => new()
    {
        { ConquerProtocolProfile.RootD11_7899, true, (int)LegacyMagicSort.Perimeter, 671 },
        { ConquerProtocolProfile.VisibleDx8D13, false, (int)LegacyMagicSort.Perimeter, 671 },
        { ConquerProtocolProfile.VisibleDx8D13, true, (int)LegacyMagicSort.Attack, 671 },
        { ConquerProtocolProfile.VisibleDx8D13, true, (int)LegacyMagicSort.Perimeter, 480 },
    };

    public static TheoryData<ushort, int, uint, ushort> D13SourceBackedSelectorCases => new()
    {
        { 12_210, (int)LegacyMagicSort.Perimeter, 61_617_617, 617 },
        { 12_220, (int)LegacyMagicSort.Perimeter, 61_617_617, 617 },
        { 12_580, (int)LegacyMagicSort.Perimeter, 61_622_622, 622 },
        { 12_590, (int)LegacyMagicSort.Perimeter, 61_622_622, 622 },
        { 12_600, (int)LegacyMagicSort.Strike, 61_622_622, 622 },
        { 12_740, (int)LegacyMagicSort.Perimeter, 61_624_624, 624 },
        { 12_750, (int)LegacyMagicSort.Perimeter, 61_624_624, 624 },
        { 12_760, (int)LegacyMagicSort.StraightFist, 61_624_624, 624 },
        { 13_040, (int)LegacyMagicSort.Perimeter, 61_626_626, 626 },
        { 13_050, (int)LegacyMagicSort.Strike, 61_626_626, 626 },
        { 13_060, (int)LegacyMagicSort.Rectangle, 61_626_626, 626 },
        { LeftChopSpellId, (int)LegacyMagicSort.Perimeter, 60_671_670, 671 },
        { RightChopSpellId, (int)LegacyMagicSort.Perimeter, 60_671_670, 671 },
        { 15_670, (int)LegacyMagicSort.Perimeter, 61_681_680, 681 },
        { 15_680, (int)LegacyMagicSort.Perimeter, 61_681_680, 681 },
        { 15_690, (int)LegacyMagicSort.Rectangle, 61_681_680, 681 },
    };

    public static TheoryData<ushort, int, uint, ushort> D13KnownUnmodeledSelectorCases => new()
    {
        { 12_230, (int)LegacyMagicSort.StraightFist, 61_617_617, 617 },
    };

    [Theory]
    [MemberData(nameof(D13SourceBackedSelectorCases))]
    public async Task HandlerD13ClientSelectedSourceBackedSelectorDispatchesLearnedAnimation(
        ushort selectedSpellId,
        int sort,
        uint configuredWeaponType,
        ushort equippedWeaponType)
    {
        var fixture = CreateD13SelectorFixture(
            selectedSpellId,
            ConquerProtocolProfile.VisibleDx8D13,
            learned: true,
            sort: (LegacyMagicSort)sort,
            configuredWeaponType,
            equippedWeaponType);

        await PacketHandlers.HandleAsync(
            fixture.Context,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(fixture.Monster, selectedSpellId)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(fixture.Sent));
        Assert.Equal(selectedSpellId, animation.SpellId);
        Assert.All(animation.Targets, target => Assert.Equal(fixture.Monster.Uid, target.Uid));
        var physicalInteractions = ParsePhysicalAttacks(fixture.Sent, fixture.Monster.Uid);
        if (selectedSpellId is 13_040 or 13_050 or 13_060 or 15_670 or 15_680 or 15_690)
        {
            Assert.Single(physicalInteractions);
        }
        else
        {
            Assert.Empty(physicalInteractions);
        }
        if (selectedSpellId is 13_040 or 15_670 or 15_680)
        {
            AssertPhysicalAttackPrecedesSpellAnimation(fixture.Sent, fixture.Monster.Uid, selectedSpellId);
        }
        Assert.True(fixture.Sessions.TryGet(fixture.Connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(0, updatedSession.LegacyRandomSpell);
        Assert.Contains(
            $"monsterUid={fixture.Monster.Uid} selector={selectedSpellId} " +
            "inboundOpcode=2135 outboundAnimationOpcode=2239",
            fixture.Log.ToString());
    }

    [Fact]
    public async Task HandlerD13ClientSelectedSpecialPerimeterIgnoresCurrentAttackStampAndDispatchesOnce()
    {
        const ushort selectedSpellId = 13_040;
        var fixture = CreateD13SelectorFixture(
            selectedSpellId,
            ConquerProtocolProfile.VisibleDx8D13,
            learned: true,
            sort: LegacyMagicSort.Perimeter,
            configuredWeaponType: 61_626_626,
            equippedWeaponType: 626,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [RuntimeLegacyAttackStampUtcAttribute] = DateTimeOffset.UtcNow
                    .ToString("O", CultureInfo.InvariantCulture),
            });

        await PacketHandlers.HandleAsync(
            fixture.Context,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(fixture.Monster, selectedSpellId)).ToFrameBytes()));

        Assert.Single(ParsePhysicalAttacks(fixture.Sent, fixture.Monster.Uid));
        var animation = Assert.Single(ParseSpellAnimations(fixture.Sent));
        Assert.Equal(selectedSpellId, animation.SpellId);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(fixture.Monster.Uid, target.Uid);
        Assert.True(target.Damage > 0);
        AssertPhysicalAttackPrecedesSpellAnimation(fixture.Sent, fixture.Monster.Uid, selectedSpellId);

        var monsterStates = Assert.IsType<MonsterRuntimeStateStore>(fixture.Context.MonsterStates);
        Assert.True(monsterStates.TryGetVisibleInstance(
            fixture.Monster,
            DateTimeOffset.UtcNow,
            out var damagedMonster));
        Assert.Equal(fixture.Monster.Hitpoints - target.Damage, damagedMonster.Hitpoints);

        Assert.True(fixture.Sessions.TryGet(fixture.Connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(0, updatedSession.LegacyRandomSpell);
    }

    [Theory]
    [MemberData(nameof(D13KnownUnmodeledSelectorCases))]
    public async Task HandlerD13ClientSelectedKnownButUnmodeledSelectorFallsBackToPhysical(
        ushort selectedSpellId,
        int sort,
        uint configuredWeaponType,
        ushort equippedWeaponType)
    {
        var fixture = CreateD13SelectorFixture(
            selectedSpellId,
            ConquerProtocolProfile.VisibleDx8D13,
            learned: true,
            sort: (LegacyMagicSort)sort,
            configuredWeaponType,
            equippedWeaponType);

        await PacketHandlers.HandleAsync(
            fixture.Context,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(fixture.Monster, selectedSpellId)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(fixture.Sent));
        Assert.Single(ParsePhysicalAttacks(fixture.Sent, fixture.Monster.Uid));
        Assert.Contains($"selector={selectedSpellId}", fixture.Log.ToString());
        Assert.Contains("fallback=normal physical attack", fixture.Log.ToString());
        Assert.DoesNotContain("adapted D13 client-selected weapon proc", fixture.Log.ToString());
    }

    [Theory]
    [MemberData(nameof(D13SelectorGuardCases))]
    public async Task HandlerD13ClientSelectedChopFallsBackUnlessProfileLearnedSortAndWeaponMatch(
        ConquerProtocolProfile profile,
        bool learned,
        int sort,
        uint configuredWeaponType)
    {
        var fixture = CreateD13SelectorFixture(
            LeftChopSpellId,
            profile,
            learned,
            (LegacyMagicSort)sort,
            configuredWeaponType);

        await PacketHandlers.HandleAsync(
            fixture.Context,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(fixture.Monster, LeftChopSpellId)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(fixture.Sent));
        Assert.Single(ParsePhysicalAttacks(fixture.Sent, fixture.Monster.Uid));
        Assert.True(fixture.Sessions.TryGet(fixture.Connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(0, updatedSession.LegacyRandomSpell);
        Assert.DoesNotContain("adapted D13 client-selected weapon proc", fixture.Log.ToString());
    }

    [Fact]
    public async Task HandlerD13ClientSelectedChopRunsCheckMeleeWindstormBeforeSelectedChop()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateWeaponItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn],
            magic:
            [
                CreateMagicRow(
                    RightChopSpellId,
                    sort: (int)LegacyMagicSort.Perimeter,
                    damage: 100,
                    maxTargets: 3,
                    range: 0,
                    weaponType: 671),
                CreateMagicRow(
                    WindstormSpellId,
                    sort: (int)LegacyMagicSort.PhysicalSpells,
                    damage: 0,
                    maxTargets: 30,
                    range: 0),
            ]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var session = CreateSession(
            new PlayerSpellSnapshot(
            [
                CreatePlayerSpell(RightChopSpellId),
                CreatePlayerSpell(WindstormSpellId),
            ]),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LegacyWeaponSpellDirectRateRoll.11140.CheckMeleePrimary"] = "0",
            });
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            ClientCompatibility: LegacyClientCompatibilityOptions.NewerClient with
            {
                DisableLegacyWeaponProcMonsterAttacks = true,
            },
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(monster, RightChopSpellId)).ToFrameBytes()));

        Assert.Equal(
            new ushort[] { WindstormSpellId, RightChopSpellId },
            ParseSpellAnimations(sent).Select(animation => animation.SpellId));
        Assert.Empty(ParsePhysicalAttacks(sent, monster.Uid));
        Assert.Contains("source=GetWeaponSpell.CheckMelee Primary", log.ToString());
    }

    [Theory]
    [MemberData(nameof(CheckMeleeCases))]
    public async Task HandlerSelectedLearnedChopRunsSourceCheckMeleeWindstormThenStillDispatchesChop(
        ushort selectedSpellId,
        uint primaryRoll,
        bool deadlySight,
        uint deadlySightRoll,
        bool expectWindstorm,
        string? expectedBranch)
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateWeaponItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn],
            magic:
            [
                CreateMagicRow(
                    selectedSpellId,
                    sort: 78,
                    damage: 100,
                    maxTargets: 3,
                    range: 0,
                    weaponType: 671,
                    rate: 100),
                CreateMagicRow(
                    WindstormSpellId,
                    sort: 55,
                    damage: 0,
                    maxTargets: 30,
                    range: 0),
            ]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyWeaponSpellOuterRateRoll"] = "0",
            [$"LegacyWeaponSpellRateRoll.{selectedSpellId}"] = "0",
            ["LegacyWeaponSpellDirectRateRoll.11140.Primary"] = "99",
            ["LegacyWeaponSpellDirectRateRoll.11140.DeadlySight"] = "99",
            ["LegacyWeaponSpellDirectRateRoll.11140.CheckMeleePrimary"] =
                primaryRoll.ToString(CultureInfo.InvariantCulture),
            ["LegacyWeaponSpellDirectRateRoll.11140.CheckMeleeDeadlySight"] =
                deadlySightRoll.ToString(CultureInfo.InvariantCulture),
        };
        if (deadlySight)
        {
            attributes["DeadlySight"] = "1";
        }

        var session = CreateSession(
            new PlayerSpellSnapshot(
            [
                CreatePlayerSpell(selectedSpellId),
                CreatePlayerSpell(WindstormSpellId),
            ]),
            attributes);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            ClientCompatibility: LegacyClientCompatibilityOptions.NewerClient with
            {
                DisableLegacyWeaponProcMonsterAttacks = false,
            });

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(monster)).ToFrameBytes()));

        var animations = ParseSpellAnimations(sent);
        var expectedSpellIds = expectWindstorm
            ? new ushort[] { WindstormSpellId, selectedSpellId }
            : [selectedSpellId];
        Assert.Equal(expectedSpellIds, animations.Select(animation => animation.SpellId));
        Assert.All(animations, animation =>
            Assert.All(animation.Targets, target => Assert.Equal(monster.Uid, target.Uid)));
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(0, updatedSession.LegacyRandomSpell);

        var output = log.ToString();
        Assert.Contains($"spellId={selectedSpellId}", output);
        Assert.Contains("source=GetWeaponSpell.Check replacement", output);
        if (expectedBranch is null)
        {
            Assert.DoesNotContain("source=GetWeaponSpell.CheckMelee", output);
        }
        else
        {
            Assert.Contains($"source=GetWeaponSpell.CheckMelee {expectedBranch}", output);
        }
    }

    private static WorldSession CreateSession(
        PlayerSpellSnapshot playerSpells,
        IReadOnlyDictionary<string, string> attributes,
        uint weaponItemType = WeaponItemType)
    {
        return new WorldSession(
            AccountId: 1,
            Username: "check-melee",
            new CharacterSnapshot(
                Uid: PlayerUid,
                Name: "CheckMelee",
                Body: 1_003,
                Face: 0,
                Hair: 0,
                Class: 142,
                FirstClass: 142,
                SecondClass: 0,
                Reborn: 0,
                Level: 120,
                Experience: 0,
                MapId: 1_015,
                X: 830,
                Y: 584,
                PreviousMapId: 1_015,
                PreviousMapX: 830,
                PreviousMapY: 584,
                Strength: 1_000,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 5_000,
                MinMana: 500,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 1,
                SourceFile: "fixture://check-melee",
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            new PlayerItemSnapshot(
                InventoryItems: [],
                RelicFuseItems: [],
                MythsoulBagItems: [],
                EquipmentItems:
                [
                    new PlayerItemSnapshotItem
                    {
                        ItemId = weaponItemType,
                        Durability = 100,
                        MaximumDurability = 100,
                        Position = 4,
                    },
                ],
                RuneItems: []),
            playerSpells,
            PlayerProficiencySnapshot.Empty,
            CombatGear: CombatGearSnapshot.Empty);
    }

    private static PlayerSpellSnapshotEntry CreatePlayerSpell(ushort spellId)
    {
        return new PlayerSpellSnapshotEntry(
            spellId,
            Level: 0,
            Experience: 0,
            PreviousLevel: 0,
            SoulLevel: 0,
            UseSpellSoul: 0);
    }

    private static MsgAttackQuery CreatePhysicalQuery(LegacyMonsterInstance monster, int damage = 0)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: monster.Uid,
            X: monster.X,
            Y: monster.Y,
            AttackType: MsgAttackType.Physical,
            Damage: damage,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0);
    }

    private static ItemType CreateWeaponItemType(uint itemType = WeaponItemType)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = itemType.ToString(CultureInfo.InvariantCulture);
        fields[1] = "CheckMeleeWeapon";
        fields[13] = "1000";
        fields[14] = "1000";
        return new ItemType(checked((int)itemType), "CheckMeleeWeapon", fields);
    }

    private static MagicType CreateMagicRow(
        ushort spellId,
        int sort,
        uint damage,
        byte maxTargets,
        byte range,
        uint weaponType = 0,
        uint rate = 100)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = sort.ToString(CultureInfo.InvariantCulture);
        fields[3] = $"Spell{spellId}";
        fields[8] = "0";
        fields[10] = damage.ToString(CultureInfo.InvariantCulture);
        fields[12] = rate.ToString(CultureInfo.InvariantCulture);
        fields[14] = maxTargets.ToString(CultureInfo.InvariantCulture);
        fields[15] = range.ToString(CultureInfo.InvariantCulture);
        fields[20] = "1";
        fields[22] = weaponType.ToString(CultureInfo.InvariantCulture);
        return new MagicType(spellId, spellId, $"Spell{spellId}", fields);
    }

    private static Monster CreateMonsterFamily()
    {
        return new Monster(
            9_001,
            "TrainingMonster",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["life"] = "100000",
                ["lookface"] = "100",
                ["level"] = "120",
                ["defense"] = "100",
                ["defence"] = "100",
                ["defense2"] = "10000",
                ["defence2"] = "10000",
                ["attack_range"] = "0",
            },
            []);
    }

    private static MonsterSpawn CreateMonsterSpawn()
    {
        return new MonsterSpawn(
            GeneratorId: 1,
            MapId: 1_015,
            MonsterTypeId: 9_001,
            BoundX: 832,
            BoundY: 584,
            BoundWidth: 1,
            BoundHeight: 1,
            MaxNpc: 1,
            RestSeconds: 10,
            MaxPerGenerator: 1,
            TimerBegin: 0,
            TimerEnd: 0,
            BornX: 0,
            BornY: 0,
            SourceMapFolderId: 1_015,
            SourceMonsterName: "TrainingMonster",
            SourceRelativePath: Path.Combine("1015", "TrainingMonster", "1.ini"),
            Attributes: new Dictionary<string, string>
            {
                ["id"] = "1",
                ["mapid"] = "1015",
                ["npctype"] = "9001",
            });
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static IReadOnlyList<LegacySpellAnimationPacket> ParseSpellAnimations(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacySpellAnimationPacket>();
        foreach (var bytes in frames)
        {
            if (LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<MsgAttackPacket> ParsePhysicalAttacks(
        IEnumerable<byte[]> frames,
        uint monsterUid)
    {
        var packets = new List<MsgAttackPacket>();
        foreach (var bytes in frames)
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Query.AttackType == MsgAttackType.Physical &&
                packet.Query.OpponentUid == monsterUid)
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static void AssertPhysicalAttackPrecedesSpellAnimation(
        IReadOnlyList<byte[]> frames,
        uint monsterUid,
        ushort spellId)
    {
        var physicalAttackIndex = -1;
        var spellAnimationIndex = -1;
        for (var index = 0; index < frames.Count; index++)
        {
            var frame = ReadFrame(frames[index]);
            if (physicalAttackIndex < 0 &&
                MsgAttackPacket.TryParse(frame, out var attack) &&
                attack.Query.AttackType == MsgAttackType.Physical &&
                attack.Query.OpponentUid == monsterUid)
            {
                physicalAttackIndex = index;
            }

            if (spellAnimationIndex < 0 &&
                LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == spellId)
            {
                spellAnimationIndex = index;
            }
        }

        Assert.True(physicalAttackIndex >= 0, "Expected a physical interaction for the selected monster.");
        Assert.True(
            spellAnimationIndex > physicalAttackIndex,
            "Expected the physical interaction before the selected spell animation.");
    }

    private static D13SelectorFixture CreateD13SelectorFixture(
        ushort selectedSpellId,
        ConquerProtocolProfile profile,
        bool learned,
        LegacyMagicSort sort,
        uint configuredWeaponType,
        ushort equippedWeaponType = 671,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        var weaponItemType = checked((uint)equippedWeaponType * 1_000U + 439U);
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateWeaponItemType(weaponItemType)],
            monsters: [monsterFamily],
            monsterSpawns: [spawn],
            magic:
            [
                CreateMagicRow(
                    selectedSpellId,
                    sort: (int)sort,
                    damage: 100,
                    maxTargets: 3,
                    range: selectedSpellId switch
                    {
                        13_060 => 5,
                        15_690 => 4,
                        _ => 0,
                    },
                    weaponType: configuredWeaponType),
            ]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var session = CreateSession(
            learned
                ? new PlayerSpellSnapshot([CreatePlayerSpell(selectedSpellId)])
                : PlayerSpellSnapshot.Empty,
            attributes ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            weaponItemType);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            ClientCompatibility: LegacyClientCompatibilityOptions.NewerClient with
            {
                DisableLegacyWeaponProcMonsterAttacks = true,
            },
            ProtocolProfile: profile);
        return new D13SelectorFixture(monster, context, sessions, connection, sent, log);
    }

    private static WorldDataCatalog CreateCatalog(
        IReadOnlyList<ItemType> items,
        IReadOnlyList<Monster> monsters,
        IReadOnlyList<MonsterSpawn> monsterSpawns,
        IReadOnlyList<MagicType> magic)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-09T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://check-melee",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: items,
            itemAddBonuses: [],
            levelExperience: [],
            runeLevelExperience: [],
            runeStorageAttributes: [],
            itemRefineEffects: [],
            itemRefineEffectEx: [],
            itemRefineUpgrades: [],
            transformInfo: [],
            furniture: [],
            hairColorTypes: [],
            hairFaceStorageTypes: [],
            coatStorageTypes: [],
            coatStorageAttributes: [],
            coatColorRules: [],
            xuanbaoAdditionAttributes: [],
            xuanbaoComposeAttributeLimits: [],
            xuanbaoRandomAttributes: [],
            christmasNpcs: [],
            tutorTypes: [],
            tutorBattleLimitTypes: [],
            beastAttributes: [],
            fateExperience: [],
            spiritRates: [],
            hundredWeapons: [],
            gouyuTypes: [],
            gouyuImmortals: [],
            specialEffectAttributes: [],
            daoqiDictTypes: [],
            syndicateFormationTypes: [],
            syndicateSkillTypes: [],
            disdainRules: [],
            jiangHuAttributes: [],
            jiangHuCultivateStatuses: [],
            refineryBoxes: [],
            refinerySouls: [],
            refineryMaterials: [],
            stateCountFiles: [],
            binarySeedFiles: [],
            emptySeedFiles: [],
            backupSeedFiles: [],
            pokerTables: [],
            rouletteTables: [],
            npcTimeEntries: [],
            activityTasks: new ActivityTaskCatalog(new ActivityTaskMetadata(0, "", 0, []), [], "fixture://activity"),
            meltingTypeRules: [],
            lotteryPrizes: [],
            arenaRecords: [],
            elitePkRecords: [],
            teamElitePkRecords: [],
            skillTeamPkRecords: [],
            classPkWarRecords: [],
            combatGearRanks: [],
            prestigeRanks: [],
            innerPowerRanks: [],
            runeRankings: [],
            staticStatues: [],
            newPlayerRewards: [],
            votes: [],
            couplesPkRecords: [],
            socialAssociations: [],
            guildWarInfo: EmptyGuildWarData("guildwar"),
            superGuildWarInfo: EmptyGuildWarData("superguildwar"),
            classLevelStats: [],
            professionLevelUps: [],
            professionLevelBenefits: [],
            professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [],
            questMissions: [],
            quizQuestions: [],
            magic: magic,
            magicEffects: [],
            magicTypeOperations: [],
            titleTypes: [],
            combatGear: [],
            traps: [],
            npcs: [],
            sobNpcs: [],
            portals: [],
            maps: [],
            mapNames: [],
            monsters: monsters,
            monsterSpawns: monsterSpawns,
            lotteryDictionary: [],
            collectionRewardTiers: [],
            taskRewardTypes: [],
            taskRewardTypeIniEntries: [],
            neiGongHeader: new NeiGongHeader(0, [], "fixture://neigong", 0, "", new Dictionary<string, string>()),
            neiGongSecrets: [],
            neiGongTypes: [],
            subProfessions: [],
            syndicateLevels: [],
            stageGoalInfo: new StageGoalInfo(0),
            stageGoalStages: [],
            stageGoalTasks: [],
            shops: [],
            shopItems: [],
            exchangeShopGoods: [],
            exchangeShopGoodsEx: [],
            exchangeShopLevels: [],
            honorShopItems: [],
            honorShopRecommendations: [],
            racePointShopItems: [],
            racePointShopRecommendations: [],
            goldenLeagueShopItems: [],
            goldenLeagueShopRecommendations: [],
            goldenLeagueShopConfig: new Dictionary<string, string>(),
            booths: [],
            boothItems: []);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private sealed record D13SelectorFixture(
        LegacyMonsterInstance Monster,
        WorldPacketHandlerContext Context,
        WorldSessionStore Sessions,
        ConquerTcpConnection Connection,
        List<byte[]> Sent,
        StringWriter Log);
}
