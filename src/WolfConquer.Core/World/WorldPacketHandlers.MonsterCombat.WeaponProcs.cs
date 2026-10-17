using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string RuntimeLegacyWeaponSpellRateRollAttribute = "LegacyWeaponSpellRateRoll";
    private const string RuntimeLegacyWeaponSpellOuterRateRollAttribute = "LegacyWeaponSpellOuterRateRoll";
    private const string RuntimeLegacyWeaponSpellIndexAttribute = "LegacyWeaponSpellIndex";
    private const string RuntimeLegacyWeaponSpellDirectRateRollAttribute = "LegacyWeaponSpellDirectRateRoll";
    private const string RuntimeLegacyWeaponEffectRateRollAttribute = "LegacyWeaponEffectRateRoll";
    private const uint LegacyWeaponEffectPoison = 200;
    private const uint LegacyWeaponEffectMp = 202;
    private const ushort LegacyWeaponEffectPoisonSpellId = 3_090;
    private const ushort LegacyWeaponEffectMpSpellId = 1_175;

    private enum LegacyWeaponProcSelectionResult
    {
        None,
        DispatchLearnedSpell,
        ConsumedNoDispatchableSpell,
    }

    private static async ValueTask<bool> TryHandleLegacyWeaponProcMonsterAttackAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        WorldSession session,
        LegacyMonsterInstance monster,
        CancellationToken cancellationToken)
    {
        if (context.ClientCompatibility?.DisableLegacyWeaponProcMonsterAttacks == true)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy weapon proc monster attack " +
                    $"{FormatSession(context)} monsterUid={monster.Uid}; " +
                    "source=MsgAttackPacket.Physical GetWeaponSpell/RandomSpell compatibility fence.")
                .ConfigureAwait(false);
            return false;
        }

        if (packet.Query.AttackType != MsgAttackType.Physical ||
            context.WorldData is not { } worldData)
        {
            return false;
        }

        var selection = SelectLegacyWeaponProcSpell(
            session,
            worldData,
            out var playerSpell,
            out var spell,
            out var selectedSpellId,
            out var selectedSpellLearned,
            out var selectedSpellMetadataResolved);
        if (selection == LegacyWeaponProcSelectionResult.None)
        {
            return false;
        }

        if (selection == LegacyWeaponProcSelectionResult.ConsumedNoDispatchableSpell)
        {
            ClearLegacyWeaponProcRandomSpell(context, session);
            await context.Log.WriteLineAsync(
                    "WorldServer consumed legacy weapon effect proc monster attack " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} spellId={selectedSpellId} " +
                    $"outcome=consumed-no-dispatchable-spell " +
                    $"learned={selectedSpellLearned.ToString().ToLowerInvariant()} " +
                    $"metadataResolved={selectedSpellMetadataResolved.ToString().ToLowerInvariant()}; " +
                    "source=GetWeaponSpell.cs:358-377 -> MsgAttackPacket.cs:910-911 ProcescMagic ClientSpells gate.")
                .ConfigureAwait(false);
            return true;
        }

        await TryDispatchLegacyCheckMeleeWindstormMonsterProcAsync(
                context,
                packet,
                session,
                monster,
                worldData,
                spell.Id,
                cancellationToken)
            .ConfigureAwait(false);
        if (context.Sessions is { } sessions &&
            sessions.TryGet(context.Connection, out var currentSession) &&
            currentSession is not null)
        {
            session = currentSession;
        }

        await DispatchLegacyWeaponProcMonsterAttackAsync(
                context with { Session = session },
                packet,
                session,
                monster,
                playerSpell,
                spell,
                "GetWeaponSpell.Check replacement",
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryDispatchLegacyCheckMeleeWindstormMonsterProcAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        WorldSession session,
        LegacyMonsterInstance monster,
        WorldDataCatalog worldData,
        ushort selectedSpellId,
        CancellationToken cancellationToken)
    {
        if (selectedSpellId is not (14_570 or 14_580) ||
            !HasLearnedLegacySpell(session.PlayerSpells, 11_140))
        {
            return false;
        }

        string? branch = null;
        if (CheckLegacyWeaponSpellDirectRateRoll(session.Character, 11_140, 46, "CheckMeleePrimary"))
        {
            branch = "Primary";
        }
        else if (CheckLegacyWeaponSpellDirectRateRoll(session.Character, 11_140, 25, "CheckMeleeDeadlySight") &&
            HasLegacyDeadlySightRune(session, worldData))
        {
            branch = "DeadlySight";
        }

        if (branch is null ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(
                session.PlayerSpells,
                worldData,
                11_140,
                out var playerSpell,
                out var spell))
        {
            return false;
        }

        await DispatchLegacyWeaponProcMonsterAttackAsync(
                context,
                packet,
                session,
                monster,
                playerSpell,
                spell,
                $"GetWeaponSpell.CheckMelee {branch}",
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyPostPhysicalLearnedMonsterProcsAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        WorldSession session,
        LegacyMonsterInstance monster,
        CancellationToken cancellationToken)
    {
        if (packet.Query.AttackType != MsgAttackType.Physical ||
            context.ClientCompatibility?.DisableLegacyWeaponProcMonsterAttacks == true ||
            context.WorldData is not { } worldData)
        {
            return false;
        }

        var handled = false;
        if (IsLegacyTrojanClass(session.Character.Class) &&
            await TryDispatchLegacyPostPhysicalLearnedMonsterProcAsync(
                    context,
                    packet,
                    session,
                    monster,
                    worldData,
                    spellId: 7_030,
                    rate: 80,
                    branch: "PostPhysicalPrimary",
                    requiresDeadlySight: false,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            handled = true;
        }

        if (await TryDispatchLegacyPostPhysicalLearnedMonsterProcAsync(
                context,
                packet,
                session,
                monster,
                worldData,
                spellId: 11_140,
                rate: 10,
                branch: "PostPhysicalPrimary",
                requiresDeadlySight: false,
                cancellationToken)
            .ConfigureAwait(false))
        {
            handled = true;
        }

        if (await TryDispatchLegacyPostPhysicalLearnedMonsterProcAsync(
                context,
                packet,
                session,
                monster,
                worldData,
                spellId: 11_140,
                rate: 45,
                branch: "PostPhysicalDeadlySight",
                requiresDeadlySight: true,
                cancellationToken)
            .ConfigureAwait(false))
        {
            handled = true;
        }

        if (await TryDispatchLegacyPostPhysicalLearnedMonsterProcAsync(
                context,
                packet,
                session,
                monster,
                worldData,
                spellId: 14_720,
                rate: 42,
                branch: "PostPhysicalPrimary",
                requiresDeadlySight: false,
                cancellationToken)
            .ConfigureAwait(false))
        {
            handled = true;
        }

        if (await TryDispatchLegacyPostPhysicalLearnedMonsterProcAsync(
                context,
                packet,
                session,
                monster,
                worldData,
                spellId: 6_011,
                rate: 100,
                branch: "PostPhysicalPrimary",
                requiresDeadlySight: false,
                cancellationToken)
            .ConfigureAwait(false))
        {
            handled = true;
        }

        if (await TryDispatchLegacyPostPhysicalLearnedMonsterProcAsync(
                context,
                packet,
                session,
                monster,
                worldData,
                spellId: 12_670,
                rate: 10,
                branch: "PostPhysicalPrimary",
                requiresDeadlySight: false,
                cancellationToken)
            .ConfigureAwait(false))
        {
            handled = true;
        }

        if (await TryDispatchLegacyPostPhysicalLearnedMonsterProcAsync(
                context,
                packet,
                session,
                monster,
                worldData,
                spellId: 12_670,
                rate: 45,
                branch: "PostPhysicalDeadlySight",
                requiresDeadlySight: true,
                cancellationToken)
            .ConfigureAwait(false))
        {
            handled = true;
        }

        return handled;
    }

    private static async ValueTask<bool> TryDispatchLegacyPostPhysicalLearnedMonsterProcAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        WorldSession fallbackSession,
        LegacyMonsterInstance monster,
        WorldDataCatalog worldData,
        ushort spellId,
        uint rate,
        string branch,
        bool requiresDeadlySight,
        CancellationToken cancellationToken)
    {
        var session = fallbackSession;
        if (context.Sessions is { } sessions &&
            sessions.TryGet(context.Connection, out var currentSession) &&
            currentSession is not null)
        {
            session = currentSession;
        }

        if (!HasLearnedLegacySpell(session.PlayerSpells, spellId) ||
            !CheckLegacyWeaponSpellDirectRateRoll(session.Character, spellId, rate, branch) ||
            (requiresDeadlySight && !HasLegacyDeadlySightRune(session, worldData)) ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(
                session.PlayerSpells,
                worldData,
                spellId,
                out var playerSpell,
                out var spell))
        {
            return false;
        }

        await DispatchLegacyWeaponProcMonsterAttackAsync(
                context with { Session = session },
                packet,
                session,
                monster,
                playerSpell,
                spell,
                $"MsgAttackPacket.Physical monster post-hit {branch}",
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask DispatchLegacyWeaponProcMonsterAttackAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        WorldSession session,
        LegacyMonsterInstance monster,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        string source,
        CancellationToken cancellationToken)
    {
        var procSession = session with
        {
            LegacyRandomSpell = spell.Id,
        };
        context.Sessions?.Set(context.Connection, procSession);
        var procContext = context with
        {
            Session = procSession,
        };
        var procQuery = packet.Query with
        {
            AttackType = MsgAttackType.Magic,
            OpponentUid = monster.Uid,
            X = monster.X,
            Y = monster.Y,
            Damage = 0,
            KilledMonster = 0,
            ResponseDamage = 0,
            Effect = MsgAttackEffect.None,
            SpellId = spell.Id,
            SpellLevel = playerSpell.Level,
        };

        await HandleLegacyPlayerMonsterMagicAttackAsync(
                procContext,
                new MsgAttackPacket(procQuery),
                cancellationToken)
            .ConfigureAwait(false);
        ClearLegacyWeaponProcRandomSpellIfPending(context, spell.Id);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy weapon proc monster attack " +
                $"{FormatSession(procContext)} spellId={spell.Id} weaponProcRandomSpell={spell.Id}; source={source}.")
            .ConfigureAwait(false);
    }

    private static LegacyWeaponProcSelectionResult SelectLegacyWeaponProcSpell(
        WorldSession session,
        WorldDataCatalog worldData,
        out PlayerSpellSnapshotEntry playerSpell,
        out LegacyMagicRuntimeSpell spell,
        out ushort selectedSpellId,
        out bool selectedSpellLearned,
        out bool selectedSpellMetadataResolved)
    {
        playerSpell = default!;
        spell = default!;
        selectedSpellId = 0;
        selectedSpellLearned = false;
        selectedSpellMetadataResolved = false;

        if (TrySelectLegacyDirectLearnedWeaponProcSpell(session, worldData, out playerSpell, out spell))
        {
            selectedSpellId = spell.Id;
            return LegacyWeaponProcSelectionResult.DispatchLearnedSpell;
        }

        if (!CheckLegacyWeaponSpellOuterRateRoll(session.Character))
        {
            return LegacyWeaponProcSelectionResult.None;
        }

        var weaponEffectSelection = SelectLegacyRightWeaponEffectProcSpell(
            session,
            worldData,
            out playerSpell,
            out spell,
            out selectedSpellId,
            out selectedSpellLearned,
            out selectedSpellMetadataResolved);
        if (weaponEffectSelection != LegacyWeaponProcSelectionResult.None)
        {
            return weaponEffectSelection;
        }

        if (TryGetCurrentWeaponType(session, normalizedPosition: 4, mapLegacyBladeToSword: true, out var rightWeaponType) &&
            TrySelectLegacyWeaponProcSpellForWeaponType(session, worldData, rightWeaponType, out playerSpell, out spell))
        {
            selectedSpellId = spell.Id;
            return LegacyWeaponProcSelectionResult.DispatchLearnedSpell;
        }

        if (TryGetCurrentWeaponType(session, normalizedPosition: 5, mapLegacyBladeToSword: false, out var leftWeaponType) &&
            TrySelectLegacyWeaponProcSpellForWeaponType(session, worldData, leftWeaponType, out playerSpell, out spell))
        {
            selectedSpellId = spell.Id;
            return LegacyWeaponProcSelectionResult.DispatchLearnedSpell;
        }

        if (TrySelectLegacyPostTableLearnedWeaponProcSpell(session, worldData, out playerSpell, out spell))
        {
            selectedSpellId = spell.Id;
            return LegacyWeaponProcSelectionResult.DispatchLearnedSpell;
        }

        return LegacyWeaponProcSelectionResult.None;
    }

    private static void ClearLegacyWeaponProcRandomSpell(
        WorldPacketHandlerContext context,
        WorldSession fallbackSession)
    {
        if (context.Sessions is not { } sessions)
        {
            return;
        }

        var session = fallbackSession;
        if (sessions.TryGet(context.Connection, out var currentSession) && currentSession is not null)
        {
            session = currentSession;
        }

        if (session.LegacyRandomSpell != 0)
        {
            sessions.Set(context.Connection, session with { LegacyRandomSpell = 0 });
        }
    }

    private static void ClearLegacyWeaponProcRandomSpellIfPending(
        WorldPacketHandlerContext context,
        ushort spellId)
    {
        if (context.Sessions is not { } sessions ||
            !sessions.TryGet(context.Connection, out var currentSession) ||
            currentSession is null ||
            currentSession.LegacyRandomSpell != spellId)
        {
            return;
        }

        sessions.Set(context.Connection, currentSession with { LegacyRandomSpell = 0 });
    }

    private static bool TrySelectLegacyDirectLearnedWeaponProcSpell(
        WorldSession session,
        WorldDataCatalog worldData,
        out PlayerSpellSnapshotEntry playerSpell,
        out LegacyMagicRuntimeSpell spell)
    {
        playerSpell = default!;
        spell = default!;

        if (HasLearnedLegacySpell(session.PlayerSpells, 11_140))
        {
            if (CheckLegacyWeaponSpellDirectRateRoll(session.Character, 11_140, 10, "Primary") &&
                LegacyMagicRuntime.TryResolveLearnedSpell(session.PlayerSpells, worldData, 11_140, out playerSpell, out spell))
            {
                return true;
            }

            if (CheckLegacyWeaponSpellDirectRateRoll(session.Character, 11_140, 25, "DeadlySight") &&
                HasLegacyDeadlySightRune(session, worldData) &&
                LegacyMagicRuntime.TryResolveLearnedSpell(session.PlayerSpells, worldData, 11_140, out playerSpell, out spell))
            {
                return true;
            }
        }

        if (CheckLegacyWeaponSpellDirectRateRoll(session.Character, 14_720, 10, "Primary") &&
            HasLearnedLegacySpell(session.PlayerSpells, 14_720) &&
            LegacyMagicRuntime.TryResolveLearnedSpell(session.PlayerSpells, worldData, 14_720, out playerSpell, out spell))
        {
            return true;
        }

        if (HasLearnedLegacySpell(session.PlayerSpells, 12_670))
        {
            if (CheckLegacyWeaponSpellDirectRateRoll(session.Character, 12_670, 10, "Primary") &&
                LegacyMagicRuntime.TryResolveLearnedSpell(session.PlayerSpells, worldData, 12_670, out playerSpell, out spell))
            {
                return true;
            }

            if (CheckLegacyWeaponSpellDirectRateRoll(session.Character, 12_670, 25, "DeadlySight") &&
                HasLegacyDeadlySightRune(session, worldData) &&
                LegacyMagicRuntime.TryResolveLearnedSpell(session.PlayerSpells, worldData, 12_670, out playerSpell, out spell))
            {
                return true;
            }
        }

        if (HasLearnedLegacySpell(session.PlayerSpells, 15_800))
        {
            if (CheckLegacyWeaponSpellDirectRateRoll(session.Character, 15_800, 10, "Primary") &&
                LegacyMagicRuntime.TryResolveLearnedSpell(session.PlayerSpells, worldData, 15_800, out playerSpell, out spell))
            {
                return true;
            }

            if (CheckLegacyWeaponSpellDirectRateRoll(session.Character, 15_800, 25, "DeadlySight") &&
                HasLegacyDeadlySightRune(session, worldData) &&
                LegacyMagicRuntime.TryResolveLearnedSpell(session.PlayerSpells, worldData, 15_800, out playerSpell, out spell))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TrySelectLegacyPostTableLearnedWeaponProcSpell(
        WorldSession session,
        WorldDataCatalog worldData,
        out PlayerSpellSnapshotEntry playerSpell,
        out LegacyMagicRuntimeSpell spell)
    {
        playerSpell = default!;
        spell = default!;

        if (CheckLegacyWeaponSpellDirectRateRoll(session.Character, 15_950, 30, "PostTable") &&
            HasLearnedLegacySpell(session.PlayerSpells, 15_950) &&
            LegacyMagicRuntime.TryResolveLearnedSpell(session.PlayerSpells, worldData, 15_950, out playerSpell, out spell))
        {
            return true;
        }

        return CheckLegacyWeaponSpellDirectRateRoll(session.Character, 7_040, 30, "PostTable") &&
            HasLearnedLegacySpell(session.PlayerSpells, 7_040) &&
            LegacyMagicRuntime.TryResolveLearnedSpell(session.PlayerSpells, worldData, 7_040, out playerSpell, out spell);
    }

    private static LegacyWeaponProcSelectionResult SelectLegacyRightWeaponEffectProcSpell(
        WorldSession session,
        WorldDataCatalog worldData,
        out PlayerSpellSnapshotEntry playerSpell,
        out LegacyMagicRuntimeSpell spell,
        out ushort selectedSpellId,
        out bool selectedSpellLearned,
        out bool selectedSpellMetadataResolved)
    {
        playerSpell = default!;
        spell = default!;
        selectedSpellId = 0;
        selectedSpellLearned = false;
        selectedSpellMetadataResolved = false;

        if (HasLegacyStatusFlag(session.Character, LegacyFatalStrikeFlag) ||
            !TryGetCurrentRightWeapon(session, out var rightWeapon) ||
            rightWeapon.Effect == 0)
        {
            return LegacyWeaponProcSelectionResult.None;
        }

        var spellId = rightWeapon.Effect switch
        {
            LegacyWeaponEffectPoison => LegacyWeaponEffectPoisonSpellId,
            LegacyWeaponEffectMp => LegacyWeaponEffectMpSpellId,
            _ => (ushort)0,
        };
        if (spellId == 0 ||
            !CheckLegacyWeaponEffectRateRoll(session.Character, spellId, 20))
        {
            return LegacyWeaponProcSelectionResult.None;
        }

        selectedSpellId = spellId;
        var learnedSpell = session.PlayerSpells.Spells.FirstOrDefault(candidate => candidate.Id == spellId);
        selectedSpellLearned = learnedSpell is not null;
        if (LegacyMagicRuntime.TryResolveLearnedSpell(
                session.PlayerSpells,
                worldData,
                spellId,
                out playerSpell,
                out spell))
        {
            selectedSpellMetadataResolved = true;
            return LegacyWeaponProcSelectionResult.DispatchLearnedSpell;
        }

        var metadataLevel = learnedSpell?.Level ?? 0;
        selectedSpellMetadataResolved = LegacyMagicRuntime.TryResolveSpell(
            worldData,
            spellId,
            metadataLevel,
            out _);
        return LegacyWeaponProcSelectionResult.ConsumedNoDispatchableSpell;
    }

    private static bool TrySelectLegacyWeaponProcSpellForWeaponType(
        WorldSession session,
        WorldDataCatalog worldData,
        ushort weaponType,
        out PlayerSpellSnapshotEntry playerSpell,
        out LegacyMagicRuntimeSpell spell)
    {
        playerSpell = default!;
        spell = default!;

        var spellIds = GetLegacyWeaponProcSpellIds(worldData, weaponType);
        if (spellIds.Count == 0)
        {
            return false;
        }

        var spellId = spellIds[ReadLegacyWeaponSpellIndex(session.Character, spellIds.Count)];
        if (!LegacyMagicRuntime.TryResolveLearnedSpell(session.PlayerSpells, worldData, spellId, out playerSpell, out spell) ||
            !TryReadLegacyMagicUInt32Field(worldData, spellId, playerSpell.Level, LegacyMagicRateField, out var rate) ||
            rate is 0 or > 100 ||
            !CheckLegacyWeaponSpellRateRoll(session.Character, spellId, rate))
        {
            playerSpell = default!;
            spell = default!;
            return false;
        }

        return true;
    }

    private static IReadOnlyList<ushort> GetLegacyWeaponProcSpellIds(
        WorldDataCatalog worldData,
        ushort weaponType)
    {
        var spellIds = new List<ushort>();
        foreach (var row in worldData.Magic)
        {
            if (!TryReadUInt16LegacyMagicField(row, fieldIndex: 1, out var spellId) ||
                !IsLegacyWeaponProcSpellId(spellId) ||
                !TryReadUInt32LegacyMagicField(row, fieldIndex: 22, out var rowWeaponType) ||
                !LegacyMagicWeaponTypeMatches(rowWeaponType, weaponType) ||
                spellIds.Contains(spellId))
            {
                continue;
            }

            spellIds.Add(spellId);
        }

        return spellIds;
    }

    private static bool LegacyMagicWeaponTypeMatches(uint rowWeaponType, ushort weaponType)
    {
        if (rowWeaponType is 0 or 50_000 || weaponType == 0)
        {
            return false;
        }

        var primaryWeaponType = (ushort)(rowWeaponType % 1_000);
        var secondaryWeaponType = (ushort)(rowWeaponType / 1_000 % 1_000);
        return primaryWeaponType == weaponType || secondaryWeaponType == weaponType;
    }

    private static bool TryGetCurrentWeaponType(
        WorldSession session,
        ushort normalizedPosition,
        bool mapLegacyBladeToSword,
        out ushort weaponType)
    {
        weaponType = 0;
        foreach (var item in LegacyEquipmentSelector.SelectCurrentEquipment(session.PlayerItems, session.AlternateEquipmentActive))
        {
            if (item.Durability == 0 ||
                item.ItemId == 0 ||
                item.Position % 50 != normalizedPosition)
            {
                continue;
            }

            weaponType = checked((ushort)(item.ItemId / 1_000));
            if (mapLegacyBladeToSword && weaponType == 421)
            {
                weaponType = 420;
            }

            return weaponType != 0;
        }

        return false;
    }

    private static bool TryGetCurrentRightWeapon(
        WorldSession session,
        out PlayerItemSnapshotItem rightWeapon)
    {
        foreach (var item in LegacyEquipmentSelector.SelectCurrentEquipment(session.PlayerItems, session.AlternateEquipmentActive))
        {
            if (item.Durability == 0 ||
                item.ItemId == 0 ||
                item.Position % 50 != 4)
            {
                continue;
            }

            rightWeapon = item;
            return true;
        }

        rightWeapon = default!;
        return false;
    }

    private static bool CheckLegacyWeaponSpellOuterRateRoll(CharacterSnapshot character)
    {
        var rate = IsLegacyTrojanClass(character.Class) ? uint.MaxValue : 42u;
        var roll = TryReadUInt32Attribute(
            character.Attributes,
            out var forcedRoll,
            RuntimeLegacyWeaponSpellOuterRateRollAttribute)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        return rate > roll;
    }

    private static int ReadLegacyWeaponSpellIndex(CharacterSnapshot character, int count)
    {
        if (count <= 1)
        {
            return 0;
        }

        if (TryReadInt64Attribute(character.Attributes, RuntimeLegacyWeaponSpellIndexAttribute, out var forcedIndex))
        {
            var normalized = forcedIndex % count;
            if (normalized < 0)
            {
                normalized += count;
            }

            return checked((int)normalized);
        }

        return Random.Shared.Next(0, count);
    }

    private static bool CheckLegacyWeaponSpellRateRoll(
        CharacterSnapshot character,
        ushort spellId,
        uint rate)
    {
        var roll = TryReadUInt32Attribute(
            character.Attributes,
            out var forcedRoll,
            RuntimeLegacyWeaponSpellRateRollAttribute + "." + spellId.ToString(CultureInfo.InvariantCulture),
            RuntimeLegacyWeaponSpellRateRollAttribute + spellId.ToString(CultureInfo.InvariantCulture),
            RuntimeLegacyWeaponSpellRateRollAttribute)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        return rate > roll;
    }

    private static bool CheckLegacyWeaponSpellDirectRateRoll(
        CharacterSnapshot character,
        ushort spellId,
        uint rate,
        string branch)
    {
        if (rate == 0)
        {
            return false;
        }

        if (rate >= 100)
        {
            return true;
        }

        var spellIdText = spellId.ToString(CultureInfo.InvariantCulture);
        var roll = TryReadUInt32Attribute(
            character.Attributes,
            out var forcedRoll,
            RuntimeLegacyWeaponSpellDirectRateRollAttribute + "." + spellIdText + "." + branch,
            RuntimeLegacyWeaponSpellDirectRateRollAttribute + "." + branch,
            RuntimeLegacyWeaponSpellDirectRateRollAttribute + "." + spellIdText,
            RuntimeLegacyWeaponSpellDirectRateRollAttribute + spellIdText,
            RuntimeLegacyWeaponSpellDirectRateRollAttribute)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        return rate > roll;
    }

    private static bool CheckLegacyWeaponEffectRateRoll(
        CharacterSnapshot character,
        ushort spellId,
        uint rate)
    {
        if (rate == 0)
        {
            return false;
        }

        if (rate >= 100)
        {
            return true;
        }

        var spellIdText = spellId.ToString(CultureInfo.InvariantCulture);
        var roll = TryReadUInt32Attribute(
            character.Attributes,
            out var forcedRoll,
            RuntimeLegacyWeaponEffectRateRollAttribute + "." + spellIdText,
            RuntimeLegacyWeaponEffectRateRollAttribute + spellIdText,
            RuntimeLegacyWeaponEffectRateRollAttribute)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        return rate > roll;
    }

    private static bool HasLegacyDeadlySightRune(WorldSession session, WorldDataCatalog worldData)
    {
        return ReadAttributeBool(
                session.Character.Attributes,
                "LegacyDeadlySightRune",
                "DeadlySightRune",
                "DeadlySight") ||
            HasLegacyEquippedRune(session.PlayerItems, worldData, "DeadlySight");
    }

    private static bool TryReadUInt16LegacyMagicField(MagicType row, int fieldIndex, out ushort value)
    {
        value = 0;
        return fieldIndex >= 0 &&
            fieldIndex < row.Fields.Count &&
            ushort.TryParse(row.Fields[fieldIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadUInt32LegacyMagicField(MagicType row, int fieldIndex, out uint value)
    {
        value = 0;
        return fieldIndex >= 0 &&
            fieldIndex < row.Fields.Count &&
            uint.TryParse(row.Fields[fieldIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
