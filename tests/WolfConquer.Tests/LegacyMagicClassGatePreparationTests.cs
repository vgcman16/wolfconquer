using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMagicClassGatePreparationTests
{
    private const ushort ThunderSpellId = 1_000;
    private const ushort SummonBatSpellId = 4_010;
    private const ushort GraceOfHeavenSpellId = 12_560;
    private const ushort FrostGazeIISpellId = 13_070;
    private const int LegacyHeavensWrathFlag = 0xEC;
    private const int LegacyDragonPierceStaminaFlag = 0x136;

    [Theory]
    [InlineData("IsLegacyWaterTaoistClass", 13002, true)]
    [InlineData("IsLegacyWaterTaoistClass", 13051, true)]
    [InlineData("IsLegacyWaterTaoistClass", 13057, true)]
    [InlineData("IsLegacyWaterTaoistClass", 13058, false)]
    [InlineData("IsLegacyWaterTaoistClass", 13001, false)]
    [InlineData("IsLegacyWaterTaoistClass", 14002, false)]
    [InlineData("IsLegacyFireTaoistClass", 14002, true)]
    [InlineData("IsLegacyFireTaoistClass", 14051, true)]
    [InlineData("IsLegacyFireTaoistClass", 14057, true)]
    [InlineData("IsLegacyFireTaoistClass", 14058, false)]
    [InlineData("IsLegacyFireTaoistClass", 14001, false)]
    [InlineData("IsLegacyFireTaoistClass", 13002, false)]
    public void TaoistClassHelpersMatchLegacyAtributesStatusRanges(
        string helperName,
        long classCode,
        bool expected)
    {
        Assert.Equal(expected, InvokeClassHelper(helperName, classCode));
    }

    [Fact]
    public async Task PreparationAllowsLearnedOffClassSpellAndSpendsMana()
    {
        var catalog = CreateCatalog(
            CreateOperation(id: 1, mode: 1, group: 13, ThunderSpellId),
            CreateOperation(id: 2, mode: 1, group: 14, ThunderSpellId),
            CreateOperation(id: 3, mode: 2, group: 1, ThunderSpellId));
        var session = CreateSession(classCode: 1_005);

        var result = await PrepareAsync(session, catalog, CreateSpell(ThunderSpellId));

        Assert.NotNull(result.PreparedSession);
        Assert.Equal(493, result.StoredSession.Character.MinMana);
        Assert.Single(result.Sent);
        Assert.DoesNotContain("profession gate rejected", result.Log);
    }

    [Fact]
    public async Task PreparationDoesNotInspectStaleRebornClassFieldsForCasting()
    {
        var catalog = CreateCatalog(
            CreateOperation(id: 1, mode: 1, group: 13, ThunderSpellId),
            CreateOperation(id: 2, mode: 1, group: 14, ThunderSpellId));
        var session = CreateSession(classCode: 1_005, firstClass: 14_002, secondClass: 13_002, reborn: 0);

        var result = await PrepareAsync(session, catalog, CreateSpell(ThunderSpellId));

        Assert.NotNull(result.PreparedSession);
        Assert.Equal(493, result.StoredSession.Character.MinMana);
        Assert.Single(result.Sent);
        Assert.DoesNotContain("profession gate rejected", result.Log);
    }

    [Fact]
    public async Task PreparationAllowsCompactFireTaoistForTaoistSpell()
    {
        var catalog = CreateCatalog(
            CreateOperation(id: 1, mode: 1, group: 13, ThunderSpellId),
            CreateOperation(id: 2, mode: 1, group: 14, ThunderSpellId));
        var session = CreateSession(classCode: 142);

        var result = await PrepareAsync(session, catalog, CreateSpell(ThunderSpellId));

        Assert.NotNull(result.PreparedSession);
        Assert.Equal(493, result.StoredSession.Character.MinMana);
        Assert.Single(result.Sent);
        Assert.DoesNotContain("profession gate rejected", result.Log);
    }

    [Fact]
    public async Task PreparationAllowsSpellFromRecordedPreviousClassFamily()
    {
        var catalog = CreateCatalog(
            CreateOperation(id: 1, mode: 1, group: 13, ThunderSpellId),
            CreateOperation(id: 2, mode: 1, group: 14, ThunderSpellId));
        var session = CreateSession(classCode: 1_005, firstClass: 142, reborn: 1);

        var result = await PrepareAsync(session, catalog, CreateSpell(ThunderSpellId));

        Assert.NotNull(result.PreparedSession);
        Assert.Equal(493, result.StoredSession.Character.MinMana);
        Assert.Single(result.Sent);
    }

    [Fact]
    public async Task PreparationAllowsLearnedSourceRebornLineageSpellWithoutClassGate()
    {
        var catalog = CreateCatalog(
            CreateOperation(150, 1, 1, 14, 6, SummonBatSpellId));
        var session = CreateSession(classCode: 1_005);

        var result = await PrepareAsync(session, catalog, CreateSpell(SummonBatSpellId));

        Assert.NotNull(result.PreparedSession);
        Assert.Equal(493, result.StoredSession.Character.MinMana);
        Assert.Single(result.Sent);
        Assert.DoesNotContain("profession gate rejected", result.Log);
    }

    [Fact]
    public async Task PreparationAllowsRebornTrojanWithSourceFireTaoistLineageSpell()
    {
        var catalog = CreateCatalog(
            CreateOperation(150, 1, 1, 14, 6, SummonBatSpellId));
        var session = CreateSession(classCode: 1_005, firstClass: 14_002, reborn: 1);

        var result = await PrepareAsync(session, catalog, CreateSpell(SummonBatSpellId));

        Assert.NotNull(result.PreparedSession);
        Assert.Equal(493, result.StoredSession.Character.MinMana);
        Assert.Single(result.Sent);
        Assert.DoesNotContain("profession gate rejected", result.Log);
    }

    [Fact]
    public async Task PreparationKeepsExistingBehaviorWhenSourceClassGateDataIsMissing()
    {
        var session = CreateSession(classCode: 1_005);

        var result = await PrepareAsync(session, CreateCatalog(), CreateSpell(ThunderSpellId));

        Assert.NotNull(result.PreparedSession);
        Assert.Equal(493, result.StoredSession.Character.MinMana);
        Assert.Single(result.Sent);
        Assert.DoesNotContain("profession gate rejected", result.Log);
    }

    [Fact]
    public void SanitizerRemovesLoadedSpellRejectedBySourceClassGate()
    {
        var catalog = CreateCatalog(
            CreateOperation(id: 1, mode: 1, group: 13, ThunderSpellId));
        var character = CreateCharacter(
            classCode: 1_005,
            firstClass: 0,
            secondClass: 0,
            reborn: 0,
            minMana: 500,
            stamina: 100,
            mapId: 1_015);
        var playerSpells = new PlayerSpellSnapshot(
        [
            CreatePlayerSpell(ThunderSpellId),
            CreatePlayerSpell(2_000),
        ]);

        var changed = LegacyPlayerSpellClassGateSanitizer.TrySanitize(
            character,
            playerSpells,
            catalog.MagicTypeOperationsByReferencedMagicTypeId,
            out var sanitized,
            out var removedSpellIds);

        Assert.True(changed);
        Assert.Equal([ThunderSpellId], removedSpellIds);
        Assert.Equal([2_000U], sanitized.SpellIds.ToArray());
    }

    [Fact]
    public void SanitizerKeepsLoadedSpellAllowedByRebornPreviousClassFamily()
    {
        var catalog = CreateCatalog(
            CreateOperation(id: 1, mode: 1, group: 14, ThunderSpellId));
        var character = CreateCharacter(
            classCode: 1_005,
            firstClass: 14_002,
            secondClass: 0,
            reborn: 1,
            minMana: 500,
            stamina: 100,
            mapId: 1_015);
        var playerSpells = new PlayerSpellSnapshot([CreatePlayerSpell(ThunderSpellId)]);

        var changed = LegacyPlayerSpellClassGateSanitizer.TrySanitize(
            character,
            playerSpells,
            catalog.MagicTypeOperationsByReferencedMagicTypeId,
            out var sanitized,
            out var removedSpellIds);

        Assert.False(changed);
        Assert.Empty(removedSpellIds);
        Assert.Same(playerSpells, sanitized);
        Assert.Equal([ThunderSpellId], sanitized.SpellIds.ToArray());
    }

    [Fact]
    public void SanitizerKeepsLearnedFrostGazeIIWithoutUnprovenLoginCleanupOverride()
    {
        var catalog = CreateCatalog(
            CreateOperation(id: 1, mode: 1, group: 16, FrostGazeIISpellId));
        var character = CreateCharacter(
            classCode: 16_005,
            firstClass: 9_005,
            secondClass: 0,
            reborn: 1,
            minMana: 500,
            stamina: 100,
            mapId: 1_015);
        var playerSpells = new PlayerSpellSnapshot([CreatePlayerSpell(FrostGazeIISpellId)]);

        var changed = LegacyPlayerSpellClassGateSanitizer.TrySanitize(
            character,
            playerSpells,
            catalog.MagicTypeOperationsByReferencedMagicTypeId,
            out var sanitized,
            out var removedSpellIds);

        Assert.False(changed);
        Assert.Empty(removedSpellIds);
        Assert.Same(playerSpells, sanitized);
        Assert.Equal([FrostGazeIISpellId], sanitized.SpellIds.ToArray());
    }

    [Fact]
    public void SanitizerStillRemovesFrostGazeIIRejectedByNormalClassGate()
    {
        var catalog = CreateCatalog(
            CreateOperation(id: 1, mode: 1, group: 16, FrostGazeIISpellId));
        var character = CreateCharacter(
            classCode: 1_005,
            firstClass: 0,
            secondClass: 0,
            reborn: 0,
            minMana: 500,
            stamina: 100,
            mapId: 1_015);
        var playerSpells = new PlayerSpellSnapshot([CreatePlayerSpell(FrostGazeIISpellId)]);

        var changed = LegacyPlayerSpellClassGateSanitizer.TrySanitize(
            character,
            playerSpells,
            catalog.MagicTypeOperationsByReferencedMagicTypeId,
            out var sanitized,
            out var removedSpellIds);

        Assert.True(changed);
        Assert.Equal([FrostGazeIISpellId], removedSpellIds);
        Assert.Empty(sanitized.SpellIds);
    }

    [Fact]
    public async Task PreparationSpendsStaminaBeforeFailingInsufficientManaWithoutRefund()
    {
        var session = CreateSession(classCode: 1_005, minMana: 4, stamina: 30);

        var result = await PrepareAsync(session, CreateCatalog(), CreateSpell(ThunderSpellId, useMana: 7, useStamina: 12));

        Assert.Null(result.PreparedSession);
        Assert.Equal(4, result.StoredSession.Character.MinMana);
        Assert.Equal("18", result.StoredSession.Character.Attributes["Stamina"]);
        var update = AssertSingleUpdate(result.Sent, LegacyMsgUpdateDataType.Stamina, 18);
        Assert.Equal(1_293_600UL, update.Uid);
        Assert.Contains("requiredMana=7", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreparationSpendsStaminaThenManaOnSuccessfulCast()
    {
        var session = CreateSession(classCode: 1_005, minMana: 20, stamina: 30);

        var result = await PrepareAsync(session, CreateCatalog(), CreateSpell(ThunderSpellId, useMana: 7, useStamina: 12));

        Assert.NotNull(result.PreparedSession);
        Assert.Equal(13, result.StoredSession.Character.MinMana);
        Assert.Equal("18", result.StoredSession.Character.Attributes["Stamina"]);
        Assert.Collection(
            ParseMsgUpdates(result.Sent),
            update =>
            {
                Assert.Equal(1_293_600UL, update.Uid);
                Assert.Equal(new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 18), Assert.Single(update.Updates));
            },
            update =>
            {
                Assert.Equal(1_293_600UL, update.Uid);
                Assert.Equal(new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 13), Assert.Single(update.Updates));
            });
    }

    [Fact]
    public async Task PreparationWithGraceOfHeavenDualMonkEpicsKeepsManaAndSkipsColdTime()
    {
        var session = CreateSession(
            classCode: 6_005,
            minMana: 20,
            stamina: 30,
            playerItems: CreatePlayerItemsWithEquipment(
                CreateEquippedItem(622_000, position: 4),
                CreateEquippedItem(622_001, position: 5)),
            playerSpellIds: [ThunderSpellId, GraceOfHeavenSpellId]);

        var result = await PrepareAsync(
            session,
            CreateCatalog(),
            CreateSpell(ThunderSpellId, useMana: 7, useStamina: 12, coldTime: 60_000));

        Assert.NotNull(result.PreparedSession);
        Assert.Equal(20, result.StoredSession.Character.MinMana);
        Assert.Equal("18", result.StoredSession.Character.Attributes["Stamina"]);
        Assert.True(result.StoredSession.MagicCooldowns is null ||
            !result.StoredSession.MagicCooldowns.ContainsKey(ThunderSpellId));
        var update = AssertSingleUpdate(result.Sent, LegacyMsgUpdateDataType.Stamina, 18);
        Assert.Equal(1_293_600UL, update.Uid);
        Assert.DoesNotContain(result.Sent, frame => LegacyMagicColdTimePacket.TryParse(ReadFrame(frame), out _));
        Assert.DoesNotContain("requiredMana", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("cooldownRemainingMs=", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreparationWithGraceOfHeavenStillRequiresSufficientManaAfterStaminaSpend()
    {
        var session = CreateSession(
            classCode: 6_005,
            minMana: 4,
            stamina: 30,
            playerItems: CreatePlayerItemsWithEquipment(
                CreateEquippedItem(622_000, position: 4),
                CreateEquippedItem(622_001, position: 5)),
            playerSpellIds: [ThunderSpellId, GraceOfHeavenSpellId]);

        var result = await PrepareAsync(
            session,
            CreateCatalog(),
            CreateSpell(ThunderSpellId, useMana: 7, useStamina: 12, coldTime: 60_000));

        Assert.Null(result.PreparedSession);
        Assert.Equal(4, result.StoredSession.Character.MinMana);
        Assert.Equal("18", result.StoredSession.Character.Attributes["Stamina"]);
        var update = AssertSingleUpdate(result.Sent, LegacyMsgUpdateDataType.Stamina, 18);
        Assert.Equal(1_293_600UL, update.Uid);
        Assert.DoesNotContain(result.Sent, frame => LegacyMagicColdTimePacket.TryParse(ReadFrame(frame), out _));
        Assert.Contains("requiredMana=7", result.Log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1_000)]
    [InlineData(17_650)]
    [InlineData(17_460)]
    [InlineData(17_470)]
    [InlineData(17_480)]
    [InlineData(17_490)]
    public async Task PreparationSkipsLegacyResourceSpendingOnMap1039(ushort spellId)
    {
        var session = CreateSession(classCode: 1_005, minMana: 4, stamina: 3, mapId: 1_039);

        var result = await PrepareAsync(session, CreateCatalog(), CreateSpell(spellId, useMana: 7, useStamina: 12));

        Assert.NotNull(result.PreparedSession);
        Assert.Equal(4, result.StoredSession.Character.MinMana);
        Assert.Equal("3", result.StoredSession.Character.Attributes["Stamina"]);
        Assert.Empty(result.Sent);
        Assert.DoesNotContain("requiredStamina", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("requiredMana", result.Log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(17_650)]
    [InlineData(17_460)]
    [InlineData(17_470)]
    [InlineData(17_480)]
    [InlineData(17_490)]
    public async Task PreparationRejectsInsufficientStaminaWithoutSpellIdCostExemptions(ushort spellId)
    {
        var session = CreateSession(classCode: 1_005, minMana: 4, stamina: 3);

        var result = await PrepareAsync(session, CreateCatalog(), CreateSpell(spellId, useMana: 7, useStamina: 12));

        Assert.Null(result.PreparedSession);
        Assert.Equal(4, result.StoredSession.Character.MinMana);
        Assert.Equal("3", result.StoredSession.Character.Attributes["Stamina"]);
        Assert.Empty(result.Sent);
        Assert.Contains("requiredStamina=12", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("requiredMana", result.Log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(17_650, 20, true)]
    [InlineData(17_460, 20, true)]
    [InlineData(17_470, 20, true)]
    [InlineData(17_480, 20, true)]
    [InlineData(17_490, 20, true)]
    [InlineData(17_650, 4, false)]
    [InlineData(17_460, 4, false)]
    [InlineData(17_470, 4, false)]
    [InlineData(17_480, 4, false)]
    [InlineData(17_490, 4, false)]
    public async Task PreparationChargesSourceRowCostsInStaminaThenManaOrderForEverySpellId(
        ushort spellId,
        long mana,
        bool enoughMana)
    {
        // CanUseSpell.cs:51-98: spend stamina before the mana check, with no ID branch.
        var session = CreateSession(classCode: 1_005, minMana: mana, stamina: 30);

        var result = await PrepareAsync(session, CreateCatalog(), CreateSpell(spellId, useMana: 7, useStamina: 12));

        Assert.Equal(enoughMana, result.PreparedSession is not null);
        Assert.Equal(enoughMana ? 13 : 4, result.StoredSession.Character.MinMana);
        Assert.Equal("18", result.StoredSession.Character.Attributes["Stamina"]);
        Assert.Equal(enoughMana ? 2 : 1, result.Sent.Count);
        Assert.All(result.Sent, bytes => Assert.Equal((ushort)2176, ReadFrame(bytes).Header.Opcode));
        var updates = ParseMsgUpdates(result.Sent);
        Assert.Equal(result.Sent.Count, updates.Count);
        Assert.All(updates, update => Assert.Equal(1_293_600U, update.Uid));
        Assert.Equal(new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 18), Assert.Single(updates[0].Updates));
        if (enoughMana)
        {
            Assert.Equal(new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 13), Assert.Single(updates[1].Updates));
            Assert.DoesNotContain("requiredMana", result.Log, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("requiredMana=7", result.Log, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(17_650)]
    [InlineData(17_460)]
    [InlineData(17_470)]
    [InlineData(17_480)]
    [InlineData(17_490)]
    public void CalculatorKeepsRowCostsAndScurvySurchargeWithoutSpellIdExemptions(ushort spellId)
    {
        var cost = LegacyMagicResourceCostCalculator.Calculate(
            CreateSpell(spellId, useMana: 100, useStamina: 5),
            PlayerItemSnapshot.Empty,
            catalog: null,
            characterAttributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyMagicResourceCostCalculator.ScurvyBombStatusFlag),
            });

        Assert.False(LegacyMagicResourceCostCalculator.IsLegacyResourceFreeSpell(spellId));
        Assert.Equal(100, cost.UseMana);
        Assert.Equal(11, cost.UseStamina);
        Assert.Equal(6, cost.ScurvyBombStaminaSurcharge);
        Assert.Equal(0, cost.DragonPierceStaminaSurcharge);
        Assert.False(cost.HasMpMaster);
    }

    [Theory]
    [InlineData(17_650)]
    [InlineData(17_460)]
    [InlineData(17_470)]
    [InlineData(17_480)]
    [InlineData(17_490)]
    public async Task PreparationAllowsZeroCostRowsButStillChargesScurvySurcharge(ushort spellId)
    {
        var session = CreateSession(classCode: 1_005, minMana: 0, stamina: 6);
        var spell = CreateSpell(spellId, useMana: 0, useStamina: 0);
        var free = await PrepareAsync(session, CreateCatalog(), spell);

        Assert.NotNull(free.PreparedSession);
        Assert.Equal(0, free.StoredSession.Character.MinMana);
        Assert.Equal("6", free.StoredSession.Character.Attributes["Stamina"]);
        Assert.Empty(free.Sent);

        session = session with
        {
            Character = session.Character with
            {
                Attributes = new Dictionary<string, string>(session.Character.Attributes)
                {
                    ["StatusFlags"] = BuildStatusFlags(LegacyMagicResourceCostCalculator.ScurvyBombStatusFlag),
                },
            },
        };
        var surcharged = await PrepareAsync(session, CreateCatalog(), spell);

        Assert.NotNull(surcharged.PreparedSession);
        Assert.Equal(0, surcharged.StoredSession.Character.MinMana);
        Assert.Equal("0", surcharged.StoredSession.Character.Attributes["Stamina"]);
        Assert.Equal((ushort)2176, ReadFrame(Assert.Single(surcharged.Sent)).Header.Opcode);
        var update = AssertSingleUpdate(surcharged.Sent, LegacyMsgUpdateDataType.Stamina, 0);
        Assert.Equal(1_293_600U, update.Uid);
    }

    [Fact]
    public async Task PreparationAppliesDragonPierceStaminaSurchargeBeforeSpend()
    {
        var session = CreateSession(classCode: 1_005, minMana: 20, stamina: 10);
        var attributes = new Dictionary<string, string>(session.Character.Attributes)
        {
            ["StatusFlags"] = BuildStatusFlags(LegacyDragonPierceStaminaFlag),
            ["DragonPierceStamina"] = "8",
        };
        session = session with
        {
            Character = session.Character with { Attributes = attributes },
        };

        var result = await PrepareAsync(
            session,
            CreateCatalog(),
            CreateSpell(ThunderSpellId, useMana: 0, useStamina: 5));

        Assert.Null(result.PreparedSession);
        Assert.Equal("10", result.StoredSession.Character.Attributes["Stamina"]);
        Assert.Empty(result.Sent);
        Assert.Contains("requiredStamina=13", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreparationUsesActiveAlternateMpMasterRuneForManaCost()
    {
        var runeItemId = CreateMpMasterRuneItemId(9);
        var session = CreateSession(
            classCode: 1_005,
            minMana: 20,
            playerItems: CreatePlayerItems(new PlayerItemSnapshotItem
            {
                ItemId = runeItemId,
                Position = 123,
            }),
            alternateEquipmentActive: true);

        var result = await PrepareAsync(
            session,
            CreateCatalogWithItems(CreateItemType((int)runeItemId, "MPMaster(+9)")),
            CreateSpell(ThunderSpellId, useMana: 10));

        Assert.NotNull(result.PreparedSession);
        Assert.Equal(19, result.StoredSession.Character.MinMana);
        var update = AssertSingleUpdate(result.Sent, LegacyMsgUpdateDataType.Mana, 19);
        Assert.Equal(1_293_600UL, update.Uid);
        Assert.DoesNotContain("requiredMana", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreparationBypassesLegacyCooldownWhileHeavensWrathStatusIsActive()
    {
        var session = CreateSession(classCode: 1_005, minMana: 20);
        var attributes = new Dictionary<string, string>(session.Character.Attributes)
        {
            ["StatusFlags"] = BuildStatusFlags(LegacyHeavensWrathFlag),
        };
        session = session with
        {
            Character = session.Character with { Attributes = attributes },
        };

        var result = await PrepareAsync(
            session,
            CreateCatalog(),
            CreateSpell(ThunderSpellId, useMana: 7, coldTime: 60_000));

        Assert.NotNull(result.PreparedSession);
        Assert.Equal(13, result.StoredSession.Character.MinMana);
        Assert.True(result.StoredSession.MagicCooldowns is null ||
            !result.StoredSession.MagicCooldowns.ContainsKey(ThunderSpellId));
        Assert.DoesNotContain(result.Sent, frame => LegacyMagicColdTimePacket.TryParse(ReadFrame(frame), out _));
        var update = AssertSingleUpdate(result.Sent, LegacyMsgUpdateDataType.Mana, 13);
        Assert.Equal(1_293_600UL, update.Uid);
        Assert.DoesNotContain("cooldownRemainingMs=", result.Log, StringComparison.Ordinal);
    }

    private static async Task<PrepareResult> PrepareAsync(
        WorldSession session,
        WorldDataCatalog catalog,
        LegacyMagicRuntimeSpell spell)
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter(CultureInfo.InvariantCulture);
        var connection = CreateConnection(sent);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: catalog,
            Sessions: sessions,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);

        var prepared = await InvokePrepareAsync(context, session, spell);

        Assert.True(sessions.TryGet(connection, out var stored));
        Assert.NotNull(stored);
        return new PrepareResult(prepared, stored, sent, log.ToString());
    }

    private static bool InvokeClassHelper(string helperName, long classCode)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            helperName,
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (bool)method.Invoke(null, [classCode])!;
    }

    private static async ValueTask<WorldSession?> InvokePrepareAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "TryPrepareLegacyPlayerMagicCastAsync",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(WorldPacketHandlerContext), typeof(WorldSession), typeof(LegacyMagicRuntimeSpell), typeof(CancellationToken), typeof(object)],
            modifiers: null);
        Assert.NotNull(method);

        var invocation = method.Invoke(null, [context, session, spell, CancellationToken.None, null]);
        Assert.NotNull(invocation);
        return await (ValueTask<WorldSession?>)invocation;
    }

    private static LegacyMagicRuntimeSpell CreateSpell(
        ushort spellId,
        ushort useMana = 7,
        byte useStamina = 0,
        int coldTime = 0)
    {
        return new LegacyMagicRuntimeSpell(
            spellId,
            LegacyMagicSort.Attack,
            $"Spell{spellId}",
            Level: 0,
            UseMana: useMana,
            Damage: 120,
            Experience: 0,
            MaxTargets: 0,
            Range: 9,
            NeedLevel: 0,
            WeaponType: 0,
            UseStamina: useStamina,
            CoolDown: 0,
            ColdTime: coldTime);
    }

    private static WorldSession CreateSession(
        long classCode,
        long firstClass = 0,
        long secondClass = 0,
        long reborn = 0,
        long minMana = 500,
        ushort stamina = 100,
        uint mapId = 1_015,
        PlayerItemSnapshot? playerItems = null,
        bool alternateEquipmentActive = false,
        IReadOnlyList<ushort>? playerSpellIds = null)
    {
        var spellIds = playerSpellIds ?? [ThunderSpellId];
        return new WorldSession(
            AccountId: 1,
            Username: "caster",
            CreateCharacter(classCode, firstClass, secondClass, reborn, minMana, stamina, mapId),
            playerItems ?? PlayerItemSnapshot.Empty,
            new PlayerSpellSnapshot(spellIds.Select(CreatePlayerSpell).ToArray()),
            PlayerProficiencySnapshot.Empty,
            AlternateEquipmentActive: alternateEquipmentActive);
    }

    private static PlayerItemSnapshot CreatePlayerItemsWithEquipment(params PlayerItemSnapshotItem[] equipmentItems)
    {
        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems: equipmentItems,
            RuneItems: []);
    }

    private static PlayerItemSnapshot CreatePlayerItems(params PlayerItemSnapshotItem[] runes)
    {
        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems: [],
            RuneItems: runes);
    }

    private static PlayerItemSnapshotItem CreateEquippedItem(uint itemId, ushort position)
    {
        return new PlayerItemSnapshotItem
        {
            ItemId = itemId,
            Position = position,
        };
    }

    private static uint CreateMpMasterRuneItemId(byte runeLevel)
    {
        return 4_030_800u + runeLevel;
    }

    private static ItemType CreateItemType(int itemId, string name)
    {
        return new ItemType(itemId, name, [itemId.ToString(CultureInfo.InvariantCulture), name]);
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

    private static CharacterSnapshot CreateCharacter(
        long classCode,
        long firstClass,
        long secondClass,
        long reborn,
        long minMana,
        ushort stamina,
        uint mapId)
    {
        return new CharacterSnapshot(
            Uid: 1_293_600,
            Name: "Caster",
            Body: 1_003,
            Face: 0,
            Hair: 0,
            Class: classCode,
            FirstClass: firstClass,
            SecondClass: secondClass,
            Reborn: reborn,
            Level: 120,
            Experience: 0,
            MapId: mapId,
            X: 830,
            Y: 584,
            PreviousMapId: 1_015,
            PreviousMapX: 830,
            PreviousMapY: 584,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: 300,
            AttributePoints: 0,
            MinHitPoints: 5_000,
            MinMana: minMana,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 1,
            SourceFile: "fixture://character",
            Attributes: new Dictionary<string, string>
            {
                ["Stamina"] = stamina.ToString(CultureInfo.InvariantCulture),
            },
            PaidNobility: new Dictionary<string, string>())
        {
            MaxMana = 500,
        };
    }

    private static MagicTypeOperationEntry CreateOperation(
        int id,
        int mode,
        int group,
        params int[] referencedMagicTypeIds)
    {
        return CreateOperation(id, mode, group, group, 0, referencedMagicTypeIds);
    }

    private static MagicTypeOperationEntry CreateOperation(
        int id,
        int mode,
        int group,
        int level,
        int flag,
        params int[] referencedMagicTypeIds)
    {
        var values = new int[65];
        values[0] = id;
        values[1] = mode;
        values[2] = group;
        values[3] = level;
        values[4] = flag;
        for (var index = 0; index < referencedMagicTypeIds.Length; index++)
        {
            values[5 + index] = referencedMagicTypeIds[index];
        }

        var fields = values.Select(value => value.ToString(CultureInfo.InvariantCulture)).ToArray();
        return new MagicTypeOperationEntry(
            id,
            mode,
            group,
            level,
            flag,
            referencedMagicTypeIds,
            values,
            SourceFile: "magictypeop.txt",
            LineNumber: id,
            RawLine: string.Join(",", fields),
            fields);
    }

    private static ConquerTcpConnection CreateConnection(List<byte[]> sent)
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

    private static LegacyMsgUpdatePacket AssertSingleUpdate(
        IReadOnlyList<byte[]> sent,
        LegacyMsgUpdateDataType type,
        ulong value)
    {
        var update = Assert.Single(ParseMsgUpdates(sent));
        Assert.Equal(new LegacyMsgUpdateEntry(type, value), Assert.Single(update.Updates));
        return update;
    }

    private static IReadOnlyList<LegacyMsgUpdatePacket> ParseMsgUpdates(IEnumerable<byte[]> sent)
    {
        var updates = new List<LegacyMsgUpdatePacket>();
        foreach (var bytes in sent)
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                updates.Add(packet);
            }
        }

        return updates;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var words = new ulong[16];
        foreach (var flag in flags)
        {
            words[flag / 32] |= 1UL << (flag % 32);
        }

        return string.Join('/', words);
    }

    private static WorldDataCatalog CreateCatalog(params MagicTypeOperationEntry[] magicTypeOperations)
    {
        return CreateCatalogWithItems(Array.Empty<ItemType>(), magicTypeOperations);
    }

    private static WorldDataCatalog CreateCatalogWithItems(
        IReadOnlyList<ItemType> items,
        params MagicTypeOperationEntry[] magicTypeOperations)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-20T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://legacy-magic-class-gate",
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
            magic: [],
            magicEffects: [],
            magicTypeOperations: magicTypeOperations,
            titleTypes: [],
            combatGear: [],
            traps: [],
            npcs: [],
            sobNpcs: [],
            portals: [],
            maps: [],
            mapNames: [],
            monsters: [],
            monsterSpawns: [],
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

    private static WorldDataCatalog CreateCatalogWithItems(params ItemType[] items)
    {
        return CreateCatalogWithItems(items, Array.Empty<MagicTypeOperationEntry>());
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData($"fixture://{name}", [], [], [], info: null, pole: null, conductors: []);
    }

    private sealed record PrepareResult(
        WorldSession? PreparedSession,
        WorldSession StoredSession,
        IReadOnlyList<byte[]> Sent,
        string Log);
}
